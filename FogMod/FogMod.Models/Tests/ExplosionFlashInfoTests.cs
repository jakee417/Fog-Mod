#nullable enable
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
}
