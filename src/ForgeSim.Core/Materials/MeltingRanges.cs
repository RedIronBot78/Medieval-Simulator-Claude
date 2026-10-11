using ForgeSim.Core.Data;

namespace ForgeSim.Core.Materials;

/// <summary>§2 Melting ranges.</summary>
public static class MeltingRanges
{
    /// <summary>TL = max(800, 1085 − 9·Sn − 12·As − 3·Pb − 6·Zn)</summary>
    public static double Liquidus(MaterialModel m, Composition c) => m.Melting.Liquidus.Evaluate(c, m.EffectiveCaps);

    /// <summary>TS = max(798, 1085 − 23·Sn − 30·As)</summary>
    public static double Solidus(MaterialModel m, Composition c) => m.Melting.Solidus.Evaluate(c, m.EffectiveCaps);

    /// <summary>T_rx = 350 + 10·Sn_eff + 8·As</summary>
    public static double Recrystallization(MaterialModel m, Composition c) => m.Melting.Recrystallization.Evaluate(c, m.EffectiveCaps);
}
