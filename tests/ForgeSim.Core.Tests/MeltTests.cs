using ForgeSim.Core.Items;
using ForgeSim.Core.Process;
using ForgeSim.Core.Rng;

namespace ForgeSim.Core.Tests;

/// <summary>§3.1 Melt.</summary>
public class MeltTests
{
    internal static readonly Forge Forge = new(TestData.Data);

    internal static MeltStep TinBronzeMelt(double snWt, double peak, double minutes, Cover cover,
        double minutesAboveLiquidus = 20, bool stirred = true, double sWt = 0) => new()
    {
        Charge =
        [
            // Copper ingot; any sulfur rides in it so the mixed charge has sWt% S.
            new ChargeItem { Composition = new() { ["S"] = sWt * 100 / (100 - snWt) }, MassKg = (100 - snWt) / 100 },
            new ChargeItem { Composition = new() { ["Sn"] = 100 }, MassKg = snWt / 100 },
        ],
        PeakTempC = peak,
        MinutesAtTemp = minutes,
        MinutesAboveLiquidus = minutesAboveLiquidus,
        Cover = cover,
        Stirred = stirred,
    };

    private static ItemInstance Melt(MeltStep step)
    {
        var item = Forge.NewItem("chisel_bronze");
        Forge.Apply(item, step, new SimRng(1));
        return item;
    }

    [Fact]
    public void Charge_mixes_by_mass()
    {
        var c = MeltProcess.MixCharge(Forge,
        [
            new ChargeItem { Composition = new() { ["Cu"] = 100 }, MassKg = 0.90 },
            new ChargeItem { Composition = new() { ["Sn"] = 100 }, MassKg = 0.105 },
        ]);
        Assert.Equal(0.105 / 1.005 * 100, c["Sn"], 9);
    }

    [Fact]
    public void Behavioral_1_tin_loss_uncovered()
    {
        var item = Melt(TinBronzeMelt(10, 1150, 20, Cover.None));
        TestData.InRange(item.Material!.Composition["Sn"], 9.3, 9.5, "10% Sn, 20 min @1150 uncovered → Sn wt%");
    }

    [Fact]
    public void Behavioral_1_tin_loss_charcoal_cover()
    {
        var item = Melt(TinBronzeMelt(10, 1150, 20, Cover.Charcoal));
        TestData.InRange(item.Material!.Composition["Sn"], 9.8, 9.9, "10% Sn, 20 min @1150 charcoal → Sn wt%");
    }

    [Fact]
    public void Spec_example_tin_loss_is_about_5_6_percent()
    {
        var item = Melt(TinBronzeMelt(10, 1150, 20, Cover.None));
        double lostFraction = 1 - item.Material!.Composition["Sn"] / 10.0;
        Assert.Equal(0.002 * 20 * Math.Exp(50.0 / 150), lostFraction, 9);
    }

    [Fact]
    public void Fails_below_liquidus()
    {
        var item = Melt(TinBronzeMelt(10, 980, 20, Cover.None)); // TL for 10% Sn is 995
        Assert.True(item.Failed);
        Assert.Null(item.Material);
    }

    [Fact]
    public void Gas_pickup_formula()
    {
        var item = Melt(TinBronzeMelt(10, 1200, 10, Cover.Charcoal));
        Assert.Equal(0.1 * 10 * Math.Exp(1.0) * 0.5, item.Material!.GasContent, 9);
    }

    [Fact]
    public void Sulfur_adds_twenty_gas_per_wt_percent_and_clamps()
    {
        var item = Melt(TinBronzeMelt(10, 1100, 0, Cover.None, sWt: 0.05));
        Assert.Equal(20 * 0.05, item.Material!.GasContent, 6);

        var dirty = Melt(TinBronzeMelt(10, 1100, 0, Cover.None, sWt: 1.0));
        Assert.Equal(10, dirty.Material!.GasContent, 9);
    }

    [Fact]
    public void Homogeneity_and_segregation()
    {
        var stirred = Melt(TinBronzeMelt(10, 1100, 10, Cover.Charcoal, minutesAboveLiquidus: 2, stirred: true));
        Assert.Equal(1 - Math.Exp(-1), stirred.Material!.Homogeneity, 9);
        var seg = stirred.FindDefect(DefectType.Segregation);
        Assert.NotNull(seg);
        Assert.False(seg!.Revealed);
        Assert.Equal(0.8 - (1 - Math.Exp(-1)), seg.Severity, 9);

        var good = Melt(TinBronzeMelt(10, 1100, 10, Cover.Charcoal, minutesAboveLiquidus: 12, stirred: true));
        Assert.Null(good.FindDefect(DefectType.Segregation));
    }

    [Fact]
    public void Zinc_and_arsenic_lost_faster_than_tin()
    {
        var step = new MeltStep
        {
            Charge = [new ChargeItem { Composition = new() { ["Sn"] = 5, ["As"] = 2, ["Zn"] = 2 }, MassKg = 1 }],
            PeakTempC = 1100, MinutesAtTemp = 10, MinutesAboveLiquidus = 10, Cover = Cover.None, Stirred = true,
        };
        var c = Melt(step).Material!.Composition;
        Assert.Equal(5 * (1 - 0.02), c["Sn"], 9);
        Assert.Equal(2 * (1 - 0.06), c["As"], 9);
        Assert.Equal(2 * (1 - 0.20), c["Zn"], 9);
    }
}
