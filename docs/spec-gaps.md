# Spec gaps and interpretations

`docs/design-spec.md` is the source of truth. This file lists every place where the spec was silent or
ambiguous and the implementation had to pick something. Each entry says what was chosen and where the
knob lives, so it can be overruled. Nothing here changes a spec number.

| # | Spec § | Gap | Chosen behaviour | Knob |
| --- | --- | --- | --- | --- |
| 1 | — | .NET version | net8.0, matching the installed Godot 4.7.2 .NET edition | `Directory.Build.props` |
| 2 | — | RNG algorithm | SplitMix64 (own implementation; `System.Random` is not guaranteed stable across runtimes) | `SimRng` |
| 3 | 3.1 | Does oxidation loss renormalize the melt? | No. Lost element's wt% is multiplied by (1 − loss) and Cu stays the balance, matching "Cu is the balance" and the spec example (10.0% → 9.4%). Renormalizing by the lost mass would give 9.50% instead of 9.44% | `MeltProcess` |
| 4 | 3.1 | Charge item compositions | Each charge item's base element is the balance of its own composition, so `{ "Sn": 100 }` is pure tin and `{ "Cu": 100 }` is pure copper | `MeltProcess.MixCharge` |
| 5 | 3.2 | Cooling factor ≤ 0 when mold preheat ≥ 1000 °C | Floored at 0.01 (grain 400 µm) instead of dividing by zero | `CastProcess.CoolingFactor` |
| 6 | 3.2 | Misrun severity when SH < 0 | Clamped to 1 | `CastProcess` |
| 7 | 9 | Validation rows don't all state grain size/porosity | "Annealed" rows: homogenized (δ = δ_eq, not cored), 40 µm, 0% porosity. "As cast" rows: cored, as-cast δ, 60 µm, 1% porosity (the conditions row 3 states). With 0% porosity, 22% Sn as cast gives ε_max 0.16, just over the ≤ 0.15 band | `ValidationTableTests` |
| 8 | 4.5 | Acoustic quality goes negative when porosity > 5% | Clamped to 0–1 | `PropertyModel.AcousticQuality` |
| 9 | 4.5 | Colour stop values | Copper (0.72, 0.42, 0.25) → gold (0.85, 0.65, 0.20) at 8–12% → pale gold (0.90, 0.80, 0.52) at 15% → grey (0.74, 0.74, 0.72) at 20%; As lerps toward silver at 8% per wt% | `bronze.json` `bulk.color` |
| 10 | 4.1 | Grain size of 0 (never cast) | Floored at 1 µm in the HV grain term | `PropertyModel.Hardness` |
| 11 | 1, 3.3, 3.4 | ε = ln(T_lastAnneal / T) breaks when hot strikes, scale or grinding thin a zone, and can't express a partial anneal (ε ← ε·(1 − X)) | Strain is stored on the zone. Cold strikes add ln(T_before / T_after), which matches the spec definition for pure cold reduction; hot strikes, scale and grinding thin the zone without adding strain; anneal multiplies by (1 − X) and resets `ThicknessAtLastAnnealMm` when X > 0.95 | `Zone.Strain` |
| 12 | 3.4 | Does annealing undo a quench? | Yes. Any anneal at or above T_rx clears `BetaQuenched` and the solution-quench flag, because it moves δ back toward equilibrium | `AnnealProcess` |
| 13 | 3.4 | Repeated burning | Burnt severity accumulates across anneals; revealed once the total passes 0.5 | `AnnealProcess` |
| 14 | 3.4 | Is homogenization cumulative? | No, per spec: each anneal's H is computed alone, and coring is removed only if a single anneal reaches H > 0.9. So 650 °C × 30 min (H = 0.81) leaves 10% Sn cored, while 700 °C × 30 min (H = 0.94) clears it. Tracking a remaining-coring fraction would be a structural change; not done | `AnnealProcess` |
| 15 | 3.3 | Order of checks within one blow | Thin first, then add strain, then the crack check against the new ε. Each blow always consumes four RNG draws (crack, fold, hot-tin, hot-lead), so retuning one constant doesn't reshuffle later rolls | `StrikeProcess` |
| 16 | 3.3, 5 | Crack severity added by a hot-short crack | The same as a cold crack: 0.05 + 0.1·Force | `StrikeProcess` |
| 17 | 3.3 | Fold severity | Each fold is its own hidden defect, severity 1; toughness counts them (−10 each) | `StrikeProcess` |
| 18 | 3.5 | Size of the "small toughness gain" from quenching above T_rx | +3 toughness, water or oil only (air cooling is the baseline), and no hardness change (within the ≤ 5% allowed) | `quench.solutionQuenchToughnessBonus` |
| 19 | 3.5 | Thermal-shock crack: which zone, how severe? | One roll per zone; a crack adds 0.15 severity (hidden below 0.3) | `quench.thermalShockCrackSeverity` |
| 20 | 3.5 | β-quench vs thermal shock order | β-quench first (sets δ = 0.05), then the shock check, so a correct β-quench can't shock-crack on δ that isn't present at 586–790 °C | `QuenchProcess` |
| 21 | 3.7 | File-test band edges | soft < 100 HV ≤ medium < 170 HV ≤ hard | `inspect.fileBands` |
| 22 | 3.7 | Can inspection find a hidden misrun (SH 15–40) or segregation? | No. Neither is in the method table, so only use reveals them | `Inspector` |
| 23 | 3.7 | Inspect is "not a process step", but reveal state must replay | Inspections go in `ItemInstance.Inspections` with their position in the history, separate from `History` | `InspectionRecord` |
| 24 | 6 | Zone thicknesses and numeric requirements for every template except the chisel | Chosen to match the §6 table's intent: axe edge HV 170–230 and body toughness ≥ 60; sickle edge retention ≥ 45 and toughness ≥ 60; knife edge retention ≥ 50; bell acoustic ≥ 0.85 and no cracks; cauldron no leaks and castability ≥ 70. Cauldron `wall` zone is marked `isWall` | `data/items/*.json` |
| 25 | 6 | Fitness score | Σ weight of met requirements / Σ weight (0–1), pass/fail per requirement. "Head softer than edge" (chisel) isn't in the spec's example requirements and isn't modelled | `Fitness` |
| 26 | 7 | When is an edge "dull"? | When accumulated edge wear reaches 10; the owner regrinds, wear resets to 0, and the event is logged | `use.dullThreshold` |
| 27 | 7 | Material lost per chip | 0.1 mm from the primary zone | `use.chipLossMm` |
| 28 | 7 | When does use reveal a hidden crack? | When it grows past the visible threshold (0.3). Breaking reveals every defect (post-mortem) | `UseSimulator` |
| 29 | 7 | Use-profile numbers | carving_hardwood: impact 0.4, abrasion 0.5, target 0.5. chopping_softwood: 0.7 / 0.3 / 0.2. cutting_stone: 0.6 / 0.8 / 0.9 | `data/uses/*.json` |
| 30 | 9 | Behavioral test 10 is probabilistic | Compared over 50 seeds × 500 uses: break count and mean life (10% Sn vs 22% Sn), and edge wear per use (10% Sn vs Cu) | `SimulateTests` |

## Results flagged for review (nothing changed)

Spec numbers that give results which look physically or game-wise wrong. Per CLAUDE.md these are reported
with inputs and outputs and a proposed fix. **None of the fixes is applied.** All numbers are from
`dotnet run --project src/ForgeSim.Cli -- data/recipes/*.json --use carving_hardwood` (500 uses × 50 seeds).

### F1. A visibly cracked chisel scores full fitness

- `chisel_10sn_overworked`: edge ε 0.61 > ε_max 0.50, crack severity 0.91 (revealed), edge toughness 19,
  breaks in about 8 uses. Fitness is still **1.00**, because the spec's example chisel requirements only check
  edge HV ≥ 180, shank toughness ≥ 55 and edge shape error.
- Proposed fix (data only): add `{ "zone": "edge", "property": "CrackSeverity", "max": 0 }` and an edge
  toughness floor (e.g. ≥ 40) to `data/items/chisel_bronze.json`. This changes the spec's example template, so
  it needs your call.

### F2. Sound castings snap in normal use, and the good bronze chisel breaks more often than copper

- `chisel_10sn_good` (no defects; edge toughness 59, porosity 0.56%, δ 0.007): breakChance
  = 0.4 · (0 + 0.056 + 0.007) · (80 − 59) / 200 = 0.0026 per use → **35/50 broke within 500 uses** (mean life 259).
- `chisel_cu_pure` (toughness 73, porosity 0.72%): 0.0010 per use → **15/50 broke**. Pure copper should bend, not snap.
- Behavioral test 10 still passes (22% Sn breaks far sooner: mean life ~ 50), but a well-made chisel should not
  usually break within a few hundred mallet blows.
- Cause: the `Porosity/10` term counts porosity even below the 1% level the spec treats as harmless, and
  `breakToughnessRef` 80 is above what most worked bronze reaches.
- Proposed fix (one of): (a) use only porosity above the defect threshold, `max(0, P − 1)/10`, which gives the good
  chisel ~0.0003/use (about 14% broken by 500 uses) and copper 0; (b) lower `breakToughnessRef` 80 → 50;
  (c) raise `breakDivisor` 200 → 2000. (a) changes the formula's shape; (b) and (c) are data-only.

### F3. Pure copper counts as "cored" forever

- §3.2 sets `cored = true` on every casting. Pure Cu has no freezing range, so physically it can't be cored, but
  it keeps the −10 toughness penalty unless one anneal reaches H > 0.9 (≈ 3 h at 500 °C). `chisel_cu_pure`
  shows "still cored" after both anneals.
- Proposed fix: set `cored` only when the alloy has a freezing range (TL − TS > 0). The copper chisel's edge
  toughness would go 73 → 83 (which also stops the copper breaks in F2).

### Observations (probably fine, worth a smith's eye)

- **The spec's own good-chisel recipe stays cored.** 650 °C × 30 min gives H = 0.81 (< 0.9) and 600 °C × 10 min gives
  H = 0.28, and they don't add up (gap 14). That costs the edge 10 toughness (59 instead of 69). Real tin bronze does
  need hours to lose its coring, so this may be right; 700 °C × 30 min clears it.
- **The good chisel's edge ends at 4.04 mm against a 3.0 mm target.** The spec's example recipe only reaches ε ≈ 0.18
  per round, and nothing penalizes thickness: `ShapeError` only grows with inaccurate blows.
- **Tin loss looks believable:** 1.0% of the tin under charcoal (15 min at 1120 °C); 5.6% uncovered (20 min at 1150 °C), as the spec says.
- **The overworked chisel cracks where you'd expect:** ε_max for this metal is 0.50, reached after ~67 blows at
  force 0.8; the 85-blow recipe ends at ε 0.61 with 7 crack events.
