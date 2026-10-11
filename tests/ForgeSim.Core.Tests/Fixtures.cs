using ForgeSim.Core.Items;
using ForgeSim.Core.Materials;
using ForgeSim.Core.Process;
using ForgeSim.Core.Rng;

namespace ForgeSim.Core.Tests;

/// <summary>Builds real items by running Melt + Cast, so process tests start from genuine as-cast state.</summary>
public static class Fixtures
{
    public static readonly Forge Forge = MeltTests.Forge;

    /// <summary>
    /// One pre-alloyed ingot melted 10 min at TL+100 under charcoal, stirred, then poured at TL+100 into a
    /// stone mold (150 °C preheat) with pour quality 0.9.
    /// </summary>
    public static ItemInstance Cast(double sn = 0, double @as = 0, double pb = 0, string template = "chisel_bronze",
        MoldType mold = MoldType.Stone, double preheat = 150, double pourQuality = 0.9, SimRng? rng = null)
    {
        rng ??= new SimRng(99);
        var item = Forge.NewItem(template);
        var charge = new Dictionary<string, double> { ["Sn"] = sn, ["As"] = @as, ["Pb"] = pb };
        double tl = MeltingRanges.Liquidus(TestData.Model, TestData.Comp(sn, @as, pb));
        Forge.Apply(item, new MeltStep
        {
            Charge = [new ChargeItem { Composition = charge, MassKg = 1.0 }],
            PeakTempC = tl + 100, MinutesAtTemp = 10, MinutesAboveLiquidus = 12, Cover = Cover.Charcoal, Stirred = true,
        }, rng);
        double tlMelt = MeltingRanges.Liquidus(TestData.Model, item.Material!.Composition);
        Forge.Apply(item, new CastStep { PourTempC = tlMelt + 100, MoldType = mold, MoldPreheatC = preheat, PourQuality = pourQuality }, rng);
        return item;
    }

    public static void Apply(ItemInstance item, ProcessStep step, SimRng? rng = null) =>
        Forge.Apply(item, step, rng ?? new SimRng(5));

    public static AnnealStep Anneal(double tempC, double minutes, Atmosphere atm = Atmosphere.Charcoal) =>
        new() { TempC = tempC, Minutes = minutes, Atmosphere = atm };

    public static StrikeStep Strike(string zone, double force, int repeat = 1, double tempC = 20, double accuracy = 1.0, double angle = 0) =>
        new() { ZoneId = zone, Force = force, Repeat = repeat, MetalTempC = tempC, Accuracy = accuracy, AngleOffDeg = angle };
}
