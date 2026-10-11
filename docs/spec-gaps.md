# Spec gaps and interpretations

`docs/design-spec.md` is the source of truth. This file lists every place where the spec was silent or
ambiguous and the implementation had to pick something. Each entry says what was chosen and where the
knob lives, so it can be overruled. Nothing here changes a spec number.

| # | Spec § | Gap | Chosen behaviour | Knob |
| --- | --- | --- | --- | --- |
| 1 | — | .NET version | net8.0, matching the installed Godot 4.7.2 .NET edition | `Directory.Build.props` |
| 2 | — | RNG algorithm | SplitMix64 (own implementation; `System.Random` is not guaranteed stable across runtimes) | `SimRng` |
