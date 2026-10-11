using System.Text;
using ForgeSim.Core.Data;
using ForgeSim.Core.Items;
using ForgeSim.Core.Performance;
using ForgeSim.Core.Properties;
using ForgeSim.Core.Recipes;
using ForgeSim.Core.Rng;

namespace ForgeSim.Cli;

/// <summary>Use simulation averaged over several seeds.</summary>
public sealed record UseStats(string Profile, int Uses, int Seeds, int Broke, double MeanLife,
    double Dull, double Bent, double Chipped, int CrackFound, double WearPerUse);

/// <summary>Formats one recipe run for a human reading it like a smith.</summary>
public sealed class RecipeReport
{
    private const int Width = 92;
    private readonly ForgeData _data;
    public RecipeRun Run { get; }
    public ItemProperties? Props { get; }
    public FitnessReport? Fit { get; }
    public UseStats? Use { get; }

    public RecipeReport(ForgeData data, RecipeRun run, UseProfile? profile, int uses, int seeds)
    {
        _data = data;
        Run = run;
        if (run.Item.Failed || run.Item.Zones.Count == 0) return;
        Props = PropertyModel.Derive(data, run.Item);
        Fit = Fitness.Evaluate(data, run.Item);
        if (profile != null && run.Item.Zones.Any(z => z.Id == profile.PrimaryZone))
            Use = SimulateUse(data, run, profile, uses, seeds);
    }

    private static UseStats SimulateUse(ForgeData data, RecipeRun run, UseProfile profile, int uses, int seeds)
    {
        int broke = 0, found = 0;
        double life = 0, dull = 0, bent = 0, chipped = 0, wear = 0;
        for (int s = 0; s < seeds; s++)
        {
            var log = UseSimulator.Simulate(data, run.Item, profile, uses, new SimRng(run.Recipe.Seed + (ulong)s));
            if (log.Broken) broke++;
            found += log.Count(UseOutcome.Cracked);
            life += log.UsesCompleted;
            dull += log.Count(UseOutcome.Dull);
            bent += log.Count(UseOutcome.Bent);
            chipped += log.Count(UseOutcome.Chipped);
            wear += log.TotalEdgeWear / Math.Max(1, log.UsesCompleted);
        }
        return new UseStats(profile.Id, uses, seeds, broke, life / seeds, dull / seeds, bent / seeds, chipped / seeds, found, wear / seeds);
    }

    public string Full()
    {
        var sb = new StringBuilder();
        var item = Run.Item;
        var template = _data.Template(item.TemplateId);

        sb.AppendLine(new string('═', Width));
        sb.AppendLine($"{Run.Recipe.Id} — {template.Name} ({template.Id}), seed {Run.Recipe.Seed}");
        if (Run.Recipe.Description.Length > 0) sb.AppendLine(Wrap(Run.Recipe.Description, Width, ""));
        sb.AppendLine(new string('─', Width));

        // Composition
        if (item.Material is { } mat)
        {
            sb.AppendLine($"COMPOSITION  {string.Join("  ", mat.Composition.All().Select(kv => $"{kv.Key} {kv.Value:0.00}%"))}");
            if (Props != null)
                sb.AppendLine($"             liquidus {Props.Bulk.LiquidusC:0} °C · solidus {Props.Bulk.SolidusC:0} °C · recrystallization {Props.Bulk.RecrystallizationC:0} °C");
        }
        sb.AppendLine();

        // Process
        sb.AppendLine("PROCESS");
        foreach (var step in Run.Log)
        {
            var label = $"  {step.Index + 1,2}  ";
            for (int i = 0; i < step.Lines.Count; i++)
                sb.AppendLine((i == 0 ? label : new string(' ', label.Length)) + step.Lines[i].TrimStart());
        }
        if (Run.StepsSkipped > 0) sb.AppendLine($"  ({Run.StepsSkipped} later step(s) skipped)");
        sb.AppendLine();

        if (item.Failed)
        {
            sb.AppendLine($"FAILED: {item.Failure}");
            sb.AppendLine();
            return sb.ToString();
        }
        if (Props == null || Fit == null || item.Material == null) return sb.ToString();

        var m = item.Material;
        var ph = m.Phase;
        sb.AppendLine($"MATERIAL     grain {m.GrainSizeUm:0} µm · porosity {m.PorosityPct:0.00}% · gas {m.GasContent:0.00} · homogeneity {m.Homogeneity:0.000}");
        sb.AppendLine($"             δ {ph.Delta:0.000}{(ph.Cored ? " · cored" : "")}{(ph.BetaQuenched ? " · β-quenched" : "")}{(ph.SolutionQuenched ? " · solution-quenched" : "")}");
        var b = Props.Bulk;
        sb.AppendLine($"BULK         corrosion {b.CorrosionResistance:0} · castability {b.Castability:0} · acoustic {b.AcousticQuality:0.00} · leaks {(b.Leaks ? "YES" : "no")} · colour {Hex(b.ColorRgb)}");
        sb.AppendLine();

        // Zones
        sb.AppendLine("ZONES        thick (target)    ε     ε_max   HV   tough  edge-ret  shape  crack  folds");
        foreach (var z in Props.Zones)
        {
            var zt = template.Zones.First(t => t.Id == z.ZoneId);
            sb.AppendLine($"  {z.ZoneId,-10} {z.ThicknessMm,5:0.00} ({zt.TargetThicknessMm,5:0.00})  {z.Strain,5:0.00}  {z.EpsMax,5:0.00}  {z.Hardness,4:0}  {z.Toughness,5:0}  {z.EdgeRetention,8:0}  {z.ShapeError,5:0.00}  {z.CrackSeverity,5:0.00}  {z.FoldCount,5}");
        }
        sb.AppendLine();

        // Fitness
        sb.AppendLine($"FITNESS {Fit.Score:0.00}");
        foreach (var r in Fit.Results)
        {
            var q = r.Requirement;
            string bound = (q.Min, q.Max) switch
            {
                ({ } lo, { } hi) => $"{lo:0.##}–{hi:0.##}",
                ({ } lo, null) => $"≥ {lo:0.##}",
                (null, { } hi) => $"≤ {hi:0.##}",
                _ => "",
            };
            sb.AppendLine($"  {(r.Met ? "✓" : "✗")} {(q.Zone ?? "bulk") + " " + q.Property,-26} {r.Value,7:0.00}   need {bound,-10} (weight {q.Weight:0.0})");
        }
        sb.AppendLine();

        // Defects
        sb.AppendLine("DEFECTS");
        var revealed = item.Defects.Where(d => d.Revealed).ToList();
        var hidden = item.Defects.Where(d => !d.Revealed).ToList();
        sb.AppendLine(revealed.Count == 0 ? "  Revealed: none" : "  Revealed (the player can see these):");
        foreach (var d in revealed) sb.AppendLine($"    • {Describe(d)} — {d.RevealedBy}");
        sb.AppendLine(hidden.Count == 0 ? "  HIDDEN: none" : "  HIDDEN (sim only — the player cannot see these until inspected, tested or failed):");
        foreach (var d in hidden) sb.AppendLine($"    • {Describe(d)}");
        sb.AppendLine();

        if (Use is { } u)
        {
            sb.AppendLine($"IN USE       {u.Profile}, {u.Uses} uses × {u.Seeds} seeds");
            sb.AppendLine($"             broke {u.Broke}/{u.Seeds} (mean life {u.MeanLife:0} uses) · per item: dull {u.Dull:0.0}, bent {u.Bent:0.0}, chipped {u.Chipped:0.0}");
            sb.AppendLine($"             edge wear {u.WearPerUse:0.0000}/use · hidden cracks found in use: {u.CrackFound}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string SideBySide(IReadOnlyList<RecipeReport> reports)
    {
        const int labelW = 22;
        int colW = Math.Max(17, reports.Max(r => r.Run.Recipe.Id.Length) + 2);
        var sb = new StringBuilder();
        int width = labelW + colW * reports.Count;
        sb.AppendLine(new string('═', width));
        sb.AppendLine("SIDE BY SIDE (first zone of each item; ✗ = fails a template requirement)");
        sb.AppendLine(new string('─', width));

        void Row(string label, Func<RecipeReport, string> cell, bool evenIfFailed = false) =>
            sb.AppendLine(label.PadRight(labelW) + string.Concat(reports.Select(r =>
                Trunc(evenIfFailed ? cell(r) : SafeCell(r, cell), colW - 1).PadRight(colW))));

        Row("recipe", r => r.Run.Recipe.Id, evenIfFailed: true);
        Row("template", r => r.Run.Item.TemplateId, evenIfFailed: true);
        Row("status", r => r.Run.Item.Failed ? "FAILED" : "ok", evenIfFailed: true);
        Row("Sn / As / Pb wt%", r => r.Run.Item.Material is { } m ? $"{m.Composition["Sn"]:0.0}/{m.Composition["As"]:0.0}/{m.Composition["Pb"]:0.0}" : "-");
        Row("porosity %", r => $"{r.Run.Item.Material!.PorosityPct:0.00}");
        Row("grain µm", r => $"{r.Run.Item.Material!.GrainSizeUm:0}");
        Row("δ / cored", r => $"{r.Run.Item.Material!.Phase.Delta:0.000}{(r.Run.Item.Material.Phase.Cored ? " cored" : "")}");
        Row("zone", r => r.Props!.Zones[0].ZoneId);
        Row("  thickness mm", r => $"{r.Props!.Zones[0].ThicknessMm:0.00}");
        Row("  ε / ε_max", r => $"{r.Props!.Zones[0].Strain:0.00} / {r.Props.Zones[0].EpsMax:0.00}");
        Row("  HV", r => $"{r.Props!.Zones[0].Hardness:0}");
        Row("  toughness", r => $"{r.Props!.Zones[0].Toughness:0}");
        Row("  edge retention", r => $"{r.Props!.Zones[0].EdgeRetention:0}");
        Row("  crack severity", r => $"{r.Props!.Zones[0].CrackSeverity:0.00}");
        Row("acoustic", r => $"{r.Props!.Bulk.AcousticQuality:0.00}");
        Row("castability", r => $"{r.Props!.Bulk.Castability:0}");
        Row("leaks", r => r.Props!.Bulk.Leaks ? "YES" : "no");
        Row("fitness", r => $"{r.Fit!.Score:0.00}" + (r.Fit.Results.Any(x => !x.Met) ? " ✗" : " ✓"));
        Row("defects shown", r => r.Run.Item.Defects.Count(d => d.Revealed).ToString());
        Row("defects HIDDEN", r => r.Run.Item.Defects.Count(d => !d.Revealed).ToString());
        if (reports.Any(r => r.Use != null))
        {
            Row("use profile", r => r.Use?.Profile ?? "n/a");
            Row("  broke", r => r.Use is { } u ? $"{u.Broke}/{u.Seeds}" : "-");
            Row("  mean life (uses)", r => r.Use is { } u ? $"{u.MeanLife:0}" : "-");
            Row("  dull / bent / chip", r => r.Use is { } u ? $"{u.Dull:0.0}/{u.Bent:0.0}/{u.Chipped:0.0}" : "-");
            Row("  wear per use", r => r.Use is { } u ? $"{u.WearPerUse:0.0000}" : "-");
        }
        sb.AppendLine();
        return sb.ToString();
    }

    private static string SafeCell(RecipeReport r, Func<RecipeReport, string> cell)
    {
        if (r.Run.Item.Failed) return "FAILED";
        try { return cell(r); } catch (NullReferenceException) { return "-"; }
    }

    private static string Describe(Defect d)
    {
        string where = d.ZoneId == null ? "" : $" in {d.ZoneId}";
        string sev = d.Type switch
        {
            DefectType.Porosity => $"{d.Severity:0.00}%",
            DefectType.Fold => "",
            DefectType.LeadSweat => $"{d.Severity:0.00} wt% Pb lost",
            _ => $"severity {d.Severity:0.00}",
        };
        return $"{d.Type}{where}{(sev.Length > 0 ? " (" + sev + ")" : "")}";
    }

    private static string Hex(double[] rgb) =>
        "#" + string.Concat(rgb.Select(c => ((int)Math.Round(Math.Clamp(c, 0, 1) * 255)).ToString("X2")));

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";

    private static string Wrap(string text, int width, string indent)
    {
        var sb = new StringBuilder();
        var line = new StringBuilder(indent);
        foreach (var word in text.Split(' '))
        {
            if (line.Length + word.Length + 1 > width && line.Length > indent.Length)
            {
                sb.AppendLine(line.ToString());
                line.Clear().Append(indent);
            }
            if (line.Length > indent.Length) line.Append(' ');
            line.Append(word);
        }
        sb.Append(line);
        return sb.ToString();
    }
}
