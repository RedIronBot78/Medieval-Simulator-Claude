namespace ForgeSim.Core.Data;

// Mirrors data/model/<tier>.json. Section and field names follow the spec section they come from.
// Sections are `required` so a missing block fails at load time instead of as a NullReferenceException mid-sim.

public sealed class MaterialModel
{
    public required string Id { get; init; }
    public string? Description { get; init; }
    public required string BaseElement { get; init; }
    public Dictionary<string, double> EffectiveCaps { get; init; } = new();

    public required MeltingConstants Melting { get; init; }
    public required MeltConstants Melt { get; init; }
    public required CastConstants Cast { get; init; }
    public required StrikeConstants Strike { get; init; }
    public required HotShortnessConstants HotShortness { get; init; }
    public required AnnealConstants Anneal { get; init; }
    public required QuenchConstants Quench { get; init; }
    public required GrindConstants Grind { get; init; }
    public required HardnessConstants Hardness { get; init; }
    public required ToughnessConstants Toughness { get; init; }
    public required WorkabilityConstants Workability { get; init; }
    public required EdgeRetentionConstants EdgeRetention { get; init; }
    public required BulkConstants Bulk { get; init; }
    public required InspectConstants Inspect { get; init; }
    public required UseConstants Use { get; init; }
}

/// <summary>§2</summary>
public sealed class MeltingConstants
{
    public required LinearTerm Liquidus { get; init; }
    public required LinearTerm Solidus { get; init; }
    public required LinearTerm Recrystallization { get; init; }
    public required string LeadElement { get; init; }
    public double LeadMeltC { get; init; }
}

/// <summary>§3.1</summary>
public sealed class MeltConstants
{
    public double OxidationRefTempC { get; init; }
    public double OxidationTempScaleC { get; init; }
    public double CharcoalCoverOxidationFactor { get; init; }
    public double GasPerMinute { get; init; }
    public double GasRefTempC { get; init; }
    public double GasTempScaleC { get; init; }
    public double CharcoalCoverGasFactor { get; init; }
    public double GasMax { get; init; }
    public double HomogenizationTauStirredMin { get; init; }
    public double HomogenizationTauUnstirredMin { get; init; }
    public double SegregationBelow { get; init; }
}

/// <summary>§3.2</summary>
public sealed class CastConstants
{
    public double MisrunSuperheatC { get; init; }
    public double MisrunVisibleBelowSuperheatC { get; init; }
    public double PorosityPerGas { get; init; }
    public double PorosityExcessSuperheatAboveC { get; init; }
    public double PorosityPer100CExcessSuperheat { get; init; }
    public double PorosityPerPourError { get; init; }
    public double PorosityMax { get; init; }
    public double PorosityDefectAbove { get; init; }
    public double PorositySurfaceAbove { get; init; }
    public Dictionary<string, double> MoldCoolingFactor { get; init; } = new();
    public double MoldPreheatDivisorC { get; init; }
    public double GrainSizeNumeratorUm { get; init; }
    public required string DeltaElement { get; init; }
    public double DeltaStartWt { get; init; }
    public double DeltaPerWt { get; init; }
    public double DeltaMax { get; init; }
}

/// <summary>§3.3</summary>
public sealed class StrikeConstants
{
    public double HvRef { get; init; }
    public double ReductionPerBlow { get; init; }
    public double CrackStrainWindow { get; init; }
    public double CrackSeverityBase { get; init; }
    public double CrackSeverityPerForce { get; init; }
    public double CrackHiddenBelow { get; init; }
    public double FoldAngleAboveDeg { get; init; }
    public double FoldChancePer45Deg { get; init; }
    public double ShapeAccuracyBelow { get; init; }
    public double ShapeErrorPerInaccuracy { get; init; }
}

/// <summary>§5</summary>
public sealed class HotShortnessConstants
{
    public required string TinElement { get; init; }
    public double TinAboveWt { get; init; }
    public double TinCrackChancePerWt { get; init; }
    public required string LeadElement { get; init; }
    public double LeadAboveWt { get; init; }
    public double LeadCrackChancePerWt { get; init; }
    public double BetaHotWorkMinC { get; init; }
    public double BetaHotWorkMaxC { get; init; }
}

/// <summary>§3.4</summary>
public sealed class AnnealConstants
{
    public double RxTauMin { get; init; }
    public double RxTempScaleC { get; init; }
    public double FullRecrystallizationAbove { get; init; }
    public double GrainGrowthPerMin { get; init; }
    public double GrainGrowthTempScaleC { get; init; }
    public double HomogenizationTauMin { get; init; }
    public double HomogenizationRefTempC { get; init; }
    public double HomogenizationTempScaleC { get; init; }
    public double CoringRemovedAbove { get; init; }
    public required string DeltaEqElement { get; init; }
    public double DeltaEqStartWt { get; init; }
    public double DeltaEqPerWt { get; init; }
    public double DeltaEqMax { get; init; }
    public double BurntTempScaleC { get; init; }
    public double BurntRevealedAbove { get; init; }
    public required string LeadSweatElement { get; init; }
    public double LeadSweatAboveWt { get; init; }
    public double LeadSweatAboveC { get; init; }
    public double LeadSweatLossWtPerHour { get; init; }
    public double ScaleMmPerMin { get; init; }
    public double ScaleRefTempC { get; init; }
    public double ScaleTempScaleC { get; init; }
}

/// <summary>§3.5</summary>
public sealed class QuenchConstants
{
    public required string ThermalShockMedium { get; init; }
    public double ThermalShockDeltaAbove { get; init; }
    public double ThermalShockChancePerDelta { get; init; }
    public double ThermalShockCrackSeverity { get; init; }
    public double SolutionQuenchToughnessBonus { get; init; }
    public required string BetaElement { get; init; }
    public double BetaMinWt { get; init; }
    public double BetaMaxWt { get; init; }
    public double BetaMinC { get; init; }
    public double BetaMaxC { get; init; }
    public required string BetaMedium { get; init; }
    public double BetaDelta { get; init; }
}

/// <summary>§3.6</summary>
public sealed class GrindConstants
{
    public double RemovalMm { get; init; }
}

/// <summary>§4.1</summary>
public sealed class HardnessConstants
{
    public required LinearTerm Base { get; init; }
    public double PerDelta { get; init; }
    public double GrainNumerator { get; init; }
    public required LinearTerm WorkHardening { get; init; }
    public double BetaQuenchBonus { get; init; }
}

/// <summary>§4.2</summary>
public sealed class ToughnessConstants
{
    public double Base { get; init; }
    public double PerHv { get; init; }
    public double HvRef { get; init; }
    public double PerDelta { get; init; }
    public double PerPorosity { get; init; }
    public double PerCrackSeverity { get; init; }
    public Dictionary<string, double> PerWt { get; init; } = new();
    public double CoredPenalty { get; init; }
    public double GrainPenaltyAboveUm { get; init; }
    public double GrainPenaltyPerUm { get; init; }
    public double PerFold { get; init; }
    public double PerBurntSeverity { get; init; }
}

/// <summary>§4.4</summary>
public sealed class WorkabilityConstants
{
    public required LinearTerm EpsMax { get; init; }
    public double PerDelta { get; init; }
    public double PorosityDivisor { get; init; }
}

/// <summary>§4.3</summary>
public sealed class EdgeRetentionConstants
{
    public double HvOffset { get; init; }
    public double HvDivisor { get; init; }
}

/// <summary>§4.5</summary>
public sealed class BulkConstants
{
    public required LinearTerm Corrosion { get; init; }
    public required LinearTerm Castability { get; init; }
    public double CastabilityPerSuperheat { get; init; }
    public double CastabilityTypicalSuperheatC { get; init; }
    public required string AcousticElement { get; init; }
    public double AcousticIdealWt { get; init; }
    public double AcousticWidthWt { get; init; }
    public double AcousticPorosityDivisor { get; init; }
    public double LeakPorosityAbove { get; init; }
    public required ColorConstants Color { get; init; }
}

public sealed class ColorConstants
{
    public required string Element { get; init; }
    public List<ColorStop> Stops { get; init; } = new();
    public required string SilverElement { get; init; }
    public double[] SilverRgb { get; init; } = [0.8, 0.8, 0.8];
    public double SilverPerWt { get; init; }
}

public sealed class ColorStop
{
    public double Wt { get; init; }
    public double[] Rgb { get; init; } = [0, 0, 0];
}

/// <summary>§3.7</summary>
public sealed class InspectConstants
{
    public double VisualCrackMin { get; init; }
    public double RingCrackMin { get; init; }
    public double RingPorosityMin { get; init; }
    public List<FileBand> FileBands { get; init; } = new();
}

public sealed class FileBand
{
    public required string Label { get; init; }
    /// <summary>Upper bound (exclusive). Null for the last, open-ended band.</summary>
    public double? BelowHv { get; init; }
}

/// <summary>§7</summary>
public sealed class UseConstants
{
    public double WearPerUse { get; init; }
    public double DullThreshold { get; init; }
    public double BendHvRef { get; init; }
    public double BendDivisor { get; init; }
    public double ChipToughnessRef { get; init; }
    public double ChipDivisor { get; init; }
    public double CrackGrowthPerImpact { get; init; }
    public double BreakToughnessRef { get; init; }
    public double BreakDivisor { get; init; }
    public double BreakPorosityDivisor { get; init; }
}
