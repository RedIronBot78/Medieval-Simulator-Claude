using ForgeSim.Core.Data;
using ForgeSim.Core.Items;
using ForgeSim.Core.Materials;

namespace ForgeSim.Core.Process;

/// <summary>§3.2 Cast.</summary>
public static class CastProcess
{
    public static void Apply(Forge forge, ItemInstance item, CastStep step, List<string> log)
    {
        var m = forge.Model;
        var c = m.Cast;
        var mat = item.RequireMaterial("Cast");
        var template = forge.Data.Template(item.TemplateId);

        double tl = MeltingRanges.Liquidus(m, mat.Composition);
        double sh = step.PourTempC - tl;
        log.Add($"Pour {step.PourTempC:0} °C into {step.MoldType.ToString().ToLowerInvariant()} mold (preheat {step.MoldPreheatC:0} °C), superheat {sh:0} °C, pour quality {step.PourQuality:0.00}");

        // Misrun / cold shut
        if (sh < c.MisrunSuperheatC)
        {
            var d = item.GetOrAddDefect(DefectType.Misrun);
            d.Severity = Math.Clamp((c.MisrunSuperheatC - sh) / c.MisrunSuperheatC, 0, 1);
            if (sh < c.MisrunVisibleBelowSuperheatC) d.Reveal("visible incomplete fill");
            log.Add($"  Misrun/cold shut (severity {d.Severity:0.00})");
        }

        // Porosity
        double porosity = c.PorosityPerGas * mat.GasContent
                          + c.PorosityPer100CExcessSuperheat * Math.Max(0, sh - c.PorosityExcessSuperheatAboveC) / 100
                          + c.PorosityPerPourError * (1 - step.PourQuality);
        mat.PorosityPct = Math.Clamp(porosity, 0, c.PorosityMax);
        log.Add($"  Porosity {mat.PorosityPct:0.00}%");
        if (mat.PorosityPct > c.PorosityDefectAbove)
        {
            var d = item.GetOrAddDefect(DefectType.Porosity);
            d.Severity = mat.PorosityPct;
            if (mat.PorosityPct > c.PorositySurfaceAbove) d.Reveal("surface porosity on casting");
        }

        // Grain size from cooling rate
        double cooling = CoolingFactor(m, step.MoldType, step.MoldPreheatC);
        mat.GrainSizeUm = c.GrainSizeNumeratorUm / Math.Sqrt(cooling);

        // As-cast phase state
        mat.Phase = new PhaseState { Cored = true, Delta = AsCastDelta(m, mat.Composition) };
        log.Add($"  Grain {mat.GrainSizeUm:0.0} µm, δ {mat.Phase.Delta:0.000}, cored");

        // Zones get template starting thickness, no strain.
        item.Zones.Clear();
        foreach (var zt in template.Zones)
            item.Zones.Add(new Zone
            {
                Id = zt.Id,
                ThicknessMm = zt.StartThicknessMm,
                ThicknessAtLastAnnealMm = zt.StartThicknessMm,
            });
    }

    /// <summary>Mold factor · (1 − preheat/1000). Floored at 0.01 so a mold preheated to 1000 °C+ doesn't divide by zero.</summary>
    public static double CoolingFactor(MaterialModel m, MoldType mold, double preheatC)
    {
        var key = mold.ToString().ToLowerInvariant();
        if (!m.Cast.MoldCoolingFactor.TryGetValue(key, out var f))
            throw new InvalidDataException($"No cooling factor for mold '{key}' in model {m.Id}.");
        return Math.Max(0.01, f * (1 - preheatC / m.Cast.MoldPreheatDivisorC));
    }

    /// <summary>δ = min(0.35, max(0, Sn − 7) · 0.015)</summary>
    public static double AsCastDelta(MaterialModel m, Composition comp) =>
        Math.Min(m.Cast.DeltaMax, Math.Max(0, comp[m.Cast.DeltaElement] - m.Cast.DeltaStartWt) * m.Cast.DeltaPerWt);
}
