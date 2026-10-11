using ForgeSim.Core.Data;
using ForgeSim.Core.Items;
using ForgeSim.Core.Process;
using ForgeSim.Core.Rng;

namespace ForgeSim.Core.Recipes;

/// <summary>§8 A recipe is a full process history plus the seed to replay it with.</summary>
public sealed class Recipe
{
    public required string Id { get; init; }
    public string Description { get; init; } = "";
    public required string Template { get; init; }
    public ulong Seed { get; init; }
    public List<ProcessStep> Steps { get; init; } = new();

    public static Recipe Load(string path) => Json.Load<Recipe>(path);
}

public sealed record StepLog(int Index, ProcessStep Step, IReadOnlyList<string> Lines);

public sealed record RecipeRun(Recipe Recipe, ItemInstance Item, IReadOnlyList<StepLog> Log, int StepsSkipped);

public static class RecipeRunner
{
    /// <summary>
    /// Replays a recipe from scratch with its seed. If a step fails the item (e.g. the charge never melts),
    /// the remaining steps are skipped and counted.
    /// </summary>
    public static RecipeRun Run(ForgeData data, Recipe recipe)
    {
        var forge = new Forge(data);
        var rng = new SimRng(recipe.Seed);
        var item = forge.NewItem(recipe.Template);
        var log = new List<StepLog>();
        int i = 0;
        for (; i < recipe.Steps.Count && !item.Failed; i++)
            log.Add(new StepLog(i, recipe.Steps[i], forge.Apply(item, recipe.Steps[i], rng)));
        return new RecipeRun(recipe, item, log, recipe.Steps.Count - i);
    }
}
