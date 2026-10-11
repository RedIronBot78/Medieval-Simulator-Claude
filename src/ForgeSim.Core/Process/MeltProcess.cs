using ForgeSim.Core.Items;
using ForgeSim.Core.Materials;

namespace ForgeSim.Core.Process;

/// <summary>§3.1 Melt.</summary>
public static class MeltProcess
{
    public static void Apply(Forge forge, ItemInstance item, MeltStep step, List<string> log)
    {
        var m = forge.Model;
        var c = m.Melt;

        var charged = MixCharge(forge, step.Charge);
        double tl = MeltingRanges.Liquidus(m, charged);
        log.Add($"Melt charge {step.Charge.Sum(ci => ci.MassKg):0.000} kg → {charged} (TL {tl:0} °C)");

        if (step.PeakTempC < tl)
        {
            item.Failure = $"Charge not fully molten: peak {step.PeakTempC:0} °C is below liquidus {tl:0} °C. Nothing can be poured.";
            log.Add(item.Failure);
            return;
        }

        // Oxidation losses: fraction lost = rate · minutes · f(T) · coverFactor
        double fT = Math.Exp((step.PeakTempC - c.OxidationRefTempC) / c.OxidationTempScaleC);
        double cover = step.Cover == Cover.Charcoal ? c.CharcoalCoverOxidationFactor : 1.0;
        var comp = charged;
        foreach (var (symbol, wt) in charged.Alloying)
        {
            double rate = forge.Data.Elements[symbol].OxidationLossPerMinute;
            if (rate <= 0) continue;
            double lossFraction = Math.Clamp(rate * step.MinutesAtTemp * fT * cover, 0, 1);
            comp = comp.With(symbol, wt * (1 - lossFraction));
            log.Add($"  {symbol} oxidation loss {lossFraction * 100:0.0}% of charge: {wt:0.00}% → {wt * (1 - lossFraction):0.00}%");
        }

        // Gas pickup
        double gasCover = step.Cover == Cover.Charcoal ? c.CharcoalCoverGasFactor : 1.0;
        double gas = c.GasPerMinute * step.MinutesAtTemp * Math.Exp((step.PeakTempC - c.GasRefTempC) / c.GasTempScaleC) * gasCover;
        foreach (var (symbol, wt) in comp.Alloying)
            gas += forge.Data.Elements[symbol].GasPickupPerWt * wt;
        gas = Math.Clamp(gas, 0, c.GasMax);

        // Homogeneity
        double tau = step.Stirred ? c.HomogenizationTauStirredMin : c.HomogenizationTauUnstirredMin;
        double homogeneity = 1 - Math.Exp(-step.MinutesAboveLiquidus / tau);

        item.Material = new Material
        {
            Composition = comp,
            GasContent = gas,
            Homogeneity = homogeneity,
        };
        log.Add($"  Gas content {gas:0.00}, homogeneity {homogeneity:0.000}");

        if (homogeneity < c.SegregationBelow)
        {
            var d = item.GetOrAddDefect(DefectType.Segregation);
            d.Severity = c.SegregationBelow - homogeneity;
            log.Add($"  Segregation (severity {d.Severity:0.00})");
        }
    }

    /// <summary>Mass-weighted mix of the charge. Each charge item's base element is the balance of its composition.</summary>
    public static Composition MixCharge(Forge forge, IReadOnlyList<ChargeItem> charge)
    {
        var baseEl = forge.Model.BaseElement;
        if (charge.Count == 0) throw new ArgumentException("Melt charge is empty.");

        var massByElement = new SortedDictionary<string, double>(StringComparer.Ordinal);
        double total = 0;
        foreach (var ci in charge)
        {
            if (ci.MassKg <= 0) throw new ArgumentException($"Charge item mass must be positive, got {ci.MassKg}.");
            forge.Data.RequireKnownElements(ci.Composition.Keys, "Melt charge");
            var c = Composition.FromWt(baseEl, ci.Composition);
            foreach (var (symbol, wt) in c.All())
                massByElement[symbol] = massByElement.GetValueOrDefault(symbol) + ci.MassKg * wt / 100.0;
            total += ci.MassKg;
        }
        return Composition.FromWt(baseEl, massByElement.Select(kv => new KeyValuePair<string, double>(kv.Key, kv.Value / total * 100.0)));
    }
}
