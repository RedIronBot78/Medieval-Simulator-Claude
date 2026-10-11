using ForgeSim.Core.Items;

namespace ForgeSim.Core.Data;

/// <summary>Everything loaded from the data/ folder. Load once and share; it is read-only after loading.</summary>
public sealed class ForgeData
{
    public required string Root { get; init; }
    public required IReadOnlyDictionary<string, ElementDef> Elements { get; init; }
    public required MaterialModel Model { get; init; }
    public required IReadOnlyDictionary<string, ItemTemplate> Templates { get; init; }

    public static ForgeData Load(string dataRoot, string modelId = "bronze")
    {
        var elements = LoadAll<ElementDef>(Path.Combine(dataRoot, "elements"))
            .ToDictionary(e => e.Symbol, StringComparer.Ordinal);
        var model = Json.Load<MaterialModel>(Path.Combine(dataRoot, "model", modelId + ".json"));
        var templates = LoadAll<ItemTemplate>(Path.Combine(dataRoot, "items"))
            .ToDictionary(t => t.Id, StringComparer.Ordinal);

        var data = new ForgeData { Root = dataRoot, Elements = elements, Model = model, Templates = templates };
        data.Validate();
        return data;
    }

    /// <summary>Walks up from <paramref name="start"/> (default: the app base directory) to find the repo's data/ folder.</summary>
    public static string FindRoot(string? start = null)
    {
        var dir = new DirectoryInfo(start ?? AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (Directory.Exists(Path.Combine(candidate, "elements")) && Directory.Exists(Path.Combine(candidate, "model")))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"No data/ folder found above {start ?? AppContext.BaseDirectory}.");
    }

    public static ForgeData LoadDefault() => Load(FindRoot());

    public ItemTemplate Template(string id) =>
        Templates.TryGetValue(id, out var t) ? t : throw new KeyNotFoundException($"No item template '{id}' in data/items.");

    private static IEnumerable<T> LoadAll<T>(string dir) =>
        Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal).Select(Json.Load<T>)
            : [];

    private void Validate()
    {
        if (!Elements.ContainsKey(Model.BaseElement))
            throw new InvalidDataException($"Model base element {Model.BaseElement} has no element file.");
        foreach (var t in Templates.Values) Fitness.Validate(t);
    }

    /// <summary>Throws if a composition mentions an element with no data file.</summary>
    public void RequireKnownElements(IEnumerable<string> symbols, string context)
    {
        foreach (var s in symbols)
            if (!Elements.ContainsKey(s))
                throw new InvalidDataException($"{context}: unknown element '{s}'. Add data/elements/{s.ToLowerInvariant()}.json.");
    }
}
