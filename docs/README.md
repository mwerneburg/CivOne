# CivOne

This is a fork of the CivOne project, which has had various homes over many years. I have finished the various victory cases, added features and rules that were not complete, and re-focused on a larger screen size than the original 15x12 playable area (31x22). GoTo actually works. The main screen tells you what your form of government is.

Because of the resize, the old city screen no longer rendered properly, so I have replaced it outright with a two-tone screen with a "cassette futurism" look. The sort of thing you could play on the Nostromo. I am slowly removing all the animations and replacing the various newspaper pop-ups with similar screens.

Almost all rules are retained from the original.
 + Amusingly, Ghandi is still an irrational and implacable war-monger (held over from the repo as I found it).

As for rule changes:
 + The "settler cheat" was not present in this open-source project and I left it that way.
 + No more building roads and rail on water (which I can't believe I never tried in the original).
 + Rivers have the same 'move' as roads. This code was optionally present in the <2017 repo.
 + Caravans have the same 'move' as diplomats. The code for Civ 2 "Freight" units was present in the <2017 repo but not active.
 + Improved bonuses to river mouths and coastlines, to reflect the gains of trade and rebalance what happens to inland cities at scale.
 + Cities have roads (and rail) by default; no more losing 1/3 move when you steam out of town.
 + Other civilizations can start diplomatic discussions.
 + Upgradable military units (like Civ 2).
 + Autosave always on. I think the crash scenarios are fixed, but let's be reasonable.
 + AI with real strategy. Yes, that includes stacking units. Watch out!
 + If WLTKD can't grow a city, it spawns a free caravan.
 + A mass transit system now costs 50% more but gives 20% bonuses to food and shields produced (cornerstone of a modern city).
 + The South Pole Expedition wonder becomes possible after the creation of the Apollo mission. It has unexpected benefits.

We Love The King Day:
 + It only triggers only when the conditions are first met and not with each successive turn. (Though city scren shows a status of WLTK)
 + If a city starts WLTK but cannot grow, you get a free caravan instead.

Platforms
 + Tested on Arm Macbook Air
 + Not tested on anything else

Technology
 + C#
 + dotnet 10
 + YAML save files (hat tip to ChrisWi)

Known issues

1. Showstoppers
 + None known; fixed several

2. Less serious
 + Units moving under GoTo don't always get a map refresh (though the prior tearing is mostly gone).
 + The SpaceRace works (is winable) but still being tested.
 + The unit graphics in the garrison (city view) are badly downscaled/upscaled; also, the citizens are barely two-legged sticks. Bear with me, folks.
 + Battle animations are a bit herky-jerky, it was this way in the code repo from 2017 that I cloned.
 + Lots and lots of natural disasters.
 + Otherwise, seems a bit too easy.
