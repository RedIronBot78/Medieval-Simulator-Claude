namespace ForgeSim.Core.Items;

public enum DefectType
{
    Crack,
    Fold,
    Porosity,
    Misrun,
    Segregation,
    Burnt,
    LeadSweat,
}

/// <summary>A flaw tracked by the sim. Hidden until <see cref="Revealed"/> (inspection, testing or failure in use).</summary>
public sealed class Defect
{
    public required DefectType Type { get; init; }
    /// <summary>Null for bulk defects (porosity, segregation, burnt…).</summary>
    public string? ZoneId { get; init; }
    public double Severity { get; set; }
    public bool Revealed { get; set; }
    /// <summary>How it was found, once revealed (e.g. "visible on casting", "ring test").</summary>
    public string? RevealedBy { get; set; }

    public void Reveal(string by)
    {
        if (Revealed) return;
        Revealed = true;
        RevealedBy = by;
    }

    public Defect Clone() => (Defect)MemberwiseClone();
}
