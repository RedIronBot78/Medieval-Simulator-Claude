# Kickoff: getting from this kit to running code

## 1. One-time setup (Windows 11, PowerShell)

```powershell
winget install Git.Git
winget install Microsoft.DotNet.SDK.8
```

Install **Godot 4 (.NET edition)** now too, even though step 3 is when you use it, so Claude Code can match its .NET version. Download the ".NET" build from godotengine.org (the standard build has no C# support).

Restart PowerShell, then check:

```powershell
git --version
dotnet --version
```

## 2. Create the repo

```powershell
New-Item -ItemType Directory -Force D:\Projects\0007-Medieval-Sim-Claude
cd D:\Projects\0007-Medieval-Sim-Claude
# unzip this kit here so CLAUDE.md sits at the top of the folder
git init
git add .
git commit -m "Project kit: CLAUDE.md, plan, bronze-tier design spec"
```

The folder should look like:

```
D:\Projects\0007-Medieval-Sim-Claude\
  CLAUDE.md
  KICKOFF.md
  docs\plan.md
  docs\design-spec.md
```

## 3. Open Claude Code on that folder

Start Claude Code with `D:\Projects\0007-Medieval-Sim-Claude` as the working folder (from the Claude desktop app, or `claude` in a PowerShell window opened in that folder). It reads `CLAUDE.md` automatically.

## 4. First prompt (paste this)

> Read CLAUDE.md, docs/plan.md and docs/design-spec.md. We are on build-order step 2: the bronze-tier material library and test harness. No Godot work.
>
> 1. Check which .NET version my installed Godot .NET edition uses and target that.
> 2. Create the solution and projects exactly as laid out in CLAUDE.md.
> 3. Put every constant from the spec in data JSON files, not code.
> 4. Implement the spec in this order, with tests for each before moving on: composition and melting ranges (§2), Melt (§3.1), Cast (§3.2), derived properties (§4), Anneal (§3.4), Strike (§3.3), Quench (§3.5), Grind (§3.6), hot-shortness (§5), Inspect (§3.7), item templates (§6), Simulate (§7).
> 5. Make every row of the validation table and every behavioral test in §9 a passing unit test.
> 6. Build the CLI so `dotnet run --project src/ForgeSim.Cli -- data/recipes/chisel_10sn_good.json` prints a readable report. Add recipes for: a good 10% Sn chisel, an overworked chisel that cracks, a 22% Sn bell, a pure copper chisel, and a leaded-bronze cauldron.
> 7. Commit after each green step.
>
> If any spec formula gives a result that looks physically wrong, stop and show me the inputs and output before changing anything. When done, show me the CLI report for all five recipes side by side.

## 5. What to do with the result

Run the five recipes and read the numbers like a smith. Does the overworked chisel crack where you'd expect? Is pure copper too good or too bad? Is the tin-loss figure believable? Every disagreement is a constant to tune in `data/`, and that tuning session is the real work of step 2.

## 6. Next prompt, once you're happy with the numbers (step 3)

> Step 3: create the Godot 4 .NET project in `game/` (3D only), referencing ForgeSim.Core. Build one 3D scene: an anvil with a bronze chisel blank and a hammer. Mouse controls aim and strike force; each strike calls the sim as a Strike step. Drive the metal's color and glow from sim temperature, the strike sound's pitch from zone HV, and show cracks on the mesh only once Inspect reveals them. Include an anneal action at a simple hearth. Placeholder CC0 assets only. The test is whether cold-working an edge feels right, so prioritise feel over visuals.
