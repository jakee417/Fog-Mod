#nullable enable
using FogMod.Models;
using Microsoft.Xna.Framework;
using Xunit;

namespace FogMod.Tests;

public class LightInfoTests
{
    [Fact]
    public void Constructor_AssignsAllFields()
    {
        var info = new LightInfo(new Vector2(4f, 8f), 64f);
        Assert.Equal(new Vector2(4f, 8f), info.Position);
        Assert.Equal(64f, info.RadiusPixels);
    }
}
