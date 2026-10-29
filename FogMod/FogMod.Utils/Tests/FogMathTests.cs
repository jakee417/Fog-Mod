#nullable enable
using System;
using System.Collections.Generic;
using FogMod.Models;
using Microsoft.Xna.Framework;
using Xunit;

namespace FogMod.Tests;

public class FogMathTests
{
    [Fact]
    public void LerpColor_Endpoints()
    {
        var a = new Color(10, 20, 30, 40);
        var b = new Color(100, 110, 120, 200);
        Assert.Equal(new Color(10, 20, 30, 40), Utils.FogMath.LerpColor(a, b, 0f));
        Assert.Equal(new Color(100, 110, 120, 40), Utils.FogMath.LerpColor(a, b, 1f));
    }

    [Fact]
    public void LerpColor_ClampsAndKeepsSourceAlpha()
    {
        var mid = Utils.FogMath.LerpColor(Color.Black, Color.White, 0.5f);
        Assert.Equal(127, mid.R);
        Assert.Equal(255, mid.A);
        Assert.Equal(Utils.FogMath.LerpColor(Color.Black, Color.White, 0f),
            Utils.FogMath.LerpColor(Color.Black, Color.White, -1f));
        Assert.Equal(Utils.FogMath.LerpColor(Color.Black, Color.White, 1f),
            Utils.FogMath.LerpColor(Color.Black, Color.White, 2f));
    }

    [Fact]
    public void Smoothstep_Shape()
    {
        Assert.Equal(0f, Utils.FogMath.Smoothstep(0f));
        Assert.Equal(0.5f, Utils.FogMath.Smoothstep(0.5f));
        Assert.Equal(1f, Utils.FogMath.Smoothstep(1f));
    }

    [Fact]
    public void SeasonTable_MatchesContract()
    {
        Assert.Equal(0.05f, Utils.FogMath.FogProbabilityForSeason("spring"));
        Assert.Equal(0.1f, Utils.FogMath.FogProbabilityForSeason("summer"));
        Assert.Equal(0.2f, Utils.FogMath.FogProbabilityForSeason("fall"));
        Assert.Equal(0.18f, Utils.FogMath.FogProbabilityForSeason("winter"));
        Assert.Equal(0.08f, Utils.FogMath.FogProbabilityForSeason("void"));
    }

    [Fact]
    public void Forecast_IsDeterministicAndBounded()
    {
        var f1 = Utils.FogMath.ComputeFogForecast(100, 7, "fall");
        var f2 = Utils.FogMath.ComputeFogForecast(100, 7, "fall");
        Assert.Equal(f1, f2);
        Assert.InRange(f1.ProbabilityOfFogRoll, 0f, 1f);
        Assert.InRange(f1.ProbabilityOfFogForADay, 0f, 1f);
        Assert.InRange(f1.DailyFogStrength, Constants.DailyRandomFogMin, Constants.DailyRandomFogMax);
    }

    [Fact]
    public void Forecast_SeasonChangesProbabilityNotRolls()
    {
        var spring = Utils.FogMath.ComputeFogForecast(100, 7, "spring");
        var fall = Utils.FogMath.ComputeFogForecast(100, 7, "fall");
        Assert.Equal(spring.ProbabilityOfFogRoll, fall.ProbabilityOfFogRoll);
        Assert.Equal(spring.DailyFogStrength, fall.DailyFogStrength);
        Assert.NotEqual(spring.ProbabilityOfFogForADay, fall.ProbabilityOfFogForADay);
    }

    [Fact]
    public void Forecast_VariesWithDayAndId()
    {
        var a = Utils.FogMath.ComputeFogForecast(100, 7, "fall");
        var b = Utils.FogMath.ComputeFogForecast(101, 7, "fall");
        var c = Utils.FogMath.ComputeFogForecast(100, 8, "fall");
        Assert.NotEqual(a.ProbabilityOfFogRoll, b.ProbabilityOfFogRoll);
        Assert.NotEqual(a.ProbabilityOfFogRoll, c.ProbabilityOfFogRoll);
    }

    [Fact]
    public void TimeOfDay_NoonDipsNightsStayFull()
    {
        Assert.InRange(Utils.FogMath.TimeOfDayOpacity(1200), 0.349f, 0.351f);
        Assert.Equal(1f, Utils.FogMath.TimeOfDayOpacity(600));
        Assert.Equal(1f, Utils.FogMath.TimeOfDayOpacity(0));
        Assert.Equal(1f, Utils.FogMath.TimeOfDayOpacity(2000));
        Assert.InRange(Utils.FogMath.TimeOfDayOpacity(900), 0.51f, 0.52f);
    }

    [Fact]
    public void Breath_StaysInRangeAndVariesByCell()
    {
        var seen = new HashSet<float>();
        for (int x = 0; x < 10; x++)
            for (int y = 0; y < 10; y++)
            {
                float b = Utils.FogMath.CellBreathOpacity(new Vector2(x * 128f, y * 128f), MathF.PI);
                Assert.InRange(b, 0f, 1f);
                seen.Add(b);
            }
        Assert.True(seen.Count > 1, "breath never varies");
        Assert.Equal(Utils.FogMath.CellBreathOpacity(new Vector2(5f, 5f), 1f),
            Utils.FogMath.CellBreathOpacity(new Vector2(5f, 5f), 1f));
    }

    [Fact]
    public void Breath_AtZeroPhasePinsToFull()
    {
        // Desync spans half a sine period, so at phase 0 every cell clamps to 1.
        for (int x = 0; x < 5; x++)
            for (int y = 0; y < 5; y++)
                Assert.Equal(1f, Utils.FogMath.CellBreathOpacity(new Vector2(x * 128f, y * 128f), 0f));
    }

    [Fact]
    public void Warmth_NoLightsIsDarkCenteredIsBright()
    {
        Assert.Equal(0f, Utils.FogMath.LightWarmth(new Vector2(0f, 0f),
            new List<LightInfo>(), 0f));
        var lights = new List<LightInfo> { new LightInfo(new Vector2(0f, 0f), 100f) };
        Assert.Equal(1f, Utils.FogMath.LightWarmth(new Vector2(0f, 0f), lights, 0f));
        Assert.Equal(0f, Utils.FogMath.LightWarmth(new Vector2(100f, 0f), lights, 0f));
        Assert.InRange(Utils.FogMath.LightWarmth(new Vector2(50f, 0f), lights, 0f), 0f, 1f);
    }

    [Fact]
    public void Thinning_FullWarmthThinsToQuarter()
    {
        Assert.Equal(1f, Utils.FogMath.LightThinningMultiplier(0f));
        Assert.Equal(0.25f, Utils.FogMath.LightThinningMultiplier(1f));
    }

    [Fact]
    public void Tint_NoFlashesIsIdentity()
    {
        var input = new Color(10, 20, 30, 40);
        Assert.Equal(input, Utils.FogMath.ApplyExplosionTint(input, Vector2.Zero,
            new List<ExplosionFlashInfo>()));
    }

    [Fact]
    public void Tint_CenteredFreshFlashGoesOrangeRed()
    {
        var input = new Color(10, 20, 30, 40);
        var flashes = new List<ExplosionFlashInfo>
        {
            new ExplosionFlashInfo("Forest", Vector2.Zero, 100f, Constants.ExplosionFlashDurationSeconds)
        };
        var tinted = Utils.FogMath.ApplyExplosionTint(input, Vector2.Zero, flashes);
        Assert.Equal(Color.OrangeRed.R, tinted.R);
        Assert.Equal(Color.OrangeRed.G, tinted.G);
        Assert.Equal(Color.OrangeRed.B, tinted.B);
        Assert.Equal(40, tinted.A);
    }

    [Fact]
    public void Tint_IgnoresDistantAndSpentFlashes()
    {
        var input = new Color(10, 20, 30, 40);
        var flashes = new List<ExplosionFlashInfo>
        {
            new ExplosionFlashInfo("Forest", new Vector2(1000f, 0f), 100f, 0.75f),
            new ExplosionFlashInfo("Forest", Vector2.Zero, 100f, 0f)
        };
        Assert.Equal(input, Utils.FogMath.ApplyExplosionTint(input, Vector2.Zero, flashes));
    }

    [Fact]
    public void DailyMult_GatesOnConfig()
    {
        Assert.Equal(1f, Utils.FogMath.DailyFogAlphaMultiplier(false, 2f));
        Assert.Equal(2f, Utils.FogMath.DailyFogAlphaMultiplier(true, 2f));
        Assert.Equal(4f, Utils.FogMath.DailyFogAlphaMultiplier(true, 9f));
        Assert.Equal(0f, Utils.FogMath.DailyFogAlphaMultiplier(true, -1f));
    }

    [Fact]
    public void ParticleAlpha_FreshParticleIsBaseAlpha()
    {
        float a = Utils.FogMath.ParticleAlpha(1f, 2f, false, 0f, 1f, 1f, 1f, 1f);
        Assert.Equal(0.3f, a);
    }

    [Fact]
    public void ParticleAlpha_NewbornAndExpiredFadeToZero()
    {
        Assert.Equal(0f, Utils.FogMath.ParticleAlpha(1f, 0f, false, 0f, 1f, 1f, 1f, 1f));
        Assert.Equal(0f, Utils.FogMath.ParticleAlpha(1f, 2f, true, 0f, 1f, 1f, 1f, 1f));
    }

    [Fact]
    public void SmokeAlpha_RisesThenFallsToFloor()
    {
        Assert.Equal(0f, Utils.FogMath.SmokeAlpha(1f, 0f, 1f));
        Assert.InRange(Utils.FogMath.SmokeAlpha(1f, 100f, 1f), 0.049f, 0.051f);
        Assert.InRange(Utils.FogMath.SmokeAlpha(1f, 2f, 1f), 0f, 0.3f);
    }
}
