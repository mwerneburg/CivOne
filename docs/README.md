# A sci-fi port of CivOne

## Note:

 1. I am replacing all of the original game's IP that is still in use, and that some graphics/colors are off.
 2. This port uses the original game's display dimensions. Please see below for display configuration.

This is a fork of the CivOne project, which has had various homes over many years. I have finished the various victory cases, added features and rules that were not complete, and re-focused on a larger screen size than the original 15x12 playable area (31x22). GoTo actually works. The main screen tells you what your form of government is.

Because of the resize, the old city screen no longer rendered properly, so I have replaced it outright with a two-tone screen with a "cassette futurism" look. The sort of thing you could play on the Nostromo. The reason being is that this clone leans heavily into the imaginative original design, which strongly featured a race to space colonization; I mean they kind of made that the point of the game!

## Game rules

Almost all rules are retained from the original.
 + Amusingly, Gandhi is still an irrational and implacable war-monger when you'd expect it least (held over from the repo as I found it).

Rule changes:
 + The "settler cheat" was not present in this open-source project and I left it that way.
 + In the repo as I found it, barracks never expire, so you don't have to rebuild.
 + No more building roads and rail on water (which I can't believe I never tried in the original).
 + Rivers have the same 'move' as roads. This code was optionally present in the <2017 repo.
 + Improved bonuses to river mouths and coastlines, to reflect the gains of trade and rebalance what happens to inland cities at scale.
 + Cities have roads (and rail) by default; no more losing 1/3 move when you steam out of town.
 + Other civilizations can start diplomatic discussions.
 + Autosave always on. I think the crash scenarios are fixed, but let's be reasonable.
 + AI with real strategy. Yes, that includes stacking units. Watch out!
 + A city's mass transit system now costs 50% more but gives 20% bonuses to food and shields produced (as a cornerstone of a modern city).
 + The South Pole Expedition wonder becomes possible after the creation of the Apollo mission. It has unexpected benefits and implications.
 + Five years after the creation of the SETI wonder, a signal is detected with a warning.
 + The Civilization Score chart does not cap annoyingly like the original.
 + I added the instant replay, but it came out a bit differently.
 + Roads and railways are much quicker to build.
 + Check box in options for Circuses (allows Colosseum)
 + Check box in options for Barricades (allows City Walls and SDI)
 + Added city improvement shipyard (allows transports, submarine, carrier, and battleship)
 + Overlays such as hills, swamps, and irrigation now load from a text file so you can redraw these to your liking.
 + Some wonders that historically happened on or near the sea can now only happen in a sea town (Colossus, Lighthouse, Magellan's Voyage, Darwin's Voyage)
 + Has a production queue, like other 4x games
 + Irrigation requires a freshwater source (rivers, lakes, wetlands)

We Love The King Day:
 + It only triggers only when the conditions are first met and not with each successive turn. (Though city scren shows a status of WLTK)
 + If a city starts WLTK but cannot grow, you get a free caravan instead.

Terrain generation
 + The terrain generation system in the <2017 repo was obviously different to the original.
 + I have modified it further:
  - drier than the original, meaning you can't expect to make unnaturally uniform city mosaics
  - equatorial jungle band
  - mid-lattitude arid bands
  - temperate forest bands
  - thick tundra/arctic bands
  - long north-south mountain ranges
  - stub rivers around the coasts
  - a few longer rivers per continent (still a bit wonky)

Civ II stuff
 + AI civilizations have to build wonders, they don't just plop.
 + Lowers the size a city can attain without an aqueduct from 11 to 7.
 + Upgradable military units. Want not, waste not.
 + The benefits of Copernicus's Observatory do not expire.
 + Caravans have the same 'move' as diplomats.
 + Settlers may auto-clean of pollution for all friendly cities.
 + Spy-vs-spy; a defending spy defeats the theft of technology.
 + (The code for Civ II "Freight" units was present in the <2017 repo but remains inactive.)

Platforms
 + Tested on Arm Macbook Air and Linux Mint on Intel
 + Not tested on anything else

Technology
 + C#, SDL
 + dotnet 10
 + YAML save files (hat tip to ChrisWi)

Known issues

1. Showstoppers
 + None known; fixed several

2. Less serious
 + The unit graphics in the garrison (city view) are badly downscaled/upscaled; also, the citizens are barely two-legged sticks. Bear with me, folks.
 + Battle animations are a bit herky-jerky, it was this way in the code repo from 2017 that I cloned.
 + There are lots and lots of natural disasters in the earlier phases.
 + Still a bit too easy to win.

## Display

The game runs in Expand mode by default, meaning the window and canvas grow to fill whatever space you give them. Each game pixel is always rendered at 2× on screen, so more screen real-estate means more of the map is visible — not just a stretched image.

The display is configured through your profile file at:

  - macOS: ~/Library/Application Support/CivOne/default.profile
  - Linux: ~/.config/CivOne/default.profile (or ~/.local/share/CivOne/default.profile)

A minimal profile looks like this:

  <?xml version="1.0" encoding="utf-8"?>
  <CivOneProfile>
    <AspectRatio>4</AspectRatio>
  </CivOneProfile>

AspectRatio value 4 is Expand mode. Without any other settings the window opens at 1152×720 with a 576×360 canvas.

Showing more of the map on a large display

Add ExpandWidth and ExpandHeight to set a fixed canvas size. The window will be sized so that the canvas fills it at the largest whole-number pixel scale that fits:

  <CivOneProfile>
    <AspectRatio>4</AspectRatio>
    <ExpandWidth>640</ExpandWidth>
    <ExpandHeight>400</ExpandHeight>
  </CivOneProfile>

On a 2560×1600 display this gives a 640×400 canvas rendered at 4× — chunky pixels and roughly twice the map area of the classic 320×200 view.

  Useful canvas sizes:

  ┌───────────────────┬──────────────────┬────────────────────────────────────────┐
  │      Canvas       │     Good for     │ Approx. pixel scale on common displays │
  ├───────────────────┼──────────────────┼────────────────────────────────────────┤
  │ 576×360 (default) │ 1920×1080 and up │ 3×                                     │
  ├───────────────────┼──────────────────┼────────────────────────────────────────┤
  │ 640×400           │ 2560×1600 / 2K   │ 4×                                     │
  ├───────────────────┼──────────────────┼────────────────────────────────────────┤
  │ 960×600           │ 3840×2160 / 4K   │ 4×                                     │
  └───────────────────┴──────────────────┴────────────────────────────────────────┘

The Scale setting (integer, 1–8) controls the initial window size hint. Set <Scale>4</Scale> if you want the window to open large before you resize it, but the canvas and pixel zoom are otherwise determined by the window size and any explicit ExpandWidth/ExpandHeight you provide.

## Debug menu

There is an in-game debug menu, disabled by default. To enable it: start a game, open Game → Options from the menu bar, and toggle "Debug Menu" to on. The setting persists across sessions (stored as `<DebugMenu>1</DebugMenu>` in `default.profile`, so you can also pre-set it there).

Once enabled, a "Debug Options" entry appears in the in-game menu, with:

 + Set Game Year — jump the calendar to any year
 + Set Player Gold — set the human player's treasury
 + Set Player Science — set accumulated science toward the current research
 + Set Player Advances — grant or revoke individual advances
 + Set City Size — resize any of your cities
 + Cause City Disaster — trigger a famine, fire, riot, etc. on a chosen city
 + Change Human Player — hand control to a different civilization
 + Spawn Unit — place any unit type on the map
 + Meet With King — open the diplomacy screen with any AI leader
 + Toggle Reveal World — flip the fog-of-war off and on
 + Show PowerGraph — show the score/power graph at any time

These are testing/debugging aids — they're cheats and are not balanced for play.
