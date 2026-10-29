#nullable enable
using System.Collections.Generic;
using FogMod.Models;
using Microsoft.Xna.Framework;
using Xunit;

namespace FogMod.Tests;

public class ExplosionFlashInfoTests
{
    [Fact]
    public void Constructor_AssignsAllFields()
    {
        var info = new ExplosionFlashInfo("Forest", new Vector2(1f, 2f), 96f, 0.5f);
        Assert.Equal("Forest", info.LocationName);
        Assert.Equal(new Vector2(1f, 2f), info.CenterWorld);
        Assert.Equal(96f, info.RadiusPixels);
        Assert.Equal(0.5f, info.TimeLeft);
    }

    [Fact]
    public void TickFlashes_DecrementsAndDropsExpired()
    {
        var flashes = new List<ExplosionFlashInfo>
        {
            new ExplosionFlashInfo("Forest", Vector2.Zero, 10f, 1f),
            new ExplosionFlashInfo("Forest", Vector2.Zero, 10f, 0.2f)
        };
        ExplosionFlashInfo.TickFlashes(flashes, 0.25f);
        Assert.Single(flashes);
        Assert.Equal(0.75f, flashes[0].TimeLeft, precision: 5);
    }
}
