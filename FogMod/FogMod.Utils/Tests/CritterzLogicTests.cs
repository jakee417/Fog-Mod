#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Xunit;

namespace FogMod.Tests;

public class CritterzLogicTests
{
    private static HashSet<Vector2> OpenGrid(int size)
    {
        var set = new HashSet<Vector2>();
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                set.Add(new Vector2(x, y));
        return set;
    }

    [Fact]
    public void FindPath_SameTileIsEmpty()
    {
        var path = Utils.CritterzLogic.FindPath(new Vector2(3, 3), new Vector2(3, 3), OpenGrid(5));
        Assert.NotNull(path);
        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_UnreachableIsNull()
    {
        var reachable = new HashSet<Vector2> { new Vector2(0, 0) };
        Assert.Null(Utils.CritterzLogic.FindPath(new Vector2(0, 0), new Vector2(5, 5), reachable));
    }

    [Fact]
    public void FindPath_OpenGridIsShortestAndOrthogonal()
    {
        var path = Utils.CritterzLogic.FindPath(new Vector2(0, 0), new Vector2(2, 2), OpenGrid(5));
        Assert.NotNull(path);
        Assert.Equal(4, path.Count);
        Vector2 prev = new Vector2(0, 0);
        foreach (Vector2 step in path)
        {
            Assert.Equal(1f, Math.Abs(step.X - prev.X) + Math.Abs(step.Y - prev.Y));
            prev = step;
        }
        Assert.Equal(new Vector2(2, 2), path.Last());
    }

    [Fact]
    public void FindPath_GoalTileIsExempt()
    {
        var reachable = new HashSet<Vector2> { new Vector2(0, 0) };
        var path = Utils.CritterzLogic.FindPath(new Vector2(0, 0), new Vector2(1, 0), reachable);
        Assert.NotNull(path);
        Assert.Single(path);
    }

    [Fact]
    public void FindPath_RoutesAroundHoles()
    {
        var reachable = OpenGrid(5);
        reachable.Remove(new Vector2(1, 0));
        var path = Utils.CritterzLogic.FindPath(new Vector2(0, 0), new Vector2(2, 0), reachable);
        Assert.NotNull(path);
        Assert.DoesNotContain(new Vector2(1, 0), path);
        Assert.Equal(new Vector2(2, 0), path.Last());
    }

    [Fact]
    public void ExpandPath_HorizontalLegPassesThrough()
    {
        var waypoints = Utils.CritterzLogic.ExpandPathWithZigZag(
            new Vector2(32f, 32f), new List<Vector2> { new Vector2(1, 0) }, 64, 1);
        Assert.Single(waypoints);
        Assert.Equal(new Vector2(96f, 32f), waypoints[0]);
    }

    [Fact]
    public void ExpandPath_VerticalLegSplitsAtAmplitude()
    {
        var waypoints = Utils.CritterzLogic.ExpandPathWithZigZag(
            new Vector2(32f, 32f), new List<Vector2> { new Vector2(0, 1) }, 64, 1);
        Assert.Equal(2, waypoints.Count);
        Assert.Equal(new Vector2(64f, 64f), waypoints[0]);
        Assert.Equal(new Vector2(32f, 96f), waypoints[1]);
    }

    [Fact]
    public void ExpandPath_SidesAlternate()
    {
        var waypoints = Utils.CritterzLogic.ExpandPathWithZigZag(
            new Vector2(32f, 32f),
            new List<Vector2> { new Vector2(0, 1), new Vector2(0, 2) }, 64, 1);
        Assert.Equal(new[]
        {
            new Vector2(64f, 64f),
            new Vector2(32f, 96f),
            new Vector2(0f, 128f),
            new Vector2(32f, 160f)
        }, waypoints.ToArray());
    }

    [Fact]
    public void ExpandPath_NoSegmentIsStraightVertical()
    {
        var tiles = new List<Vector2>
        {
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 2), new Vector2(2, 2)
        };
        var waypoints = Utils.CritterzLogic.ExpandPathWithZigZag(
            new Vector2(32f, 32f), tiles, 64, -1);
        Vector2 prev = new Vector2(32f, 32f);
        foreach (Vector2 wp in waypoints)
        {
            bool straightVertical = Math.Abs(wp.X - prev.X) < 0.5f && Math.Abs(wp.Y - prev.Y) > 0.5f;
            Assert.False(straightVertical, $"vertical hop {prev} -> {wp}");
            prev = wp;
        }
    }

    [Fact]
    public void PickWeighted_SingleAndZeroWeightPickFirst()
    {
        Assert.Equal(0, Utils.CritterzLogic.PickWeightedIndex(
            new[] { new Vector2(10f, 0f) }, Vector2.Zero, new Random(1)));
        Assert.Equal(0, Utils.CritterzLogic.PickWeightedIndex(
            new[] { Vector2.Zero, Vector2.Zero }, Vector2.Zero, new Random(1)));
    }

    [Fact]
    public void PickWeighted_FartherWinsMajority()
    {
        var positions = new[] { new Vector2(1f, 0f), new Vector2(100f, 0f) };
        int far = 0;
        var rng = new Random(7);
        for (int i = 0; i < 200; i++)
            if (Utils.CritterzLogic.PickWeightedIndex(positions, Vector2.Zero, rng) == 1)
                far++;
        Assert.True(far > 100, $"far picked only {far}/200");
    }

    [Fact]
    public void PickSkin_MapsRollAndSeason()
    {
        Assert.Equal(Constants.SquirrelRow, Utils.CritterzLogic.PickSkin(0, false));
        Assert.Equal(Constants.RaccoonRow, Utils.CritterzLogic.PickSkin(2, true));
        Assert.Equal(Constants.WhiteBunnyRow, Utils.CritterzLogic.PickSkin(1, true));
        Assert.Equal(Constants.GreyBunnyRow, Utils.CritterzLogic.PickSkin(1, false));
    }

    [Fact]
    public void Loot_IsDeterministicPerSeed()
    {
        var a = Utils.CritterzLogic.RollLootToDrop(Constants.GreyBunnyRow, false, new Random(3));
        var b = Utils.CritterzLogic.RollLootToDrop(Constants.GreyBunnyRow, false, new Random(3));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Loot_StaysWithinSkinTables()
    {
        var bunny = new HashSet<string> { "78", "440", "446" };
        var nuts = new HashSet<string> { "408", "309", "311" };
        var trash = new HashSet<string> { "168", "169", "170", "171", "172" };
        for (int seed = 0; seed < 200; seed++)
        {
            foreach (string id in Utils.CritterzLogic.RollLootToDrop(
                Constants.WhiteBunnyRow, seed % 2 == 0, new Random(seed)))
                Assert.Contains(id, bunny);
            foreach (string id in Utils.CritterzLogic.RollLootToDrop(
                Constants.SquirrelRow, seed % 2 == 0, new Random(seed)))
                Assert.Contains(id, nuts);
            foreach (string id in Utils.CritterzLogic.RollLootToDrop(
                Constants.RaccoonRow, seed % 2 == 0, new Random(seed)))
                Assert.Contains(id, trash);
            foreach (string id in Utils.CritterzLogic.RollLootToDrop(99, false, new Random(seed)))
                Assert.Contains(id, trash);
        }
    }

    [Fact]
    public void Loot_CommonCountsStayBounded()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            var plain = Utils.CritterzLogic.RollLootToDrop(
                Constants.GreyBunnyRow, false, new Random(seed));
            Assert.True(plain.Count(id => id == "78") <= 1, "non-lucky carrots over cap");
            var lucky = Utils.CritterzLogic.RollLootToDrop(
                Constants.GreyBunnyRow, true, new Random(seed));
            Assert.True(lucky.Count(id => id == "78") <= 2, "lucky carrots over cap");
            Assert.True(lucky.Count(id => id == "440") <= 1, "double wool");
            Assert.True(lucky.Count(id => id == "446") <= 1, "double foot");
        }
    }
}
