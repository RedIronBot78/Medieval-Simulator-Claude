using ForgeSim.Core.Materials;
using ForgeSim.Core.Rng;

namespace ForgeSim.Core.Tests;

public class FoundationTests
{
    [Fact]
    public void Rng_same_seed_same_sequence()
    {
        var a = new SimRng(1234);
        var b = new SimRng(1234);
        for (int i = 0; i < 1000; i++) Assert.Equal(a.NextUInt64(), b.NextUInt64());
    }

    [Fact]
    public void Rng_known_first_values_are_stable()
    {
        // Pinned so an accidental change to the generator is caught; changing it breaks every saved seed.
        var r = new SimRng(0);
        Assert.Equal(0xE220A8397B1DCDAFUL, r.NextUInt64());
        Assert.Equal(0x6E789E6AA1B965F4UL, r.NextUInt64());
    }

    [Fact]
    public void Rng_doubles_are_in_unit_interval()
    {
        var r = new SimRng(42);
        for (int i = 0; i < 10000; i++)
        {
            var d = r.NextDouble();
            Assert.InRange(d, 0.0, 0.9999999999999999);
        }
    }

    [Fact]
    public void Composition_base_is_balance()
    {
        var c = TestData.Comp(sn: 10, pb: 2);
        Assert.Equal(88.0, c["Cu"], 9);
        Assert.Equal(10.0, c["Sn"]);
        Assert.Equal(0.0, c["As"]);
    }

    [Fact]
    public void Composition_rejects_over_100()
    {
        Assert.Throws<ArgumentException>(() => TestData.Comp(sn: 60, pb: 50));
    }

    [Fact]
    public void Data_loads_all_bronze_tier_elements()
    {
        foreach (var s in new[] { "Cu", "Sn", "As", "Pb", "Zn", "Fe", "S" })
            Assert.True(TestData.Data.Elements.ContainsKey(s), $"missing element {s}");
        Assert.Equal("Cu", TestData.Model.BaseElement);
    }
}
