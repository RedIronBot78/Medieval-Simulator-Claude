using ForgeSim.Core.Materials;

namespace ForgeSim.Core.Data;

/// <summary>
/// <c>Base + Σ PerWt[el] · wt%(el)</c>, optionally clamped. Most spec formulas are a composition-linear
/// part plus a few state terms; this is the composition part, so new elements are pure data.
/// When <see cref="UseEffective"/> is set, element wt% is first capped by the model's effective caps
/// (e.g. Sn_eff = min(Sn, 14)).
/// </summary>
public sealed class LinearTerm
{
    public double Base { get; init; }
    public Dictionary<string, double> PerWt { get; init; } = new();
    public bool UseEffective { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }

    public double Evaluate(Composition c, IReadOnlyDictionary<string, double> effectiveCaps)
    {
        double v = Base;
        foreach (var (symbol, coef) in PerWt)
        {
            double wt = c[symbol];
            if (UseEffective && effectiveCaps.TryGetValue(symbol, out var cap)) wt = Math.Min(wt, cap);
            v += coef * wt;
        }
        if (Min is { } lo) v = Math.Max(lo, v);
        if (Max is { } hi) v = Math.Min(hi, v);
        return v;
    }
}
