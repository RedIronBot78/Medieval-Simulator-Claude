using ForgeSim.Core.Items;
using ForgeSim.Core.Process;
using ForgeSim.Core.Properties;
using ForgeSim.Core.Rng;
using static ForgeSim.Core.Tests.Fixtures;

namespace ForgeSim.Core.Tests;

/// <summary>§3.5 Quench.</summary>
public class QuenchTests
{
    private static ZoneProperties Edge(ItemInstance item) => PropertyModel.DeriveZone(TestData.Model, item, item.Zone("edge"));

    [Fact]
    public void Behavioral_2_quench_does_not_harden_bronze()
    {
        var water = Cast(sn: 10);
        Apply(water, Anneal(650, 30));
        Apply(water, new QuenchStep { FromTempC = 650, Medium = QuenchMedium.Water });

        var air = Cast(sn: 10);
        Apply(air, Anneal(650, 30));
        Apply(air, new QuenchStep { FromTempC = 650, Medium = QuenchMedium.Air });

        double hw = Edge(water).Hardness, ha = Edge(air).Hardness;
        Assert.True(Math.Abs(hw - ha) / ha <= 0.05, $"water HV {hw:0.0} vs air HV {ha:0.0}");
    }

    [Fact]
    public void Solution_quench_gives_small_toughness_gain_only()
    {
        var water = Cast(sn: 10);
        Apply(water, Anneal(650, 30));
        Apply(water, new QuenchStep { FromTempC = 650, Medium = QuenchMedium.Water });
        var air = Cast(sn: 10);
        Apply(air, Anneal(650, 30));
        Apply(air, new QuenchStep { FromTempC = 650, Medium = QuenchMedium.Air });

        double gain = Edge(water).Toughness - Edge(air).Toughness;
        Assert.InRange(gain, 0.1, 5);
    }

    private static ItemInstance AnnealedBellMetal()
    {
        var item = Cast(sn: 22);
        Apply(item, Anneal(700, 30));
        return item;
    }

    [Fact]
    public void Behavioral_8_beta_quench_doubles_toughness()
    {
        var beta = AnnealedBellMetal();
        Apply(beta, new QuenchStep { FromTempC = 700, Medium = QuenchMedium.Water });
        var air = AnnealedBellMetal();
        Apply(air, new QuenchStep { FromTempC = 700, Medium = QuenchMedium.Air });

        Assert.True(beta.Material!.Phase.BetaQuenched);
        double tb = Edge(beta).Toughness, ta = Edge(air).Toughness;
        Assert.True(tb >= 2 * ta, $"β-quenched toughness {tb:0.0} vs air-cooled {ta:0.0}");
    }

    [Theory]
    [InlineData(22, 800, QuenchMedium.Water)] // too hot
    [InlineData(22, 580, QuenchMedium.Water)] // too cool
    [InlineData(22, 700, QuenchMedium.Oil)]   // wrong medium
    [InlineData(18, 700, QuenchMedium.Water)] // too little tin
    public void Beta_quench_needs_the_exact_window(double sn, double fromC, QuenchMedium medium)
    {
        var item = Cast(sn: sn);
        Apply(item, Anneal(700, 30));
        Apply(item, new QuenchStep { FromTempC = fromC, Medium = medium });
        Assert.False(item.Material!.Phase.BetaQuenched);
    }

    [Fact]
    public void Delta_rich_metal_shock_cracks_in_water()
    {
        // 22% Sn annealed (δ ≈ 0.39) quenched from below the β window: p = 2·(0.39 − 0.15) ≈ 0.48 per zone.
        int cracked = 0;
        for (ulong seed = 0; seed < 50; seed++)
        {
            var item = AnnealedBellMetal();
            Apply(item, new QuenchStep { FromTempC = 500, Medium = QuenchMedium.Water }, new SimRng(seed));
            cracked += item.Zones.Count(z => z.CrackSeverity > 0);
        }
        Assert.InRange(cracked, 45, 100); // 150 zone rolls × 0.48 ≈ 72
    }

    [Fact]
    public void Low_delta_never_shock_cracks()
    {
        for (ulong seed = 0; seed < 20; seed++)
        {
            var item = Cast(sn: 10);
            Apply(item, new QuenchStep { FromTempC = 650, Medium = QuenchMedium.Water }, new SimRng(seed));
            Assert.All(item.Zones, z => Assert.Equal(0, z.CrackSeverity));
        }
    }

    [Fact]
    public void Annealing_undoes_beta_quench()
    {
        var item = AnnealedBellMetal();
        Apply(item, new QuenchStep { FromTempC = 700, Medium = QuenchMedium.Water });
        Apply(item, Anneal(650, 30));
        Assert.False(item.Material!.Phase.BetaQuenched);
    }
}
