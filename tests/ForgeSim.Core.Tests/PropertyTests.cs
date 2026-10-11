using ForgeSim.Core.Items;
using ForgeSim.Core.Materials;
using ForgeSim.Core.Properties;

namespace ForgeSim.Core.Tests;

/// <summary>§4 formulas, term by term.</summary>
public class PropertyTests
{
    private static Material Mat(Composition c, double delta = 0, bool cored = false, double grain = 36, double porosity = 0,
        bool beta = false, bool solutionQuenched = false) => new()
    {
        Composition = c,
        GrainSizeUm = grain,
        PorosityPct = porosity,
        Phase = new PhaseState { Delta = delta, Cored = cored, BetaQuenched = beta, SolutionQuenched = solutionQuenched },
    };

    [Fact]
    public void Hardness_exact()
    {
        // grain 36 → 60/6 = 10
        var mat = Mat(TestData.Comp(sn: 10, @as: 1, zn: 2, fe: 1, pb: 2), delta: 0.1);
        double hvBase = 42 + 45 + 9 + 3 + 3 - 3 + 25 + 10;
        Assert.Equal(hvBase, PropertyModel.Hardness(TestData.Model, mat, 0), 9);
        Assert.Equal(hvBase + (110 + 90 + 12) * 0.5, PropertyModel.Hardness(TestData.Model, mat, 0.25), 9);
    }

    [Fact]
    public void Hardness_caps_tin_at_14()
    {
        var a = PropertyModel.Hardness(TestData.Model, Mat(TestData.Comp(sn: 14)), 0.25);
        var b = PropertyModel.Hardness(TestData.Model, Mat(TestData.Comp(sn: 20)), 0.25);
        Assert.Equal(a, b, 9);
    }

    [Fact]
    public void Beta_quench_uses_delta_005_and_adds_40()
    {
        var mat = Mat(TestData.Comp(sn: 22), delta: 0.4, beta: true);
        Assert.Equal(42 + 63 + 250 * 0.05 + 10 + 40, PropertyModel.Hardness(TestData.Model, mat, 0), 9);
    }

    [Fact]
    public void Toughness_exact_with_every_term()
    {
        var mat = Mat(TestData.Comp(sn: 10, pb: 1), delta: 0.05, cored: true, grain: 70, porosity: 1.5);
        double hv = 145;
        double expected = 100 - 0.18 * 100 - 120 * 0.05 - 4 * 1.5 - 30 * 0.2 - 2 * 1 - 10 - 0.05 * 20 - 10 * 1 - 25 * 0.1;
        Assert.Equal(expected, PropertyModel.Toughness(TestData.Model, mat, hv, crackSeverity: 0.2, foldCount: 1, burntSeverity: 0.1), 9);
    }

    [Fact]
    public void Toughness_clamps_to_0_100()
    {
        var soft = Mat(TestData.Comp());
        Assert.Equal(100, PropertyModel.Toughness(TestData.Model, soft, 30, 0, 0, 0));
        Assert.Equal(0, PropertyModel.Toughness(TestData.Model, soft, 400, 1, 3, 2));
    }

    [Fact]
    public void Solution_quench_adds_small_toughness_bonus()
    {
        var a = Mat(TestData.Comp(sn: 10));
        var b = Mat(TestData.Comp(sn: 10), solutionQuenched: true);
        double diff = PropertyModel.Toughness(TestData.Model, b, 150, 0, 0, 0) - PropertyModel.Toughness(TestData.Model, a, 150, 0, 0, 0);
        Assert.Equal(TestData.Model.Quench.SolutionQuenchToughnessBonus, diff, 9);
    }

    [Theory]
    [InlineData(40, 0)]
    [InlineData(170, 50)]
    [InlineData(400, 100)]
    public void Edge_retention(double hv, double expected)
    {
        Assert.Equal(expected, PropertyModel.EdgeRetention(TestData.Model, hv), 9);
    }

    [Fact]
    public void EpsMax_exact()
    {
        var mat = Mat(TestData.Comp(sn: 10, pb: 2), delta: 0.05, porosity: 1);
        Assert.Equal(0.85 - 0.30 - 0.06 - 0.05 - 0.04, PropertyModel.EpsMax(TestData.Model, mat), 9);
    }

    [Fact]
    public void Corrosion_and_castability()
    {
        var c = TestData.Comp(sn: 8, @as: 1, zn: 1, pb: 4, s: 0.2);
        Assert.Equal(50 + 24 + 5 - 2 - 1, TestData.Model.Bulk.Corrosion.Evaluate(c, TestData.Model.EffectiveCaps), 9);
        Assert.Equal(40 + 16 + 16 + 20, PropertyModel.Castability(TestData.Model, c), 9);
        Assert.Equal(100, PropertyModel.Castability(TestData.Model, TestData.Comp(sn: 10, pb: 10)));
    }

    [Fact]
    public void Acoustic_peaks_near_21_5_tin()
    {
        var bell = Mat(TestData.Comp(sn: 21.5), porosity: 0);
        Assert.Equal(1.0, PropertyModel.AcousticQuality(TestData.Model, bell, 0), 9);
        var flawed = Mat(TestData.Comp(sn: 24), porosity: 1);
        Assert.Equal(Math.Exp(-1) * 0.8 * 0.5, PropertyModel.AcousticQuality(TestData.Model, flawed, 0.5), 9);
        Assert.True(PropertyModel.AcousticQuality(TestData.Model, Mat(TestData.Comp(sn: 10)), 0) < 0.001);
    }

    [Fact]
    public void Color_runs_copper_to_gold_to_silver_and_arsenic_silvers()
    {
        var m = TestData.Model;
        var cu = PropertyModel.Color(m, TestData.Comp());
        var gold = PropertyModel.Color(m, TestData.Comp(sn: 10));
        var bell = PropertyModel.Color(m, TestData.Comp(sn: 22));
        Assert.True(cu[0] > cu[1] + 0.2, "copper is red");
        Assert.True(gold[1] > cu[1], "10% Sn is more yellow than copper");
        Assert.True(Math.Abs(bell[0] - bell[2]) < 0.05, "bell metal is grey");
        var arsenical = PropertyModel.Color(m, TestData.Comp(@as: 4));
        Assert.True(arsenical[2] > cu[2], "arsenic shifts toward silver");
    }

    [Fact]
    public void Leaks_on_porosity_or_wall_crack()
    {
        // The chisel template has no wall zones, so only porosity can make it "leak".
        var item = new ItemInstance { TemplateId = "chisel_bronze", Material = Mat(TestData.Comp(sn: 10), porosity: 3.5) };
        item.Zones.Add(new Zone { Id = "edge", ThicknessMm = 3 });
        Assert.True(PropertyModel.Derive(TestData.Data, item).Bulk.Leaks);
        item.Material.PorosityPct = 0.5;
        item.Zones[0].CrackSeverity = 0.5;
        Assert.False(PropertyModel.Derive(TestData.Data, item).Bulk.Leaks);
    }
}
