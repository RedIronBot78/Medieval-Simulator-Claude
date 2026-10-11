# CLAUDE.md

Project guide for Claude Code. Read this first every session.

## What this is

A 3D medieval/fantasy life simulator. The long-term goal is an open-world survival crafter where everything interacts and the player can do anything. **The current iteration is blacksmithing only**: the player moves into a small village, supplies villagers with tools, deals with a rival smith, and can sell into a city at war.

The defining feature is **real-world-grounded metallurgy**: the player chooses alloy ratios and works the metal through skill-based minigames, and composition plus process history determine the item's actual properties and hidden defects.

- `docs/plan.md` — overall plan and build order
- `docs/design-spec.md` — **the source of truth** for the material model, process steps, minigame contracts, item schema and validation targets. If code and spec disagree, stop and ask; do not silently change either.

## Stack

- **Engine:** Godot 4 (.NET edition), C#. 3D only. There is no 2D version of this game; never propose or scaffold 2D scenes.
- **Sim library:** plain .NET class library, no Godot references. Target the same .NET version the installed Godot .NET edition uses (check before creating projects).
- **Tests:** xUnit.
- **Data:** JSON files under `data/`, loaded by the sim library. Godot `.tres` only for engine-side presentation data.
- **OS:** the developer is on Windows 11. Give PowerShell commands, not bash, when asking the user to run anything.

## Repository layout

Repo root on the developer's machine: `D:\Projects\0007-Medieval-Sim-Claude`. All paths below are relative to it.

```
ForgeSim.sln
src/ForgeSim.Core/          # material model, process steps, items. NO Godot dependency.
src/ForgeSim.Cli/           # console harness: run a recipe JSON, print a property report
tests/ForgeSim.Core.Tests/  # xUnit tests, including the validation table from the spec
data/elements/              # element definitions
data/model/                 # model constants per tier (bronze.json first)
data/items/                 # item templates (zones, property requirements)
data/uses/                  # use profiles for performance simulation
data/recipes/               # example process histories used by tests and the CLI
game/                       # Godot project (created at build-order step 3, not before)
docs/
```

## Architecture rules (non-negotiable)

1. **The sim never depends on the engine.** `ForgeSim.Core` must build and test with `dotnet test` alone. Godot calls into it; it never calls Godot.
2. **Minigames only produce process steps.** A minigame's output is a process-step record (temperatures, times, strikes, etc., as defined in the spec). Minigames never set hardness, quality, price or defects directly.
3. **Properties are derived, never stored as truth.** An item's properties are computed from composition + process history + geometry. Cache if needed, but the history is the source of truth.
4. **Deterministic.** All randomness goes through a seeded RNG passed in explicitly. Same inputs + same seed = same output, byte for byte. No `Random.Shared`, no `DateTime.Now` in the sim.
5. **Data-driven.** Element constants, item templates and requirements live in JSON. Tuning should not require recompiling. Future professions (carpentry, tailoring) must be addable as new data plus new minigames, not rewrites.
6. **Defects are hidden state.** The sim tracks defects the player cannot see until the item is inspected, tested or fails in use.

## Working style

- Build in small, tested steps. Each formula in the spec gets a unit test before it is wired into anything else.
- When a spec number produces an implausible result, report it with the inputs and output and propose a fix; do not quietly re-tune.
- Keep the CLI report readable: composition, process summary, per-zone properties, defects (revealed and hidden, clearly labeled).
- Commit after each green step with a clear message.

## Commands

```
dotnet build
dotnet test
dotnet run --project src/ForgeSim.Cli -- data/recipes/<recipe>.json
```

## Current status

Build-order step 2: material library + test harness for the **bronze tier** (copper, tin bronze, arsenical bronze, leaded bronze). No Godot work yet.
