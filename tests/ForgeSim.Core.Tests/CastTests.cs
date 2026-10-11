using ForgeSim.Core.Items;
using ForgeSim.Core.Materials;
using ForgeSim.Core.Process;
using ForgeSim.Core.Rng;

namespace ForgeSim.Core.Tests;

/// <summary>§3.2 Cast.</summary>
public class CastTests
{
    private static readonly Forge Forge = MeltTests.Forge;

    private static ItemInstance MeltAndCast(double sn, double pourAboveTl, MoldType mold = MoldType.Stone,
        double preheat = 0, double pourQuality = 1.0)
    {
        var item = Forge.NewItem("chisel_bronze");
        var rng = new SimRng(7);
        Forge.Apply(item, MeltTests.TinBronzeMelt(sn, 1100, 10, Cover.Charcoal), rng);
        double tl = MeltingRanges.Liquidus(TestData.Model, item.Material!.Composition);
        Forge.Apply(item, new CastStep { PourTempC = tl + pourAboveTl, MoldType = mold, MoldPreheatC = preheat, PourQuality = pourQuality }, rng);
        return item;
    }

    [Fact]
    public void Zones_get_template_start_thickness_and_no_strain()
    {
        var item = MeltAndCast(10, 100);
        Assert.Equal(["edge", "shank", "head"], item.Zones.Select(z => z.Id));
        var edge = item.Zone("edge");
        Assert.Equal(6.0, edge.ThicknessMm);
        Assert.Equal(6.0, edge.ThicknessAtLastAnnealMm);
        Assert.Equal(0, edge.Strain);
    }

    [Fact]
    public void Good_pour_has_no_misrun()
    {
        Assert.Null(MeltAndCast(10, 100).FindDefect(DefectType.Misrun));
    }

    [Fact]
    public void Low_superheat_misrun_is_hidden_between_15_and_40()
    {
        var d = MeltAndCast(10, 20).FindDefect(DefectType.Misrun);
        Assert.NotNull(d);
        Assert.Equal(0.5, d!.Severity, 9);
        Assert.False(d.Revealed);
    }

    [Fact]
    public void Very_low_superheat_misrun_is_visible()
    {
        var d = MeltAndCast(10, 10).FindDefect(DefectType.Misrun);
        Assert.NotNull(d);
        Assert.Equal(0.75, d!.Severity, 9);
        Assert.True(d.Revealed);
    }

    [Fact]
    public void Porosity_formula()
    {
        var item = MeltAndCast(10, 250, pourQuality: 0.8);
        double gas = item.Material!.GasContent;
        double expected = 0.5 * gas + 1.5 * (250 - 150) / 100.0 + 1.0 * 0.2;
        Assert.Equal(expected, item.Material.PorosityPct, 9);
    }

    [Fact]
    public void Porosity_defect_hidden_above_1_visible_above_3()
    {
        var low = MeltAndCast(10, 100, pourQuality: 0.9);
        Assert.True(low.Material!.PorosityPct <= 1.0, $"porosity {low.Material.PorosityPct}");
        Assert.Null(low.FindDefect(DefectType.Porosity));

        var mid = MeltAndCast(10, 100, pourQuality: 0.0); // 0.25 gas-porosity + 1.0 from bad pour
        Assert.InRange(mid.Material!.PorosityPct, 1.0, 3.0);
        Assert.False(mid.FindDefect(DefectType.Porosity)!.Revealed);

        var high = MeltAndCast(10, 300, pourQuality: 0.0);
        Assert.True(high.Material!.PorosityPct > 3.0);
        Assert.True(high.FindDefect(DefectType.Porosity)!.Revealed);
    }

    [Theory]
    [InlineData(MoldType.Stone, 0, 40.0)]
    [InlineData(MoldType.Sand, 0, 63.245553)]
    [InlineData(MoldType.Bronze, 0, 32.659863)]
    [InlineData(MoldType.Stone, 150, 43.386092)]
    public void Grain_size_from_mold_cooling(MoldType mold, double preheat, double expectedUm)
    {
        Assert.Equal(expectedUm, MeltAndCast(10, 100, mold, preheat).Material!.GrainSizeUm, 5);
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(10, 0.045)]
    [InlineData(22, 0.225)]
    [InlineData(40, 0.35)]
    public void As_cast_delta(double sn, double expected)
    {
        Assert.Equal(expected, CastProcess.AsCastDelta(TestData.Model, TestData.Comp(sn: sn)), 9);
    }

    [Fact]
    public void As_cast_is_cored()
    {
        Assert.True(MeltAndCast(10, 100).Material!.Phase.Cored);
    }
}
