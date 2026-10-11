namespace ForgeSim.Core.Materials;

/// <summary>§1 Material: bulk state of the metal. Zone-level state (thickness, strain, cracks) lives on <see cref="Items.Zone"/>.</summary>
public sealed class Material
{
    public required Composition Composition { get; set; }
    public PhaseState Phase { get; set; } = new();
    public double GrainSizeUm { get; set; }
    /// <summary>%, 0–10</summary>
    public double PorosityPct { get; set; }
    /// <summary>Index, 0–10. Dissolved gas picked up during melting.</summary>
    public double GasContent { get; set; }
    /// <summary>0–1. How well the alloy mixed.</summary>
    public double Homogeneity { get; set; } = 1.0;

    public Material Clone() => new()
    {
        Composition = Composition,
        Phase = Phase.Clone(),
        GrainSizeUm = GrainSizeUm,
        PorosityPct = PorosityPct,
        GasContent = GasContent,
        Homogeneity = Homogeneity,
    };
}

public sealed class PhaseState
{
    /// <summary>δ phase fraction, 0–1.</summary>
    public double Delta { get; set; }
    public bool Cored { get; set; }
    public bool BetaQuenched { get; set; }
    /// <summary>Quenched from at or above T_rx (§3.5 "small toughness gain"). See spec-gaps.</summary>
    public bool SolutionQuenched { get; set; }

    public PhaseState Clone() => (PhaseState)MemberwiseClone();
}
