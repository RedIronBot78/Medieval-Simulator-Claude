using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeSim.Core.Process;

/// <summary>Reads/writes <see cref="ProcessStep"/> using its "type" field. "type" may appear anywhere in the object.</summary>
public sealed class ProcessStepConverter : JsonConverter<ProcessStep>
{
    private static readonly Dictionary<string, Type> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Melt"] = typeof(MeltStep),
        ["Cast"] = typeof(CastStep),
        ["Strike"] = typeof(StrikeStep),
        ["Anneal"] = typeof(AnnealStep),
        ["Quench"] = typeof(QuenchStep),
        ["Grind"] = typeof(GrindStep),
    };

    public override ProcessStep Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        string? name = null;
        foreach (var p in doc.RootElement.EnumerateObject())
            if (string.Equals(p.Name, "type", StringComparison.OrdinalIgnoreCase)) name = p.Value.GetString();
        if (name == null) throw new JsonException("Process step is missing \"type\".");
        if (!Types.TryGetValue(name, out var concrete))
            throw new JsonException($"Unknown process step type '{name}'. Known: {string.Join(", ", Types.Keys)}.");
        return (ProcessStep)(doc.RootElement.Deserialize(concrete, options)
                             ?? throw new JsonException($"Step '{name}' deserialized to null."));
    }

    public override void Write(Utf8JsonWriter writer, ProcessStep value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}
