using ForgeSim.Core.Items;

namespace ForgeSim.Core.Process;

/// <summary>§3.6 Grind. Removes 0.05·(1 + 1 − Finish) mm and records edge geometry. No cold work.</summary>
public static class GrindProcess
{
    public static void Apply(Forge forge, ItemInstance item, GrindStep step, List<string> log)
    {
        item.RequireMaterial("Grind");
        var zone = item.Zone(step.ZoneId);
        double removed = forge.Model.Grind.RemovalMm * (1 + 1 - Math.Clamp(step.Finish, 0, 1));
        double before = zone.ThicknessMm;
        zone.ThicknessMm = Math.Max(0, zone.ThicknessMm - removed);
        zone.EdgeAngleDeg = step.EdgeAngleDeg;
        zone.Finish = step.Finish;
        log.Add($"Grind {zone.Id} to {step.EdgeAngleDeg:0}° included, finish {step.Finish:0.00}: {before:0.00} → {zone.ThicknessMm:0.00} mm");
    }
}
