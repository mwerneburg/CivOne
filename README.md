# CivOne

A Civilization I remake in C#, forked from the original CivOne project and extended with a sci-fi storyline, improved AI, procedural map generation, and a machine-learning decision logger.

Licensed under [CC0](LICENSE.md) — public domain.

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

See [`docs/README.md`](docs/README.md) for a full description of game rules, rule changes from the original, terrain generation, display configuration, and known issues.

---

## Contributing

The main branch is `master`. There are no automated tests; the game is the test. Build, play, and report bugs as issues or discuss in whatever channel you're using.

The `CLAUDE.md` file at the repo root contains coding guidelines used when working with Claude Code — keep changes surgical, match existing style, no speculative abstractions.
