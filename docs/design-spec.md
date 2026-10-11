# Design Spec v0 — Bronze Tier Material Model

Source of truth for `ForgeSim.Core`. Covers copper and the bronzes: tin bronze, arsenical bronze, leaded bronze and bell metal. Iron and steel tiers come later and must fit the same structure.

All constants below are **starting values, tuned for gameplay**. They are deliberately "truthy": shaped like real metallurgy, not exact. Every constant lives in `data/elements/*.json` or `data/model/bronze.json` so it can be retuned without recompiling. When a result looks wrong to someone who has worked metal, the constant changes, not the structure.

Units everywhere: **°C, minutes, millimetres, mass percent (wt%)**. Hardness is Vickers (HV). Indices are 0–100 unless stated.

---

## 1. Core data model

```
Material
  Composition   : map element -> wt%   (Cu is the balance; sum = 100)
  PhaseState    : delta fraction δ (0–1), cored (bool), betaQuenched (bool)
  GrainSize     : µm
  Porosity      : % (0–10)
  GasContent    : index (0–10)       # dissolved gas picked up during melting
  Homogeneity   : 0–1                # how well the alloy mixed

ItemInstance
  TemplateId    : e.g. "chisel_bronze"
  Material      : as above (bulk)
  Zones         : list of Zone
  History       : ordered list of ProcessStep (source of truth)
  Defects       : list of Defect (each with Revealed: bool)

Zone                                  # e.g. edge, body, socket, head
  Id
  ThicknessMm, ThicknessAtLastAnnealMm
  Strain ε      : ln(ThicknessAtLastAnnealMm / ThicknessMm)  # cold work since last anneal
  ShapeError    : 0–1                 # deviation from template target
  CrackSeverity : 0–1
```

`Properties = Derive(ItemInstance)` is a pure function. Properties are never stored as truth.

### Elements in v0

| Element | Role | Notes |
| --- | --- | --- |
| Cu | Base | Balance of composition |
| Sn | Main alloying element | Hardness, strength, castability; brittle above ~14% |
| As | Alternative alloying element | Arsenical bronze; hardens well when worked; volatile when melting |
| Pb | Additive | Improves castability and fluidity; softens; causes hot-shortness |
| Zn | Impurity / later brass tier | Fumes off when melting |
| Fe | Ore impurity | Small hardening, raises inclusion count |
| S | Ore impurity | Raises gas content and brittleness |

---

## 2. Melting ranges

```
Liquidus TL = max(800, 1085 − 9·Sn − 12·As − 3·Pb − 6·Zn)
Solidus  TS = max(798, 1085 − 23·Sn − 30·As)
Recrystallization T_rx = 350 + 10·Sn_eff + 8·As
Sn_eff = min(Sn, 14)
```

| Alloy | TL | TS | T_rx |
| --- | --- | --- | --- |
| Pure Cu | 1085 | 1085 | 350 |
| 6% Sn | 1031 | 947 | 410 |
| 10% Sn | 995 | 855 | 450 |
| 14% Sn | 959 | 798 | 490 |
| 22% Sn (bell metal) | 887 | 798 | 490 |

Lead does not dissolve in copper. Above **326 °C** it is liquid between the grains, which is why leaded bronze cracks if worked hot (see §5).

---

## 3. Process steps

Every minigame produces exactly one of these records. The sim applies them in order to the `ItemInstance`. Nothing else changes an item.

### 3.1 `Melt` (crucible)

Inputs: charge (list of ingots or scrap, each with composition and mass in kg), `PeakTempC`, `MinutesAtTemp`, `MinutesAboveLiquidus`, `Cover` (none | charcoal), `Stirred` (bool).

Effects:

- **Fails** if `PeakTempC < TL` of the mixed charge: returns a "not fully molten" result; nothing can be poured.
- **Oxidation losses** (fraction of that element lost):
  ```
  f(T)       = exp((PeakTempC − 1100) / 150)
  coverFactor = 0.3 if charcoal cover else 1.0
  loss_Sn = 0.002 · MinutesAtTemp · f(T) · coverFactor
  loss_As = 0.006 · MinutesAtTemp · f(T) · coverFactor
  loss_Zn = 0.020 · MinutesAtTemp · f(T) · coverFactor
  ```
  Example: 20 min at 1150 °C uncovered loses ~5.6% of the tin (10.0% → 9.4%). With a charcoal cover, ~1.7%. This is why the ratio you weigh out is not the ratio you get, and why experienced smiths overcharge tin slightly.
- **Gas pickup**: `GasContent += 0.1 · MinutesAtTemp · exp((PeakTempC − 1100) / 100) · (0.5 if charcoal cover else 1.0)` plus `20 · S` from sulfur. Clamp 0–10.
- **Homogeneity** = `1 − exp(−MinutesAboveLiquidus / (Stirred ? 2 : 5))`. Below 0.8 adds a `Segregation` defect (hidden), severity `0.8 − Homogeneity`.

### 3.2 `Cast`

Inputs: `PourTempC`, `MoldType` (stone | clay | sand | bronze), `MoldPreheatC`, `PourQuality` (0–1, from the pour minigame: steady stream, no splashing, no pauses).

```
Superheat SH = PourTempC − TL
```

- **Misrun / cold shut** if SH < 40: defect severity `(40 − SH) / 40` (revealed: visible incomplete fill when SH < 15, hidden otherwise).
- **Porosity %** = `0.5 · GasContent + 1.5 · max(0, SH − 150) / 100 + 1.0 · (1 − PourQuality)`, clamp 0–10. Porosity above 1% is a hidden defect; above 3% it shows on the surface.
- **Cooling factor** by mold: stone 1.0, bronze 1.5, clay 0.6, sand 0.4; multiplied by `(1 − MoldPreheatC / 1000)`.
- **Grain size µm** = `40 / sqrt(coolingFactor)`.
- **As-cast phase state**: `cored = true`, `δ = min(0.35, max(0, Sn − 7) · 0.015)`. Fast, uneven cooling leaves tin-rich delta phase between dendrites even in alloys that would be single-phase at equilibrium.
- All zones get template starting thickness; `ThicknessAtLastAnnealMm = ThicknessMm`; strain 0.

### 3.3 `Strike` (hammer work, batched)

The anvil minigame emits a list of strikes. Each strike: `ZoneId`, `Force` (0–1), `Accuracy` (0–1, on target), `AngleOffDeg`, `MetalTempC` at the moment of the strike.

Per strike:

```
HVref = 100
reduction = 0.02 · Force · Accuracy · (HVref / currentZoneHV)
ThicknessMm *= (1 − reduction)
```

Harder metal moves less per blow. This is the core feel: as the edge work-hardens, it stops moving and starts ringing, and the player has to decide whether to anneal.

- **Cold vs hot**: if `MetalTempC ≥ T_rx`, strain does not accumulate (the metal recrystallizes as it is worked), but hot-shortness rules apply (§5).
- **Crack check** (cold strikes): if zone strain ε > ε_max (§4.4), crack probability for this strike `p = clamp((ε − ε_max) / 0.1, 0, 1) · Force`. On a crack, `CrackSeverity += 0.05 + 0.1 · Force`. Severity under 0.3 is hidden.
- **Folds / laps**: `p = 0.05 · AngleOffDeg / 45` when AngleOffDeg > 25; adds a hidden `Fold` defect.
- **Shape**: off-target strikes (Accuracy < 0.7) increase `ShapeError` by `0.01 · (1 − Accuracy)`.

### 3.4 `Anneal`

Inputs: `TempC`, `Minutes`, `Atmosphere` (air | charcoal).

```
if TempC ≥ T_rx:
  τ_rx = 10 · exp(−(TempC − T_rx) / 100)
  X    = 1 − exp(−Minutes / τ_rx)                # recrystallized fraction
  every zone: ε ← ε·(1 − X); when X > 0.95, ThicknessAtLastAnnealMm = ThicknessMm
  GrainSize += 0.5 · Minutes · exp((TempC − T_rx) / 150)

  homogenization (removes coring, moves δ toward equilibrium):
  τ_h = 30 · exp(−(TempC − 600) / 100)
  H   = 1 − exp(−Minutes / τ_h)
  δ   ← δ + (δ_eq − δ) · H ;  cored = false when H > 0.9
  δ_eq = min(0.6, max(0, Sn − 14) · 0.05)
```

- A good anneal on 10% tin bronze dissolves the as-cast delta phase and makes it far more workable. Under-annealing leaves it brittle; long, hot anneals grow coarse grains (softer, slightly less tough).
- **Burning**: if `TempC > TS`, add a `Burnt` defect, severity `(TempC − TS) / 50`, revealed if > 0.5. Incipient melting ruins the grain boundaries.
- **Lead sweat**: if Pb > 2 and TempC > 600, lead beads on the surface (revealed), Pb loses `0.5 · Minutes / 60` wt%.
- **Scale**: in air, each zone loses `0.002 · Minutes · exp((TempC − 600) / 200)` mm of thickness.

### 3.5 `Quench`

Inputs: `FromTempC`, `Medium` (air | water | oil).

- **Standard bronzes do not harden by quenching.** Hardness changes by at most 5%. Quenching from above T_rx keeps the alloy single-phase (δ stays where annealing left it rather than re-precipitating), giving a small toughness gain. This is a deliberate realism lesson: players who expect steel-like behavior will learn bronze is hardened by hammering, not quenching.
- **Thermal shock**: water quench with δ > 0.15 → crack chance `2 · (δ − 0.15)`.
- **β-quench (advanced, discoverable)**: if 19 ≤ Sn ≤ 25 and 586 ≤ FromTempC ≤ 790 and Medium = water, set `betaQuenched = true` and `δ = 0.05`. High-tin bronze quenched from this window becomes hot-workable and much tougher. This is how traditional gongs and cymbals are made. Never hint it in the UI; let players find it.

### 3.6 `Grind`

Inputs: `ZoneId`, `EdgeAngleDeg` (included angle), `Finish` (0–1). Removes `0.05 · (1 + 1 − Finish)` mm from the zone. Records edge geometry used by §6.

### 3.7 `Inspect` (not a process step)

Reveals hidden defects by method:

| Method | Reveals |
| --- | --- |
| Visual (good light) | Cracks ≥ 0.15, laps, surface porosity, burnt |
| Ring test (tap and listen) | Cracks ≥ 0.05, porosity ≥ 1% (dull ring) |
| File test | Approximate hardness band (soft / medium / hard) |
| Use | Everything, eventually, by failure |

---

## 4. Derived properties (per zone, unless noted)

### 4.1 Hardness (HV)

```
HV_base = 42 + 4.5·Sn_eff + 9·As + 1.5·Zn + 3·Fe − 1.5·Pb
        + 250·δ
        + 60 / sqrt(GrainSize)
HV      = HV_base + (110 + 9·Sn_eff + 12·As) · sqrt(ε)
```

If `betaQuenched`, use δ = 0.05 and add a flat +40.

### 4.2 Toughness index (0–100)

```
T = 100 − 0.18·(HV − 45) − 120·δ − 4·Porosity − 30·CrackSeverity − 2·Pb
      − (cored ? 10 : 0) − max(0, 0.05·(GrainSize − 50))
      − 10·FoldCount − 25·BurntSeverity
clamp 0–100
```

### 4.3 Edge retention (0–100)

`clamp((HV − 40) / 2.6, 0, 100)`

### 4.4 Workability

```
ε_max = 0.85 − 0.03·Sn_eff − 1.2·δ − Porosity/20 − 0.02·Pb
RemainingWorkability = ε_max − ε
```

When this drops below zero, further cold strikes risk cracking. The anvil minigame should telegraph this through sound (higher, sharper ring) and feel (metal stops moving), not a number on screen.

### 4.5 Bulk properties

- **Corrosion resistance** = `clamp(50 + 3·Sn + 5·As − 2·Zn − 5·S, 0, 100)`
- **Castability** (fluidity) = `clamp(40 + 2·Sn + 4·Pb + 0.2·SH_typical, 0, 100)` with SH_typical = 100
- **Acoustic quality** (bells, gongs) = `exp(−((Sn − 21.5) / 2.5)²) · (1 − Porosity / 5) · Homogeneity · (1 − CrackSeverity_max)`
- **Leaks** (vessels): Porosity > 3% or any crack in a wall zone.
- **Color** (for the shader): interpolate copper red (0% Sn) → gold (8–12%) → pale gold (15%) → silvery grey (≥ 20%). Arsenic shifts toward silver.

---

## 5. Hot-shortness and failure rules

- Strike with `MetalTempC > T_rx` and **Sn > 6**: intergranular crack chance per strike `0.03 · (Sn − 6)`. (Gameplay simplification: tin bronze is cold-worked with anneals between rounds.)
- Strike with `MetalTempC > 326` and **Pb > 0.5**: crack chance per strike `0.1 · Pb`.
- Exception: `betaQuenched` alloys may be hot-worked between 600 and 700 °C without the Sn rule.

---

## 6. Item templates (data/items/*.json)

Each template defines zones, starting and target thickness, which process path is expected, and requirement weights used to score fitness for a job.

| Item | Zones | Typical path | What matters most |
| --- | --- | --- | --- |
| Chisel | edge, shank, head | Cast bar → cold work edge → anneal cycles → final cold work edge → grind | Edge HV ≥ 180, shank toughness ≥ 55, head softer than edge |
| Flat axe | edge, body | Cast in mold → edge cold work → grind | Edge HV 170–230, body toughness ≥ 60 |
| Sickle | edge, blade | Cast → cold work along whole edge → grind | Edge retention, toughness ≥ 60 (bending is OK, snapping is not) |
| Knife | edge, spine, tang | Cast blank → cold work → grind | Edge retention, toughness |
| Bell | body | Cast only, 20–23% Sn | Acoustic quality; brittleness accepted |
| Cauldron | wall, rim, feet | Cast, thin walls, leaded bronze acceptable | No leaks, castability |

Example template:

```json
{
  "id": "chisel_bronze",
  "zones": [
    { "id": "edge",  "startThicknessMm": 6.0,  "targetThicknessMm": 3.0 },
    { "id": "shank", "startThicknessMm": 12.0, "targetThicknessMm": 11.0 },
    { "id": "head",  "startThicknessMm": 14.0, "targetThicknessMm": 14.0 }
  ],
  "requirements": [
    { "zone": "edge",  "property": "HV",        "min": 180, "weight": 0.5 },
    { "zone": "shank", "property": "Toughness", "min": 55,  "weight": 0.3 },
    { "zone": "edge",  "property": "ShapeError","max": 0.15,"weight": 0.2 }
  ]
}
```

---

## 7. Performance in use

`Simulate(item, useProfile, uses, rng)` returns a log of what happened. This is what villagers remember, and what drives reputation later.

A `UseProfile` (data/uses/*.json) has `ImpactLoad` (0–1), `Abrasion` (0–1), `TargetHardness` (e.g. softwood 0.2, hardwood 0.5, stone 0.9), `PrimaryZone`.

Per use, on the primary zone:

```
edgeWear     += Abrasion · TargetHardness · 40 / HV            # sharpness lost
bendChance    = ImpactLoad · max(0, 120 − HV) / 400            # soft metal deforms (repairable)
chipChance    = ImpactLoad · TargetHardness · max(0, 70 − T) / 300
crackGrowth   = CrackSeverity > 0 ? ImpactLoad · 0.01 : 0
breakChance   = ImpactLoad · (CrackSeverity + Porosity/10 + δ) · max(0, 80 − T) / 200
```

Outcomes: dull (needs regrind), bent (smith can straighten), chipped (regrind, loses material), cracked (hidden → revealed), broken (item destroyed). The log is deterministic for a given seed.

---

## 8. Recipe file (data/recipes/*.json)

A recipe is a full process history. Tests and the CLI run these.

```json
{
  "id": "chisel_10sn_good",
  "template": "chisel_bronze",
  "seed": 1234,
  "steps": [
    { "type": "Melt", "charge": [
        { "composition": { "Cu": 100 }, "massKg": 0.90 },
        { "composition": { "Sn": 100 }, "massKg": 0.105 } ],
      "peakTempC": 1120, "minutesAtTemp": 15, "minutesAboveLiquidus": 12,
      "cover": "charcoal", "stirred": true },
    { "type": "Cast", "pourTempC": 1100, "moldType": "stone", "moldPreheatC": 150, "pourQuality": 0.9 },
    { "type": "Anneal", "tempC": 650, "minutes": 30, "atmosphere": "charcoal" },
    { "type": "Strike", "repeat": 25, "zone": "edge", "force": 0.7, "accuracy": 0.9, "angleOffDeg": 5, "metalTempC": 20 },
    { "type": "Anneal", "tempC": 600, "minutes": 10, "atmosphere": "charcoal" },
    { "type": "Strike", "repeat": 25, "zone": "edge", "force": 0.6, "accuracy": 0.9, "angleOffDeg": 5, "metalTempC": 20 },
    { "type": "Grind", "zone": "edge", "edgeAngleDeg": 30, "finish": 0.8 }
  ]
}
```

`repeat` is a convenience for test recipes; the game emits individual strikes.

---

## 9. Validation targets (must pass as unit tests)

Built directly from the formulas above. Bands are ±15% to allow tuning. If a test fails after a change, the failure message prints inputs and the computed value.

| Case | HV | Toughness | ε_max |
| --- | --- | --- | --- |
| Pure Cu, annealed, 40 µm | 45–60 | ≥ 90 | 0.85 |
| Pure Cu, cold worked ε = 0.5 | 110–150 | 75–95 | 0.85 |
| 10% Sn, as cast (60 µm, 1% porosity) | 90–120 | 60–80 | 0.40–0.50 |
| 10% Sn, annealed (homogenized, 40 µm) | 85–110 | 80–100 | 0.50–0.60 |
| 10% Sn, annealed then ε = 0.5 | 205–270 | 55–75 | 0.50–0.60 |
| 4% As, annealed then ε = 0.5 | 170–230 | 62–82 | 0.80–0.90 |
| 6% Sn, annealed then ε = 0.5 | 165–225 | 63–83 | 0.60–0.72 |
| 22% Sn, as cast | 145–195 | ≤ 42 | ≤ 0.15 |
| 22% Sn, annealed | ≥ 180 | ≤ 28 | ≤ 0 (cannot be cold worked) |
| 8% Sn 8% Pb, as cast | 65–90 | 52–72 | 0.32–0.44 |

Behavioral tests (must hold):

1. **Tin loss**: melting 10.0% Sn for 20 min at 1150 °C uncovered yields 9.3–9.5% Sn; with charcoal cover, 9.8–9.9%.
2. **Quench does not harden bronze**: 10% Sn annealed at 650 °C then water quenched vs air cooled differ by ≤ 5% HV.
3. **Annealing restores workability**: 10% Sn worked to ε = 0.5, then annealed 650 °C × 30 min, returns ε < 0.05.
4. **Overwork cracks**: 10% Sn struck cold past ε_max with Force 0.8 develops CrackSeverity > 0 within 20 strikes (seeded).
5. **Burning**: annealing 10% Sn at 900 °C (above TS 855) adds a Burnt defect.
6. **Leaded bronze is hot-short**: 8% Pb struck at 500 °C cracks within 10 strikes (seeded).
7. **Bell metal**: 22% Sn cast with good pour has acoustic quality ≥ 0.85; 10% Sn ≤ 0.1.
8. **β-quench**: 22% Sn water quenched from 700 °C has toughness ≥ 2× the air-cooled version.
9. **Determinism**: running any recipe twice with the same seed produces identical output.
10. **Item comparison**: the same chisel template made from 10% Sn worked edge outperforms 22% Sn (breaks less) and pure Cu (dulls faster) over 500 uses of a "carving hardwood" profile.

---

## 10. Out of scope for v0

Smelting ore (v0 buys copper and tin ingots of known or unknown purity), iron and steel, fuel economics, the 3D presentation, villagers. Those come in later build-order steps and must plug into this model without changing its structure.
