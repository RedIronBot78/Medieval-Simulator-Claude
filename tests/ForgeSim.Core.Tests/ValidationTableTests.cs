using ForgeSim.Core.Materials;
using ForgeSim.Core.Process;
using ForgeSim.Core.Properties;

namespace ForgeSim.Core.Tests;

/// <summary>
/// §9 validation table. States are built directly from each row's description.
/// Convention where a row is silent: "annealed" = homogenized (δ = δ_eq, not cored), 40 µm, 0% porosity;
/// "as cast" = cored, as-cast δ, 60 µm, 1% porosity (the conditions row 3 states explicitly).
/// </summary>
public class ValidationTableTests
{
    private static Material Annealed(Composition c) => new()
    {
        Composition = c,
        GrainSizeUm = 40,
        PorosityPct = 0,
        Phase = new PhaseState { Cored = false, Delta = DeltaEq(c) },
    };

    private static Material AsCast(Composition c) => new()
    {
        Composition = c,
        GrainSizeUm = 60,
        PorosityPct = 1,
        Phase = new PhaseState { Cored = true, Delta = CastProcess.AsCastDelta(TestData.Model, c) },
    };

    // δ_eq = min(0.6, max(0, Sn − 14) · 0.05) (§3.4)
    private static double DeltaEq(Composition c) => Math.Min(0.6, Math.Max(0, c["Sn"] - 14) * 0.05);

    public sealed record Row(string Case, Material Mat, double Strain,
        double HvLo, double HvHi, double TLo, double THi, double EpsLo, double EpsHi);

    public static TheoryData<string> Cases => new(Rows.Keys);

    private static readonly Dictionary<string, Row> Rows = new[]
    {
        new Row("Pure Cu, annealed, 40 µm",          Annealed(TestData.Comp()),            0.0, 45, 60,  90, 100, 0.85, 0.85),
        new Row("Pure Cu, cold worked ε = 0.5",      Annealed(TestData.Comp()),            0.5, 110, 150, 75, 95,  0.85, 0.85),
        new Row("10% Sn, as cast (60 µm, 1% por.)",  AsCast(TestData.Comp(sn: 10)),        0.0, 90, 120, 60, 80,  0.40, 0.50),
        new Row("10% Sn, annealed (homog., 40 µm)",  Annealed(TestData.Comp(sn: 10)),      0.0, 85, 110, 80, 100, 0.50, 0.60),
        new Row("10% Sn, annealed then ε = 0.5",     Annealed(TestData.Comp(sn: 10)),      0.5, 205, 270, 55, 75, 0.50, 0.60),
        new Row("4% As, annealed then ε = 0.5",      Annealed(TestData.Comp(@as: 4)),      0.5, 170, 230, 62, 82, 0.80, 0.90),
        new Row("6% Sn, annealed then ε = 0.5",      Annealed(TestData.Comp(sn: 6)),       0.5, 165, 225, 63, 83, 0.60, 0.72),
        new Row("22% Sn, as cast",                   AsCast(TestData.Comp(sn: 22)),        0.0, 145, 195, 0, 42,  double.NegativeInfinity, 0.15),
        new Row("22% Sn, annealed",                  Annealed(TestData.Comp(sn: 22)),      0.0, 180, double.PositiveInfinity, 0, 28, double.NegativeInfinity, 0.0),
        new Row("8% Sn 8% Pb, as cast",              AsCast(TestData.Comp(sn: 8, pb: 8)),  0.0, 65, 90,  52, 72,  0.32, 0.44),
    }.ToDictionary(r => r.Case);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Row_matches_spec(string @case)
    {
        var r = Rows[@case];
        var m = TestData.Model;
        double hv = PropertyModel.Hardness(m, r.Mat, r.Strain);
        double t = PropertyModel.Toughness(m, r.Mat, hv, 0, 0, 0);
        double eps = PropertyModel.EpsMax(m, r.Mat);
        string inputs = $"[{r.Mat.Composition}, δ={r.Mat.Phase.Delta:0.000}, cored={r.Mat.Phase.Cored}, grain={r.Mat.GrainSizeUm}µm, por={r.Mat.PorosityPct}%, ε={r.Strain}]";

        TestData.InRange(hv, r.HvLo, r.HvHi, $"{@case} HV {inputs}");
        TestData.InRange(t, r.TLo, r.THi, $"{@case} toughness {inputs} (HV {hv:0.0})");
        TestData.InRange(eps, r.EpsLo - 1e-9, r.EpsHi + 1e-9, $"{@case} ε_max {inputs}");
    }
}
