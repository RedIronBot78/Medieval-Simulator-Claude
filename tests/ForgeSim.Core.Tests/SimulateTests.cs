using System.Text.Json;
using ForgeSim.Core.Data;
using ForgeSim.Core.Items;
using ForgeSim.Core.Performance;
using ForgeSim.Core.Process;
using ForgeSim.Core.Properties;
using ForgeSim.Core.Recipes;
using ForgeSim.Core.Rng;
using static ForgeSim.Core.Tests.Fixtures;

namespace ForgeSim.Core.Tests;

/// <summary>§7 Performance in use, §8 recipes, and the remaining §9 behavioral tests (7, 9, 10).</summary>
public class SimulateTests
{
    private static readonly UseProfile Carving = TestData.Data.Use("carving_hardwood");

    internal static RecipeRun RunRecipe(string id) =>
        RecipeRunner.Run(TestData.Data, Recipe.Load(Path.Combine(TestData.Data.Root, "recipes", id + ".json")));

    [Fact]
    public void Simulate_does_not_modify_the_input_item()
    {
        var item = RunRecipe("chisel_10sn_good").Item;
        string before = JsonSerializer.Serialize(item, Json.Options);
        UseSimulator.Simulate(TestData.Data, item, Carving, 500, new SimRng(1));
        Assert.Equal(before, JsonSerializer.Serialize(item, Json.Options));
    }

    [Fact]
    public void Tough_metal_never_breaks_and_soft_metal_bends()
    {
        var cu = Cast(sn: 0);
        Apply(cu, Anneal(500, 30)); // HV ~ 50, toughness ~ 97
        var log = UseSimulator.Simulate(TestData.Data, cu, Carving, 300, new SimRng(2));
        Assert.False(log.Broken);
        Assert.Equal(300, log.UsesCompleted);
        Assert.InRange(log.Count(UseOutcome.Bent), 10, 40); // p = 0.4·70/400 = 0.07
        Assert.Equal(0, log.Count(UseOutcome.Chipped));
    }

    [Fact]
    public void Wear_formula_and_dulling()
    {
        var item = Cast(sn: 10);
        Apply(item, Anneal(700, 30));
        double hv = PropertyModel.DeriveZone(TestData.Model, item, item.Zone("edge")).Hardness;
        var log = UseSimulator.Simulate(TestData.Data, item, Carving, 100, new SimRng(3));
        Assert.Equal(100 * 0.5 * 0.5 * 40 / hv, log.TotalEdgeWear, 6);
        Assert.Equal((int)Math.Floor(log.TotalEdgeWear / 10), log.Count(UseOutcome.Dull));
    }

    [Fact]
    public void Hidden_crack_grows_until_found_in_use()
    {
        var item = Cast(sn: 10);
        Apply(item, Anneal(700, 30));
        StrikeProcess.AddCrack(Smith, item, item.Zone("edge"), 0.25, "test");
        Assert.False(item.FindDefect(DefectType.Crack, "edge")!.Revealed);

        // 0.4 impact · 0.01 per use: 0.25 → 0.30 after 13 uses
        var log = UseSimulator.Simulate(TestData.Data, item, TestData.Data.Use("carving_hardwood"), 20, new SimRng(4));
        if (!log.Broken || log.UsesCompleted >= 13)
        {
            var found = log.Events.Single(e => e.Outcome == UseOutcome.Cracked);
            Assert.Equal(13, found.Use);
            Assert.True(log.FinalState.FindDefect(DefectType.Crack, "edge")!.Revealed);
        }
    }

    [Fact]
    public void Breaking_reveals_every_defect()
    {
        var bell = RunRecipe("chisel_22sn_cast").Item; // brittle: breaks fast
        bell.Defects.Add(new Defect { Type = DefectType.Segregation, Severity = 0.1 });
        var log = UseSimulator.Simulate(TestData.Data, bell, TestData.Data.Use("cutting_stone"), 1000, new SimRng(5));
        Assert.True(log.Broken);
        Assert.Equal(UseOutcome.Broken, log.Events[^1].Outcome);
        Assert.All(log.FinalState.Defects, d => Assert.True(d.Revealed));
    }

    [Fact]
    public void Behavioral_7_bell_metal_rings()
    {
        var bell = RunRecipe("bell_22sn").Item;
        double q = PropertyModel.Derive(TestData.Data, bell).Bulk.AcousticQuality;
        Assert.True(q >= 0.85, $"22% Sn bell acoustic quality {q:0.000} (composition {bell.Material!.Composition}, porosity {bell.Material.PorosityPct:0.00}%)");

        var tenTin = Cast(sn: 10, template: "bell_bronze", mold: MoldType.Clay, preheat: 300, pourQuality: 0.95);
        double q10 = PropertyModel.Derive(TestData.Data, tenTin).Bulk.AcousticQuality;
        Assert.True(q10 <= 0.1, $"10% Sn acoustic quality {q10:0.000}");
    }

    public static TheoryData<string> AllRecipes => new(
        Directory.GetFiles(Path.Combine(TestData.Data.Root, "recipes"), "*.json").Select(Path.GetFileNameWithoutExtension).Order()!);

    [Theory]
    [MemberData(nameof(AllRecipes))]
    public void Behavioral_9_determinism(string recipeId)
    {
        string Once()
        {
            var run = RunRecipe(recipeId);
            bool usable = !run.Item.Failed && run.Item.Zones.Any(z => z.Id == Carving.PrimaryZone);
            var use = usable ? UseSimulator.Simulate(TestData.Data, run.Item, Carving, 200, new SimRng(run.Recipe.Seed)) : null;
            return JsonSerializer.Serialize(new { run.Item, run.Log, Use = use?.Events, Final = use?.FinalState }, Json.Options);
        }
        Assert.Equal(Once(), Once());
    }

    [Fact]
    public void Behavioral_10_bronze_chisel_beats_bell_metal_and_copper()
    {
        var bronze = RunRecipe("chisel_10sn_good").Item;
        var bell = RunRecipe("chisel_22sn_cast").Item;
        var copper = RunRecipe("chisel_cu_pure").Item;
        const int seeds = 50, uses = 500;

        (int breaks, double meanUses, double meanWear) Trial(ItemInstance item)
        {
            int breaks = 0; double usesSum = 0, wearSum = 0;
            for (ulong s = 0; s < seeds; s++)
            {
                var log = UseSimulator.Simulate(TestData.Data, item, Carving, uses, new SimRng(s));
                if (log.Broken) breaks++;
                usesSum += log.UsesCompleted;
                wearSum += log.TotalEdgeWear / log.UsesCompleted;
            }
            return (breaks, usesSum / seeds, wearSum / seeds);
        }

        var b = Trial(bronze);
        var h = Trial(bell);
        var c = Trial(copper);
        string summary = $"10% Sn: {b.breaks}/{seeds} broke, mean life {b.meanUses:0}, wear/use {b.meanWear:0.0000}; " +
                         $"22% Sn: {h.breaks}/{seeds} broke, mean life {h.meanUses:0}; " +
                         $"Cu: {c.breaks}/{seeds} broke, wear/use {c.meanWear:0.0000}";

        Assert.True(b.meanUses > h.meanUses && b.breaks < h.breaks, "10% Sn should break less than 22% Sn. " + summary);
        Assert.True(c.meanWear > b.meanWear, "Pure Cu should dull faster than 10% Sn. " + summary);
    }
}
