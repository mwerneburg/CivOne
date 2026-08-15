#!/usr/bin/env python3
"""
run-report.py — the standing summary of a finished or paused game.

    tools/run-report.py [save.cos]

Defaults to the autosave. Reads the .cos directly (it is YAML) so it needs no
game running and no build.

WHY THIS EXISTS, and why it does NOT report "small starving cities":

    That was the metric used to judge AI city growth through July and August
    2026 — the share of cities at size <= 3 with food income <= 0 — and it
    became actively misleading once the growth work started landing. It is a
    ratio whose denominator the fix changes: an AI that grows its cities also
    founds MORE cities, and every newly founded city enters the world at size 1
    with thin food. So the numerator refills as fast as it drains, and the ratio
    can sit flat or worsen while the empire is plainly thriving. A selection
    effect, not a measurement.

    MEAN CITY SIZE has no such problem. It moves with the thing actually being
    asked about, it is comparable between runs, and a new city drags it down by
    exactly its true weight rather than by a whole unit of "bad".

    Population is reported beside it because the two together separate the
    cases mean size alone cannot: a civ can raise its mean by losing its small
    cities. Rising mean with rising population is growth; rising mean with
    falling population is amputation.
"""
import sys
import os
import collections

try:
    import yaml
except ImportError:
    sys.exit("needs pyyaml:  pip3 install pyyaml")

DEFAULT = os.path.expanduser("~/Library/Application Support/CivOne/saves/autosave.cos")


def main(path):
    with open(path) as fh:
        save = yaml.safe_load(fh)

    game = save["Game"]
    players = save["Players"]
    cities = [c for c in save["Cities"] if c["Size"] > 0]
    units = save["Units"]

    by_owner = collections.defaultdict(list)
    for c in cities:
        by_owner[c["Owner"]].append(c["Size"])
    unit_count = collections.Counter(u["Owner"] for u in units)

    print(f"{os.path.basename(path)} — turn {game['Turn']}")
    print()
    print(f"{'civ':<15}{'cities':>7}{'mean size':>11}{'largest':>9}{'population':>12}"
          f"{'units':>7}{'adv':>5}{'gold':>7}{'culture':>10}")

    rows = []
    for i, p in enumerate(players):
        if p is None:
            continue
        sizes = by_owner.get(i, [])
        if not sizes and not unit_count.get(i):
            continue
        rows.append((
            len(sizes),
            p["CivilizationName"],
            sum(sizes) / len(sizes) if sizes else 0.0,
            max(sizes) if sizes else 0,
            sum(sizes),
            unit_count.get(i, 0),
            len(p.get("Advances") or []),
            p["Gold"],
            p.get("Culture", 0),
        ))

    for n, name, mean, biggest, pop, u, adv, gold, culture in sorted(rows, reverse=True):
        print(f"{name:<15}{n:>7}{mean:>11.2f}{biggest:>9}{pop:>12}{u:>7}{adv:>5}{gold:>7}{culture:>10}")

    world_sizes = [s for sizes in by_owner.values() for s in sizes]
    if world_sizes:
        print()
        print(f"{'WORLD':<15}{len(world_sizes):>7}"
              f"{sum(world_sizes) / len(world_sizes):>11.2f}"
              f"{max(world_sizes):>9}{sum(world_sizes):>12}{len(units):>7}")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else DEFAULT)
