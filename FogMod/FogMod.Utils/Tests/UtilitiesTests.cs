#nullable enable
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Xunit;

namespace FogMod.Tests;

public class UtilitiesTests
{
    [Fact]
    public void Hash01_StaysInUnitRange()
    {
        for (int seed = -1000; seed <= 1000; seed++)
        {
            float h = Utils.Utilities.Hash01(seed);
            Assert.InRange(h, 0f, 1f);
        }
    }

    [Fact]
    public void Hash01_IsDeterministic()
    {
        Assert.Equal(Utils.Utilities.Hash01(42), Utils.Utilities.Hash01(42));
        Assert.Equal(Utils.Utilities.Hash01(-7), Utils.Utilities.Hash01(-7));
    }

    [Fact]
    public void Hash01_SpreadsDistinctSeeds()
    {
        var hashes = new HashSet<float>();
        for (int seed = 0; seed < 256; seed++)
            hashes.Add(Utils.Utilities.Hash01(seed));
        Assert.True(hashes.Count >= 250, $"only {hashes.Count}/256 distinct hashes");
    }

    [Fact]
    public void DeterministicBool_IsDeterministic()
    {
        var pos = new Vector2(12f, 34f);
        Assert.Equal(
            Utils.Utilities.DeterministicBool(pos, 3),
            Utils.Utilities.DeterministicBool(pos, 3));
    }

    [Fact]
    public void DeterministicBool_VariesAcrossInputs()
    {
        var outcomes = new HashSet<bool>();
        for (int i = 0; i < 100; i++)
            outcomes.Add(Utils.Utilities.DeterministicBool(new Vector2(i, i * 7), i));
        Assert.Contains(true, outcomes);
        Assert.Contains(false, outcomes);
    }

    [Fact]
    public void ApplyMomentumThruTurn_ZeroTurn_KeepsVelocity()
    {
        var vel = new Vector2(5f, -2f);
        var result = Utils.Utilities.ApplyMomentumThruTurn(
            new Vector2(100f, 100f), 10f, Vector2.Zero, vel, 0f);
        Assert.Equal(vel, result);
    }

    [Fact]
    public void ApplyMomentumThruTurn_FullTurn_PointsAtTarget()
    {
        var result = Utils.Utilities.ApplyMomentumThruTurn(
            new Vector2(3f, 4f), 10f, Vector2.Zero, Vector2.Zero, 1f);
        Assert.InRange(result.X, 5.999f, 6.001f);
        Assert.InRange(result.Y, 7.999f, 8.001f);
    }

    [Fact]
    public void ApplyMomentumThruTurn_HalfTurn_LandsOnSegment()
    {
        var from = new Vector2(10f, 0f);
        var result = Utils.Utilities.ApplyMomentumThruTurn(
            new Vector2(0f, 10f), 10f, Vector2.Zero, from, 0.5f);
        Assert.InRange(result.X, 4.999f, 5.001f);
        Assert.InRange(result.Y, 4.999f, 5.001f);
    }

    [Fact]
    public void GetDeterministicId_StaysNonNegative()
    {
        for (int i = -50; i <= 50; i++)
        {
            int id = Utils.Utilities.GetDeterministicId(i, i * 31, new Vector2(i, -i), null);
            Assert.True(id >= 0, $"negative id {id} for seed {i}");
        }
    }

    [Fact]
    public void GetDeterministicId_IsDeterministic()
    {
        var pos = new Vector2(9f, 3f);
        Assert.Equal(
            Utils.Utilities.GetDeterministicId(5, 11, pos, null),
            Utils.Utilities.GetDeterministicId(5, 11, pos, null));
    }

    [Fact]
    public void GetDeterministicId_SaltXorsBase()
    {
        var pos = new Vector2(9f, 3f);
        int unsalted = Utils.Utilities.GetDeterministicId(5, 11, pos, null);
        int salted = Utils.Utilities.GetDeterministicId(5, 11, pos, 7);
        Assert.Equal(unsalted ^ 7, salted);
    }
}
