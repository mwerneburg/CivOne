# CivOne

A Civilization I remake in C#, forked from the original CivOne project and extended with a sci-fi storyline, improved AI, procedural map generation, and a machine-learning decision logger.

Licensed under [GPL-3.0-or-later](LICENSE). Forked from the CC0 original — see
[NOTICE](NOTICE) for how the two fit together, [AUTHORS](AUTHORS) for who wrote
what, and [PROVENANCE.md](PROVENANCE.md) for where the art and map data came
from. No original Civilization I assets are included, and none are required.

---

## Requirements

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 10.0+ | `runtime/sdl` targets net10.0 |
| SDL2 | any recent | installed via Homebrew on macOS |
| Python 3 | 3.10+ | optional — only needed for the analysis notebook |

On macOS with Homebrew:

```
brew install dotnet sdl2 sdl2_image sdl2_mixer
```

---

## Building and running

```bash
# Debug build + run (macOS)
dotnet build runtime/sdl/CivOne.SDL.csproj -c DebugMacOS
runtime/sdl/bin/Debug/net10.0/CivOne.SDL

# Release build + run
dotnet build runtime/sdl/CivOne.SDL.csproj -c ReleaseMacOS
runtime/sdl/bin/Release/net10.0/CivOne.SDL
```

The `build.sh` script at the repo root does both builds and launches the release binary.

Game data (saves, assets, logs) is stored in `~/Library/Application Support/CivOne/` on macOS. The game copies bundled defaults there on first launch and overwrites them on every subsequent launch, so pulling and rebuilding is enough to pick up asset updates.

---

## Save files

Save files live in `~/Library/Application Support/CivOne/saves/` and are not tracked by git. The format is `.cos` (a custom text serialisation). You can load them from the in-game menu.

---

## Decision logger and analysis notebook

The game logs AI decisions to `~/Library/Application Support/CivOne/data/decisions.jsonl` while it runs. Each line is a JSON record describing a settler action, city production choice, or game outcome. The file is append-only across sessions.

To analyse the log:

```bash
# Install Python dependencies (one-time)
pip3 install pandas scikit-learn matplotlib jupyter

# Open the notebook
jupyter notebook notebooks/decision_analysis.ipynb
```

The notebook fits decision trees to the logged data and shows feature importances, action distributions, and score correlations. It is intended to drive future improvements to the AI.

### Keeping the notebook clean in git

Jupyter embeds chart images and cell outputs in the `.ipynb` file after you run it. We use [nbstripout](https://github.com/kynan/nbstripout) to strip those automatically on `git add` so commits stay small and diffs stay readable.

After cloning, run this once:

```bash
pip3 install nbstripout
nbstripout --install
```

That's it. The `.gitattributes` file at the repo root wires the filter in; `nbstripout --install` registers the filter command with your local git config. You won't need to think about it again.

---

## Project layout

```
src/                    Core game library (netstandard2.0)
runtime/sdl/            SDL2 frontend (net10.0, the binary you run)
  src/Runtime.cs        Asset installation, window management
  Resources/defaults/   Bundled assets copied to Application Support on launch
design/                 Source artwork and design notes (not shipped)
notebooks/              Analysis tools
  decision_analysis.ipynb
.gitattributes          nbstripout filter for notebooks
```

---

## Game rules and features

See [`docs/README.md`](docs/README.md) for a full description of game rules, rule changes from the original, terrain generation, display configuration, the debug menu, and known issues.

---

## Contributing

The main branch is `master`. There are no automated tests; the game is the test. Build, play, and report bugs as issues or discuss in whatever channel you're using.

The `CLAUDE.md` file at the repo root contains coding guidelines used when working with Claude Code — keep changes surgical, match existing style, no speculative abstractions.

---

## Color codes

# ─── Cassette theme (runtime overrides, always safe) ────────────────────────
    0  transparent        (  0,  0,  0)  pass-through — does not paint
    1  BG0                ( 10,  8,  6)  deepest near-black background
    2  BG1                ( 18, 16,  9)  panel fill
    3  BG2                ( 27, 24, 16)  raised surface
    4  BG3                ( 38, 34, 26)  input well
    5  BORDER             ( 58, 49, 34)  dark brown — outlines / dividers
    6  INK_LOW            (106, 90, 60)  warm mid-brown — shadow / disabled
    7  INK_MID            (179,156,114)  warm tan — labels / mid tone
    8  INK_HIGH           (244,230,200)  cream — highlights / main text
    9  PHOS_GHOST         ( 32, 21,  8)  very faint amber
   10  PHOS_FAINT         ( 61, 42, 16)  faint amber
   11  PHOS_DIM           (192,120, 24)  dim amber — meter fill
   12  PHOS               (240,160, 48)  phosphor amber accent
   13  PHOS_GLOW          (248,192, 96)  bright amber / gold
   14  OK                 ( 93,181, 54)  green status
   15  WHITE              (248,244,236)  near-white
   16  ALERT              (196, 40, 32)  red alert
   17  CYAN               ( 58,172,204)  info blue
   18  OCEAN              ( 22, 80,128)  deep ocean blue

# ─── SP257 grays ─────────────────────────────────────────────────────────────
   19  (236,236,236)  near-white gray
   20  (216,216,216)  light gray
   21  (200,200,200)  light-mid gray
   22  (184,184,184)  mid-light gray
   23  (168,168,168)  mid gray
   24  (152,152,152)  mid-dark gray
   25  (132,132,132)  medium gray
   26  (116,116,116)  dark-mid gray
   27  (100,100,100)  dark gray
   28  ( 84, 84, 84)  darker gray
   29  ( 68, 68, 68)  very dark gray
   30  ( 52, 52, 52)  near-black gray
   31  ( 32, 32, 32)  almost black
   32  ( 16, 16, 16)  near-black

# ─── SP257 terrain greens ────────────────────────────────────────────────────
   33  (108,176, 84)  bright mid green (grassland)
   34  (108,164, 68)  mid green
   35  (108,152, 60)  medium olive green
   36  (108,144, 48)  olive green
   37  (112,144, 48)  olive / dark yellow-green (forest shadow)
   38  ( 88,112, 32)  dark olive green (forest canopy shadow)
   39  (156,188, 96)  yellow-green (forest canopy lit)
   40  ( 36,128,  0)  rich forest green
   41  ( 32, 88,  0)  dark forest green
   42  ( 56,120, 16)  deep green
   43  ( 92,152, 44)  medium green
   44  (132,184, 84)  light green
   45  (176,216,136)  pale mint green

# ─── SP257 earth / tan ───────────────────────────────────────────────────────
   46  (160,100, 48)  tan / leather brown
   47  (200,152, 96)  light tan / sand

# ─── SP257 dark reds ─────────────────────────────────────────────────────────
   48  ( 84,  0,  0)  dark red / maroon
   49  ( 64,  0,  0)  very dark red

# ─── SP257 miscellaneous ─────────────────────────────────────────────────────
   50  (252,232,216)  light peach / cream
   51  ( 84, 88,160)  slate blue-purple
   52  ( 24,144, 24)  vivid green
   53  (  0,168,168)  teal
   54  (252,168, 92)  warm orange
   55  (252,152, 64)  orange
   56  (188,180, 84)  yellow-tan / khaki
   57  (168,168,168)  mid gray (duplicate of 23)
   58  ( 84, 84, 84)  dark gray (duplicate of 28)
   59  (116,116,236)  periwinkle blue
   60  (164,244,164)  pale mint / light green
   61  (136,236,232)  pale cyan / light teal
   62  (132, 64,  0)  dark brown
   63  (112, 56,  0)  darker brown
   64  (236,232,168)  pale yellow / straw
   65  ( 64, 32,  0)  very dark brown

# ─── SP257 blues ─────────────────────────────────────────────────────────────
   66  ( 84, 72,160)  dark blue-purple
   67  ( 84, 76,168)  medium blue-purple
   68  ( 84, 84,180)  mid blue
   69  ( 88, 96,192)  blue
   70  ( 96,108,200)  medium-light blue
   71  (100,124,212)  light-mid blue
   72  (108,140,224)  light blue
   73  (116,156,236)  pale blue

# ─── SP257 gold / yellows ────────────────────────────────────────────────────
   74  (228,216,  0)  bright yellow-gold
   75  (204,196,  0)  gold
   76  (180,172,  0)  dark gold
   77  (156,152,  0)  olive gold
   78  (132,128,  0)  dark olive
   79  (112,108,  0)  very dark olive
   80  ( 88, 84,  0)  near-black olive
   81  ( 64, 64,  0)  darkest olive

# ─── SP257 plains / grassland gradient ──────────────────────────────────────
   82  (248,252,216)  near-white yellow-green
   83  (236,232,168)  pale straw (same as 64)
   84  (236,232,168)  pale straw
   85  (224,224,152)  light yellow-green
   86  (208,216,136)  light straw-green
   87  (192,204,120)  straw-green
   88  (172,196,108)  light olive-green
   89  (156,188, 96)  yellow-green (same as 39)
   90  (120,196, 92)  mid green (plains)
   91  (112,184, 80)  medium green
   92  (104,172, 68)  grass green
   93  (100,164, 56)  darker grass green
   94  ( 96,152, 48)  dark grass
   95  ( 92,144, 40)  dark olive grass

# ─── Wave cycling (animated) ────────────────────────────────────────────────
   96  (210,235,248)  WAVE_FOAM  — surf white
   97  (130,200,230)  WAVE_SURF  — light cyan-blue
   98  ( 58,172,204)  WAVE_MID   — mid-blue
   99  ( 28, 95,150)  WAVE_BASE  — dark wave
  100  ( 20, 70,120)  trough 1
  101  ( 15, 55, 98)  trough 2
  102  ( 12, 45, 80)  trough 3
  103  ( 10, 35, 65)  trough deepest

# ─── SP257 extra blues (unit flag / civ colours range) ───────────────────────
  104  ( 84, 72,160)  dark blue-purple
  105  ( 80, 80,176)  medium blue-purple
  106–113            medium blues (similar to 70–73)

# ─── 116–129: black (0,0,0)  ─────────────────────────────────────────────────
# ─── 130–225: magenta (252,84,252) — AVOID, chroma-key / unused ──────────────

# ─── SP257 skin / portrait tones ────────────────────────────────────────────
  226  (188,240,252)  pale blue-white
  227  (144,200,216)  light steel blue
  228  (108,160,184)  steel blue
  229  ( 76,124,152)  mid steel blue
  230  ( 48, 92,120)  dark steel blue
  231  ( 28, 60, 88)  very dark steel blue

# ─── SP257 earth browns ─────────────────────────────────────────────────────
  232  (204,180,128)  light sand
  233  (184,160,104)  sand
  234  (168,140, 80)  tan
  235  (148,120, 60)  mid brown
  236  (128,104, 44)  brown
  237  (112, 88, 28)  dark brown
  238  ( 92, 72, 16)  darker brown
  239  ( 76, 56,  4)  very dark brown
  240  ( 56, 40,  0)  near-black brown
  241  ( 40, 28,  0)  darkest brown

# ─── SP257 gold / orange ────────────────────────────────────────────────────
  242  (252,192, 88)  light gold
  243  (248,172, 56)  gold
  244  (244,156, 24)  deep gold / amber
  245  (244,140,  0)  saturated amber

# ─── SP257 reds ─────────────────────────────────────────────────────────────
  246  (220,152,152)  pink / pale red
  247  (188, 60, 60)  mid red
  248  (156,  0,  0)  deep red

# ─── SP257 skin tones ───────────────────────────────────────────────────────
  249  (248,204,156)  very light skin
  250  (224,176,128)  light skin
  251  (204,152,100)  medium skin
  252  (184,128, 80)  mid-dark skin
  253  (164,104, 60)  warm brown skin
  254  (144, 84, 40)  dark skin
  255  (124, 64, 28)  dark brown skin

  For drawing animals the most useful ranges are: 1–8 (cassette grays/tans for outlines and shading), 33–45 (greens, for
  forest creatures), 46–47, 62–65, 232–241 (earth browns/tans for fur), 54–55, 242–245 (orange/gold for warm-toned
  animals).
