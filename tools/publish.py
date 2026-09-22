#!/usr/bin/env python3
"""Upload a package zip to Hexium or Thunderstore and submit it as a new version.

Hexium describes its own API as "Thunderstore-compatible mod repository API"
(https://hexium.gg/api/openapi.json), and the endpoints match Thunderstore's experimental API
one for one, so both stores take the same requests:

    1. POST /api/experimental/usermedia/initiate-upload/   -> an upload uuid + presigned part URLs
    2. PUT  <each presigned URL>                           -> the bytes, one part at a time
    3. POST /api/experimental/usermedia/{uuid}/finish-upload/
    4. POST /api/experimental/submission/submit/           -> the version goes live

The differences are the host, the token and the categories: Hexium takes category names,
Thunderstore takes community category slugs (see each store's
/api/experimental/community/valheim/category/).

Standard library only: this runs on a CI box with nothing installed.

Neither store documents the shape of its Authorization header, so the first authenticated call
tries `Bearer` (what Thunderstore's own tooling uses) and falls back to `Token` once, reporting
which one the server accepted.

Usage:
    HEXIUM_TOKEN=... python3 tools/publish.py dist/hexium/ScrollToArms-0.1.0.zip --store hexium \\
        --team isimp --categories "Quality of Life,Combat,Open Source,Valheim 1.0"

    THUNDERSTORE_TOKEN=... python3 tools/publish.py dist/thunderstore/ScrollToArms-0.1.0.zip --store thunderstore \\
        --team isimp --categories "ai-generated,mods,tweaks,client-side,deep-north-update"

    python3 tools/publish.py dist/hexium/ScrollToArms-0.1.0.zip --check-only
"""

import argparse
import json
import os
import struct
import sys
import urllib.error
import urllib.request
import zipfile

COMMUNITY = "valheim"

STORES = {
    "hexium": {
        "base": "https://hexium.gg",
        "token": "HEXIUM_TOKEN",
        "page": "https://valheim.hexium.gg/mods/{team}/{name}",
        "where": "your Hexium team settings",
    },
    "thunderstore": {
        "base": "https://thunderstore.io",
        "token": "THUNDERSTORE_TOKEN",
        "page": "https://thunderstore.io/c/valheim/p/{team}/{name}/",
        "where": "a service account of your Thunderstore team",
    },
}

# What both stores require of a package, checked here so a bad zip fails in seconds
# rather than halfway through an upload.
ICON_SIDE = 256
MAX_DESCRIPTION = 256


class Failed(Exception):
    pass


def log(message):
    print(message, flush=True)


def call(base, path, payload, token, scheme):
    """POST JSON to the store. Returns (status, parsed body or raw text)."""
    request = urllib.request.Request(
        base + path,
        data=json.dumps(payload).encode("utf-8"),
        method="POST",
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json",
            "Authorization": f"{scheme} {token}",
            "User-Agent": "isimp-release/1.0",
        },
    )
    try:
        with urllib.request.urlopen(request) as response:
            body = response.read().decode("utf-8")
            return response.status, json.loads(body) if body else {}
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode("utf-8", "replace")


def authenticated_call(base, path, payload, token, state):
    """Like call(), but works out the header scheme on first use and then sticks to it."""
    schemes = [state["scheme"]] if state["scheme"] else ["Bearer", "Token"]

    for scheme in schemes:
        status, body = call(base, path, payload, token, scheme)
        if status == 401 and state["scheme"] is None and scheme != schemes[-1]:
            log(f"  (401 with '{scheme}', trying the next scheme)")
            continue

        if status >= 400:
            raise Failed(f"{path} answered {status}: {body}")

        if state["scheme"] is None:
            state["scheme"] = scheme
            log(f"  authenticated with '{scheme}'")
        return body

    raise Failed(f"{path} rejected every Authorization scheme tried")


def put_part(url, chunk):
    """Upload one part to its presigned URL and return the ETag the server assigns it."""
    request = urllib.request.Request(
        url,
        data=chunk,
        method="PUT",
        headers={"Content-Type": "application/octet-stream"},
    )
    try:
        with urllib.request.urlopen(request) as response:
            etag = response.headers.get("ETag")
            if not etag:
                raise Failed(f"part upload returned no ETag ({response.status})")
            return etag
    except urllib.error.HTTPError as error:
        raise Failed(f"part upload failed {error.code}: {error.read().decode('utf-8', 'replace')[:400]}")


def check(zip_path):
    """Validate the package against the stores' stated rules. Returns the manifest."""
    if not os.path.isfile(zip_path):
        raise Failed(f"no such file: {zip_path}")

    with zipfile.ZipFile(zip_path) as archive:
        names = archive.namelist()

        for required in ("manifest.json", "icon.png", "README.md"):
            if required not in names:
                raise Failed(f"{required} must sit at the root of the archive; found {names}")

        manifest = json.loads(archive.read("manifest.json"))

        # PNG puts width and height in the IHDR chunk, at a fixed offset.
        icon = archive.read("icon.png")
        if icon[:8] != b"\x89PNG\r\n\x1a\n":
            raise Failed("icon.png is not a PNG")
        width, height = struct.unpack(">II", icon[16:24])
        if (width, height) != (ICON_SIDE, ICON_SIDE):
            raise Failed(f"icon.png must be exactly {ICON_SIDE}x{ICON_SIDE}, is {width}x{height}")

    name = manifest.get("name", "")
    if not name or not all(c.isalnum() or c == "_" for c in name) or not name.isascii():
        raise Failed(f"manifest name may only hold letters, digits and underscores: {name!r}")

    description = manifest.get("description", "")
    if len(description) > MAX_DESCRIPTION:
        raise Failed(f"description is {len(description)} characters, the limit is {MAX_DESCRIPTION}")

    if not manifest.get("version_number"):
        raise Failed("manifest has no version_number")

    log(f"package ok: {name} {manifest['version_number']}, icon {width}x{height}, "
        f"description {len(description)}/{MAX_DESCRIPTION}, "
        f"dependencies {manifest.get('dependencies', [])}")
    return manifest


def publish(store, zip_path, manifest, team, categories, token):
    site = STORES[store]
    base = site["base"]
    size = os.path.getsize(zip_path)
    filename = os.path.basename(zip_path)
    state = {"scheme": None}

    log(f"initiating upload of {filename} ({size:,} bytes) to {base}")
    initiated = authenticated_call(
        base,
        "/api/experimental/usermedia/initiate-upload/",
        {"filename": filename, "file_size_bytes": size},
        token,
        state,
    )

    uuid = initiated["user_media"]["uuid"]
    urls = initiated["upload_urls"]
    log(f"  upload {uuid}, {len(urls)} part(s)")

    parts = []
    with open(zip_path, "rb") as handle:
        for entry in urls:
            handle.seek(entry["offset"])
            chunk = handle.read(entry["length"])
            etag = put_part(entry["url"], chunk)
            parts.append({"ETag": etag, "PartNumber": entry["part_number"]})
            log(f"  part {entry['part_number']}/{len(urls)} uploaded ({len(chunk):,} bytes)")

    authenticated_call(base, f"/api/experimental/usermedia/{uuid}/finish-upload/", {"parts": parts}, token, state)
    log("  upload finished")

    # Hexium's categories are its own site-wide list, so they go in both fields. Thunderstore's
    # Valheim categories belong to the community, so only community_categories carries them.
    submission = {
        "author_name": team,
        "communities": [COMMUNITY],
        "categories": categories if store == "hexium" else [],
        "community_categories": {COMMUNITY: categories},
        "has_nsfw_content": False,
        "upload_uuid": uuid,
    }

    log(f"submitting as {team}/{manifest['name']} {manifest['version_number']}")
    result = authenticated_call(base, "/api/experimental/submission/submit/", submission, token, state)

    version = result.get("package_version", {}) if isinstance(result, dict) else {}
    full_name = version.get("full_name") or f"{team}-{manifest['name']}-{manifest['version_number']}"
    log(f"published {full_name}")
    log(site["page"].format(team=team, name=manifest["name"]))


def main():
    parser = argparse.ArgumentParser(description="Publish a package zip to Hexium or Thunderstore.")
    parser.add_argument("zip", help="the package to upload")
    parser.add_argument("--store", choices=sorted(STORES), help="where to publish")
    parser.add_argument("--team", help="the team to publish under (the package namespace)")
    parser.add_argument("--categories", default="", help="comma-separated categories for that store")
    parser.add_argument("--check-only", action="store_true", help="validate the package and stop")
    arguments = parser.parse_args()

    try:
        manifest = check(arguments.zip)
        if arguments.check_only:
            return 0

        if not arguments.store:
            raise Failed("--store is required to publish")
        if not arguments.team:
            raise Failed("--team is required to publish")

        site = STORES[arguments.store]
        token = os.environ.get(site["token"], "").strip()
        if not token:
            raise Failed(
                f"{site['token']} is empty. Create a token in {site['where']} and store it "
                f"as the {site['token']} repository secret."
            )

        categories = [c.strip() for c in arguments.categories.split(",") if c.strip()]
        publish(arguments.store, arguments.zip, manifest, arguments.team, categories, token)
        return 0
    except Failed as failure:
        print(f"::error::{failure}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
