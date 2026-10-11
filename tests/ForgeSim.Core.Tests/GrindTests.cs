using ForgeSim.Core.Process;
using static ForgeSim.Core.Tests.Fixtures;

namespace ForgeSim.Core.Tests;

/// <summary>§3.6 Grind.</summary>
public class GrindTests
{
    [Theory]
    [InlineData(1.0, 0.05)]
    [InlineData(0.8, 0.06)]
    [InlineData(0.0, 0.10)]
    public void Removes_more_material_for_a_rougher_finish(double finish, double removedMm)
    {
        var item = Cast(sn: 10);
        Apply(item, new GrindStep { ZoneId = "edge", EdgeAngleDeg = 30, Finish = finish });
        Assert.Equal(6.0 - removedMm, item.Zone("edge").ThicknessMm, 9);
    }

    [Fact]
    public void Records_edge_geometry_without_cold_work()
    {
        var item = Cast(sn: 10);
        item.Zone("edge").Strain = 0.2;
        Apply(item, new GrindStep { ZoneId = "edge", EdgeAngleDeg = 25, Finish = 0.9 });
        var edge = item.Zone("edge");
        Assert.Equal(25, edge.EdgeAngleDeg);
        Assert.Equal(0.9, edge.Finish);
        Assert.Equal(0.2, edge.Strain);
        Assert.Equal(12.0, item.Zone("shank").ThicknessMm);
    }
}
