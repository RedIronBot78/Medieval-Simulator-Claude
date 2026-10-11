using ForgeSim.Core.Inspection;
using ForgeSim.Core.Items;
using ForgeSim.Core.Process;
using ForgeSim.Core.Properties;
using static ForgeSim.Core.Tests.Fixtures;

namespace ForgeSim.Core.Tests;

/// <summary>§3.7 Inspect.</summary>
public class InspectTests
{
    private static ItemInstance WithEdgeCrack(double severity)
    {
        var item = Cast(sn: 10);
        StrikeProcess.AddCrack(Fixtures.Forge, item, item.Zone("edge"), severity, "test");
        return item;
    }

    private static Defect EdgeCrack(ItemInstance item) => item.FindDefect(DefectType.Crack, "edge")!;

    [Fact]
    public void Visual_finds_cracks_from_0_15()
    {
        var small = WithEdgeCrack(0.1);
        Inspector.Inspect(TestData.Data, small, InspectMethod.Visual);
        Assert.False(EdgeCrack(small).Revealed);

        var big = WithEdgeCrack(0.2);
        var r = Inspector.Inspect(TestData.Data, big, InspectMethod.Visual);
        Assert.True(EdgeCrack(big).Revealed);
        Assert.Contains(EdgeCrack(big), r.Revealed);
    }

    [Fact]
    public void Ring_test_finds_cracks_from_0_05()
    {
        var hairline = WithEdgeCrack(0.06);
        var r = Inspector.Inspect(TestData.Data, hairline, InspectMethod.Ring);
        Assert.True(EdgeCrack(hairline).Revealed);
        Assert.Equal("Dull ring.", r.Findings.Single());

        var tiny = WithEdgeCrack(0.04);
        Inspector.Inspect(TestData.Data, tiny, InspectMethod.Ring);
        Assert.False(EdgeCrack(tiny).Revealed);
    }

    [Fact]
    public void Ring_test_hears_hidden_porosity_visual_does_not()
    {
        var item = Cast(sn: 10, pourQuality: 0.0); // ~1.25% porosity: hidden defect
        var por = item.FindDefect(DefectType.Porosity)!;
        Assert.False(por.Revealed);
        Inspector.Inspect(TestData.Data, item, InspectMethod.Visual);
        Assert.False(por.Revealed);
        Inspector.Inspect(TestData.Data, item, InspectMethod.Ring);
        Assert.True(por.Revealed);
        Assert.Equal("ring test", por.RevealedBy);
    }

    [Fact]
    public void Sound_casting_rings_clear()
    {
        var item = Cast(sn: 10);
        Assert.Equal("Clear ring.", Inspector.Inspect(TestData.Data, item, InspectMethod.Ring).Findings.Single());
    }

    [Fact]
    public void Visual_finds_laps_and_burning()
    {
        var item = Cast(sn: 10);
        item.Defects.Add(new Defect { Type = DefectType.Fold, ZoneId = "edge", Severity = 1 });
        item.Defects.Add(new Defect { Type = DefectType.Burnt, Severity = 0.2 });
        Inspector.Inspect(TestData.Data, item, InspectMethod.Visual);
        Assert.All(item.Defects.Where(d => d.Type is DefectType.Fold or DefectType.Burnt), d => Assert.True(d.Revealed));
    }

    [Fact]
    public void Segregation_is_only_found_in_use()
    {
        var item = Cast(sn: 10);
        item.Defects.Add(new Defect { Type = DefectType.Segregation, Severity = 0.3 });
        foreach (var m in Enum.GetValues<InspectMethod>()) Inspector.Inspect(TestData.Data, item, m);
        Assert.False(item.FindDefect(DefectType.Segregation)!.Revealed);
    }

    [Fact]
    public void File_test_gives_hardness_bands()
    {
        var item = Cast(sn: 10);
        Apply(item, Anneal(700, 30));
        item.Zone("edge").Strain = 0.5;
        var r = Inspector.Inspect(TestData.Data, item, InspectMethod.File);
        Assert.Contains("edge: hard", r.Findings);
        Assert.Contains("shank: soft", r.Findings);
        Assert.Equal("medium", Inspector.HardnessBand(TestData.Model, 140));
    }

    [Fact]
    public void Inspection_never_changes_properties_and_is_recorded()
    {
        var item = WithEdgeCrack(0.2);
        var before = PropertyModel.Derive(TestData.Data, item);
        Inspector.Inspect(TestData.Data, item, InspectMethod.Visual);
        var after = PropertyModel.Derive(TestData.Data, item);
        Assert.Equal(before.Zones, after.Zones);
        Assert.Equal(new InspectionRecord(item.History.Count, InspectMethod.Visual), item.Inspections.Single());
    }
}
