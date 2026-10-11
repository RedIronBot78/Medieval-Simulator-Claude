using ForgeSim.Core.Items;
using ForgeSim.Core.Materials;
using ForgeSim.Core.Rng;

namespace ForgeSim.Core.Process;

/// <summary>
/// §3.5 Quench. Standard bronzes do not harden by quenching: the only effects are a small toughness gain
/// (solution quench), thermal-shock cracking of δ-rich metal, and the discoverable β-quench of high-tin bronze.
/// </summary>
public static class QuenchProcess
{
    public static void Apply(Forge forge, ItemInstance item, QuenchStep step, SimRng rng, List<string> log)
    {
        var m = forge.Model;
        var q = m.Quench;
        var mat = item.RequireMaterial("Quench");
        double trx = MeltingRanges.Recrystallization(m, mat.Composition);
        string medium = step.Medium.ToString().ToLowerInvariant();
        log.Add($"Quench from {step.FromTempC:0} °C into {medium}");

        // β-quench window first: there the metal is α+β with no δ to shock.
        double sn = mat.Composition[q.BetaElement];
        if (sn >= q.BetaMinWt && sn <= q.BetaMaxWt
            && step.FromTempC >= q.BetaMinC && step.FromTempC <= q.BetaMaxC
            && medium == q.BetaMedium)
        {
            mat.Phase.BetaQuenched = true;
            mat.Phase.Delta = q.BetaDelta;
            log.Add("  β retained: the metal rings differently");
        }
        else if (step.FromTempC >= trx && step.Medium != QuenchMedium.Air)
        {
            mat.Phase.SolutionQuenched = true;
        }

        // Thermal shock: one roll per zone, always drawn.
        foreach (var zone in item.Zones)
        {
            double roll = rng.NextDouble();
            if (medium != q.ThermalShockMedium || mat.Phase.Delta <= q.ThermalShockDeltaAbove) continue;
            double p = Math.Clamp(q.ThermalShockChancePerDelta * (mat.Phase.Delta - q.ThermalShockDeltaAbove), 0, 1);
            if (roll < p)
            {
                StrikeProcess.AddCrack(forge, item, zone, q.ThermalShockCrackSeverity, "quench crack");
                log.Add($"  Thermal shock crack in {zone.Id} (severity {zone.CrackSeverity:0.00})");
            }
        }
    }
}
