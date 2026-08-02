#!/usr/bin/env python3
"""Fast terminal triage for CivOne's decisions.jsonl.

Complements notebooks/decision_analysis.ipynb, which does the heavy statistical
work (pandas, sklearn, decision trees over settler features). This does the
thing you actually want ninety percent of the time: point it at the log and ask
"where did the last five hours go, and what was the AI building?"

Stdlib only, and it streams rather than loading the file, because the log runs
to hundreds of megabytes on a long run.

  ./tools/decisions.py                 # what games are in the log
  ./tools/decisions.py timing          # per-turn wall clock, where it went
  ./tools/decisions.py split           # AI.Move breakdown (the move_split probe)
  ./tools/decisions.py prod            # what cities chose to build
  ./tools/decisions.py settlers        # what settlers did with their turns
  ./tools/decisions.py all

By default it reports on the LAST game in the file. Note that resuming a save
mints a NEW game_id, so one played-through game can span several ids — use
`games` to see them and `-g` to pick one.
"""

import argparse
import collections
import json
import os
import sys

CANDIDATES = [
    "~/Library/Application Support/CivOne/data/decisions.jsonl",   # macOS
    "~/.local/share/CivOne/data/decisions.jsonl",                  # Linux / XDG
]


def find_log(explicit):
    if explicit:
        return os.path.expanduser(explicit)
    env = os.environ.get("CIVONE_DECISIONS")
    if env:
        return os.path.expanduser(env)
    for c in CANDIDATES:
        p = os.path.expanduser(c)
        if os.path.exists(p):
            return p
    sys.exit("no decisions.jsonl found; pass --log or set CIVONE_DECISIONS")


def stream(path, game=None, types=None):
    """Yield records, skipping the occasional torn line at a truncation point.

    game=None or "all" means every game_id — which is what you usually want on a
    long run, because each pause and resume mints a new id and one played-through
    game routinely spans a dozen of them.
    """
    if game == "all":
        game = None
    bad = 0
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                rec = json.loads(line)
            except ValueError:
                bad += 1
                continue
            if game and rec.get("game_id") != game:
                continue
            if types and rec.get("type") not in types:
                continue
            yield rec
    if bad:
        print(f"  ({bad} unparsable line(s) skipped — normal if the log was trimmed)",
              file=sys.stderr)


def inventory(path):
    """game_id -> (records, min turn, max turn, type counter)."""
    games = collections.OrderedDict()
    for rec in stream(path):
        gid = rec.get("game_id")
        if gid is None:
            continue
        g = games.setdefault(gid, {"n": 0, "lo": None, "hi": None,
                                   "types": collections.Counter()})
        g["n"] += 1
        g["types"][rec.get("type")] += 1
        t = rec.get("turn")
        if isinstance(t, int):
            g["lo"] = t if g["lo"] is None else min(g["lo"], t)
            g["hi"] = t if g["hi"] is None else max(g["hi"], t)
    return games


def pct(n, total):
    return f"{100.0 * n / total:5.1f}%" if total else "    -"


# ── reports ──────────────────────────────────────────────────────────────────

def report_games(path, _game):
    games = inventory(path)
    if not games:
        print("no records")
        return
    print(f"{'game_id':10} {'records':>9}  {'turns':>11}  contents")
    for gid, g in games.items():
        span = f"{g['lo']}-{g['hi']}" if g["lo"] is not None else "-"
        top = ", ".join(f"{k} {v}" for k, v in g["types"].most_common(4))
        print(f"{gid:10} {g['n']:9d}  {span:>11}  {top}")
    print("\nresuming a save mints a new game_id, so one game can span several rows.")


def report_timing(path, game):
    rows = sorted(stream(path, game, {"turn_timing"}), key=lambda r: r["turn"])
    seen_turn = set()
    rows = [r for r in rows if not (r["turn"] in seen_turn or seen_turn.add(r["turn"]))]
    if not rows:
        print("no turn_timing records (needs a run with timing instrumentation)")
        return

    total = sum(r["wall_ms"] for r in rows) / 60000.0
    print(f"{len(rows)} turns logged, {total:.1f} minutes of wall clock\n")

    # Sample about 20 rows so the shape is visible without a wall of text.
    step = max(1, len(rows) // 20)
    print(f"{'turn':>5} {'wall':>7} {'move':>7} {'path':>6} {'queue':>7} "
          f"{'screen':>7} {'other':>6} {'cities':>7} {'units':>6} {'frames':>7}")
    for r in rows[::step]:
        print(f"{r['turn']:5d} {r['wall_ms']/1000:6.1f}s {r['ai_move_ms']/1000:6.1f} "
              f"{r['path_ms']/1000:5.1f} {r['task_queue_ms']/1000:6.1f} "
              f"{r['screen_ms']/1000:6.1f} {r['other_ms']/1000:5.1f} "
              f"{r['cities']:7d} {r['units']:6d} {r['frames']:7d}")

    # Where the time actually goes, over the last quarter of the run — early
    # turns are cheap and drown the late-game signal in an average.
    tail = rows[max(0, len(rows) * 3 // 4):]
    wall = sum(r["wall_ms"] for r in tail)
    print(f"\nlast {len(tail)} turns — {wall/60000:.1f} min, "
          f"{wall/len(tail)/1000:.1f}s per turn:")
    for label, key in (("ai_move   ", "ai_move_ms"), ("  of which pathfinding", "path_ms"),
                       ("task_queue", "task_queue_ms"), ("screen    ", "screen_ms"),
                       ("city_turn ", "city_turn_ms"), ("render    ", "render_ms"),
                       ("other     ", "other_ms")):
        v = sum(r.get(key, 0) for r in tail)
        print(f"  {label:24} {v/1000:8.1f}s  {pct(v, wall)}")

    worst = sorted(tail, key=lambda r: -r["wall_ms"])[:3]
    print("  slowest turns: " + ", ".join(
        f"t{r['turn']} ({r['wall_ms']/1000:.0f}s)" for r in worst))


def report_split(path, game):
    """The move_split probe: which unit kinds and which site scans cost the time."""
    ms = collections.Counter()
    calls = collections.Counter()
    turns = 0
    for r in stream(path, game, {"turn_timing"}):
        raw = r.get("move_split")
        if not raw:
            continue
        turns += 1
        for item in raw.split():
            key, _, val = item.partition("=")
            got_ms, _, got_calls = val.partition("/")
            try:
                ms[key] += int(got_ms)
                calls[key] += int(got_calls)
            except ValueError:
                continue
    if not turns:
        print("no move_split data — that probe is temporary; this run may predate it")
        return

    print(f"AI.Move breakdown over {turns} turns\n")
    print(f"{'bucket':32} {'total':>9} {'per turn':>9} {'calls':>10} {'us/call':>9}")
    for key, v in ms.most_common(20):
        c = calls[key]
        print(f"{key:32} {v/1000:8.1f}s {v/turns/1000:8.2f}s {c:10d} "
              f"{(v*1000.0/c if c else 0):9.0f}")

    units = sum(v for k, v in ms.items() if k.startswith("unit:"))
    sites = sum(v for k, v in ms.items() if k.startswith("site:"))
    if units:
        print(f"\nsite scans are {pct(sites, units)} of all unit-move time.")


def report_prod(path, game):
    actions = collections.Counter()
    stance = collections.Counter()
    war = collections.Counter()
    for r in stream(path, game, {"city_prod"}):
        a = r.get("action")
        if a == "queued":          # already had something in the queue; not a choice
            stance["(queued)"] += 1
            continue
        actions[a] += 1
        stance[r.get("stance")] += 1
        war[bool(r.get("at_war"))] += 1

    total = sum(actions.values())
    if not total:
        print("no city_prod decisions")
        return
    print(f"{total} production decisions\n")
    for a, n in actions.most_common(25):
        print(f"  {n:6d} {pct(n, total)}  {a}")
    print("\nstance: " + ", ".join(f"{k} {v}" for k, v in stance.most_common()))
    print(f"at war: {war[True]} ({pct(war[True], war[True] + war[False])}), "
          f"at peace: {war[False]}")


def report_settlers(path, game):
    actions = collections.Counter()
    banded = collections.defaultdict(collections.Counter)
    for r in stream(path, game, {"settler"}):
        a = r.get("action")
        actions[a] += 1
        t = r.get("turn")
        if isinstance(t, int):
            banded[t // 100 * 100][a] += 1

    total = sum(actions.values())
    if not total:
        print("no settler decisions")
        return
    print(f"{total} settler decisions\n")
    for a, n in actions.most_common():
        print(f"  {n:6d} {pct(n, total)}  {a}")

    if len(banded) > 1:
        keys = sorted({a for c in banded.values() for a in c})[:6]
        print(f"\n{'turns':>7} " + " ".join(f"{k:>10}" for k in keys))
        for band in sorted(banded):
            print(f"{band:5d}+  " + " ".join(f"{banded[band][k]:>10d}" for k in keys))


REPORTS = {
    "games": report_games,
    "timing": report_timing,
    "split": report_split,
    "prod": report_prod,
    "settlers": report_settlers,
}


def selftest():
    """One runnable check: a synthetic log through every report."""
    import tempfile
    recs = [
        {"type": "turn_timing", "game_id": "aaa", "turn": 1, "wall_ms": 1000,
         "ai_move_ms": 600, "path_ms": 100, "task_queue_ms": 700, "screen_ms": 200,
         "city_turn_ms": 50, "render_ms": 10, "other_ms": 90, "cities": 5,
         "units": 20, "frames": 30, "move_split": "unit:Settlers=400/10 site:BestSettleSite=250/8"},
        {"type": "city_prod", "game_id": "aaa", "turn": 1, "action": "Temple",
         "stance": "Develop", "at_war": False},
        {"type": "city_prod", "game_id": "aaa", "turn": 1, "action": "queued"},
        {"type": "settler", "game_id": "aaa", "turn": 1, "action": "irrigate"},
    ]
    with tempfile.NamedTemporaryFile("w", suffix=".jsonl", delete=False) as f:
        for r in recs:
            f.write(json.dumps(r) + "\n")
        f.write("{ this line is torn\n")          # the truncation case
        path = f.name

    games = inventory(path)
    assert list(games) == ["aaa"], games
    assert games["aaa"]["n"] == 4, games          # torn line skipped, not counted
    assert games["aaa"]["lo"] == 1 and games["aaa"]["hi"] == 1

    for name, fn in REPORTS.items():
        fn(path, "aaa")                            # must not raise
    os.unlink(path)
    print("\nselftest ok")


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("report", nargs="?", default="games",
                    choices=sorted(REPORTS) + ["all", "selftest"])
    ap.add_argument("--log", help="path to decisions.jsonl")
    ap.add_argument("-g", "--game",
                    help="game_id, or 'all' to span every resume (default: the last one)")
    args = ap.parse_args()

    if args.report == "selftest":
        selftest()
        return

    path = find_log(args.log)
    game = args.game
    if game is None and args.report != "games":
        games = inventory(path)
        if not games:
            sys.exit("no records in " + path)
        game = list(games)[-1]
        note = ""
        if len(games) > 1:
            note = f"; {len(games)} ids in file — use -g all to span the resumes"
        print(f"# {path}\n# game {game} (last in file{note})\n")

    names = sorted(REPORTS) if args.report == "all" else [args.report]
    for i, name in enumerate(names):
        if i:
            print()
        print(f"── {name} " + "─" * (66 - len(name)))
        REPORTS[name](path, game)


if __name__ == "__main__":
    main()
