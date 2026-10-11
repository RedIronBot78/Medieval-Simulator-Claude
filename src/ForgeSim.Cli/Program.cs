using System.Globalization;
using System.Text;
using ForgeSim.Cli;
using ForgeSim.Core.Data;
using ForgeSim.Core.Recipes;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
Console.OutputEncoding = Encoding.UTF8;

var recipes = new List<string>();
string? useProfile = null;
int uses = 500, seeds = 50;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--use": useProfile = args[++i]; break;
        case "--uses": uses = int.Parse(args[++i]); break;
        case "--seeds": seeds = int.Parse(args[++i]); break;
        case "-h" or "--help":
            PrintUsage();
            return 0;
        default: recipes.Add(args[i]); break;
    }
}

if (recipes.Count == 0)
{
    PrintUsage();
    return 1;
}

try
{
    var data = ForgeData.Load(FindDataRoot(recipes[0]));
    var profile = useProfile == null ? null : data.Use(useProfile);

    var reports = recipes.Select(path => new RecipeReport(data, RecipeRunner.Run(data, Recipe.Load(path)), profile, uses, seeds)).ToList();
    foreach (var r in reports) Console.Write(r.Full());
    if (reports.Count > 1) Console.Write(RecipeReport.SideBySide(reports));
    return 0;
}
catch (Exception e) when (e is IOException or InvalidDataException or KeyNotFoundException or System.Text.Json.JsonException or ArgumentException)
{
    Console.Error.WriteLine($"error: {e.Message}");
    return 2;
}

// data/ is looked for above the recipe, then above the working directory, then above the app.
static string FindDataRoot(string recipePath)
{
    foreach (var start in new[] { Path.GetDirectoryName(Path.GetFullPath(recipePath)), Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        try { return ForgeData.FindRoot(start); }
        catch (DirectoryNotFoundException) { }
    }
    throw new DirectoryNotFoundException("No data/ folder found above the recipe, the working directory or the app.");
}

static void PrintUsage()
{
    Console.WriteLine("""
        ForgeSim — run recipe JSON files through the bronze-tier material model and print a property report.

        usage: dotnet run --project src/ForgeSim.Cli -- <recipe.json> [more.json ...] [options]

          --use <profile>   also simulate use (e.g. carving_hardwood, chopping_softwood, cutting_stone)
          --uses <n>        uses per simulation (default 500)
          --seeds <n>       simulations to average over (default 50)

        Several recipes print each report, then a side-by-side comparison.
        """);
}
