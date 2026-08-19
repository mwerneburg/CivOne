#!/usr/bin/env bash
#
# Run a batch of headless games in parallel and collect their decision logs.
#
#   ./sweep.sh                          # 7 field sizes x 2 seeds, 4 at a time, Earth epic
#   CIVS="5 9 13" REPEATS=4 ./sweep.sh  # 3 field sizes x 4 seeds
#   PARALLEL=6 TURNS=400 ./sweep.sh     # more concurrency, shorter games
#   MAP=earth-standard ./sweep.sh       # the 80x50 board
#
# The map is held CONSTANT and only the seed varies. On a generated map the seed decides the
# continents, so a 13-civ run and a 5-civ run would differ in the shape of the planet as well
# as the size of the field and nothing could be attributed to either. On Earth the ground is
# identical in every run and the seed varies only the die rolls.
#
# Each run gets its own storage directory because everything the game writes hangs off one —
# decisions.jsonl, autosave.cos, the hall of fame — and runs sharing a directory would
# interleave their logs and overwrite each other's saves.

set -euo pipefail

export PATH=$PATH:/opt/homebrew/bin/dotnet
export DOTNET_ROOT=/opt/homebrew/Cellar/dotnet/10.0.300/libexec

CIVS="${CIVS:-3 5 7 9 11 13 15}"
REPEATS="${REPEATS:-2}"
PARALLEL="${PARALLEL:-4}"
TURNS="${TURNS:-750}"
MAP="${MAP:-earth-epic}"
DIFFICULTY="${DIFFICULTY:-0}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT="${OUT:-$ROOT/sweeps/$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$OUT"

# Build once. Parallel jobs then run --no-build; letting four test hosts build the same
# assemblies at once is a race with no upside.
echo "building..."
dotnet build "$ROOT/tests/CivOne.Tests.csproj" -v q >/dev/null

jobs_run=0
for civs in $CIVS; do
	for rep in $(seq 1 "$REPEATS"); do
		seed=$(( civs * 100 + rep ))
		name="civs${civs}-seed${seed}"
		dir="$OUT/$name"
		mkdir -p "$dir"

		(
			CIVONE_HARNESS_STORAGE="$dir" \
			CIVONE_HARNESS_MAP="$MAP" \
			CIVONE_HARNESS_CIVS="$civs" \
			CIVONE_HARNESS_TURNS="$TURNS" \
			CIVONE_HARNESS_SEED="$seed" \
			CIVONE_HARNESS_DIFFICULTY="$DIFFICULTY" \
			CIVONE_HARNESS_LOG="$dir/harness.log" \
			dotnet test "$ROOT/tests/CivOne.Tests.csproj" \
				--filter Autoplay_DevelopsAWorld --no-build \
				> "$dir/test.log" 2>&1 \
				&& echo "done  $name" \
				|| echo "FAILED $name (see $dir/test.log)"
		) &

		jobs_run=$((jobs_run + 1))
		# Poll rather than `wait -n`: macOS ships bash 3.2, which does not have it, and the
		# script silently degraded to no throttling at all when it was tried.
		while [ "$(jobs -rp | wc -l)" -ge "$PARALLEL" ]; do sleep 2; done
	done
done
wait

# One file to analyse. Every row already carries its own game_id and session_id, so the
# concatenation is unambiguous no matter what order the runs finished in.
cat "$OUT"/*/data/decisions.jsonl > "$OUT/decisions.jsonl" 2>/dev/null || true

echo
echo "$jobs_run runs -> $OUT"
grep -h '"type": "game_outcome"' "$OUT/decisions.jsonl" 2>/dev/null \
	| sed 's/.*"victory": "\([^"]*\)".*"turns": \([0-9]*\).*/\1 t\2/' | sort | uniq -c | sort -rn
