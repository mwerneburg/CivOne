#!/usr/bin/env bash
# Regenerates ART-MANIFEST.txt from the git history.
#
# Every tracked binary asset, grouped by directory, with the date it first
# appeared and who added it. Nothing is hand-maintained: the manifest is a view
# of the repository, so it cannot drift from what is actually shipped.
#
#   tools/art-manifest.sh > ART-MANIFEST.txt
set -euo pipefail
cd "$(dirname "$0")/.."

cat <<'HEADER'
Art manifest
============

Every binary asset tracked in this repository, with the date it first appeared
in the history and the committer who added it. Generated — do not hand-edit.

    Regenerate with:  tools/art-manifest.sh > ART-MANIFEST.txt

Provenance and licensing of these files is described in PROVENANCE.md.

HEADER

git ls-files | grep -iE '\.(png|gif|ico|icns|bin)$' | sort | awk -F/ '
  { dir = (NF == 1 ? "." : substr($0, 1, length($0) - length($NF) - 1)); print dir "\t" $0 }
' | sort | while IFS=$'\t' read -r dir path; do
	if [ "$dir" != "${last_dir:-}" ]; then
		[ -n "${last_dir:-}" ] && echo
		echo "$dir"
		last_dir="$dir"
	fi
	added=$(git log --format='%ad  %an' --date=short --diff-filter=A -- "$path" | tail -1)
	printf '    %s  %s\n' "$added" "$(basename "$path")"
done
