using ForgeSim.Core.Items;
using ForgeSim.Core.Materials;
using ForgeSim.Core.Process;
using ForgeSim.Core.Properties;
using static ForgeSim.Core.Tests.Fixtures;

namespace ForgeSim.Core.Tests;

/// <summary>§3.4 Anneal.</summary>
public class AnnealTests
{
    [Fact]
    public void Behavioral_3_annealing_restores_workability()
    {
        var item = Cast(sn: 10);
        item.Zone("edge").Strain = 0.5;
        Apply(item, Anneal(650, 30));
        Assert.True(item.Zone("edge").Strain < 0.05, $"ε after anneal = {item.Zone("edge").Strain}");
    }

    [Fact]
    public void Full_recrystallization_resets_reference_thickness()
    {
        var item = Cast(sn: 10);
        var edge = item.Zone("edge");
        edge.ThicknessMm = 4.0;
        edge.Strain = Math.Log(6.0 / 4.0);
        Apply(item, Anneal(650, 30));
        Assert.Equal(4.0, edge.ThicknessAtLastAnnealMm, 9);
    }

    [Fact]
    public void Recrystallized_fraction_formula()
    {
        var item = Cast(sn: 10);
        double trx = MeltingRanges.Recrystallization(TestData.Model, item.Material!.Composition);
        item.Zone("edge").Strain = 0.4;
        Apply(item, Anneal(trx, 5)); // τ_rx = 10 at T = T_rx
        Assert.Equal(0.4 * Math.Exp(-0.5), item.Zone("edge").Strain, 9);
        Assert.Equal(6.0, item.Zone("edge").ThicknessAtLastAnnealMm); // X < 0.95: reference unchanged
    }

    [Fact]
    public void Below_recrystallization_nothing_softens()
    {
        var item = Cast(sn: 10);
        item.Zone("edge").Strain = 0.3;
        double grain = item.Material!.GrainSizeUm;
        Apply(item, Anneal(300, 60));
        Assert.Equal(0.3, item.Zone("edge").Strain);
        Assert.Equal(grain, item.Material.GrainSizeUm);
        Assert.True(item.Material.Phase.Cored);
    }

    [Fact]
    public void Grain_growth_formula()
    {
        var item = Cast(sn: 10);
        double trx = MeltingRanges.Recrystallization(TestData.Model, item.Material!.Composition);
        double g0 = item.Material.GrainSizeUm;
        Apply(item, Anneal(trx + 150, 20));
        Assert.Equal(g0 + 0.5 * 20 * Math.E, item.Material.GrainSizeUm, 9);
    }

    [Fact]
    public void Homogenization_dissolves_as_cast_delta_and_removes_coring()
    {
        var item = Cast(sn: 10);
        double d0 = item.Material!.Phase.Delta;
        Assert.True(d0 > 0.04);
        Apply(item, Anneal(700, 30)); // τ_h = 30·e^-1 = 11 min → H = 0.935
        double h = 1 - Math.Exp(-30 / (30 * Math.Exp(-1)));
        Assert.Equal(d0 * (1 - h), item.Material.Phase.Delta, 9);
        Assert.False(item.Material.Phase.Cored);
    }

    [Fact]
    public void Short_cool_anneal_leaves_coring()
    {
        var item = Cast(sn: 10);
        Apply(item, Anneal(600, 10)); // H = 1 − e^(−1/3) = 0.28
        Assert.True(item.Material!.Phase.Cored);
    }

    [Fact]
    public void High_tin_delta_moves_toward_equilibrium()
    {
        var item = Cast(sn: 22);
        double eq = AnnealProcess.DeltaEq(TestData.Model, item.Material!.Composition);
        Assert.InRange(eq, 0.39, 0.41);
        Apply(item, Anneal(700, 120));
        Assert.Equal(eq, item.Material.Phase.Delta, 3);
    }

    [Fact]
    public void Good_anneal_makes_10_tin_far_more_workable()
    {
        var item = Cast(sn: 10);
        double before = PropertyModel.EpsMax(TestData.Model, item.Material!);
        Apply(item, Anneal(650, 30));
        double after = PropertyModel.EpsMax(TestData.Model, item.Material);
        Assert.True(after > before + 0.04, $"ε_max {before:0.000} → {after:0.000}");
    }

    [Fact]
    public void Behavioral_5_burning_above_solidus()
    {
        var item = Cast(sn: 10);
        double ts = MeltingRanges.Solidus(TestData.Model, item.Material!.Composition);
        Apply(item, Anneal(900, 10));
        var burnt = item.FindDefect(DefectType.Burnt);
        Assert.NotNull(burnt);
        Assert.Equal((900 - ts) / 50, burnt!.Severity, 9);
        Assert.True(burnt.Revealed);
    }

    [Fact]
    public void Slight_burn_stays_hidden()
    {
        var item = Cast(sn: 10);
        double ts = MeltingRanges.Solidus(TestData.Model, item.Material!.Composition);
        Apply(item, Anneal(ts + 10, 10));
        Assert.False(item.FindDefect(DefectType.Burnt)!.Revealed);
    }

    [Fact]
    public void Lead_sweats_out_above_600()
    {
        var item = Cast(sn: 8, pb: 8);
        double pb0 = item.Material!.Composition["Pb"];
        Apply(item, Anneal(650, 30));
        Assert.Equal(pb0 - 0.25, item.Material.Composition["Pb"], 9);
        Assert.True(item.FindDefect(DefectType.LeadSweat)!.Revealed);

        var cool = Cast(sn: 8, pb: 8);
        Apply(cool, Anneal(550, 30));
        Assert.Null(cool.FindDefect(DefectType.LeadSweat));
    }

    [Fact]
    public void Scale_only_in_air()
    {
        var air = Cast(sn: 10);
        Apply(air, Anneal(800, 10, Atmosphere.Air));
        Assert.Equal(6.0 - 0.002 * 10 * Math.E, air.Zone("edge").ThicknessMm, 9);

        var charcoal = Cast(sn: 10);
        Apply(charcoal, Anneal(800, 10, Atmosphere.Charcoal));
        Assert.Equal(6.0, charcoal.Zone("edge").ThicknessMm);
    }
}
