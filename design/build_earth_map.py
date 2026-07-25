#!/usr/bin/env python3
"""
build_earth_map.py — turn a real Earth elevation grayscale into a Civ-style
terrain grid the CivOne engine can load via Map.LoadEarthBin (see
src/Map.LoadSave.cs for the binary format and terrain-code conventions).

Inputs:
  --elev PATH    Equirectangular grayscale PNG/JPG. Brighter = higher elevation.
                 Image must cover the whole globe: left edge=180°W, right=180°E,
                 top=90°N, bottom=90°S. Any aspect ratio is rescaled.
  --width N      Output grid width  (default 320 — matches CivOne Epic).
  --height N     Output grid height (default 200 — matches CivOne Epic).
  --output PATH  Output binary file. Default is the platform's CivOne data
                 directory + earth_epic.bin so the MapPreview hotkey 'E'
                 picks it up automatically.
  --sea-level N  Pixel value (0-255) below which a tile is Ocean. Default 1
                 (matches NASA SRTM-derived images where ocean = 0). Raise to
                 shrink land mass, e.g. --sea-level 5 to drop shallow shelves.
  --hill-level N Pixel >= this becomes Hills. Default 60 (~75th percentile of
                 land pixels on the SRTM topography image).
  --mtn-level N  Pixel >= this becomes Mountains. Default 115 (~90th percentile).

Source data — any equirectangular elevation PNG works; recommended:
  - NASA Visible Earth "Topography 5400×2700"
      https://visibleearth.nasa.gov/images/73934/topography
  - Natural Earth "Cross Blended Hypso with Relief and Water"
      https://www.naturalearthdata.com/downloads/10m-raster-data/10m-cross-blend-hypso/
  - The "World Topo Bathy" derived images on Visible Earth
None of these need an account; they're public domain / CC0.

Terrain classification:
  pixel < sea-level                  → Ocean
  pixel in mid range                 → biome by latitude band (see LAT_BANDS)
  pixel high                         → Hills
  pixel very high                    → Mountains
  Plus a hand-tuned moisture mask    → forces Sahara/Arabia → Desert, Amazon
                                       → Jungle, etc., for plausibility.

The moisture mask is a small list of (lat_min, lat_max, lon_min, lon_max,
biome) triples in MOIST_OVERRIDES below. Coastline-sensitive cases (Sahara
ends at the Atlantic) are handled because Ocean tiles never get overridden.

Output bytes match the byte values used by MAP.PIC and Map.LoadEarthBin:
  0=Ocean 2=Forest 3=Swamp 6=Plains 7=Tundra 9=River
  10=Grassland 11=Jungle 12=Hills 13=Mountains 14=Desert 15=Arctic

Latitude window and warp (see LAT_WEIGHTS below):
  --lat-north/--lat-south  clip the sampled band. The default south edge of -60
                 drops Antarctica, which is unplayable and crowds Patagonia and
                 New Zealand against the engine's polar ice ring.
  --no-warp      sample latitude flat, as a plain equirectangular image. By
                 default rows are re-spent by LAT_WEIGHTS: fewer in the tropics,
                 half again as many across 30-60 deg, so the Mediterranean is a
                 sea rather than a puddle and the Congo stops dwarfing Europe.
  --edge-rows N  clearance between real land and the ice ring (scales with height).
  --max-rivers N how many of MAJOR_RIVERS to draw (scales with board area).

Usage examples:
  ./build_earth_map.py --elev ~/Downloads/topography.png          # 320x200 Epic
  ./build_earth_map.py --elev topo.png --width 80 --height 50 \
      --output ../resources/earth_standard.bin     # replaces the old MAP.PIC
  ./build_earth_map.py --elev topo.png --width 160 --height 100   # Huge size
  ./build_earth_map.py --elev topo.png --sea-level 100            # less land
  ./build_earth_map.py --elev topo.png --lat-south -90 --no-warp  # old behaviour
"""

import argparse
import os
import struct
import sys
from pathlib import Path

try:
    from PIL import Image
    import numpy as np
except ImportError as e:
    sys.exit(f"missing dependency: {e}. Install with: pip3 install Pillow numpy")

# ── terrain codes (match MAP.PIC and src/Map.LoadSave.cs:LoadEarthBin) ────────
OCEAN, FOREST, SWAMP, PLAINS, TUNDRA, RIVER = 0, 2, 3, 6, 7, 9
GRASS, JUNGLE, HILLS, MTNS, DESERT, ARCTIC  = 10, 11, 12, 13, 14, 15

# ── latitude bands ───────────────────────────────────────────────────────────
# (south_lat, north_lat, default_biome)
# Bands are queried from the equator outward; first match wins.
LAT_BANDS = [
    ( 0, 15, JUNGLE),    # tropical
    (15, 30, GRASS),     # subtropical — was DESERT, which turned monsoon India and
                         # the wet subtropics into dead sand. The real deserts at this
                         # latitude (Sahara, Arabia, Thar, Kalahari, Australia, American
                         # SW) are explicit MOIST_OVERRIDES below, so they stay desert.
    (30, 45, GRASS),     # warm temperate — the world's grasslands/steppe/prairie
    (45, 60, GRASS),     # cool temperate — was FOREST; defaulted the whole temperate
                         # belt (every civ heartland) to 1-food forest, which starved
                         # cities under Despotism. blend_biomes() sprinkles forest and
                         # wetland back in so it's mixed grassland, not a green carpet.
    (60, 70, TUNDRA),    # subarctic
    (70, 90, ARCTIC),    # polar
]

# ── moisture overrides — small, hand-curated regions where the latitude-band
# default is obviously wrong. lon_min and lon_max are in degrees, west=negative.
# Wraps the antimeridian if lon_min > lon_max.
# Order matters: later overrides win.
MOIST_OVERRIDES = [
    # Sahara + Arabia (vast dry belt 15-30N across Africa to Persian Gulf)
    (15, 30, -18,  60, DESERT),
    # Sahel / Sudano-Sahelian (savanna grass below Sahara)
    (10, 18,  -18, 40, GRASS),
    # Congo + Amazon + Indonesia rainforests
    (-10, 10, -78, -45, JUNGLE),  # Amazon basin
    (-10, 10,  10,  35, JUNGLE),  # Congo
    (-10, 10,  95, 145, JUNGLE),  # Indonesia / New Guinea
    # Kalahari + Namib (southern Africa)
    (-30, -18, 12,  25, DESERT),
    # Patagonia steppe (cool, dry)
    (-50, -38, -75, -65, PLAINS),
    # Atacama (chilean coastal desert)
    (-30, -18, -72, -68, DESERT),
    # Gobi. Was 40-50N, which pushed sand right over Mongolia proper and left
    # the Mongols' start (47N 105E) in desert; the real Gobi stops around 46N and
    # the steppe above it is grassland.
    (41, 46,  92, 112, DESERT),
    # Central Asia steppe
    (40, 50,  55,  90, PLAINS),
    # Australia outback (huge dry interior)
    (-30, -18, 118, 145, DESERT),
    # Australia tropical north (Top End / Cape York)
    (-20, -10, 130, 145, JUNGLE),
    # Indian subcontinent — wet on the east (Bengal), arid in Rajasthan
    ( 20, 30,  68,  78, DESERT),  # Thar
    ( 20, 28,  85,  95, JUNGLE),  # Bengal
    # Mediterranean / Iberia / Anatolia — warm scrub, not jungle
    ( 30, 42, -10,  45, PLAINS),
    # Great Plains / Mexican Plateau
    ( 25, 45, -110, -95, PLAINS),
    # American Southwest deserts
    ( 28, 38, -120, -106, DESERT),
    # Boreal forests of Canada + Siberia (taiga)
    ( 50, 65, -160,  -60, FOREST),
    ( 50, 65,   60,  170, FOREST),
    # Arabian fertile pockets. The Sahara/Arabia belt above paints the whole
    # peninsula desert, which left the Arabs' start (centroid 24N 45E) in
    # unbroken sand with nothing to farm. These are the places Arabia is
    # genuinely arable, so the civ has somewhere to grow without the peninsula
    # ceasing to be a desert.
    ( 12, 18,   42,  46, GRASS),   # Yemen/Asir highlands — monsoon-fed terraces
    ( 23, 26, 38.5,  41, PLAINS),  # Medina/Khaybar oasis chain in the Hejaz
    ( 25, 27, 48.5,50.5, PLAINS),  # Al-Hasa/Qatif — the largest oasis on earth
    ( 17, 23,   53,  59, PLAINS),  # Dhofar and the Omani monsoon coast
    # Wetland patches — eastern Europe and the Middle East are otherwise an
    # uninterrupted carpet of Forest/Plains/Desert with no swamp variation, so
    # the lower-Dnieper steppe and the Tigris-Euphrates basin play like astroturf.
    # These boxes are tight (a few tiles each) so we don't drown the surrounding
    # biomes — just enough swamp variety for irrigation chains and local realism.
    ( 50, 53,  25,  30, SWAMP),   # Pripyat Marshes (largest wetland in Europe)
    ( 45, 47,  46,  49, SWAMP),   # Volga delta + Caspian Lowland steppe wetland
    ( 30, 32,  46,  48, SWAMP),   # Mesopotamian marshes (Tigris-Euphrates confluence)
    # Greenland interior, Antarctica handled by the >70 band automatically.
]

# ── Major rivers ──────────────────────────────────────────────────────────────
# Each river is a (name, [(lat, lon), ...]) polyline traced source → mouth.
# Coordinates are approximate, picked to read recognisably at 320×200; you can
# adjust individual waypoints or add more rivers without touching the engine.
# When the script rasterises a river, any *land* tile crossed is forced to River.
# Ocean tiles are left alone so the river ends cleanly at the coast.
#
# Source data: hand-traced from Natural Earth's "Rivers, lake centerlines"
# 10m vector dataset (https://www.naturalearthdata.com/downloads/10m-physical-vectors/
# 10m-rivers-lake-centerlines/, CC0). To add more rivers cheaply, open that
# dataset in QGIS, sample 5-10 waypoints per river, paste tuples here.
# Listed most-significant first: the river budget on small boards takes the
# first N of this list (see default_max_rivers), because 26 polylines that read
# as a delicate tracery at 320x200 swallow an eighth of all land at 80x50.
MAJOR_RIVERS = [
    ("Amazon",       [( -4.3, -71.0), ( -3.5, -64.0), ( -2.5, -55.0), ( -1.5, -50.0), ( -0.5, -48.0)]),
    ("Nile",         [(  1.5, 32.0), (  9.5, 31.0), ( 18.5, 31.5), ( 26.0, 31.0), ( 31.5, 30.5)]),
    ("Mississippi",  [( 47.5, -95.0), ( 43.0, -91.0), ( 38.0, -90.0), ( 33.0, -91.0), ( 29.5, -89.0)]),
    ("Missouri",     [( 47.0,-112.0), ( 47.0,-104.0), ( 45.0, -97.0), ( 41.0, -95.0), ( 38.7, -90.2)]),
    ("Yangtze",      [( 31.5, 91.0), ( 30.5, 105.0), ( 30.5, 114.0), ( 31.5, 120.0), ( 31.5, 122.0)]),
    ("Yellow",       [( 35.0, 96.0), ( 36.0, 105.0), ( 37.0, 110.0), ( 37.5, 116.0), ( 37.7, 119.0)]),
    ("Mekong",       [( 33.0, 95.0), ( 23.0, 101.0), ( 18.0, 104.0), ( 13.0, 105.0), ( 10.5, 106.5)]),
    ("Ganges",       [( 30.5, 79.0), ( 27.0, 83.0), ( 25.5, 87.0), ( 23.5, 88.5), ( 22.0, 91.0)]),
    ("Indus",        [( 32.0, 79.0), ( 32.0, 73.0), ( 28.5, 70.0), ( 25.0, 68.0), ( 24.0, 67.5)]),
    ("Volga",        [( 57.0, 33.0), ( 56.0, 45.0), ( 51.5, 47.0), ( 48.5, 45.0), ( 45.5, 47.5)]),
    ("Dnieper",      [( 54.8, 32.0), ( 52.5, 31.0), ( 50.5, 30.5), ( 48.5, 35.0), ( 47.8, 33.6), ( 46.5, 32.5)]),
    ("Danube",       [( 48.0,  8.0), ( 48.0, 16.0), ( 45.0, 21.0), ( 44.0, 27.0), ( 45.2, 29.6)]),
    ("Rhine",        [( 47.0,  9.5), ( 50.0,  7.0), ( 51.5,  6.5), ( 52.0,  5.5), ( 52.0,  4.5)]),
    ("Niger",        [( 11.0,-10.0), ( 15.0, -5.0), ( 16.0,  0.5), ( 12.0,  4.0), (  5.0,  6.0)]),
    ("Congo",        [(  3.0, 26.0), (  0.0, 22.0), ( -2.0, 18.0), ( -4.5, 15.0), ( -6.0, 12.5)]),
    ("Zambezi",      [(-13.0, 24.0), (-16.0, 28.0), (-17.5, 32.0), (-18.0, 35.5)]),
    ("Lena",         [( 53.5,106.0), ( 60.0,118.0), ( 65.0,124.0), ( 70.0,127.0), ( 72.5,127.0)]),
    ("Ob",           [( 51.0, 85.0), ( 56.0, 76.0), ( 60.0, 71.0), ( 65.0, 67.0), ( 67.0, 70.0)]),
    ("Yenisei",      [( 51.5, 95.0), ( 57.0, 92.0), ( 62.0, 90.0), ( 67.0, 86.0), ( 71.0, 82.5)]),
    ("Murray",       [(-35.0,148.0), (-34.0,143.0), (-34.5,140.0), (-35.5,139.0)]),
    ("Parana",       [(-19.0,-50.0), (-23.0,-54.0), (-27.0,-56.0), (-32.0,-58.0), (-34.0,-58.5)]),
    ("Orinoco",      [(  3.0,-65.0), (  6.0,-67.0), (  8.0,-65.0), (  9.0,-62.0), (  8.5,-60.5)]),
    ("Colorado",     [( 40.5,-105.5), ( 38.0,-110.5), ( 36.0,-112.5), ( 33.0,-114.5), ( 31.8,-114.6)]),
    ("Rio Grande",   [( 38.0,-106.5), ( 35.0,-106.5), ( 31.5,-106.0), ( 27.0, -99.5), ( 25.9, -97.2)]),
    ("Saint Lawrence",[( 44.0, -76.5), ( 46.0, -73.5), ( 47.0, -71.0), ( 48.5, -68.0), ( 50.0, -65.0)]),
    ("Mackenzie",    [( 61.0,-117.0), ( 65.0,-125.0), ( 68.0,-130.0), ( 69.5,-134.0)]),
]

# ── Land-force polygons ──────────────────────────────────────────────────────
# Regions whose real-world elevation is so close to sea level that the
# elevation pass writes them as Ocean. Each entry is
# (name, biome_code, [(lat, lon), ...]). The carver only converts tiles that
# are currently Ocean — it never overwrites Hills/Mountains/biome that the
# elevation pass classified correctly. Applied BEFORE lakes/rivers so those
# passes can still cut through the forced land normally.
LAND_FORCES = [
    # Florida: pancake-flat peninsula, real elevation mostly < 30 m. Use SWAMP
    # because the southern half is the Everglades and the engine treats Swamp
    # as a freshwater source (settler-friendly). North Florida is technically
    # higher and drier (pine woods) — refine into two polygons if you want
    # northern Plains + southern Swamp.
    ("Florida", SWAMP, [
        ( 30.5, -87.5), ( 31.0, -85.0), ( 31.0, -82.0),
        ( 27.5, -80.0), ( 25.0, -80.4), ( 24.6, -81.5),
        ( 26.0, -81.8), ( 28.0, -82.7), ( 29.5, -83.4),
        ( 30.2, -86.0),
    ]),
]

# ── Strait overrides ──────────────────────────────────────────────────────────
# Real-world straits (Gibraltar, Bosporus, Dardanelles, etc.) are narrower than
# our 1.125°/pixel longitude resolution, so the procedural elevation pipeline
# always rounds them to "land" and turns enclosed seas into lakes. List one
# (lat, lon, name) per strait to force-Ocean exactly one tile and reopen the
# corresponding sea passage. Search radius is 1 tile so a slight latitude/
# longitude miss still hits the right corridor.
STRAITS = [
    (36.0,  -5.5, "Gibraltar"),     # opens Mediterranean to Atlantic
    (12.5,  43.3, "Bab-el-Mandeb"), # opens Red Sea to Indian Ocean
    (26.3,  56.3, "Hormuz"),        # opens Persian Gulf to Indian Ocean
    ( 1.2, 103.8, "Malacca"),       # Sumatra/Malay strait (may already be open at this resolution)
]

# A one-tile-wide ocean lane hugging the North African coast, from the Atlantic west of
# Gibraltar east to the waters off Tripoli, so the Mediterranean opens to the Atlantic. At
# ~1.1°/tile the basin otherwise floods to an isolated inland lake (Gibraltar's lone strait
# tile can't bridge it). Traced (lat, lon) just along the coast: Morocco/Algeria/Tunisia at
# ~36-37°N, dipping toward Tripoli at ~33°N. carve_sea_channel only fills the land gaps.
MED_CHANNEL = [
    (35.8, -7.5), (35.7, -5.5), (36.2, -1.0), (36.7,  4.0),
    (37.0,  9.0), (35.0, 11.5), (33.5, 13.5),
]


# ── Major lakes ───────────────────────────────────────────────────────────────
# Each entry forces *all* tiles inside the bounding box to Ocean. After the
# load, the engine's ComputeFreshwaterLakes pass at Map.cs detects any inland
# Ocean tile as freshwater, so these come back as proper lakes automatically.
# Boxes are (lat_min, lat_max, lon_min, lon_max, name).
#
# Source data: bounding boxes hand-derived from Natural Earth's 10m Lakes
# polygon set. Boxes are intentionally tight to avoid swallowing coast.
MAJOR_LAKES = [
    # North America — the Laurentian Great Lakes are now polygons (see below) so
    # their distinctive orientations read better than five rectangles.
    ( 60.5, 63.0,-117.0,-108.0, "GreatSlave"),
    ( 65.0, 67.0,-125.0,-119.0, "GreatBear"),
    # Eurasia — Aral, Balkhash, Baikal and the Baltic lowland lakes (Ladoga,
    # Onega, Peipus, Saimaa) are all polygons now; see below. The old "Ladoga"
    # box (60-63N, 31.5-35.5E) was both square and ~1.5 deg too far north, and
    # it swallowed the gap where Lake Onega belongs.
    # "Tana" box (lat -15..-12.5, lon 33.5-36) removed: it was not Lake Tana
    # (that is in Ethiopia, +12°N) — it sat on the southern end of Lake Malawi and
    # gave it an unrealistic rectangular bulge. Malawi's polygon now runs the lake's
    # full length, and the real Lake Tana is added as a polygon below.
    # "WestSiberian" (lat 60-62, lon 78-85) removed: there is no present-day lake
    # in the West Siberian Plain — that area is the Vasyugan lowland/swamp, not
    # open water. Let the elevation pass decide the terrain there.
]

# Lakes whose real-world shorelines are too irregular to approximate as a
# bounding box at our 1.125°×0.9° tile resolution. Each entry is
# (name, [(lat, lon), ...]) — a closed polygon traced from Natural Earth's
# 10m Lakes polygon set, simplified to 5-9 vertices. Rasterised via PIL's
# polygon filler, so all tiles inside the shape become Ocean (then engine
# flags them freshwater on load). Refine via QGIS if you want tighter shapes.
MAJOR_LAKE_POLYGONS = [
    # Caspian: 1200 km N-S, narrows in middle, widest in the north.
    ("Caspian", [
        ( 47.0, 51.5), ( 46.0, 53.0), ( 43.0, 51.0), ( 40.0, 52.5),
        ( 37.5, 53.5), ( 36.5, 50.5), ( 39.0, 48.7), ( 42.0, 47.5),
        ( 45.5, 47.5),
    ]),
    # Lake Chad: small irregular oval (the modern shoreline, not Mega-Chad).
    ("Chad", [
        ( 14.0, 14.0), ( 13.7, 14.8), ( 13.0, 14.6),
        ( 12.7, 13.7), ( 13.3, 13.5),
    ]),
    # Victoria: large irregular body, slightly squarer than the others.
    ("Victoria", [
        (  0.3, 32.6), (  0.0, 34.5), ( -2.5, 34.7),
        ( -3.0, 32.2), ( -1.5, 31.6),
    ]),
    # Tanganyika: long narrow rift lake, ~670 km × 50 km. Kept ~1.2° wide so the
    # near-vertical strip stays continuous when rasterised at 1.1°/tile (a thinner
    # outline drops tiles and breaks the lake into segments).
    ("Tanganyika", [
        ( -3.3, 28.9), ( -3.3, 29.7), ( -6.0, 30.3), ( -8.9, 31.1),
        ( -8.9, 30.1), ( -6.0, 29.1),
    ]),
    # Malawi: long narrow rift lake, ~580 km × 75 km. Runs the full length to its
    # southern basin (~-14.5) now that the bogus "Tana" box no longer covers it.
    ("Malawi", [
        ( -9.4, 33.9), (-11.5, 34.3), (-13.0, 34.7), (-14.5, 35.2),
        (-14.5, 34.6), (-13.0, 34.1), (-11.5, 33.8),
    ]),
    # Turkana: narrow N-S rift lake.
    ("Turkana", [
        (  4.6, 35.9), (  3.5, 36.4), (  2.4, 36.2), (  3.5, 35.7),
    ]),
    # ── Laurentian Great Lakes ──────────────────────────────────────────────
    # Each traced to its real orientation rather than a bounding box: Superior
    # tilts ENE, Michigan runs N-S, Huron hooks NW-SE, Erie/Ontario are thin
    # E-W ribbons. At ~1.1°/tile these are small, but the outlines stop the
    # cluster reading as five stacked rectangles.
    ("Superior", [
        ( 48.4, -91.5), ( 48.5, -88.0), ( 47.6, -84.8),
        ( 46.7, -85.2), ( 46.9, -88.5), ( 47.6, -91.8),
    ]),
    ("Michigan", [
        ( 45.9, -86.7), ( 44.2, -86.0), ( 42.2, -86.4),
        ( 41.6, -87.2), ( 43.2, -87.7), ( 45.4, -87.4),
    ]),
    ("Huron", [
        ( 46.3, -84.3), ( 46.0, -82.0), ( 44.4, -80.2),
        ( 43.1, -81.8), ( 44.1, -83.5), ( 45.4, -84.4),
    ]),
    ("Erie", [
        ( 42.9, -83.3), ( 42.7, -80.0), ( 42.2, -78.7),
        ( 41.5, -81.5), ( 41.9, -83.2),
    ]),
    ("Ontario", [
        ( 44.4, -79.8), ( 44.2, -76.8), ( 43.5, -75.7),
        ( 43.3, -78.5), ( 43.8, -79.7),
    ]),
    # ── Central Asia ────────────────────────────────────────────────────────
    # Aral: oval, elongated N-S, widest in the middle (1960 shoreline).
    ("Aral", [
        ( 46.8, 60.0), ( 46.0, 61.6), ( 44.4, 61.3),
        ( 43.5, 59.8), ( 44.3, 58.4), ( 45.9, 58.8),
    ]),
    # Balkhash: long crescent, narrows and bends south-east at its midpoint.
    ("Balkhash", [
        ( 46.4, 73.2), ( 46.6, 75.0), ( 45.9, 77.5),
        ( 45.0, 79.0), ( 44.8, 77.8), ( 45.6, 75.5), ( 45.8, 73.5),
    ]),
    # Baikal: long, narrow, crescent rift lake oriented NE-SW (~636 km × 50 km).
    # Was a fat 6-tile box; this traces the thin diagonal sliver instead.
    ("Baikal", [
        ( 55.6, 110.4), ( 53.4, 107.8), ( 51.4, 104.8),
        ( 51.6, 103.6), ( 53.6, 106.5), ( 55.8, 109.2),
    ]),
    # ── Baltic lowlands ─────────────────────────────────────────────────────
    # Northern Europe's lake belt: glacial basins with long, irregular
    # shorelines. Traced to their real extents so the region reads as scoured
    # lowland rather than a row of rectangles.
    ("Ladoga", [
        ( 61.6, 31.0), ( 61.3, 32.4), ( 60.5, 32.9),
        ( 59.95, 31.6), ( 60.3, 30.1), ( 61.0, 29.9),
    ]),
    ("Onega", [
        ( 62.9, 34.8), ( 62.4, 35.9), ( 61.7, 36.5),
        ( 60.95, 35.2), ( 61.5, 34.3), ( 62.3, 34.4),
    ]),
    ("Peipus", [
        ( 59.0, 27.5), ( 58.6, 28.2), ( 57.9, 27.9), ( 58.2, 27.0), ( 58.7, 27.1),
    ]),
    ("Saimaa", [
        ( 62.0, 28.0), ( 61.7, 29.3), ( 61.1, 29.0), ( 61.0, 27.8), ( 61.5, 27.5),
    ]),
    # ── East Africa ─────────────────────────────────────────────────────────
    # Lake Tana (Ethiopia, source of the Blue Nile) — the REAL one at +12°N, not
    # the mislabelled box that used to sit on Lake Malawi's southern end.
    ("Tana", [
        ( 12.3, 37.0), ( 12.2, 37.6), ( 11.6, 37.5), ( 11.6, 37.0),
    ]),
]


def draw_river_polyline(grid: np.ndarray, waypoints, sea_level: int = 1) -> int:
    """Rasterise each segment as a 4-connected path of River tiles. Returns the
    number of tiles converted (helpful for sanity-checking the polyline).

    The CivOne tile renderer at src/Graphics/Sprites/MapTile.cs:478-503 picks the
    river sprite from a 4-bit mask of orthogonal (N/E/S/W) river neighbours.
    Bresenham produces 8-connected paths, so a pure diagonal step leaves the two
    endpoints diagonally adjacent only — the renderer treats each as an isolated
    stub, producing "discontinuous thread" rivers. We fix that here by inserting
    an orthogonal stepping-stone tile on every diagonal step, guaranteeing every
    river tile has at least one orthogonal river neighbour."""
    h, w = grid.shape
    painted = 0

    def paint(x: int, y: int) -> int:
        if not (0 <= x < w and 0 <= y < h): return 0
        if grid[y, x] == OCEAN: return 0
        if grid[y, x] == RIVER: return 0
        grid[y, x] = RIVER
        return 1

    pts = []
    for lat, lon in waypoints:
        y = row_for_lat(lat, h)
        x = max(0, min(w - 1, int(((lon + 180.0) / 360.0) * w)))
        pts.append((x, y))
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        dx = abs(x1 - x0); dy = -abs(y1 - y0)
        sx = 1 if x0 < x1 else -1
        sy = 1 if y0 < y1 else -1
        err = dx + dy
        cx, cy = x0, y0
        while True:
            painted += paint(cx, cy)
            if cx == x1 and cy == y1: break
            e2 = 2 * err
            step_x = (e2 >= dy)
            step_y = (e2 <= dx)
            if step_x and step_y:
                # Diagonal step: paint a horizontal stepping stone first so the
                # destination tile ends up 4-connected to the path behind it.
                # Picking horizontal consistently keeps the river one-tile wide
                # except on diagonals, where it briefly thickens to two.
                painted += paint(cx + sx, cy)
                err += dy + dx
                cx += sx; cy += sy
            elif step_x:
                err += dy; cx += sx
            elif step_y:
                err += dx; cy += sy
    return painted


def carve_strait(grid: np.ndarray, lat: float, lon: float) -> bool:
    """Carve one strait tile: find the closest land tile within 1-tile radius of
    the target lat/lon and flip it to Ocean. Returns True if a tile was carved.
    Used to reopen narrow sea passages (Gibraltar, Bosporus, Suez) that the
    elevation pipeline's coarse resolution always closes off."""
    h, w = grid.shape
    cy = row_for_lat(lat, h)
    cx = int(((lon + 180.0) / 360.0) * w)
    for dy in (0, -1, 1):
        for dx in (0, -1, 1):
            x = (cx + dx + w) % w
            y = cy + dy
            if y < 0 or y >= h: continue
            if grid[y, x] != OCEAN:
                grid[y, x] = OCEAN
                return True
    return False


def carve_sea_channel(grid: np.ndarray, waypoints) -> int:
    """Rasterise a 4-connected polyline, flipping every land tile it crosses to Ocean.
    A single strait shave (carve_strait) only opens one tile; this cuts a continuous
    one-tile-wide lane where the coarse resolution seals a whole sea off — e.g. the
    Mediterranean, which floods to a 140-tile inland lake until a channel along the
    North African coast reconnects it to the Atlantic. Tiles already Ocean are left
    as-is, so the lane only fills the land gaps. Returns the number of tiles carved."""
    h, w = grid.shape
    carved = 0

    def carve(x: int, y: int):
        nonlocal carved
        if not (0 <= x < w and 0 <= y < h): return
        if grid[y, x] != OCEAN:
            grid[y, x] = OCEAN
            carved += 1

    pts = []
    for lat, lon in waypoints:
        y = row_for_lat(lat, h)
        x = max(0, min(w - 1, int(((lon + 180.0) / 360.0) * w)))
        pts.append((x, y))
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        dx = abs(x1 - x0); dy = -abs(y1 - y0)
        sx = 1 if x0 < x1 else -1
        sy = 1 if y0 < y1 else -1
        err = dx + dy
        cx, cy = x0, y0
        while True:
            carve(cx, cy)
            if cx == x1 and cy == y1: break
            e2 = 2 * err
            step_x = (e2 >= dy)
            step_y = (e2 <= dx)
            if step_x and step_y:
                carve(cx + sx, cy)   # orthogonal stepping stone keeps the lane 4-connected
                err += dy + dx; cx += sx; cy += sy
            elif step_x:
                err += dy; cx += sx
            elif step_y:
                err += dx; cy += sy
    return carved


def carve_lake_box(grid: np.ndarray, lat_min: float, lat_max: float, lon_min: float, lon_max: float) -> int:
    """Force all tiles inside the lat/lon box to Ocean. Returns count carved."""
    h, w = grid.shape
    y0 = row_for_lat(lat_max, h)
    y1 = row_for_lat(lat_min, h)
    x0 = int(((lon_min + 180.0) / 360.0) * w)
    x1 = int(((lon_max + 180.0) / 360.0) * w)
    y0, y1 = max(0, y0), min(h, y1 + 1)
    x0, x1 = max(0, x0), min(w, x1 + 1)
    region = grid[y0:y1, x0:x1]
    n_change = int(np.sum(region != OCEAN))
    region[:] = OCEAN
    return n_change


def force_land_polygon(grid: np.ndarray, polygon, biome: int) -> int:
    """Force every Ocean tile inside the lat/lon polygon to the given biome.
    Non-Ocean tiles (Hills/Mountains/biome) are left as the elevation pass set
    them, so a polygon spanning a coastal range only converts the truly-flooded
    cells. Returns count converted."""
    from PIL import Image, ImageDraw
    h, w = grid.shape
    pixels = []
    for lat, lon in polygon:
        x = ((lon + 180.0) / 360.0) * w
        y = row_for_lat_f(lat, h)
        pixels.append((x, y))
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).polygon(pixels, fill=1)
    mask_arr = np.array(mask, dtype=bool)
    target = mask_arr & (grid == OCEAN)
    n = int(np.sum(target))
    grid[target] = biome
    return n


def carve_lake_polygon(grid: np.ndarray, polygon) -> int:
    """Force all tiles inside the lat/lon polygon to Ocean. Returns count carved.
    Polygon is a list of (lat, lon) vertices, automatically closed. Uses PIL's
    polygon filler so shapes like Caspian and Tanganyika get realistic outlines
    instead of bounding-box squares."""
    from PIL import Image, ImageDraw
    h, w = grid.shape
    # Convert lat/lon vertices to pixel coords (col=x, row=y).
    pixels = []
    for lat, lon in polygon:
        x = ((lon + 180.0) / 360.0) * w
        y = row_for_lat_f(lat, h)
        pixels.append((x, y))
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).polygon(pixels, fill=1)
    mask_arr = np.array(mask, dtype=bool)
    n_change = int(np.sum(mask_arr & (grid != OCEAN)))
    grid[mask_arr] = OCEAN
    return n_change

# Elevation thresholds default to values tuned for the NASA Visible Earth
# SRTM "Topography" 5400×2700 image. CLI flags --hill-level / --mtn-level
# override them — see the docstring at the top of the file for how to pick
# new ones for a different elevation source.
DEFAULT_SEA_LEVEL  = 1
DEFAULT_HILL_PIXEL = 60
DEFAULT_MTN_PIXEL  = 115

# ── latitude window + warp ───────────────────────────────────────────────────
# The source image is equirectangular: every degree of latitude gets the same
# number of rows. That is a poor fit for a playable board.
#
#   1. Antarctica. The bottom ~17% of a full-globe map is a continent nobody
#      can use, and it crowds Patagonia/New Zealand/Tasmania against the
#      engine's polar ice ring (Map.Generate.cs:CreatePoles forces row 0 and
#      row HEIGHT-1 to Arctic). Clipping the window at DEFAULT_LAT_SOUTH drops
#      Antarctica entirely and leaves those southern lands well clear of it.
#
#   2. Tile density. Under equirectangular sampling a tropical tile covers far
#      more ground east-west than a temperate one (a degree of longitude is
#      111 km at the equator, 71 km at 50°N), so the Congo sprawls while the
#      Mediterranean is squeezed into a puddle. LAT_WEIGHTS re-spends the rows:
#      relative rows-per-degree by |latitude|, integrated and normalised over
#      the window. Above 1.0 means "give this band more rows than a flat map
#      would"; below means fewer. Tune freely — the whole pipeline (biomes,
#      rivers, lakes, straits, polygons) reads latitude through the same
#      lat_for_row/row_for_lat pair, so nothing drifts out of register.
#
# Pass --no-warp for the old flat mapping, or --lat-south -90 to keep the ice.
DEFAULT_LAT_NORTH  =  90.0
DEFAULT_LAT_SOUTH  = -60.0   # ~4°/7 rows south of Cape Horn (-55.9)
# Rows of clearance between real land and the ice ring, scaled to the board so
# a small map does not lose a whole peninsula to a margin sized for the Epic
# one: ~3 rows at 320x200, 2 at 160x100, 1 at 80x50. Override with --edge-rows.
def default_edge_rows(height: int) -> int:
    return max(1, round(height / 64.0))


def default_max_rivers(width: int, height: int) -> int:
    """River polylines to draw, scaled by board area against the 320x200 Epic
    reference. Keeps rivers at roughly a constant share of land: all 26 on Epic,
    ~8 on the 80x50 standard board."""
    share = (width * height) / 64000.0
    return max(8, min(len(MAJOR_RIVERS), round(len(MAJOR_RIVERS) * share ** 0.5)))

LAT_WEIGHTS = [
    ( 0, 10, 0.55),   # deep tropics — the worst of the sprawl
    (10, 23, 0.70),
    (23, 35, 1.15),
    (35, 50, 1.50),   # Mediterranean, China, the American midwest, the Levant
    (50, 62, 1.30),   # northern Europe, Great Lakes, southern Siberia
    (62, 72, 0.85),
    (72, 90, 0.50),   # ice and empty ocean; also the buffer above Greenland
]

# Row edges in degrees, north-to-south, length height+1. Populated by
# build_lat_edges() before any lat/lon conversion happens.
LAT_EDGES: "np.ndarray | None" = None


def weight_for_lat(abs_lat: float) -> float:
    for lo, hi, weight in LAT_WEIGHTS:
        if lo <= abs_lat < hi:
            return weight
    return LAT_WEIGHTS[-1][2]


def build_lat_edges(height: int, north: float, south: float, warp: bool) -> np.ndarray:
    """Return height+1 latitude edges, descending from `north` to `south`.

    Integrates the LAT_WEIGHTS density over the window and inverts it, so each
    output row covers an equal share of *weighted* latitude rather than an equal
    number of degrees. With warp=False this degenerates to the flat mapping."""
    lats = np.linspace(north, south, 40001)
    w = np.ones_like(lats) if not warp else np.array([weight_for_lat(abs(l)) for l in lats])
    step = abs(lats[1] - lats[0])
    cum = np.concatenate(([0.0], np.cumsum(w[1:] * step)))
    cum *= height / cum[-1]
    return np.interp(np.arange(height + 1), cum, lats)

# ── helpers ──────────────────────────────────────────────────────────────────

def default_output_path() -> Path:
    """Match what MapPreview.LoadEarth reads from. Engine resolves the path via
    Settings.DataDirectory (see src/Settings.cs:54) which puts everything under
    StorageDirectory/data/, so include that subdirectory here."""
    home = Path.home()
    if sys.platform == "darwin":
        base = home / "Library" / "Application Support" / "CivOne"
    elif sys.platform.startswith("win"):
        base = home / "AppData" / "Roaming" / "CivOne"
    else:
        base = home / ".local" / "share" / "CivOne"
    return base / "data" / "earth_epic.bin"

def lat_for_row(y: int, height: int) -> float:
    """Centre latitude of output row y. Row 0 is the northern edge of the
    window. Reads LAT_EDGES, so it honours both the clip and the warp."""
    e = LAT_EDGES
    y = max(0, min(height - 1, y))
    return float((e[y] + e[y + 1]) / 2.0)

def row_for_lat(lat: float, height: int) -> int:
    """Inverse of lat_for_row: the output row containing this latitude.
    Latitudes outside the window clamp to the first/last row — callers that
    care (the river/lake/polygon passes) are all no-ops on ocean anyway."""
    e = LAT_EDGES
    # e descends, so negate to give np.interp an increasing x-axis.
    y = int(np.interp(-lat, -e, np.arange(height + 1)))
    return max(0, min(height - 1, y))

def row_for_lat_f(lat: float, height: int) -> float:
    """Fractional row, for polygon rasterisation where sub-tile precision
    keeps small lakes from collapsing."""
    e = LAT_EDGES
    return float(np.interp(-lat, -e, np.arange(height + 1)))

def lon_for_col(x: int, width: int) -> float:
    """x=0 is 180°W (left edge), wrapping. Most equirectangular images
    publish with x=0 at the antimeridian, but in practice the offsets are
    image-specific. The terrain output wraps east-west the same way the
    engine does (cylindrical world), so a 180° offset only shifts the map
    horizontally — it doesn't break gameplay."""
    return -180.0 + (360.0 * (x + 0.5) / width)

def biome_for_lat(lat: float) -> int:
    abs_lat = abs(lat)
    for south, north, biome in LAT_BANDS:
        if south <= abs_lat < north:
            return biome
    return ARCTIC

def in_lon_range(lon: float, lon_min: float, lon_max: float) -> bool:
    if lon_min <= lon_max:
        return lon_min <= lon <= lon_max
    return lon >= lon_min or lon <= lon_max

def moisture_override(lat: float, lon: float) -> int | None:
    out = None
    for lat_min, lat_max, lon_min, lon_max, biome in MOIST_OVERRIDES:
        if lat_min <= lat <= lat_max and in_lon_range(lon, lon_min, lon_max):
            out = biome
    return out

def build_jitter(width: int, height: int, tiles: float, seed: int = 20260725):
    """Per-tile lat/lon offsets used only when looking up the biome.

    MOIST_OVERRIDES and LAT_BANDS are rectangles and horizontal strips, so their
    edges land as dead-straight lines: the Gobi and the Australian outback read
    as painted boxes rather than deserts. Offsetting the *lookup* coordinate by a
    coherent random field breaks those edges into a ragged fringe a tile or two
    deep, without moving any of the boxes or touching a single elevation value.

    The field is generated coarse and scaled up, so neighbouring tiles wander
    together into lobes instead of dissolving into salt-and-pepper. Deterministic
    (fixed seed): the same board every run. `tiles` is the amplitude in tiles;
    latitude uses each row's own height, so the fringe stays a constant number of
    tiles deep whether the row is a compressed tropical one or a stretched
    temperate one."""
    if tiles <= 0:
        return np.zeros((height, width)), np.zeros((height, width))
    rng = np.random.default_rng(seed)
    def octave(divisor: int) -> np.ndarray:
        cw, ch = max(2, width // divisor), max(2, height // divisor)
        coarse = rng.uniform(-1.0, 1.0, (ch, cw)).astype(np.float32)
        img = Image.fromarray(coarse, mode="F").resize((width, height), Image.BILINEAR)
        return np.array(img, dtype=np.float32)
    def field() -> np.ndarray:
        # Two octaves: broad lobes that bow a whole coastline of desert in or
        # out, plus a fine one that frays it tile by tile. One octave alone
        # either shifts the straight edge without bending it, or dissolves into
        # noise that reads as static.
        f = octave(6) * 0.7 + octave(2) * 0.5
        # Smoothing an upsampled random field crushes its variance, so a raw sum
        # only wanders a fraction of a tile. Rescale to the full range first.
        scale = np.percentile(np.abs(f), 98)
        return np.clip(f / scale, -1.0, 1.0) if scale > 0 else f
    row_deg = np.abs(np.diff(LAT_EDGES)).reshape(height, 1)
    col_deg = 360.0 / width
    return field() * tiles * row_deg, field() * tiles * col_deg


def blend_biomes(grid: np.ndarray, seed: int = 20260621) -> int:
    """Break up the big single-biome swaths so temperate grassland, forest, and
    the great deserts all read as mixed country instead of flat carpets — and the
    deserts get the occasional oasis. Deterministic (fixed seed); per-tile, so it
    only salts the flat biomes and never touches elevation-set Hills/Mountains/
    Tundra/Jungle or any water. Each tile's fate is decided by one random draw:

        open flatland (Grass/Plains) → some Forest + a little Swamp
        Forest                       → some Plains + a little Swamp
        Desert                       → a little Plains + Swamp (oasis)
    """
    rng = np.random.default_rng(seed)
    r = rng.random(grid.shape)
    before = grid.copy()

    open_flat = (before == GRASS) | (before == PLAINS)
    forest    = (before == FOREST)
    desert    = (before == DESERT)

    grid[open_flat & (r < 0.05)]                  = SWAMP
    grid[open_flat & (r >= 0.05) & (r < 0.20)]    = FOREST
    grid[forest    & (r < 0.06)]                  = SWAMP
    grid[forest    & (r >= 0.06) & (r < 0.26)]    = PLAINS
    grid[desert    & (r < 0.03)]                  = SWAMP
    grid[desert    & (r >= 0.03) & (r < 0.10)]    = PLAINS

    return int(np.sum(grid != before))

def resample_rows(img: "Image.Image", width: int, height: int) -> np.ndarray:
    """Rescale the elevation image onto the warped row grid.

    Width is a plain resize (longitude is still linear). Height cannot be, since
    each output row now covers a different span of latitude — so each row
    averages the source rows its own span covers. Averaging rather than point-
    sampling matters in the compressed tropics, where one output row can stand
    for three or four source rows and nearest-neighbour would drop coastlines."""
    src_h = img.size[1]
    flat = np.array(img.resize((width, src_h), Image.BILINEAR), dtype=np.float32)
    out = np.zeros((height, width), dtype=np.uint8)
    for y in range(height):
        r0 = (90.0 - LAT_EDGES[y])     / 180.0 * src_h
        r1 = (90.0 - LAT_EDGES[y + 1]) / 180.0 * src_h
        a = max(0, min(src_h - 1, int(np.floor(r0))))
        b = max(a + 1, min(src_h, int(np.ceil(r1))))
        out[y] = np.rint(flat[a:b].mean(axis=0)).astype(np.uint8)
    return out


def clear_edge_land(grid: np.ndarray, rows: int) -> int:
    """Drown any land within `rows` of the top or bottom edge.

    The engine stamps an Arctic ice ring over row 0 and row HEIGHT-1 and salts
    tundra into rows 0/1 and HEIGHT-2/HEIGHT-1 (Map.Generate.cs:CreatePoles), so
    real coastline that close to the edge ends up fused to the frame and
    unplayable. With the default window nothing should be caught here — this is
    a guard rail that reports when a latitude change has pushed land too far."""
    if rows <= 0: return 0
    h = grid.shape[0]
    edge = np.concatenate((grid[:rows], grid[h - rows:]))
    n = int(np.sum(edge != OCEAN))
    grid[:rows] = OCEAN
    grid[h - rows:] = OCEAN
    return n


# ── main ─────────────────────────────────────────────────────────────────────

def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--elev", required=True, help="equirectangular elevation image (PNG/JPG)")
    ap.add_argument("--width", type=int, default=320)
    ap.add_argument("--height", type=int, default=200)
    ap.add_argument("--output", default=str(default_output_path()))
    ap.add_argument("--sea-level",  type=int, default=DEFAULT_SEA_LEVEL)
    ap.add_argument("--hill-level", type=int, default=DEFAULT_HILL_PIXEL)
    ap.add_argument("--mtn-level",  type=int, default=DEFAULT_MTN_PIXEL)
    ap.add_argument("--lat-north", type=float, default=DEFAULT_LAT_NORTH,
                    help="northern edge of the sampled window (default 90)")
    ap.add_argument("--lat-south", type=float, default=DEFAULT_LAT_SOUTH,
                    help="southern edge; the default -60 clips Antarctica")
    ap.add_argument("--edge-rows", type=int, default=None,
                    help="drown any land within N rows of the top/bottom ice ring "
                         "(default scales with height: 3 at 200 rows, 1 at 50)")
    ap.add_argument("--edge-jitter", type=float, default=1.6,
                    help="tiles of raggedness on biome-box edges (0 = hard rectangles)")
    ap.add_argument("--max-rivers", type=int, default=None,
                    help="how many of MAJOR_RIVERS to draw (default scales with board area)")
    ap.add_argument("--no-warp", action="store_true",
                    help="flat equirectangular rows instead of the LAT_WEIGHTS warp")
    args = ap.parse_args()
    if args.edge_rows is None:
        args.edge_rows = default_edge_rows(args.height)
    if args.max_rivers is None:
        args.max_rivers = default_max_rivers(args.width, args.height)

    src = Path(args.elev).expanduser()
    if not src.exists():
        sys.exit(f"elevation image not found: {src}")
    out = Path(args.output).expanduser()
    out.parent.mkdir(parents=True, exist_ok=True)

    global LAT_EDGES
    LAT_EDGES = build_lat_edges(args.height, args.lat_north, args.lat_south, not args.no_warp)

    img = Image.open(src).convert("L")
    print(f"input  {src} ({img.size[0]}x{img.size[1]})")
    arr = resample_rows(img, args.width, args.height)

    jitter_lat, jitter_lon = build_jitter(args.width, args.height, args.edge_jitter)

    grid = np.full((args.height, args.width), OCEAN, dtype=np.uint8)
    counts = {}
    for y in range(args.height):
        lat = lat_for_row(y, args.height)
        is_polar = abs(lat) >= 70.0
        for x in range(args.width):
            elev = int(arr[y, x])
            if elev < args.sea_level:
                code = OCEAN
            elif elev >= args.mtn_level:
                code = MTNS
            elif elev >= args.hill_level:
                code = HILLS
            else:
                # Jittered lookup only — the tile keeps its true latitude for the
                # polar test below, so the fringe never drags jungle into the ice.
                jlat = lat + jitter_lat[y, x]
                jlon = lon_for_col(x, args.width) + jitter_lon[y, x]
                code = moisture_override(jlat, jlon) or biome_for_lat(jlat)
                # Polar override: nothing tropical at the poles regardless of moisture
                if is_polar:
                    code = ARCTIC if abs(lat) >= 80 else TUNDRA
            grid[y, x] = code
            counts[code] = counts.get(code, 0) + 1

    # Blend complementary terrain into the big single-biome swaths (and add desert
    # oases). Runs after the biome/override pass but before land-force/lakes/rivers
    # so those still win on the tiles they touch.
    blend_tiles = blend_biomes(grid)

    # Land-force overrides run BEFORE lakes/straits so subsequent water passes
    # can still carve through (e.g. Mississippi delta river crossing forced FL
    # land would still draw the river segment). Only converts existing Ocean
    # tiles — never overwrites a Hill/Mountain/biome the elevation pass set.
    land_force_tiles = 0
    for name, biome, polygon in LAND_FORCES:
        land_force_tiles += force_land_polygon(grid, polygon, biome)

    # Apply lakes BEFORE rivers: lakes overwrite land with ocean, then river
    # polylines that happen to cross a lake's bounding box terminate naturally
    # because draw_river_polyline skips ocean tiles.
    lake_tiles = 0
    for lat_min, lat_max, lon_min, lon_max, name in MAJOR_LAKES:
        lake_tiles += carve_lake_box(grid, lat_min, lat_max, lon_min, lon_max)
    for name, polygon in MAJOR_LAKE_POLYGONS:
        lake_tiles += carve_lake_polygon(grid, polygon)
    # Strait shaves: open one tile at each narrow sea passage so the elevation
    # pipeline's tiles-too-coarse rounding doesn't trap Mediterranean/Black-Sea/
    # Red-Sea bodies as inland lakes.
    strait_tiles = 0
    for lat, lon, name in STRAITS:
        if carve_strait(grid, lat, lon): strait_tiles += 1
    # Sea channels: cut continuous lanes where a single strait shave isn't enough.
    channel_tiles = carve_sea_channel(grid, MED_CHANNEL)
    river_tiles = 0
    for name, waypoints in MAJOR_RIVERS[:args.max_rivers]:
        river_tiles += draw_river_polyline(grid, waypoints, args.sea_level)

    # Last, so nothing downstream can put land back against the ice ring.
    edge_tiles = clear_edge_land(grid, args.edge_rows)

    # Recount after overlays for an accurate breakdown.
    counts = {}
    for code in grid.flat:
        counts[int(code)] = counts.get(int(code), 0) + 1

    # Write binary: 4-byte magic, version, 3 reserved, uint32 W, uint32 H, tiles,
    # then height+1 little-endian float32 latitude edges (north to south).
    #
    # The edge table is what lets the engine place civilizations correctly. It
    # projects each civ's real-world centroid onto the board, and since the rows
    # are no longer a linear slice of the globe it cannot recompute that itself.
    # Shipping the table beats duplicating LAT_WEIGHTS in C#, where the two could
    # silently drift apart the first time these weights are retuned.
    #
    # Appended after the tiles rather than in the header, so the format stays
    # version 1: Map.LoadEarthBin length-checks with >=, so older builds ignore
    # the trailing bytes and newer builds treat their absence as "linear".
    header = b"CIVE" + bytes([1, 0, 0, 0]) + struct.pack("<II", args.width, args.height)
    with open(out, "wb") as f:
        f.write(header)
        f.write(grid.tobytes())
        f.write(struct.pack(f"<{args.height + 1}f", *[float(v) for v in LAT_EDGES]))

    # Report
    name = {OCEAN:"Ocean", FOREST:"Forest", SWAMP:"Swamp", PLAINS:"Plains",
            TUNDRA:"Tundra", RIVER:"River", GRASS:"Grass", JUNGLE:"Jungle",
            HILLS:"Hills", MTNS:"Mtns", DESERT:"Desert", ARCTIC:"Arctic"}
    total = args.width * args.height
    land = total - counts.get(OCEAN, 0)
    print(f"wrote {out} ({len(header) + grid.size} bytes)")
    print(f"  dimensions: {args.width}x{args.height} = {total} tiles")
    print(f"  latitude:   {args.lat_north:+.0f}° to {args.lat_south:+.0f}°"
          f" ({'flat' if args.no_warp else 'warped'})")
    for lo, hi in ((0, 15), (15, 30), (30, 45), (45, 60), (60, 90)):
        n_rows = row_for_lat(lo, args.height) - row_for_lat(hi, args.height)
        flat_rows = (hi - lo) / (args.lat_north - args.lat_south) * args.height
        print(f"    {lo:2d}-{hi:2d}°N: {n_rows:3d} rows ({n_rows/flat_rows:.2f}x flat)")
    print(f"  edge guard: {edge_tiles} land tiles drowned within {args.edge_rows} rows of the ice ring")
    print(f"  land:   {land} ({100*land/total:.1f}%)")
    print(f"  ocean:  {counts.get(OCEAN, 0)} ({100*counts.get(OCEAN, 0)/total:.1f}%)")
    print(f"  blend:   {blend_tiles} tiles re-mixed across grassland/forest/desert")
    print(f"  rivers:  {river_tiles} tiles drawn across {args.max_rivers} of {len(MAJOR_RIVERS)} polylines (4-connected)")
    print(f"  lakes:   {lake_tiles} land tiles carved across {len(MAJOR_LAKES)} boxes + {len(MAJOR_LAKE_POLYGONS)} polygons (engine flags as freshwater)")
    print(f"  land-force: {land_force_tiles} ocean tiles reclaimed across {len(LAND_FORCES)} polygons")
    print(f"  straits: {strait_tiles} tiles opened of {len(STRAITS)} configured")
    print(f"  channels: {channel_tiles} tiles carved (Mediterranean -> Atlantic lane)")
    for code, count in sorted(counts.items(), key=lambda kv: -kv[1]):
        print(f"  {name.get(code, '?'):8s} {count:6d} ({100*count/total:5.1f}%)")
    return 0

if __name__ == "__main__":
    sys.exit(main())
