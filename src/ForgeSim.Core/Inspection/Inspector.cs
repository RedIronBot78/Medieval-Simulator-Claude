using ForgeSim.Core.Data;
using ForgeSim.Core.Items;
using ForgeSim.Core.Properties;

namespace ForgeSim.Core.Inspection;

public enum InspectMethod
{
    /// <summary>Good light: cracks ≥ 0.15, laps, surface porosity, burnt.</summary>
    Visual,
    /// <summary>Tap and listen: cracks ≥ 0.05, porosity ≥ 1% (dull ring).</summary>
    Ring,
    /// <summary>Approximate hardness band per zone. Reveals no defects.</summary>
    File,
}

public sealed record InspectionResult(InspectMethod Method, IReadOnlyList<Defect> Revealed, IReadOnlyList<string> Findings);

/// <summary>
/// §3.7 Inspect. Not a process step: it never changes the metal, only what the player knows. Each inspection
/// is recorded on the item with its position in the history so a replay reveals the same defects.
/// "Use" (the fourth method in the spec) is <see cref="Performance.UseSimulator"/>: defects reveal themselves by failing.
/// </summary>
public static class Inspector
{
    public static InspectionResult Inspect(ForgeData data, ItemInstance item, InspectMethod method)
    {
        var c = data.Model.Inspect;
        var mat = item.RequireMaterial("Inspect");
        item.Inspections.Add(new InspectionRecord(item.History.Count, method));

        var revealed = new List<Defect>();
        var findings = new List<string>();
        void Reveal(Defect d, string by)
        {
            if (d.Revealed) return;
            d.Reveal(by);
            revealed.Add(d);
        }

        switch (method)
        {
            case InspectMethod.Visual:
                foreach (var d in item.Defects)
                {
                    bool seen = d.Type switch
                    {
                        DefectType.Crack => d.Severity >= c.VisualCrackMin,
                        DefectType.Fold => true,
                        DefectType.Porosity => d.Severity > data.Model.Cast.PorositySurfaceAbove,
                        DefectType.Burnt => true,
                        _ => false,
                    };
                    if (seen) Reveal(d, "visual inspection");
                }
                findings.Add(revealed.Count == 0 ? "Nothing new visible." : $"Spotted {revealed.Count} new flaw(s).");
                break;

            case InspectMethod.Ring:
                foreach (var d in item.Defects)
                {
                    bool heard = d.Type switch
                    {
                        DefectType.Crack => d.Severity >= c.RingCrackMin,
                        DefectType.Porosity => mat.PorosityPct >= c.RingPorosityMin,
                        _ => false,
                    };
                    if (heard) Reveal(d, "ring test");
                }
                bool dull = item.Zones.Any(z => z.CrackSeverity >= c.RingCrackMin) || mat.PorosityPct >= c.RingPorosityMin;
                findings.Add(dull ? "Dull ring." : "Clear ring.");
                break;

            case InspectMethod.File:
                foreach (var z in item.Zones)
                {
                    double hv = PropertyModel.Hardness(data.Model, mat, z.Strain);
                    findings.Add($"{z.Id}: {HardnessBand(data.Model, hv)}");
                }
                break;
        }

        return new InspectionResult(method, revealed, findings);
    }

    public static string HardnessBand(MaterialModel m, double hv)
    {
        foreach (var band in m.Inspect.FileBands)
            if (band.BelowHv is not { } limit || hv < limit) return band.Label;
        return m.Inspect.FileBands[^1].Label;
    }
}
