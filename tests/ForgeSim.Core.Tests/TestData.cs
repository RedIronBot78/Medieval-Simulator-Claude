using ForgeSim.Core.Data;
using ForgeSim.Core.Materials;

namespace ForgeSim.Core.Tests;

/// <summary>Loads the real data/ folder once. Tests run against the shipped constants, not copies.</summary>
public static class TestData
{
    public static readonly ForgeData Data = ForgeData.LoadDefault();
    public static MaterialModel Model => Data.Model;

    public static Composition Comp(double sn = 0, double @as = 0, double pb = 0, double zn = 0, double fe = 0, double s = 0) =>
        Composition.FromWt("Cu", new Dictionary<string, double>
        {
            ["Sn"] = sn, ["As"] = @as, ["Pb"] = pb, ["Zn"] = zn, ["Fe"] = fe, ["S"] = s,
        });

    /// <summary>Asserts value is in [lo, hi] and prints the inputs on failure, as the spec requires.</summary>
    public static void InRange(double value, double lo, double hi, string what)
    {
        Assert.True(value >= lo && value <= hi, $"{what}: computed {value:0.####}, expected {lo}–{hi}.");
    }
}
