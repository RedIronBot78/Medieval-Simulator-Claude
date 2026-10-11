using ForgeSim.Core.Items;
using static ForgeSim.Core.Tests.Fixtures;

namespace ForgeSim.Core.Tests;

/// <summary>§6 Item templates and fitness.</summary>
public class TemplateTests
{
    [Fact]
    public void All_six_spec_templates_load()
    {
        foreach (var id in new[] { "chisel_bronze", "flat_axe_bronze", "sickle_bronze", "knife_bronze", "bell_bronze", "cauldron_bronze" })
            Assert.True(TestData.Data.Templates.ContainsKey(id), $"missing template {id}");
    }

    [Fact]
    public void Chisel_template_matches_spec_example()
    {
        var t = TestData.Data.Template("chisel_bronze");
        Assert.Equal([("edge", 6.0, 3.0), ("shank", 12.0, 11.0), ("head", 14.0, 14.0)],
            t.Zones.Select(z => (z.Id, z.StartThicknessMm, z.TargetThicknessMm)));
        Assert.Equal(1.0, t.Requirements.Sum(r => r.Weight), 9);
    }

    [Fact]
    public void Validation_rejects_unknown_zone_and_property()
    {
        var badZone = new ItemTemplate { Id = "x", Zones = [new ZoneTemplate { Id = "edge" }], Requirements = [new Requirement { Zone = "tip", Property = "HV", Min = 1 }] };
        Assert.Throws<InvalidDataException>(() => Fitness.Validate(badZone));
        var badProp = new ItemTemplate { Id = "x", Zones = [new ZoneTemplate { Id = "edge" }], Requirements = [new Requirement { Zone = "edge", Property = "Sharpness", Min = 1 }] };
        Assert.Throws<InvalidDataException>(() => Fitness.Validate(badProp));
    }

    [Fact]
    public void Worked_10_tin_chisel_scores_full_and_as_cast_does_not()
    {
        var worked = Cast(sn: 10);
        Apply(worked, Anneal(700, 30));
        worked.Zone("edge").Strain = 0.4;
        var f = Fitness.Evaluate(TestData.Data, worked);
        Assert.True(f.Score == 1.0, string.Join("; ", f.Results.Select(r => $"{r.Requirement.Zone}.{r.Requirement.Property}={r.Value:0.0} met={r.Met}")));

        var asCast = Cast(sn: 10);
        var g = Fitness.Evaluate(TestData.Data, asCast);
        Assert.False(g.Results.Single(r => r.Requirement.Property == "HV").Met);
        Assert.Equal(0.5, g.Score, 9);
    }

    [Fact]
    public void Cracked_cauldron_wall_leaks()
    {
        var pot = Cast(sn: 8, pb: 8, template: "cauldron_bronze");
        Assert.Equal(1.0, Fitness.Evaluate(TestData.Data, pot).Score, 9);
        pot.Zone("wall").CrackSeverity = 0.1;
        var f = Fitness.Evaluate(TestData.Data, pot);
        Assert.False(f.Results.Single(r => r.Requirement.Property == "Leaks").Met);
        pot.Zone("wall").CrackSeverity = 0;
        pot.Zone("feet").CrackSeverity = 0.5; // feet aren't a wall
        Assert.True(Fitness.Evaluate(TestData.Data, pot).Results.Single(r => r.Requirement.Property == "Leaks").Met);
    }
}
