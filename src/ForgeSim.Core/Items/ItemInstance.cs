using ForgeSim.Core.Materials;
using ForgeSim.Core.Process;

namespace ForgeSim.Core.Items;

/// <summary>
/// §1 ItemInstance. <see cref="History"/> is the source of truth; everything else is the cached result of
/// replaying it with the item's seed.
/// </summary>
public sealed class ItemInstance
{
    public required string TemplateId { get; init; }
    /// <summary>Null until a Melt succeeds.</summary>
    public Material? Material { get; set; }
    /// <summary>Empty until Cast.</summary>
    public List<Zone> Zones { get; init; } = new();
    public List<ProcessStep> History { get; init; } = new();
    public List<Defect> Defects { get; init; } = new();
    /// <summary>Inspections performed, in order. They reveal defects but never change the metal.</summary>
    public List<InspectionRecord> Inspections { get; init; } = new();

    /// <summary>Set when a step makes further processing impossible (e.g. charge never fully melted).</summary>
    public string? Failure { get; set; }
    public bool Failed => Failure != null;

    public Zone Zone(string id) =>
        Zones.FirstOrDefault(z => z.Id == id) ?? throw new ArgumentException($"Item {TemplateId} has no zone '{id}'.");

    public Material RequireMaterial(string step) =>
        Material ?? throw new InvalidOperationException($"{step} needs molten or cast metal; run Melt first.");

    public Defect? FindDefect(DefectType type, string? zoneId = null) =>
        Defects.FirstOrDefault(d => d.Type == type && d.ZoneId == zoneId);

    /// <summary>Returns the single defect of this type/zone, creating it (hidden, severity 0) if absent.</summary>
    public Defect GetOrAddDefect(DefectType type, string? zoneId = null)
    {
        var d = FindDefect(type, zoneId);
        if (d != null) return d;
        d = new Defect { Type = type, ZoneId = zoneId };
        Defects.Add(d);
        return d;
    }

    public int FoldCount(string zoneId) => Defects.Count(d => d.Type == DefectType.Fold && d.ZoneId == zoneId);

    public double BurntSeverity => Defects.Where(d => d.Type == DefectType.Burnt).Sum(d => d.Severity);

    public ItemInstance Clone() => new()
    {
        TemplateId = TemplateId,
        Material = Material?.Clone(),
        Zones = Zones.Select(z => z.Clone()).ToList(),
        History = new List<ProcessStep>(History),
        Defects = Defects.Select(d => d.Clone()).ToList(),
        Inspections = new List<InspectionRecord>(Inspections),
        Failure = Failure,
    };
}

/// <summary>An inspection performed after <paramref name="AfterStep"/> history steps.</summary>
public sealed record InspectionRecord(int AfterStep, Inspection.InspectMethod Method);
