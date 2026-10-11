using System.Text.Json.Serialization;

namespace ForgeSim.Core.Process;

/// <summary>
/// §3 A process step: the only thing a minigame produces and the only thing that changes an item.
/// Serialized with a leading "type" field (see <see cref="ProcessStepConverter"/>).
/// </summary>
public abstract class ProcessStep
{
    [JsonPropertyOrder(-1)]
    public abstract string Type { get; }
}

public enum Cover { None, Charcoal }
public enum MoldType { Stone, Clay, Sand, Bronze }
public enum Atmosphere { Air, Charcoal }
public enum QuenchMedium { Air, Water, Oil }

public sealed class ChargeItem
{
    /// <summary>wt% by element. The base element is the balance if omitted.</summary>
    public Dictionary<string, double> Composition { get; init; } = new();
    public double MassKg { get; init; }
}

/// <summary>§3.1 Crucible melt.</summary>
public sealed class MeltStep : ProcessStep
{
    public override string Type => "Melt";
    public List<ChargeItem> Charge { get; init; } = new();
    public double PeakTempC { get; init; }
    public double MinutesAtTemp { get; init; }
    public double MinutesAboveLiquidus { get; init; }
    public Cover Cover { get; init; }
    public bool Stirred { get; init; }
}

/// <summary>§3.2 Pour into a mold.</summary>
public sealed class CastStep : ProcessStep
{
    public override string Type => "Cast";
    public double PourTempC { get; init; }
    public MoldType MoldType { get; init; }
    public double MoldPreheatC { get; init; }
    /// <summary>0–1 from the pour minigame.</summary>
    public double PourQuality { get; init; }
}

/// <summary>§3.3 Hammer work. <see cref="Repeat"/> is a test-recipe convenience; the game emits single strikes.</summary>
public sealed class StrikeStep : ProcessStep
{
    public override string Type => "Strike";
    [JsonPropertyName("zone")]
    public required string ZoneId { get; init; }
    /// <summary>0–1</summary>
    public double Force { get; init; }
    /// <summary>0–1, on target.</summary>
    public double Accuracy { get; init; } = 1.0;
    public double AngleOffDeg { get; init; }
    public double MetalTempC { get; init; }
    public int Repeat { get; init; } = 1;
}

/// <summary>§3.4</summary>
public sealed class AnnealStep : ProcessStep
{
    public override string Type => "Anneal";
    public double TempC { get; init; }
    public double Minutes { get; init; }
    public Atmosphere Atmosphere { get; init; }
}

/// <summary>§3.5</summary>
public sealed class QuenchStep : ProcessStep
{
    public override string Type => "Quench";
    public double FromTempC { get; init; }
    public QuenchMedium Medium { get; init; }
}

/// <summary>§3.6</summary>
public sealed class GrindStep : ProcessStep
{
    public override string Type => "Grind";
    [JsonPropertyName("zone")]
    public required string ZoneId { get; init; }
    /// <summary>Included angle.</summary>
    public double EdgeAngleDeg { get; init; }
    /// <summary>0–1</summary>
    public double Finish { get; init; }
}
