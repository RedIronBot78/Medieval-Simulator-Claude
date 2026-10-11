using ForgeSim.Core.Data;
using ForgeSim.Core.Items;
using ForgeSim.Core.Process;
using ForgeSim.Core.Properties;
using ForgeSim.Core.Rng;

namespace ForgeSim.Core.Performance;

/// <summary>§7 A kind of work, from data/uses/*.json.</summary>
public sealed class UseProfile
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    /// <summary>0–1</summary>
    public double ImpactLoad { get; init; }
    /// <summary>0–1</summary>
    public double Abrasion { get; init; }
    /// <summary>softwood 0.2, hardwood 0.5, stone 0.9</summary>
    public double TargetHardness { get; init; }
    public required string PrimaryZone { get; init; }
}

public enum UseOutcome { Dull, Bent, Chipped, Cracked, Broken }

public sealed record UseEvent(int Use, UseOutcome Outcome, string Detail);

public sealed record UseLog(
    string ProfileId,
    int UsesRequested,
    int UsesCompleted,
    bool Broken,
    double TotalEdgeWear,
    IReadOnlyList<UseEvent> Events,
    ItemInstance FinalState)
{
    public int Count(UseOutcome o) => Events.Count(e => e.Outcome == o);
}

/// <summary>
/// §7 Performance in use. Works on a copy of the item; the input is never modified. Deterministic for a seed:
/// every use draws exactly three random numbers (bend, chip, break).
/// </summary>
public static class UseSimulator
{
    public static UseLog Simulate(ForgeData data, ItemInstance item, UseProfile profile, int uses, SimRng rng)
    {
        var m = data.Model;
        var u = m.Use;
        var it = item.Clone();
        var mat = it.RequireMaterial("Simulate");
        var zone = it.Zone(profile.PrimaryZone);
        var events = new List<UseEvent>();
        double wear = 0, totalWear = 0;
        int done = 0;
        bool broken = false;

        for (int n = 1; n <= uses; n++)
        {
            double bendRoll = rng.NextDouble(), chipRoll = rng.NextDouble(), breakRoll = rng.NextDouble();
            var p = PropertyModel.DeriveZone(m, it, zone);
            double hv = p.Hardness, t = p.Toughness;

            // Sharpness lost; the owner regrinds when it goes dull.
            double dw = profile.Abrasion * profile.TargetHardness * u.WearPerUse / hv;
            wear += dw;
            totalWear += dw;
            if (wear >= u.DullThreshold)
            {
                events.Add(new UseEvent(n, UseOutcome.Dull, "edge dull, needs regrind"));
                wear = 0;
            }

            double bend = profile.ImpactLoad * Math.Max(0, u.BendHvRef - hv) / u.BendDivisor;
            if (bendRoll < bend) events.Add(new UseEvent(n, UseOutcome.Bent, "edge bent, straightened"));

            double chip = profile.ImpactLoad * profile.TargetHardness * Math.Max(0, u.ChipToughnessRef - t) / u.ChipDivisor;
            if (chipRoll < chip)
            {
                zone.ThicknessMm = Math.Max(0, zone.ThicknessMm - u.ChipLossMm);
                events.Add(new UseEvent(n, UseOutcome.Chipped, $"edge chipped, reground ({u.ChipLossMm:0.0} mm lost)"));
            }

            if (zone.CrackSeverity > 0)
            {
                var crack = it.GetOrAddDefect(DefectType.Crack, zone.Id);
                bool wasHidden = !crack.Revealed;
                zone.CrackSeverity = Math.Min(1, zone.CrackSeverity + profile.ImpactLoad * u.CrackGrowthPerImpact);
                crack.Severity = zone.CrackSeverity;
                if (wasHidden && zone.CrackSeverity >= m.Strike.CrackHiddenBelow)
                {
                    crack.Reveal("found in use");
                    events.Add(new UseEvent(n, UseOutcome.Cracked, $"crack in {zone.Id} now visible (severity {zone.CrackSeverity:0.00})"));
                }
            }

            double brk = profile.ImpactLoad
                         * (zone.CrackSeverity + mat.PorosityPct / u.BreakPorosityDivisor + PropertyModel.EffectiveDelta(m, mat))
                         * Math.Max(0, u.BreakToughnessRef - t) / u.BreakDivisor;
            done = n;
            if (breakRoll < brk)
            {
                broken = true;
                foreach (var d in it.Defects) d.Reveal("post-mortem after breaking");
                events.Add(new UseEvent(n, UseOutcome.Broken, "BROKE"));
                break;
            }
        }

        return new UseLog(profile.Id, uses, done, broken, totalWear, events, it);
    }
}
