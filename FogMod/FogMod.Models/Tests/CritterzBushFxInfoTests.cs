#nullable enable
using FogMod.Models;
using Microsoft.Xna.Framework;
using Xunit;

namespace FogMod.Tests;

public class CritterzBushFxInfoTests
{
    [Fact]
    public void Constructor_AssignsAllFields()
    {
        var info = new CritterzBushFxInfo("Forest", new Vector2(50f, 30f));
        Assert.Equal("Forest", info.LocationName);
        Assert.Equal(new Vector2(50f, 30f), info.BushTile);
    }
}
