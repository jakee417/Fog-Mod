#nullable enable
using System.Collections.Generic;
using FogMod.Models;
using Microsoft.Xna.Framework;
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

    private static FogParticle At(float x, float y, bool fading = false, float age = 0f)
    {
        return new FogParticle(null!, 1f, 0f, new Vector2(x, y), Vector2.Zero,
            1f, age, fading, 5f);
    }

    [Fact]
    public void ComputeCellOccupancy_CountsPerCell()
    {
        var grid = new Grid(256, 256, 0, 0, 128f, 0);
        var particles = new List<FogParticle> { At(10f, 10f), At(20f, 20f), At(200f, 10f) };
        var occupancy = CellOccupancy.ComputeCellOccupancy(particles, grid);
        Assert.Equal(2, occupancy.Counts[0, 0]);
        Assert.Equal(1, occupancy.Counts[1, 0]);
        Assert.Equal(new[] { 0, 1 }, occupancy.Indices[0, 0]!.ToArray());
        Assert.Equal(new[] { 2 }, occupancy.Indices[1, 0]!.ToArray());
    }

    [Fact]
    public void ComputeCellOccupancy_FadingCountedButNotIndexed()
    {
        var grid = new Grid(256, 256, 0, 0, 128f, 0);
        var particles = new List<FogParticle> { At(10f, 10f, fading: true) };
        var occupancy = CellOccupancy.ComputeCellOccupancy(particles, grid);
        Assert.Equal(1, occupancy.Counts[0, 0]);
        Assert.Null(occupancy.Indices[0, 0]);
    }

    [Fact]
    public void ComputeCellOccupancy_SkipsOffGrid()
    {
        var grid = new Grid(256, 256, 0, 0, 128f, 0);
        var particles = new List<FogParticle> { At(9999f, 9999f) };
        var occupancy = CellOccupancy.ComputeCellOccupancy(particles, grid);
        Assert.Equal(0, occupancy.Counts[0, 0]);
        Assert.Equal(0, occupancy.Counts[1, 1]);
    }

    [Fact]
    public void FadeExtraSmoke_FadesOldestOverTarget()
    {
        var grid = new Grid(256, 256, 0, 0, 128f, 0);
        var smoke = new List<FogParticle>();
        for (int i = 0; i < Constants.MaximumSmokeParticlesPerCell + 2; i++)
            smoke.Add(At(10f, 10f, age: i));
        var occupancy = CellOccupancy.ComputeCellOccupancy(smoke, grid);
        occupancy.FadeExtraSmoke(smoke, grid.ExtCols, grid.ExtRows);
        int fading = 0;
        for (int i = 0; i < smoke.Count; i++)
        {
            if (smoke[i].IsFadingOut)
            {
                fading++;
                Assert.True(i >= Constants.MaximumSmokeParticlesPerCell,
                    $"young particle {i} faded instead of an elder");
                Assert.Equal(Constants.ParticleFadeOutSeconds, smoke[i].FadeOutSecondsLeft);
            }
        }
        Assert.Equal(2, fading);
    }
}
