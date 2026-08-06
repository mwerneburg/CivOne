# CLAUDE.md

Project-specific rules for CivOne. General "write less code" discipline lives in the
ponytail plugin; general "don't overstep" discipline is in Claude Code's own prompt.
What follows is only what those two can't know.

**Bias toward verification over speed, and toward less code over more.** If a simpler
approach exists — or the thing being asked for already exists — say so before building.

## 1. Surgical changes

Touch only what you must. Don't improve adjacent code, don't refactor what isn't
broken, and remove only the orphans your own change created.

Unrelated dead code gets mentioned, not deleted. This overrides ponytail's "deletion
over addition": here, unfamiliar code is usually load-bearing for an arc you haven't
read.

## 2. Verify the test fails without the fix

A passing test proves nothing until you've watched it fail. Toggle the fix off, confirm
the *exact expected* test fails, restore. Report which tests the negative check killed.

This is not ceremony — tests here have repeatedly passed against unfixed code because
something incidental agreed with the rule: a row-major scan reaching the right tile by
luck; a size-12 city falling into disorder, and disorder halves the incite price,
masking the comparison the test existed to make. Green suites hid both.

Corollary: a missing asset degrades **silently** — `FindPath` returns null, a fallback
plays, nobody notices for a week. New event art, tile art and portraits get a
file-exists test (`LeaderPortraitTests`, `ProbeContactArtTests`).

Ponytail's "ONE runnable check, no frameworks" is the floor for new logic, not a
ceiling. This project has a real suite and it stays that way.

For multi-step work, state the plan first — one line per step with its check.

## Build and test

Environment (also in `build.sh`, which builds and runs the game):

```bash
export PATH=$PATH:/opt/homebrew/bin/dotnet
export DOTNET_ROOT=/opt/homebrew/Cellar/dotnet/10.0.300/libexec
```

Both projects must build clean — the core is `netstandard2.0` (so no `init`
accessors) and the SDL runtime is `net10.0`, so a change can compile in one and
break the other:

```bash
dotnet build CivOne.csproj -v q
dotnet build runtime/sdl/CivOne.SDL.csproj -v q
```

Tests are ~5 min; `AutoplayHarness` runs hour-long games, so exclude it:

```bash
dotnet test tests/CivOne.Tests.csproj --filter "FullyQualifiedName!~Autoplay"
```

## Git

The user handles all commits and pushes. Never run `git commit` or `git push`.
