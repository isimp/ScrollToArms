# Releasing

Pushing builds the mod. Releasing is a manual workflow that creates a GitHub release and then publishes to [Hexium](https://valheim.hexium.gg/), [Thunderstore](https://thunderstore.io/c/valheim/) or both.

## One-time setup

The icon lives at `packaging/icon.png`: a 256x256 PNG, committed to the repository. The release workflow stops if it is missing or the wrong size.

Each store needs a token stored as a repository secret. The workflow checks for the tokens of the selected stores before building.

| Secret | Where to create it |
|---|---|
| `HEXIUM_TOKEN` | Hexium team settings (the settings icon next to your username, then the bottom of the page). |
| `THUNDERSTORE_TOKEN` | Thunderstore: Settings, Teams, your team, Service Accounts, Add service account. The token is shown once. |

## Cutting a release

1. Set the new version in `ScrollToArms.csproj` (`<Version>`), `src/Plugin.cs` (`public const string Version`) and `packaging/manifest.json` (`version_number`). The workflow checks that all three agree.
2. Add a CHANGELOG entry. It ships in the package and appears on the mod pages.
3. Push, and wait for the Build workflow to pass.
4. Run the Release workflow from the Actions tab. It asks for the version (without `v`), optional release notes, the draft flag, which stores to publish to (both, Hexium only or Thunderstore only), the team, and each store's categories.

The workflow then runs these steps in order:

| Step | |
|---|---|
| Version check | All three version sources match the input. |
| Icon check | `packaging/icon.png` exists. |
| Token checks | The token of each selected store is set. |
| Build | Against `lib/`, so no game install is needed. |
| Package | One zip per store with the manifest, icon, README, CHANGELOG, LICENSE and the DLL. The Thunderstore zip's manifest also lists `denikson-BepInExPack_Valheim-5.4.2350`. |
| Package check | Both zips against the stores' rules: required files at the root, icon exactly 256x256, name of letters, digits and underscores, description of at most 256 characters. |
| GitHub release | Tags `vX.Y.Z` and attaches the Hexium zip and the DLL. |
| Hexium | Uploads and submits the version, if selected. |
| Thunderstore | Uploads and submits the version, if selected. |

If a publish step fails, fix the cause and run the workflow again with the same version and only the store that failed. The GitHub release is updated rather than duplicated, and each store refuses a version it already has.

## Categories

The two stores have different category lists. Hexium takes category names, Thunderstore takes slugs. Thunderstore's rules require the `ai-generated` category for packages with AI-generated code, so keep it in the Thunderstore list. The current lists are at `https://hexium.gg/api/experimental/community/valheim/category/` and `https://thunderstore.io/api/experimental/community/valheim/category/`.

## Install location

ScrollToArms is client-side only. Servers do not need it.

## How the publish works

`tools/publish.py` uses the standard library only. Both stores take the same requests:

1. `POST /api/experimental/usermedia/initiate-upload/` returns an upload UUID and a presigned URL per part.
2. `PUT` each part, keeping the returned `ETag`.
3. `POST /api/experimental/usermedia/{uuid}/finish-upload/` with those ETags.
4. `POST /api/experimental/submission/submit/` with `author_name` set to the team, `communities` set to `["valheim"]` and the categories.

For authorization the script tries `Bearer` first and falls back to `Token`, printing which one the server accepted.

It can also be run by hand:

```bash
HEXIUM_TOKEN=... python3 tools/publish.py dist/hexium/ScrollToArms-0.1.0.zip --store hexium --team isimp --categories "Quality of Life,Combat,User Interface,Open Source,Valheim 1.0"
THUNDERSTORE_TOKEN=... python3 tools/publish.py dist/thunderstore/ScrollToArms-0.1.0.zip --store thunderstore --team isimp --categories "ai-generated,mods,tweaks,utility,client-side,deep-north-update"
python3 tools/publish.py dist/hexium/ScrollToArms-0.1.0.zip --check-only   # validate only
```

## After a game update

Regenerate the reference stubs, then rebuild and commit `lib/`:

```powershell
pwsh tools/strip-references.ps1
```

The script reads the installed Valheim and BepInEx, replaces every method body with `throw null` and writes the result to `lib/`. The stubs can be compiled against but not run. If you add a reference to `ScrollToArms.csproj`, add it to the list in the script as well.
