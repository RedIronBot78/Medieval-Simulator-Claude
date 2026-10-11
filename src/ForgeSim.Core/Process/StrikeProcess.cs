using ForgeSim.Core.Items;
using ForgeSim.Core.Materials;
using ForgeSim.Core.Properties;
using ForgeSim.Core.Rng;

namespace ForgeSim.Core.Process;

/// <summary>§3.3 Strike (hammer work) and §5 hot-shortness.</summary>
public static class StrikeProcess
{
    public static void Apply(Forge forge, ItemInstance item, StrikeStep step, SimRng rng, List<string> log)
    {
        var m = forge.Model;
        var mat = item.RequireMaterial("Strike");
        var zone = item.Zone(step.ZoneId);
        double trx = MeltingRanges.Recrystallization(m, mat.Composition);
        bool hot = step.MetalTempC >= trx;

        double t0 = zone.ThicknessMm, e0 = zone.Strain, c0 = zone.CrackSeverity;
        double hv0 = PropertyModel.Hardness(m, mat, zone.Strain);
        int folds0 = item.FoldCount(zone.Id);
        int cracks = 0;

        for (int i = 0; i < Math.Max(1, step.Repeat); i++)
            if (StrikeOnce(forge, item, zone, step, hot, rng)) cracks++;

        double hv1 = PropertyModel.Hardness(m, mat, zone.Strain);
        log.Add($"Strike {zone.Id} ×{Math.Max(1, step.Repeat)} force {step.Force:0.00} acc {step.Accuracy:0.00} at {step.MetalTempC:0} °C ({(hot ? "hot" : "cold")}): " +
                $"{t0:0.00} → {zone.ThicknessMm:0.00} mm, ε {e0:0.000} → {zone.Strain:0.000}, HV {hv0:0} → {hv1:0}");
        if (cracks > 0) log.Add($"  {cracks} crack event(s): crack severity {c0:0.00} → {zone.CrackSeverity:0.00}");
        int newFolds = item.FoldCount(zone.Id) - folds0;
        if (newFolds > 0) log.Add($"  {newFolds} fold(s)/lap(s)");
    }

    /// <summary>One blow. Returns true if it cracked the zone. Always consumes exactly four RNG draws.</summary>
    private static bool StrikeOnce(Forge forge, ItemInstance item, Zone zone, StrikeStep step, bool hot, SimRng rng)
    {
        var m = forge.Model;
        var s = m.Strike;
        var mat = item.Material!;

        double crackRoll = rng.NextDouble();
        double foldRoll = rng.NextDouble();
        double hotTinRoll = rng.NextDouble();
        double hotLeadRoll = rng.NextDouble();

        // Thinning: harder metal moves less per blow.
        double hv = PropertyModel.Hardness(m, mat, zone.Strain);
        double reduction = Math.Clamp(s.ReductionPerBlow * step.Force * step.Accuracy * (s.HvRef / hv), 0, 0.99);
        double before = zone.ThicknessMm;
        zone.ThicknessMm *= 1 - reduction;

        bool cracked = false;
        if (!hot)
        {
            zone.Strain += Math.Log(before / zone.ThicknessMm);

            double epsMax = PropertyModel.EpsMax(m, mat);
            if (zone.Strain > epsMax)
            {
                double p = Math.Clamp((zone.Strain - epsMax) / s.CrackStrainWindow, 0, 1) * step.Force;
                if (crackRoll < p) cracked = true;
            }
        }

        if (HotShortCrack(forge, mat, step, hotTinRoll, hotLeadRoll)) cracked = true;

        if (cracked) AddCrack(forge, item, zone, s.CrackSeverityBase + s.CrackSeverityPerForce * step.Force, "visible crack");

        // Folds / laps from glancing blows.
        if (step.AngleOffDeg > s.FoldAngleAboveDeg && foldRoll < s.FoldChancePer45Deg * step.AngleOffDeg / 45)
            item.Defects.Add(new Defect { Type = DefectType.Fold, ZoneId = zone.Id, Severity = 1 });

        // Shape error from off-target blows.
        if (step.Accuracy < s.ShapeAccuracyBelow)
            zone.ShapeError = Math.Min(1, zone.ShapeError + s.ShapeErrorPerInaccuracy * (1 - step.Accuracy));

        return cracked;
    }

    /// <summary>§5 Hot-shortness checks for one strike.</summary>
    private static bool HotShortCrack(Forge forge, Materials.Material mat, StrikeStep step, double tinRoll, double leadRoll)
    {
        var h = forge.Model.HotShortness;
        double trx = MeltingRanges.Recrystallization(forge.Model, mat.Composition);
        bool cracked = false;

        // Tin bronze above T_rx: intergranular cracking. β-quenched alloys may be hot-worked in their window.
        double sn = mat.Composition[h.TinElement];
        bool betaWindow = mat.Phase.BetaQuenched && step.MetalTempC >= h.BetaHotWorkMinC && step.MetalTempC <= h.BetaHotWorkMaxC;
        if (step.MetalTempC > trx && sn > h.TinAboveWt && !betaWindow)
            cracked |= tinRoll < h.TinCrackChancePerWt * (sn - h.TinAboveWt);

        // Lead is liquid between the grains above 326 °C.
        double pb = mat.Composition[h.LeadElement];
        if (step.MetalTempC > forge.Model.Melting.LeadMeltC && pb > h.LeadAboveWt)
            cracked |= leadRoll < h.LeadCrackChancePerWt * pb;

        return cracked;
    }

    /// <summary>Grows a zone's crack and keeps its Crack defect in sync. Severity ≥ the visible threshold reveals it.</summary>
    public static void AddCrack(Forge forge, ItemInstance item, Zone zone, double severity, string revealedBy)
    {
        zone.CrackSeverity = Math.Min(1, zone.CrackSeverity + severity);
        var d = item.GetOrAddDefect(DefectType.Crack, zone.Id);
        d.Severity = zone.CrackSeverity;
        if (zone.CrackSeverity >= forge.Model.Strike.CrackHiddenBelow) d.Reveal(revealedBy);
    }
}
