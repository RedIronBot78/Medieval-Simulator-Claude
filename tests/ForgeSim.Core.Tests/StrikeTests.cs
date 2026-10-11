using ForgeSim.Core.Items;
using ForgeSim.Core.Properties;
using ForgeSim.Core.Rng;
using static ForgeSim.Core.Tests.Fixtures;

namespace ForgeSim.Core.Tests;

/// <summary>§3.3 Strike.</summary>
public class StrikeTests
{
    private static ItemInstance AnnealedBar(double sn = 10, double @as = 0, double pb = 0)
    {
        var item = Cast(sn: sn, @as: @as, pb: pb);
        Apply(item, Anneal(700, 30));
        return item;
    }

    [Fact]
    public void Single_blow_reduction_formula()
    {
        var item = AnnealedBar();
        double hv = PropertyModel.Hardness(TestData.Model, item.Material!, 0);
        Apply(item, Strike("edge", force: 0.7, accuracy: 0.9));
        double reduction = 0.02 * 0.7 * 0.9 * (100 / hv);
        Assert.Equal(6.0 * (1 - reduction), item.Zone("edge").ThicknessMm, 9);
        Assert.Equal(-Math.Log(1 - reduction), item.Zone("edge").Strain, 9);
    }

    [Fact]
    public void Harder_metal_moves_less_per_blow()
    {
        var soft = AnnealedBar();
        var hard = AnnealedBar();
        hard.Zone("edge").Strain = 0.4;
        Apply(soft, Strike("edge", 0.7));
        Apply(hard, Strike("edge", 0.7));
        Assert.True(6.0 - soft.Zone("edge").ThicknessMm > 2 * (6.0 - hard.Zone("edge").ThicknessMm));
    }

    [Fact]
    public void Work_hardening_raises_hv()
    {
        var item = AnnealedBar();
        double hv0 = PropertyModel.Hardness(TestData.Model, item.Material!, item.Zone("edge").Strain);
        Apply(item, Strike("edge", 0.7, repeat: 25));
        double hv1 = PropertyModel.Hardness(TestData.Model, item.Material!, item.Zone("edge").Strain);
        Assert.True(hv1 > hv0 + 60, $"HV {hv0:0} → {hv1:0}");
    }

    [Fact]
    public void Hot_strikes_thin_without_strain()
    {
        var item = AnnealedBar(sn: 0); // pure Cu: T_rx 350, no hot-shortness
        Apply(item, Strike("edge", 0.8, repeat: 10, tempC: 600));
        Assert.True(item.Zone("edge").ThicknessMm < 6.0);
        Assert.Equal(0, item.Zone("edge").Strain);
    }

    [Fact]
    public void Below_eps_max_never_cracks()
    {
        var item = AnnealedBar();
        Apply(item, Strike("edge", 1.0, repeat: 30), new SimRng(3));
        var e = item.Zone("edge");
        Assert.True(e.Strain < PropertyModel.EpsMax(TestData.Model, item.Material!), "test setup: still below ε_max");
        Assert.Equal(0, e.CrackSeverity);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(1234UL)]
    public void Behavioral_4_overwork_cracks_within_20_strikes(ulong seed)
    {
        var item = AnnealedBar();
        var edge = item.Zone("edge");
        double epsMax = PropertyModel.EpsMax(TestData.Model, item.Material!);
        edge.Strain = epsMax + 0.01; // just past ε_max
        Apply(item, Strike("edge", 0.8, repeat: 20), new SimRng(seed));
        Assert.True(edge.CrackSeverity > 0, $"seed {seed}: no crack after 20 strikes at F 0.8 from ε = ε_max + 0.01");
    }

    [Fact]
    public void Crack_increment_and_reveal_threshold()
    {
        var item = AnnealedBar();
        var edge = item.Zone("edge");
        edge.Strain = 2.0; // far past ε_max: p = Force
        Apply(item, Strike("edge", 1.0), new SimRng(1));
        Assert.Equal(0.15, edge.CrackSeverity, 9);
        var crack = item.FindDefect(DefectType.Crack, "edge")!;
        Assert.False(crack.Revealed);

        Apply(item, Strike("edge", 1.0, repeat: 2), new SimRng(1));
        Assert.Equal(0.45, edge.CrackSeverity, 9);
        Assert.True(crack.Revealed);
    }

    [Fact]
    public void Glancing_blows_fold_and_square_blows_do_not()
    {
        var square = AnnealedBar(sn: 0);
        Apply(square, Strike("edge", 0.3, repeat: 200, angle: 25), new SimRng(8));
        Assert.Equal(0, square.FoldCount("edge"));

        var glancing = AnnealedBar(sn: 0);
        Apply(glancing, Strike("edge", 0.3, repeat: 200, angle: 45), new SimRng(8));
        Assert.InRange(glancing.FoldCount("edge"), 3, 20); // expected 10
        Assert.All(glancing.Defects.Where(d => d.Type == DefectType.Fold), d => Assert.False(d.Revealed));
    }

    [Fact]
    public void Inaccurate_blows_add_shape_error()
    {
        var item = AnnealedBar();
        Apply(item, Strike("edge", 0.5, repeat: 10, accuracy: 0.5));
        Assert.Equal(10 * 0.01 * 0.5, item.Zone("edge").ShapeError, 9);

        var ok = AnnealedBar();
        Apply(ok, Strike("edge", 0.5, repeat: 10, accuracy: 0.7));
        Assert.Equal(0, ok.Zone("edge").ShapeError);
    }

    [Fact]
    public void Same_seed_same_result()
    {
        string Run()
        {
            var item = AnnealedBar();
            item.Zone("edge").Strain = 0.5;
            Apply(item, Strike("edge", 0.9, repeat: 40, angle: 35), new SimRng(77));
            var e = item.Zone("edge");
            return $"{e.ThicknessMm:R}|{e.Strain:R}|{e.CrackSeverity:R}|{item.FoldCount("edge")}";
        }
        Assert.Equal(Run(), Run());
    }
}
