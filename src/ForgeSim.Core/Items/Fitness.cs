using ForgeSim.Core.Data;
using ForgeSim.Core.Properties;

namespace ForgeSim.Core.Items;

public sealed record RequirementResult(Requirement Requirement, double Value, bool Met);

/// <summary>Score = Σ weight of met requirements / Σ weight, 0–1.</summary>
public sealed record FitnessReport(string TemplateId, IReadOnlyList<RequirementResult> Results, double Score);

/// <summary>§6 Fitness of an item for its template's job.</summary>
public static class Fitness
{
    public static readonly IReadOnlyList<string> ZoneProperties =
        ["HV", "Toughness", "EdgeRetention", "ShapeError", "CrackSeverity", "Strain", "EpsMax", "RemainingWorkability", "ThicknessMm"];

    public static readonly IReadOnlyList<string> BulkProperties =
        ["CorrosionResistance", "Castability", "AcousticQuality", "Leaks", "Porosity"];

    public static FitnessReport Evaluate(ForgeData data, ItemInstance item)
    {
        var template = data.Template(item.TemplateId);
        var props = PropertyModel.Derive(data, item);
        var results = template.Requirements.Select(r =>
        {
            double v = Value(props, item, r);
            bool met = (r.Min is not { } lo || v >= lo) && (r.Max is not { } hi || v <= hi);
            return new RequirementResult(r, v, met);
        }).ToList();
        double total = results.Sum(r => r.Requirement.Weight);
        double score = total <= 0 ? 0 : results.Where(r => r.Met).Sum(r => r.Requirement.Weight) / total;
        return new FitnessReport(template.Id, results, score);
    }

    public static double Value(ItemProperties props, ItemInstance item, Requirement r)
    {
        if (r.Zone == null)
            return r.Property switch
            {
                "CorrosionResistance" => props.Bulk.CorrosionResistance,
                "Castability" => props.Bulk.Castability,
                "AcousticQuality" => props.Bulk.AcousticQuality,
                "Leaks" => props.Bulk.Leaks ? 1 : 0,
                "Porosity" => item.Material!.PorosityPct,
                _ => throw new InvalidDataException($"Unknown bulk property '{r.Property}'. Known: {string.Join(", ", BulkProperties)}."),
            };

        var z = props.Zone(r.Zone);
        return r.Property switch
        {
            "HV" => z.Hardness,
            "Toughness" => z.Toughness,
            "EdgeRetention" => z.EdgeRetention,
            "ShapeError" => z.ShapeError,
            "CrackSeverity" => z.CrackSeverity,
            "Strain" => z.Strain,
            "EpsMax" => z.EpsMax,
            "RemainingWorkability" => z.RemainingWorkability,
            "ThicknessMm" => z.ThicknessMm,
            _ => throw new InvalidDataException($"Unknown zone property '{r.Property}'. Known: {string.Join(", ", ZoneProperties)}."),
        };
    }

    /// <summary>Load-time check that every requirement names a real zone and property.</summary>
    public static void Validate(ItemTemplate t)
    {
        var zones = t.Zones.Select(z => z.Id).ToHashSet();
        foreach (var r in t.Requirements)
        {
            if (r.Zone != null && !zones.Contains(r.Zone))
                throw new InvalidDataException($"Template {t.Id}: requirement on unknown zone '{r.Zone}'.");
            var known = r.Zone == null ? BulkProperties : ZoneProperties;
            if (!known.Contains(r.Property))
                throw new InvalidDataException($"Template {t.Id}: unknown {(r.Zone == null ? "bulk" : "zone")} property '{r.Property}'.");
            if (r.Min == null && r.Max == null)
                throw new InvalidDataException($"Template {t.Id}: requirement {r.Property} has neither min nor max.");
        }
    }
}
