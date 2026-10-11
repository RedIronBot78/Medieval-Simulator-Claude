using ForgeSim.Core.Data;
using ForgeSim.Core.Items;
using ForgeSim.Core.Rng;

namespace ForgeSim.Core.Process;

/// <summary>
/// Applies process steps to items. The only entry point that changes an item's material state.
/// Each call appends the step to the item's history and returns human-readable log lines.
/// </summary>
public sealed class Forge
{
    public ForgeData Data { get; }
    public MaterialModel Model => Data.Model;

    public Forge(ForgeData data) => Data = data;

    public ItemInstance NewItem(string templateId) => new() { TemplateId = templateId };

    public IReadOnlyList<string> Apply(ItemInstance item, ProcessStep step, SimRng rng)
    {
        if (item.Failed)
            throw new InvalidOperationException($"Cannot apply {step.Type}: item already failed ({item.Failure}).");

        item.History.Add(step);
        var log = new List<string>();
        switch (step)
        {
            case MeltStep s: MeltProcess.Apply(this, item, s, log); break;
            case CastStep s: CastProcess.Apply(this, item, s, log); break;
            case AnnealStep s: AnnealProcess.Apply(this, item, s, log); break;
            case StrikeStep s: StrikeProcess.Apply(this, item, s, rng, log); break;
            case QuenchStep s: QuenchProcess.Apply(this, item, s, rng, log); break;
            case GrindStep s: GrindProcess.Apply(this, item, s, log); break;
            default: throw new NotSupportedException($"Unknown step type {step.Type}.");
        }
        return log;
    }
}
