using ForgeSim.Core.Data;
using ForgeSim.Core.Items;
using ForgeSim.Core.Materials;

namespace ForgeSim.Core.Properties;

public sealed record ZoneProperties(
    string ZoneId,
    double ThicknessMm,
    double Strain,
    double Hardness,
    double Toughness,
    double EdgeRetention,
    double EpsMax,
    double RemainingWorkability,
    double ShapeError,
    double CrackSeverity,
    int FoldCount);

public sealed record BulkProperties(
    double LiquidusC,
    double SolidusC,
    double RecrystallizationC,
    double CorrosionResistance,
    double Castability,
    double AcousticQuality,
    bool Leaks,
    double[] ColorRgb);

public sealed record ItemProperties(BulkProperties Bulk, IReadOnlyList<ZoneProperties> Zones)
{
    public ZoneProperties Zone(string id) => Zones.First(z => z.ZoneId == id);
}

/// <summary>
/// §4 Derived properties. Pure functions of composition + state; nothing here is stored on the item
/// (architecture rule 3).
/// </summary>
public static class PropertyModel
{
    /// <summary>δ used by property formulas: a β-quenched alloy counts as δ = 0.05 (§4.1).</summary>
    public static double EffectiveDelta(MaterialModel m, Material mat) =>
        mat.Phase.BetaQuenched ? m.Quench.BetaDelta : mat.Phase.Delta;

    /// <summary>§4.1 HV = HV_base + (110 + 9·Sn_eff + 12·As)·sqrt(ε)</summary>
    public static double Hardness(MaterialModel m, Material mat, double strain)
    {
        var h = m.Hardness;
        double hvBase = h.Base.Evaluate(mat.Composition, m.EffectiveCaps)
                        + h.PerDelta * EffectiveDelta(m, mat)
                        + h.GrainNumerator / Math.Sqrt(Math.Max(1.0, mat.GrainSizeUm));
        if (mat.Phase.BetaQuenched) hvBase += h.BetaQuenchBonus;
        return hvBase + h.WorkHardening.Evaluate(mat.Composition, m.EffectiveCaps) * Math.Sqrt(Math.Max(0, strain));
    }

    /// <summary>§4.2 Toughness index, 0–100.</summary>
    public static double Toughness(MaterialModel m, Material mat, double hv, double crackSeverity, int foldCount, double burntSeverity)
    {
        var t = m.Toughness;
        double v = t.Base
                   - t.PerHv * (hv - t.HvRef)
                   - t.PerDelta * EffectiveDelta(m, mat)
                   - t.PerPorosity * mat.PorosityPct
                   - t.PerCrackSeverity * crackSeverity
                   - (mat.Phase.Cored ? t.CoredPenalty : 0)
                   - Math.Max(0, t.GrainPenaltyPerUm * (mat.GrainSizeUm - t.GrainPenaltyAboveUm))
                   - t.PerFold * foldCount
                   - t.PerBurntSeverity * burntSeverity;
        foreach (var (symbol, coef) in t.PerWt) v -= coef * mat.Composition[symbol];
        if (mat.Phase.SolutionQuenched) v += m.Quench.SolutionQuenchToughnessBonus;
        return Math.Clamp(v, 0, 100);
    }

    /// <summary>§4.3 clamp((HV − 40) / 2.6, 0, 100)</summary>
    public static double EdgeRetention(MaterialModel m, double hv) =>
        Math.Clamp((hv - m.EdgeRetention.HvOffset) / m.EdgeRetention.HvDivisor, 0, 100);

    /// <summary>§4.4 ε_max = 0.85 − 0.03·Sn_eff − 1.2·δ − Porosity/20 − 0.02·Pb</summary>
    public static double EpsMax(MaterialModel m, Material mat) =>
        m.Workability.EpsMax.Evaluate(mat.Composition, m.EffectiveCaps)
        - m.Workability.PerDelta * EffectiveDelta(m, mat)
        - mat.PorosityPct / m.Workability.PorosityDivisor;

    /// <summary>§4.5 clamp(40 + 2·Sn + 4·Pb + 0.2·SH_typical, 0, 100)</summary>
    public static double Castability(MaterialModel m, Composition c) =>
        Math.Clamp(m.Bulk.Castability.Evaluate(c, m.EffectiveCaps) + m.Bulk.CastabilityPerSuperheat * m.Bulk.CastabilityTypicalSuperheatC, 0, 100);

    /// <summary>§4.5 exp(−((Sn − 21.5)/2.5)²) · (1 − Porosity/5) · Homogeneity · (1 − CrackSeverity_max), clamped 0–1.</summary>
    public static double AcousticQuality(MaterialModel m, Material mat, double maxCrackSeverity)
    {
        var b = m.Bulk;
        double x = (mat.Composition[b.AcousticElement] - b.AcousticIdealWt) / b.AcousticWidthWt;
        double v = Math.Exp(-x * x) * (1 - mat.PorosityPct / b.AcousticPorosityDivisor) * mat.Homogeneity * (1 - maxCrackSeverity);
        return Math.Clamp(v, 0, 1);
    }

    /// <summary>§4.5 Color for the shader: interpolate the tin stops, then shift toward silver with arsenic.</summary>
    public static double[] Color(MaterialModel m, Composition c)
    {
        var col = m.Bulk.Color;
        double wt = c[col.Element];
        var stops = col.Stops;
        double[] rgb;
        if (wt <= stops[0].Wt) rgb = (double[])stops[0].Rgb.Clone();
        else if (wt >= stops[^1].Wt) rgb = (double[])stops[^1].Rgb.Clone();
        else
        {
            int i = 0;
            while (stops[i + 1].Wt < wt) i++;
            double f = (wt - stops[i].Wt) / (stops[i + 1].Wt - stops[i].Wt);
            rgb = [.. Enumerable.Range(0, 3).Select(k => stops[i].Rgb[k] + f * (stops[i + 1].Rgb[k] - stops[i].Rgb[k]))];
        }
        double s = Math.Clamp(c[col.SilverElement] * col.SilverPerWt, 0, 1);
        return [.. Enumerable.Range(0, 3).Select(k => rgb[k] + s * (col.SilverRgb[k] - rgb[k]))];
    }

    public static ZoneProperties DeriveZone(MaterialModel m, ItemInstance item, Zone zone)
    {
        var mat = item.RequireMaterial("Derive");
        double hv = Hardness(m, mat, zone.Strain);
        int folds = item.FoldCount(zone.Id);
        double t = Toughness(m, mat, hv, zone.CrackSeverity, folds, item.BurntSeverity);
        double epsMax = EpsMax(m, mat);
        return new ZoneProperties(zone.Id, zone.ThicknessMm, zone.Strain, hv, t, EdgeRetention(m, hv), epsMax,
            epsMax - zone.Strain, zone.ShapeError, zone.CrackSeverity, folds);
    }

    public static ItemProperties Derive(ForgeData data, ItemInstance item)
    {
        var m = data.Model;
        var mat = item.RequireMaterial("Derive");
        var c = mat.Composition;
        var template = data.Template(item.TemplateId);

        double maxCrack = item.Zones.Count == 0 ? 0 : item.Zones.Max(z => z.CrackSeverity);
        var wallIds = template.Zones.Where(z => z.IsWall).Select(z => z.Id).ToHashSet();
        bool leaks = mat.PorosityPct > m.Bulk.LeakPorosityAbove
                     || item.Zones.Any(z => wallIds.Contains(z.Id) && z.CrackSeverity > 0);

        var bulk = new BulkProperties(
            MeltingRanges.Liquidus(m, c),
            MeltingRanges.Solidus(m, c),
            MeltingRanges.Recrystallization(m, c),
            m.Bulk.Corrosion.Evaluate(c, m.EffectiveCaps),
            Castability(m, c),
            AcousticQuality(m, mat, maxCrack),
            leaks,
            Color(m, c));

        return new ItemProperties(bulk, item.Zones.Select(z => DeriveZone(m, item, z)).ToList());
    }
}
