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
