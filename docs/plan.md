# Medieval Life Sim — Blacksmith Iteration Plan

As of 2026-10-10.

## The core bet: prove the metallurgy sim first

The alloy-and-process-affects-the-item idea is what makes this game different, so it gets built and proven before any village, open world, or engine scene. Everything hinges on one question: is a physically grounded metallurgy sim fun to play?

If a player can feel the difference between 8% tin and 14% tin bronze, or between a well-worked edge and an overworked one, there is a game. If it reads as a stat spreadsheet, no amount of village economy saves it.

So the first thing to build is the **material model as a standalone library with a test harness**. No graphics. Feed it a composition and a process history; it outputs the item's properties and flaws. That can be iterated in hours, and sanity-checked against real shop experience.

## Material model

Keep it truthy, not textbook. Full phase diagrams would eat months and players cannot perceive the difference; a hand-tuned response-surface model is enough.

**Material = composition vector + process state.** Composition is mass percentages (Cu, Sn, As, Pb, Zn, Fe, C, plus fantasy elements later). Process state is accumulated history: peak temperature, cold work, cooling rate, temper temperature, slag and inclusion count, grain condition.

**Derived properties** come from composition plus state: hardness, toughness, ductility, edge retention, corrosion resistance, melting range, workable window. Bronze gets harder and more brittle as tin rises past about 12%. Iron gets harder with carbon but needs quench and temper to get both hardness and toughness. Overwork cracks; underwork leaves it soft.

**Item quality is not one number.** It is derived from material properties, geometry, and a defect list. A tin-heavy bronze makes a great bell and a terrible chisel. A sword with a hidden inclusion looks fine until it fails in combat. That is what makes selling to a city at war interesting: a desperate buyer pays for looks; a veteran tests the blade.

**Each minigame outputs process parameters, nothing else.** Minigames never touch quality directly; they only write process steps. That separation lets feel and physics be tuned independently.

| Minigame | Writes |
| --- | --- |
| Heat management (furnace, bellows) | Peak temperature, time at temperature |
| Casting (pour) | Pour temperature, pour rate, mold preheat |
| Hammer work | Strikes per zone: force, angle, temperature at strike |
| Annealing | Anneal temperature and time |
| Quench | Cooling rate |
| Tempering (steel tier) | Temper temperature and time |
| Grinding | Edge angle, finish |
| Forge welding (iron tier) | Weld temperature, flux, strikes |

**Material tiers**, each extending the same system:

1. Copper
2. Bronze: ratio play (tin bronze, arsenical bronze, leaded bronze, bell metal)
3. Bloomery iron: slag management, forge welding
4. Steel: carburizing, quench and temper, pattern welding
5. Fantasy alloys: new elements on the same composition vector

## Stack: Godot 4 with C#

- Scenes and resources are text files, so Claude Code can read and edit the whole project. Unreal's Blueprints are opaque to that workflow.
- C# lets the material model live in a plain .NET class library with unit tests, completely outside the engine. Godot just references it. That is the "prove it first" step, and the sim survives an engine change.
- Free, no revenue licensing, runs on Windows 11, exports everywhere.
- Unity is the defensible alternative (bigger asset store, more tutorials), but its licensing history and heavier editor buy nothing for this project.

**Data-driven from day one.** Materials, recipes, item templates, and villager needs live in JSON, not code. Adding carpentry later should mean adding data files and one new minigame, not rewriting systems.

**3D from the first build.** The village is a small 3D scene and the forge minigames are close-up 3D interactions: heat glow is a shader driven by the sim's temperature, strike pitch comes from the sim's hardness, cracks appear on the mesh. Placeholder art comes from free CC0 3D packs (Kenney, Quaternius, Poly Haven) so effort stays on the sim. Godot 4's 3D handles a village and a forge interior well; large open-world streaming is its weakest area (terrain needs a plugin such as Terrain3D), so revisit the engine at the open-world stage. Because the sim is a plain C# library, Unity is a fallback that reuses it unchanged.

## First iteration scope: "The Smithy"

One village, one forge, one player. The loop:

1. **Procure**: buy or dig ore and charcoal; quality and price vary.
2. **Smelt and alloy**: choose ratios, manage heat; output is an ingot with a composition and a slag count.
3. **Forge**: the minigames, producing an item with real properties and flaws.
4. **Sell**: villagers have needs driven by their own work (a farmer's hoe wears, a cooper's adze breaks), a budget, and a memory of how the last tool held up. Reputation comes from tool performance over time, not from a quality stat on the sale screen.
5. **Competitor event**: a second smith arrives, running on the same economic model. Undercut, buy out, talk, boycott campaign, or violence, each with consequences through the same reputation and demand systems. Built as a generic rival agent, so the same code becomes a rival carpenter later.

**Deferred:** open world, survival needs, other professions, war cities, LLM dialogue. LLM-driven NPCs fit as a later layer on top of a deterministic sim; they need real state to talk about.

## Build order

1. **Design spec**: material model, minigame contracts, item schema. Done for the bronze tier: `docs/design-spec.md`.
2. **Material library + test harness** (C#, no engine): can it make a chisel, a bell, and an axe and give sensible, distinguishable results? Tune until the numbers match gut feel from the shop.
3. **One minigame in Godot (3D)**: cold-working a bronze edge at the anvil, placeholder art, hooked to the library. This is the fun test. If hammer work does not feel right here, stop and fix it before building anything else.
4. **Full loop end to end**, ugly: procure, melt, cast, work, sell to three villagers.
5. **Demand sim and the competitor.**
6. **Polish**: art, more minigames, iron and steel tiers, then the second profession.
