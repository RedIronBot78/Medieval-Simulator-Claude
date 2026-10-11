namespace ForgeSim.Core.Items;

/// <summary>§6 Item template, from data/items/*.json.</summary>
public sealed class ItemTemplate
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    /// <summary>Typical process path, for humans.</summary>
    public string TypicalPath { get; init; } = "";
    public List<ZoneTemplate> Zones { get; init; } = new();
    public List<Requirement> Requirements { get; init; } = new();
}

public sealed class ZoneTemplate
{
    public required string Id { get; init; }
    public double StartThicknessMm { get; init; }
    public double TargetThicknessMm { get; init; }
    /// <summary>Vessel wall: any crack here makes the item leak (§4.5).</summary>
    public bool IsWall { get; init; }
}

/// <summary>One fitness requirement. <see cref="Zone"/> null means a bulk property.</summary>
public sealed class Requirement
{
    public string? Zone { get; init; }
    public required string Property { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public double Weight { get; init; } = 1.0;
}
