namespace ForgeSim.Core.Data;

/// <summary>One element, from data/elements/*.json.</summary>
public sealed class ElementDef
{
    public required string Symbol { get; init; }
    public required string Name { get; init; }
    public string Role { get; init; } = "";
    public string Notes { get; init; } = "";

    /// <summary>Fraction of this element lost per minute in the melt at the reference temperature (§3.1).</summary>
    public double OxidationLossPerMinute { get; init; }

    /// <summary>Gas content added per wt% of this element in the charge (§3.1, sulfur's 20·S).</summary>
    public double GasPickupPerWt { get; init; }
}
