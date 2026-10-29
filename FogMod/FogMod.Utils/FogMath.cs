#nullable enable
using System;
using System.Collections.Generic;
using FogMod.Models;
using Microsoft.Xna.Framework;

namespace FogMod.Utils;

// Pure fog/color math, extracted from the FogMod partials so it can run
// under dotnet test. The partials keep thin wrappers that read game state
// (Game1, Config, fields) and delegate here; behavior is unchanged.
public static class FogMath
{
    public static Color LerpColor(Color a, Color b, float t)
    {
        t = MathHelper.Clamp(t, 0f, 1f);
        byte r = (byte)MathHelper.Lerp(a.R, b.R, t);
        byte g = (byte)MathHelper.Lerp(a.G, b.G, t);
        byte bl = (byte)MathHelper.Lerp(a.B, b.B, t);
        return new Color(r, g, bl, a.A);
    }

    public static float Smoothstep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    public static float FogProbabilityForSeason(string season)
    {
        float seasonalProbability;
        switch (season)
        {
            case "spring":
                seasonalProbability = 0.05f;
                break;
            case "summer":
                seasonalProbability = 0.1f;
                break;
            case "fall":
                seasonalProbability = 0.2f;
                break;
            case "winter":
                seasonalProbability = 0.18f;
                break;
            default:
                seasonalProbability = 0.08f;
                break;
        }
        return seasonalProbability;
    }

    public static FogForecast ComputeFogForecast(int daysPlayed, ulong uniqueId, string season)
    {
        int seed = daysPlayed ^ (int)(uniqueId & 0x7FFFFFFF);
        var rng = new Random(seed);
        float probabilityOfFogRoll = (float)rng.NextDouble();
        float probabilityOfFogForADay = FogProbabilityForSeason(season);
        bool isFogDay = probabilityOfFogRoll <= probabilityOfFogForADay;
        float strengthRoll = (float)rng.NextDouble();
        float dailyFogStrength = MathHelper.Lerp(Constants.DailyRandomFogMin, Constants.DailyRandomFogMax, strengthRoll);
        return new FogForecast(
            isFogDay: isFogDay,
            dailyFogStrength: dailyFogStrength,
            probabilityOfFogForADay: probabilityOfFogForADay,
            probabilityOfFogRoll: probabilityOfFogRoll
        );
    }

    // Global opacity multiplier based on time of day.
    public static float TimeOfDayOpacity(int timeOfDay)
    {
        float multiplier = 1f;
        // Time-based burn-off: lowest at midday, full at dawn/dusk/night
        int tod = timeOfDay; // e.g., 600..2600
        int hour = Math.Max(0, Math.Min(23, tod / 100));
        int minute = Math.Max(0, Math.Min(59, tod % 100));
        float h = hour + minute / 60f;
        // Daylight fraction 0 at 6:00, 1 at 18:00
        float d = MathHelper.Clamp((h - 6f) / 12f, 0f, 1f);
        float middayBump = 4f * d * (1f - d); // 0 at edges, 1 at 12:00
        float minAtNoon = 0.35f; // how low at noon
        float timeFactor = 1f - (1f - minAtNoon) * middayBump;
        // At night, keep full
        if (h < 6f || h > 19.5f)
            timeFactor = 1f;
        multiplier *= timeFactor;
        return MathHelper.Clamp(multiplier, 0.15f, 1.3f);
    }

    // Breathing opacity per cell (based on world position).
    public static float CellBreathOpacity(Vector2 worldPosition, float breathBasePhase)
    {
        float cellSize = Math.Max(1f, Constants.FogTileSize);
        // Derive a stable cell id by quantizing world position
        int col = (int)Math.Floor(worldPosition.X / cellSize);
        int row = (int)Math.Floor(worldPosition.Y / cellSize);
        // Simple hash to desync phases per cell
        float jitter = Utilities.Hash01(col * 73856093 ^ row * 19349663);
        float phase = breathBasePhase + jitter * MathHelper.TwoPi * Constants.BreathDesync;
        float s = (float)Math.Sin(phase);
        // Map to [1 - A, 1 + A], then clamp to [0, 1.5] and finally to [0,1]
        float mult = 1f + Constants.BreathAmplitude * s;
        // Since we apply to alpha, clamp to [0,1]
        return MathHelper.Clamp(mult, 0f, 1f);
    }

    public static float LightWarmth(Vector2 worldPosition, IReadOnlyList<LightInfo> lightSources, float time)
    {
        if (lightSources == null || lightSources.Count == 0) return 0f;
        float brighten = 0f;
        for (int i = 0; i < lightSources.Count; i++)
        {
            var li = lightSources[i];
            float dist = Vector2.Distance(worldPosition, li.Position);
            if (dist >= li.RadiusPixels) continue;
            float t = 1f - (dist / Math.Max(1e-3f, li.RadiusPixels));
            // smooth falloff
            t = Smoothstep(t);
            // per-light flicker (windy shimmer): 1 ± 0.2
            float phase = time * 3.0f;
            float flicker = 1f + 0.05f * (float)Math.Sin(phase);
            brighten += t * 1.95f * flicker;
        }
        return MathHelper.Clamp(brighten, 0f, 1.0f);
    }

    public static float LightThinningMultiplier(float warmth)
    {
        return MathHelper.Clamp(1f - warmth * Constants.LightThinningStrength, 0f, 1f);
    }

    public static Color ApplyExplosionTint(Color inputColor, Vector2 worldPosition, IReadOnlyList<ExplosionFlashInfo> flashes)
    {
        if (flashes == null || flashes.Count == 0)
            return inputColor;
        float strongestInfluence = 0f;
        Color explosionColor = Color.OrangeRed;
        foreach (var flash in flashes)
        {
            float dist = Vector2.Distance(worldPosition, flash.CenterWorld);
            float radius = flash.RadiusPixels;
            if (dist >= radius) continue;
            float distanceRatio = dist / radius;
            float timeRatio = flash.TimeLeft / Constants.ExplosionFlashDurationSeconds;
            float influence = (1f - distanceRatio) * timeRatio;
            influence = influence * influence;
            strongestInfluence = Math.Max(strongestInfluence, influence);
        }
        return strongestInfluence <= 0f ? inputColor : LerpColor(inputColor, explosionColor, strongestInfluence);
    }

    public static float DailyFogAlphaMultiplier(bool enableDailyRandomFog, float dailyFogStrength)
    {
        return enableDailyRandomFog ? MathHelper.Clamp(dailyFogStrength, 0f, 4f) : 1f;
    }

    // Combined particle alpha. Op order matches ComposeParticleColor exactly.
    public static float ParticleAlpha(
        float particleAlpha,
        float ageSeconds,
        bool isFadingOut,
        float fadeOutSecondsLeft,
        float breath,
        float timeOfDay,
        float daily,
        float lightMult)
    {
        float fadeIn = MathHelper.Clamp(ageSeconds / Constants.ParticleFadeInSeconds, 0f, 1f);
        float fadeOut = isFadingOut
            ? MathHelper.Clamp(fadeOutSecondsLeft / Constants.ParticleFadeOutSeconds, 0f, 1f)
            : 1f;
        float a = Constants.DefaultFogAlpha * particleAlpha * fadeIn * fadeOut * breath * timeOfDay * daily;
        a = MathHelper.Clamp(a, 0f, 1f) * lightMult;
        return a;
    }

    // Combined smoke alpha. Op order matches ComposeSmokeColor exactly.
    public static float SmokeAlpha(float particleAlpha, float ageSeconds, float lightMult)
    {
        float fadeIn = MathHelper.Clamp(ageSeconds / Constants.SmokeFadeInSeconds, 0f, 1f);
        fadeIn = Smoothstep(fadeIn);
        float fadeToFloorT = MathHelper.Clamp(ageSeconds / Constants.SmokeFadeToFloorSeconds, 0f, 1f);
        fadeToFloorT = Smoothstep(fadeToFloorT);
        float initialOpacity = Constants.DefaultFogAlpha * particleAlpha * fadeIn;
        float currentOpacity = MathHelper.Lerp(initialOpacity, Constants.SmokeMinAlpha, fadeToFloorT);
        float a = currentOpacity * lightMult;
        return a;
    }
}
