#!/bin/sh
# Compares the files this repository shares with its sibling mod, listed in
# tools/shared-files.txt, with the sibling's copies, and fails with the difference where one has
# changed without the other. The copies are the same but for the mod's name, which is swapped
# before comparing. The pre-push hook runs this.
#
# The sibling is looked for beside this repository, under its name. SHARED_SIBLING_DIR points
# elsewhere. Where there is no sibling checkout, nothing is compared and this says so.

root="$(git rev-parse --show-toplevel)"
list="$root/tools/shared-files.txt"

names=$(grep -m1 '^names	' "$list")
me=$(printf '%s' "$names" | cut -f2)
sibling=$(printf '%s' "$names" | cut -f3)
if [ -z "$me" ] || [ -z "$sibling" ]; then
    echo "check-shared: $list has no names line" >&2
    exit 1
fi

there="${SHARED_SIBLING_DIR:-$root/../$sibling}"
if [ ! -d "$there" ]; then
    echo "check-shared: no $sibling checkout at $there, so the shared files were not compared" >&2
    exit 0
fi

ME=$(printf '%s' "$me" | tr '[:lower:]' '[:upper:]')
SIBLING=$(printf '%s' "$sibling" | tr '[:lower:]' '[:upper:]')
me_lower=$(printf '%s' "$me" | tr '[:upper:]' '[:lower:]')
sibling_lower=$(printf '%s' "$sibling" | tr '[:upper:]' '[:lower:]')
failed=0
work=$(mktemp -d)

grep -v '^#' "$list" | grep -v '^names	' | grep -v '^$' | while IFS='	' read -r here theirs; do
    if [ ! -f "$root/$here" ] || [ ! -f "$there/$theirs" ]; then
        echo "check-shared: $here or $sibling's $theirs is missing" >&2
        echo fail >> "$work/failed"
        continue
    fi
    tr -d '\r' < "$root/$here" > "$work/here"
    sed "s/$sibling/$me/g; s/$SIBLING/$ME/g; s/$sibling_lower/$me_lower/g" "$there/$theirs" | tr -d '\r' > "$work/theirs"
    if ! diff -u "$work/theirs" "$work/here" > "$work/diff"; then
        echo "check-shared: $here differs from $sibling's $theirs:" >&2
        sed "s|^--- $work/theirs.*|--- $sibling/$theirs|; s|^+++ $work/here.*|+++ $here|" "$work/diff" >&2
        echo fail >> "$work/failed"
    fi
done

[ -f "$work/failed" ] && failed=1
rm -rf "$work"
if [ "$failed" -ne 0 ]; then
    echo "check-shared: make the change in both repositories" >&2
    exit 1
fi
exit 0
