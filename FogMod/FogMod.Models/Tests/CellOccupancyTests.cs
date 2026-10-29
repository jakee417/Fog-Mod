#nullable enable
using System.Collections.Generic;
using FogMod.Models;
using Xunit;

namespace FogMod.Tests;

public class CellOccupancyTests
{
    [Fact]
    public void Default_IsInvalid()
    {
        Assert.False(default(CellOccupancy).IsValid);
    }

    [Fact]
    public void Constructed_IsValid()
    {
        var occupancy = new CellOccupancy(new int[2, 3], new List<int>[2, 3]);
        Assert.True(occupancy.IsValid);
    }

    [Fact]
    public void ComputeCellOccupancy_EmptyInput_StaysValidAndEmpty()
    {
        var occupancy = CellOccupancy.ComputeCellOccupancy(
            new List<FogParticle>(), default);
        Assert.True(occupancy.IsValid);
        Assert.Empty(occupancy.Counts);
    }
}
