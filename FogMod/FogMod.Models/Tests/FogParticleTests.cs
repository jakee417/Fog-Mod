#nullable enable
using System.Collections.Generic;
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

    private static FogParticle Moving()
    {
        return new FogParticle(null!, 1f, 0f, Vector2.Zero, new Vector2(10f, 0f),
            1f, 0f, false, 0f);
    }

    [Fact]
    public void RemoveUnusedParticles_MovesAndAgesSurvivors()
    {
        var grid = new Grid(256, 256, 0, 0, 128f, 0);
        var particles = new List<FogParticle> { Moving() };
        FogParticle.RemoveUnusedParticles(particles, grid, 0.5f, false);
        Assert.Single(particles);
        Assert.Equal(new Vector2(5f, 0f), particles[0].Position);
        Assert.Equal(0.5f, particles[0].AgeSeconds);
    }

    [Fact]
    public void RemoveUnusedParticles_DropsSpentFades()
    {
        var grid = new Grid(256, 256, 0, 0, 128f, 0);
        var spent = Moving();
        spent.IsFadingOut = true;
        spent.FadeOutSecondsLeft = 0.1f;
        var fresh = Moving();
        fresh.IsFadingOut = true;
        fresh.FadeOutSecondsLeft = 5f;
        var particles = new List<FogParticle> { spent, fresh };
        FogParticle.RemoveUnusedParticles(particles, grid, 0.5f, false);
        Assert.Single(particles);
        Assert.Equal(4.5f, particles[0].FadeOutSecondsLeft, precision: 4);
    }

    [Fact]
    public void RemoveUnusedParticles_OffscreenRemovalIsOptIn()
    {
        var grid = new Grid(256, 256, 0, 0, 128f, 0);
        var far = Moving();
        far.Position = new Vector2(500f, 500f);
        var culled = new List<FogParticle> { far };
        FogParticle.RemoveUnusedParticles(culled, grid, 0f, true);
        Assert.Empty(culled);
        var kept = new List<FogParticle> { far };
        FogParticle.RemoveUnusedParticles(kept, grid, 0f, false);
        Assert.Single(kept);
    }
}
