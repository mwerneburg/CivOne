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

Usage examples:
  ./build_earth_map.py --elev ~/Downloads/topography.png
  ./build_earth_map.py --elev topo.png --width 160 --height 100  # Huge size
  ./build_earth_map.py --elev topo.png --sea-level 100           # less land
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
    (15, 30, DESERT),    # subtropical (Sahara/Sahel band)
    (30, 45, FOREST),    # warm temperate
    (45, 60, FOREST),    # cool temperate
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
    # Mongolia + Gobi
    (40, 50,  90, 115, DESERT),
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


# ── Major lakes ───────────────────────────────────────────────────────────────
# Each entry forces *all* tiles inside the bounding box to Ocean. After the
# load, the engine's ComputeFreshwaterLakes pass at Map.cs detects any inland
# Ocean tile as freshwater, so these come back as proper lakes automatically.
# Boxes are (lat_min, lat_max, lon_min, lon_max, name).
#
# Source data: bounding boxes hand-derived from Natural Earth's 10m Lakes
# polygon set. Boxes are intentionally tight to avoid swallowing coast.
MAJOR_LAKES = [
    # North America
    ( 46.5, 48.5, -92.0, -85.0, "Superior"),
    ( 41.5, 46.0, -88.0, -84.5, "Michigan"),
    ( 43.0, 46.5, -84.5, -80.0, "Huron"),
    ( 41.5, 43.0, -83.5, -78.5, "Erie"),
    ( 43.5, 44.5, -80.0, -75.5, "Ontario"),
    ( 60.5, 63.0,-117.0,-108.0, "GreatSlave"),
    ( 65.0, 67.0,-125.0,-119.0, "GreatBear"),
    # Eurasia
    ( 43.0, 47.0,  58.0,  62.0, "Aral"),
    ( 51.5, 56.0, 104.0, 110.0, "Baikal"),
    ( 45.0, 46.5,  73.0,  79.0, "Balkhash"),
    ( 60.0, 63.0,  31.5,  35.5, "Ladoga"),
    # Other
    (-15.0,-12.5,  33.5,  36.0, "Tana"),
    ( 60.0, 62.0,  78.0,  85.0, "WestSiberian"),
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
    # Tanganyika: long narrow rift lake, ~670 km × 50 km.
    ("Tanganyika", [
        ( -3.4, 29.3), ( -5.5, 29.7), ( -7.8, 30.6), ( -8.7, 30.8),
        ( -7.8, 30.4), ( -5.5, 29.4),
    ]),
    # Malawi: long narrow rift lake, ~580 km × 75 km.
    ("Malawi", [
        ( -9.5, 34.0), (-11.5, 34.6), (-13.5, 34.7), (-14.5, 35.1),
        (-13.5, 34.4), (-11.5, 34.2),
    ]),
    # Turkana: narrow N-S rift lake.
    ("Turkana", [
        (  4.6, 35.9), (  3.5, 36.4), (  2.4, 36.2), (  3.5, 35.7),
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
        y = int(((90.0 - lat) / 180.0) * h)
        x = int(((lon + 180.0) / 360.0) * w)
        y = max(0, min(h - 1, y))
        x = max(0, min(w - 1, x))
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
    cy = int(((90.0 - lat) / 180.0) * h)
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


def carve_lake_box(grid: np.ndarray, lat_min: float, lat_max: float, lon_min: float, lon_max: float) -> int:
    """Force all tiles inside the lat/lon box to Ocean. Returns count carved."""
    h, w = grid.shape
    y0 = int(((90.0 - lat_max) / 180.0) * h)
    y1 = int(((90.0 - lat_min) / 180.0) * h)
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
        y = ((90.0 - lat) / 180.0) * h
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
        y = ((90.0 - lat) / 180.0) * h
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
    """y=0 is north pole (+90°), y=height-1 is south pole (-90°)."""
    return 90.0 - (180.0 * (y + 0.5) / height)

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
    args = ap.parse_args()

    src = Path(args.elev).expanduser()
    if not src.exists():
        sys.exit(f"elevation image not found: {src}")
    out = Path(args.output).expanduser()
    out.parent.mkdir(parents=True, exist_ok=True)

    img = Image.open(src).convert("L")
    print(f"input  {src} ({img.size[0]}x{img.size[1]})")
    img = img.resize((args.width, args.height), Image.BILINEAR)
    arr = np.array(img, dtype=np.uint8)

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
                lon = lon_for_col(x, args.width)
                code = moisture_override(lat, lon) or biome_for_lat(lat)
                # Polar override: nothing tropical at the poles regardless of moisture
                if is_polar:
                    code = ARCTIC if abs(lat) >= 80 else TUNDRA
            grid[y, x] = code
            counts[code] = counts.get(code, 0) + 1

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
    river_tiles = 0
    for name, waypoints in MAJOR_RIVERS:
        river_tiles += draw_river_polyline(grid, waypoints, args.sea_level)

    # Recount after overlays for an accurate breakdown.
    counts = {}
    for code in grid.flat:
        counts[int(code)] = counts.get(int(code), 0) + 1

    # Write binary: 4-byte magic, version, 3 reserved, uint32 W, uint32 H, tiles.
    header = b"CIVE" + bytes([1, 0, 0, 0]) + struct.pack("<II", args.width, args.height)
    with open(out, "wb") as f:
        f.write(header)
        f.write(grid.tobytes())

    # Report
    name = {OCEAN:"Ocean", FOREST:"Forest", SWAMP:"Swamp", PLAINS:"Plains",
            TUNDRA:"Tundra", RIVER:"River", GRASS:"Grass", JUNGLE:"Jungle",
            HILLS:"Hills", MTNS:"Mtns", DESERT:"Desert", ARCTIC:"Arctic"}
    total = args.width * args.height
    land = total - counts.get(OCEAN, 0)
    print(f"wrote {out} ({len(header) + grid.size} bytes)")
    print(f"  dimensions: {args.width}x{args.height} = {total} tiles")
    print(f"  land:   {land} ({100*land/total:.1f}%)")
    print(f"  ocean:  {counts.get(OCEAN, 0)} ({100*counts.get(OCEAN, 0)/total:.1f}%)")
    print(f"  rivers:  {river_tiles} tiles drawn across {len(MAJOR_RIVERS)} polylines (4-connected)")
    print(f"  lakes:   {lake_tiles} land tiles carved across {len(MAJOR_LAKES)} boxes + {len(MAJOR_LAKE_POLYGONS)} polygons (engine flags as freshwater)")
    print(f"  land-force: {land_force_tiles} ocean tiles reclaimed across {len(LAND_FORCES)} polygons")
    print(f"  straits: {strait_tiles} tiles opened of {len(STRAITS)} configured")
    for code, count in sorted(counts.items(), key=lambda kv: -kv[1]):
        print(f"  {name.get(code, '?'):8s} {count:6d} ({100*count/total:5.1f}%)")
    return 0

if __name__ == "__main__":
    sys.exit(main())
