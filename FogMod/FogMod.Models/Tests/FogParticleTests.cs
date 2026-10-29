#nullable enable
using FogMod.Models;
using Microsoft.Xna.Framework;
using Xunit;

namespace FogMod.Tests;

public class FogParticleTests
{
    [Fact]
    public void Constructor_AssignsAllFields()
    {
        var p = new FogParticle(
            null!,
            scale: 1.5f,
            rotation: 0.25f,
            position: new Vector2(10f, 20f),
            velocity: new Vector2(1f, 0f),
            alpha: 0.5f,
            ageSeconds: 2f,
            isFadingOut: true,
            fadeOutSecondsLeft: 3f);
        Assert.Null(p.Texture);
        Assert.Equal(1.5f, p.Scale);
        Assert.Equal(new Vector2(10f, 20f), p.Position);
        Assert.Equal(new Vector2(1f, 0f), p.Velocity);
        Assert.Equal(0.5f, p.Alpha);
        Assert.Equal(2f, p.AgeSeconds);
        Assert.True(p.IsFadingOut);
        Assert.Equal(3f, p.FadeOutSecondsLeft);
    }
}
