namespace ForgeSim.Core.Items;

/// <summary>§1 Zone: a region of the item worked separately (edge, body, socket, head…).</summary>
public sealed class Zone
{
    public required string Id { get; init; }
    public double ThicknessMm { get; set; }
    public double ThicknessAtLastAnnealMm { get; set; }

    /// <summary>
    /// ε, cold work since last anneal. The spec defines ε = ln(ThicknessAtLastAnneal / Thickness) for pure cold
    /// reduction; it is stored explicitly so hot strikes, scale and grinding (which thin the zone without cold-working
    /// it) and partial anneals (ε ← ε·(1 − X)) stay correct. See spec-gaps.
    /// </summary>
    public double Strain { get; set; }

    /// <summary>0–1, deviation from the template target.</summary>
    public double ShapeError { get; set; }

    /// <summary>0–1.</summary>
    public double CrackSeverity { get; set; }

    /// <summary>Included edge angle from the last Grind, if any.</summary>
    public double? EdgeAngleDeg { get; set; }
    public double? Finish { get; set; }

    public Zone Clone() => (Zone)MemberwiseClone();
}
