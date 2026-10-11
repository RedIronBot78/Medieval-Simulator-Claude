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
| 15 | 3.3 | Order of checks within one blow | Thin first, then add strain, then the crack check against the new ε. Each blow always consumes four RNG draws (crack, fold, hot-tin, hot-lead), so retuning one constant doesn't reshuffle later rolls | `StrikeProcess` |
| 16 | 3.3, 5 | Crack severity added by a hot-short crack | The same as a cold crack: 0.05 + 0.1·Force | `StrikeProcess` |
| 17 | 3.3 | Fold severity | Each fold is its own hidden defect, severity 1; toughness counts them (−10 each) | `StrikeProcess` |
| 18 | 3.5 | Size of the "small toughness gain" from quenching above T_rx | +3 toughness, water or oil only (air cooling is the baseline), and no hardness change (within the ≤ 5% allowed) | `quench.solutionQuenchToughnessBonus` |
| 19 | 3.5 | Thermal-shock crack: which zone, how severe? | One roll per zone; a crack adds 0.15 severity (hidden below 0.3) | `quench.thermalShockCrackSeverity` |
| 20 | 3.5 | β-quench vs thermal shock order | β-quench first (sets δ = 0.05), then the shock check, so a correct β-quench can't shock-crack on δ that isn't present at 586–790 °C | `QuenchProcess` |
| 21 | 3.7 | File-test band edges | soft < 100 HV ≤ medium < 170 HV ≤ hard | `inspect.fileBands` |
| 22 | 3.7 | Can inspection find a hidden misrun (SH 15–40) or segregation? | No. Neither is in the method table, so only use reveals them | `Inspector` |
| 23 | 3.7 | Inspect is "not a process step", but reveal state must replay | Inspections go in `ItemInstance.Inspections` with their position in the history, separate from `History` | `InspectionRecord` |
| 14 | 3.4 | Is homogenization cumulative? | No, per spec: each anneal's H is computed alone, and coring is removed only if a single anneal reaches H > 0.9. So 650 °C × 30 min (H = 0.81) leaves 10% Sn cored, while 700 °C × 30 min (H = 0.94) clears it. Tracking a remaining-coring fraction would be a structural change; not done | `AnnealProcess` |
