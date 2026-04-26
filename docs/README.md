# CivOne

This is a fork of the CivOne project, which has had various homes over many years. I have finished the various victory cases, added features and rules that were not complete, and re-focused on a larger screen size than the original 15x12 playable area (31x22). Go To actually works. The main screen tells you what your form of government is.

Because of the resize, the old city screen no longer rendered properly, so I have replaced it outright with a two-tone screen with a "cassette futurism" look. The sort of thing you could play on the Nostromo. I am slowly removing all the animations and replacing the various newspaper pop-ups with similar screens.

As for rule changes:
 + The "settler cheat" was not present in this open-source project and I left it that way.
 + No more building things on water (which I can't believe I never tried).
 + Rivers have the same 'move' as roads.
 + Caravans have the same 'move' as diplomats.
 + Improved bonuses to river mouths and coastlines, to reflect the gains of trade and rebalance what happens to inland cities at scale.
 + Cities have roads (and rail) by default; no more losing 1/3 move when you steam out of town.
 + Other civilizations can start diplomatic discussions.
 + Upgradable military units (like Civ 2).
 + Autosave always on. Yes, I have a lock-up or two.
 + AI with real strategy. Yes, that includes stacking units. Watch out!

We Love The King Day:
 + It only triggers only when the conditions are first met and not with each successive turn. (Though city scren shows a status of WLTK)
 + If a city cannot grow, you get a free caravan instead.

Platforms
 + Tested on Arm Macbook Air
 + Not tested on anything else

Technology
 + dotnet 10
 + YAML save files (hat tip to ChrisWi)
