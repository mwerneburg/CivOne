#!/usr/bin/env python3
"""
move-timing.py — where AI unit-movement time goes, per session.

    tools/move-timing.py [unit] [turns]

    tools/move-timing.py              # Armor, last 120 turn records
    tools/move-timing.py MechInf      # a different unit type
    tools/move-timing.py Armor 400    # a longer window

Reads turn_timing records from decisions.jsonl and reports, per SESSION, the mean
cost of moving one unit of that type. Sessions are the point: a game keeps its
game_id across a save/load but gets a fresh session_id, so a fix applied mid-run
shows up as two rows of the same game that can be compared directly.

The number that matters is the last column, ms per move. Total ai_move_ms moves
with the size of the war and tells you much less.
"""
import collections
import json
import os
import re
import sys

LOG = os.path.expanduser("~/Library/Application Support/CivOne/data/decisions.jsonl")


def main(unit="Armor", window=120):
    if not os.path.exists(LOG):
        sys.exit(f"no decision log at {LOG}")

    rows = []
    with open(LOG, encoding="utf-8-sig") as fh:
        for line in fh:
            try:
                d = json.loads(line)
            except ValueError:
                continue
            if d.get("type") == "turn_timing":
                rows.append(d)

    if not rows:
        sys.exit("no turn_timing records in the log")

    pattern = re.compile(rf"unit:{re.escape(unit)}=(\d+)/(\d+)")
    by_session = collections.OrderedDict()
    for r in rows[-window:]:
        m = pattern.search(r.get("move_split") or "")
        if not m:
            continue
        by_session.setdefault(r.get("session_id") or "(pre-session-id)", []).append(
            (r["turn"], r.get("ai_move_ms", 0), int(m.group(1)), int(m.group(2)))
        )

    if not by_session:
        sys.exit(f"no '{unit}' entries in the last {window} turn records")

    print(f"{unit}, last {window} turn records\n")
    print(f"{'session':<18}{'turns':>13}{'ai_move':>10}{'unit ms':>10}{'calls':>8}{'ms/move':>9}")
    for sid, v in by_session.items():
        ms = sum(x[2] for x in v) / len(v)
        calls = sum(x[3] for x in v) / len(v)
        print(
            f"{sid:<18}{f'{v[0][0]}-{v[-1][0]}':>13}"
            f"{sum(x[1] for x in v) / len(v):>10.0f}"
            f"{ms:>10.0f}{calls:>8.0f}{ms / max(calls, 1):>9.1f}"
        )


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Armor",
         int(sys.argv[2]) if len(sys.argv) > 2 else 120)
