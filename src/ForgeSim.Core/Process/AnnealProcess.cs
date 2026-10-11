using ForgeSim.Core.Data;
using ForgeSim.Core.Items;
using ForgeSim.Core.Materials;

namespace ForgeSim.Core.Process;

/// <summary>§3.4 Anneal.</summary>
public static class AnnealProcess
{
    public static void Apply(Forge forge, ItemInstance item, AnnealStep step, List<string> log)
    {
        var m = forge.Model;
        var a = m.Anneal;
        var mat = item.RequireMaterial("Anneal");
        var comp = mat.Composition;
        double trx = MeltingRanges.Recrystallization(m, comp);
        double ts = MeltingRanges.Solidus(m, comp);
        log.Add($"Anneal {step.TempC:0} °C × {step.Minutes:0} min in {step.Atmosphere.ToString().ToLowerInvariant()} (T_rx {trx:0}, TS {ts:0})");

        if (step.TempC >= trx)
        {
            // Recrystallization
            double tauRx = a.RxTauMin * Math.Exp(-(step.TempC - trx) / a.RxTempScaleC);
            double x = 1 - Math.Exp(-step.Minutes / tauRx);
            foreach (var z in item.Zones)
            {
                z.Strain *= 1 - x;
                if (x > a.FullRecrystallizationAbove) z.ThicknessAtLastAnnealMm = z.ThicknessMm;
            }
            double grainBefore = mat.GrainSizeUm;
            mat.GrainSizeUm += a.GrainGrowthPerMin * step.Minutes * Math.Exp((step.TempC - trx) / a.GrainGrowthTempScaleC);

            // Homogenization
            double tauH = a.HomogenizationTauMin * Math.Exp(-(step.TempC - a.HomogenizationRefTempC) / a.HomogenizationTempScaleC);
            double h = 1 - Math.Exp(-step.Minutes / tauH);
            double deltaBefore = mat.Phase.Delta;
            double deltaEq = DeltaEq(m, comp);
            mat.Phase.Delta += (deltaEq - mat.Phase.Delta) * h;
            if (h > a.CoringRemovedAbove) mat.Phase.Cored = false;

            // Annealing moves δ back toward equilibrium, so any quench state is undone (spec-gaps).
            mat.Phase.BetaQuenched = false;
            mat.Phase.SolutionQuenched = false;

            log.Add($"  Recrystallized {x * 100:0}%; grain {grainBefore:0} → {mat.GrainSizeUm:0} µm; " +
                    $"homogenized {h * 100:0}%, δ {deltaBefore:0.000} → {mat.Phase.Delta:0.000}{(mat.Phase.Cored ? ", still cored" : ", coring removed")}");
        }
        else
        {
            log.Add($"  Below T_rx: no recrystallization");
        }

        // Burning: incipient melting above the solidus.
        if (step.TempC > ts)
        {
            var d = item.GetOrAddDefect(DefectType.Burnt);
            d.Severity += (step.TempC - ts) / a.BurntTempScaleC;
            if (d.Severity > a.BurntRevealedAbove) d.Reveal("blistered, grainy surface");
            log.Add($"  BURNT: {step.TempC - ts:0} °C above solidus (severity {d.Severity:0.00})");
        }

        // Lead sweat
        double pb = comp[a.LeadSweatElement];
        if (pb > a.LeadSweatAboveWt && step.TempC > a.LeadSweatAboveC)
        {
            double loss = Math.Min(pb, a.LeadSweatLossWtPerHour * step.Minutes / 60);
            mat.Composition = comp.With(a.LeadSweatElement, pb - loss);
            var d = item.GetOrAddDefect(DefectType.LeadSweat);
            d.Severity += loss;
            d.Reveal("lead beads on the surface");
            log.Add($"  Lead sweat: {a.LeadSweatElement} {pb:0.00}% → {pb - loss:0.00}%");
        }

        // Scale
        if (step.Atmosphere == Atmosphere.Air)
        {
            double scale = a.ScaleMmPerMin * step.Minutes * Math.Exp((step.TempC - a.ScaleRefTempC) / a.ScaleTempScaleC);
            foreach (var z in item.Zones)
            {
                z.ThicknessMm = Math.Max(0, z.ThicknessMm - scale);
                z.ThicknessAtLastAnnealMm = Math.Max(0, z.ThicknessAtLastAnnealMm - scale);
            }
            log.Add($"  Scale: {scale:0.000} mm lost per zone");
        }
    }

    /// <summary>δ_eq = min(0.6, max(0, Sn − 14) · 0.05)</summary>
    public static double DeltaEq(MaterialModel m, Composition comp) =>
        Math.Min(m.Anneal.DeltaEqMax, Math.Max(0, comp[m.Anneal.DeltaEqElement] - m.Anneal.DeltaEqStartWt) * m.Anneal.DeltaEqPerWt);
}
