using ForgeSim.Core.Materials;

namespace ForgeSim.Core.Tests;

/// <summary>§2 table.</summary>
public class MeltingRangeTests
{
    [Theory]
    [InlineData(0, 1085, 1085, 350)]
    [InlineData(6, 1031, 947, 410)]
    [InlineData(10, 995, 855, 450)]
    [InlineData(14, 959, 798, 490)]
    [InlineData(22, 887, 798, 490)]
    public void Tin_bronze_table(double sn, double tl, double ts, double trx)
    {
        var c = TestData.Comp(sn: sn);
        Assert.Equal(tl, MeltingRanges.Liquidus(TestData.Model, c), 6);
        Assert.Equal(ts, MeltingRanges.Solidus(TestData.Model, c), 6);
        Assert.Equal(trx, MeltingRanges.Recrystallization(TestData.Model, c), 6);
    }

    [Fact]
    public void Liquidus_includes_as_pb_zn()
    {
        var c = TestData.Comp(@as: 2, pb: 5, zn: 1);
        Assert.Equal(1085 - 24 - 15 - 6, MeltingRanges.Liquidus(TestData.Model, c), 6);
    }

    [Fact]
    public void Liquidus_floor_is_800()
    {
        Assert.Equal(800, MeltingRanges.Liquidus(TestData.Model, TestData.Comp(sn: 40)), 6);
    }

    [Fact]
    public void Solidus_includes_arsenic()
    {
        Assert.Equal(1085 - 120, MeltingRanges.Solidus(TestData.Model, TestData.Comp(@as: 4)), 6);
    }

    [Fact]
    public void Recrystallization_uses_capped_tin_and_arsenic()
    {
        Assert.Equal(350 + 140 + 32, MeltingRanges.Recrystallization(TestData.Model, TestData.Comp(sn: 20, @as: 4)), 6);
    }
}
