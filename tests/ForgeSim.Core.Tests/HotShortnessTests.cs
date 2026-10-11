using ForgeSim.Core.Items;
using ForgeSim.Core.Process;
using ForgeSim.Core.Rng;
using static ForgeSim.Core.Tests.Fixtures;

namespace ForgeSim.Core.Tests;

/// <summary>§5 Hot-shortness and failure rules.</summary>
public class HotShortnessTests
{
    private static ItemInstance Annealed(double sn = 0, double pb = 0)
    {
        var item = Cast(sn: sn, pb: pb);
        Apply(item, Anneal(600, 30));
        return item;
    }

    private static int CrackingStrikes(ItemInstance item, double tempC, int strikes, ulong seed)
    {
        var rng = new SimRng(seed);
        int cracked = 0;
        for (int i = 0; i < strikes; i++)
        {
            double before = item.Zone("edge").CrackSeverity;
            Apply(item, Strike("edge", 0.5, tempC: tempC), rng);
            if (item.Zone("edge").CrackSeverity > before) cracked++;
            if (item.Zone("edge").CrackSeverity >= 1) break;
        }
        return cracked;
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(1234UL)]
    public void Behavioral_6_leaded_bronze_is_hot_short(ulong seed)
    {
        var item = Annealed(sn: 8, pb: 8);
        Apply(item, Strike("edge", 0.5, repeat: 10, tempC: 500), new SimRng(seed));
        Assert.True(item.Zone("edge").CrackSeverity > 0, $"seed {seed}: 8% Pb struck 10× at 500 °C did not crack");
    }

    [Fact]
    public void Lead_is_safe_below_its_melting_point()
    {
        // 8% Pb at 300 °C: lead is solid; only cold-work rules apply, and this is far below ε_max.
        var item = Annealed(sn: 0, pb: 8);
        Assert.Equal(0, CrackingStrikes(item, 300, 10, 4));
    }

    [Fact]
    public void Trace_lead_is_not_hot_short()
    {
        var item = Annealed(sn: 0, pb: 0.4);
        Assert.Equal(0, CrackingStrikes(item, 600, 40, 4));
    }

    [Fact]
    public void Tin_bronze_above_t_rx_cracks_at_spec_rate()
    {
        // 10% Sn at 600 °C (> T_rx 450): p = 0.03·(10 − 6) = 0.12 per strike.
        int cracks = 0, strikes = 0;
        for (ulong seed = 0; seed < 40; seed++)
        {
            var item = Annealed(sn: 10);
            cracks += CrackingStrikes(item, 600, 10, seed);
            strikes += 10;
        }
        double rate = (double)cracks / strikes;
        TestData.InRange(rate, 0.08, 0.16, "hot-short crack rate for 10% Sn at 600 °C (expected 0.12)");
    }

    [Fact]
    public void Low_tin_and_copper_hot_work_safely()
    {
        Assert.Equal(0, CrackingStrikes(Annealed(sn: 5), 600, 40, 9));
        Assert.Equal(0, CrackingStrikes(Annealed(sn: 0), 700, 40, 9));
    }

    [Fact]
    public void Beta_quenched_bronze_hot_works_in_its_window_only()
    {
        ItemInstance BetaGong()
        {
            var item = Cast(sn: 22);
            Apply(item, Anneal(700, 30));
            Apply(item, new QuenchStep { FromTempC = 700, Medium = QuenchMedium.Water });
            Assert.True(item.Material!.Phase.BetaQuenched);
            return item;
        }

        Assert.Equal(0, CrackingStrikes(BetaGong(), 650, 20, 11));
        Assert.True(CrackingStrikes(BetaGong(), 750, 20, 11) > 0, "outside 600–700 °C the tin rule applies (p ≈ 0.48)");
    }
}
