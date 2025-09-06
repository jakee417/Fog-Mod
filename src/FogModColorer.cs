#nullable enable
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;

namespace FogMod;

public partial class FogMod : Mod
{
    private Color GetEffectiveFogColor()
    {
        // Debug override
        // return Color.Black;

        Color fog = FogAtLocation();

        // Adjust fog for weather
        if (Config.EnableWeatherBasedFog)
            fog = AdjustFogForWeather(fog);

        return fog;
    }

    // Adjust fog color based on environment/location
    private Color FogAtLocation()
    {
        try
        {
            var loc = Game1.currentLocation;
            Color target = DefaultFogColor;

            if (loc == null) return target;

            string lname = (loc.NameOrUniqueName ?? loc.Name ?? string.Empty).ToLowerInvariant();

            // Desert
            if (loc is StardewValley.Locations.Desert || lname.Contains("desert"))
            {
                target = Color.SaddleBrown;
            }
            // Deep forest (Secret Woods)
            else if (loc is StardewValley.Locations.Woods || lname.Contains("woods"))
            {
                target = Color.ForestGreen;
            }
            // Town / Railroad tend to stone/urban
            else if (loc is StardewValley.Locations.Town || lname.Contains("town") || lname.Contains("railroad"))
            {
                target = Color.DarkGray;
            }
            // High elevation areas become white
            else if (loc is StardewValley.Locations.Mountain || lname.Contains("mountain"))
            {
                target = Color.White;
            }
            return target;
        }
        catch
        {
            return DefaultFogColor;
        }
    }

    private Color AdjustFogForWeather(Color currentFogColor)
    {
        try
        {
            float intensityFactor = 1f;

            // Stronger fog during precipitation
            if (Game1.isRaining)
                intensityFactor += 0.25f;
            if (Game1.isLightning)
                intensityFactor += 0.15f;
            if (Game1.isSnowing)
                intensityFactor += 0.20f;
            if (Game1.isDebrisWeather)
                intensityFactor += 0.05f;

            intensityFactor = MathHelper.Clamp(intensityFactor, 0.4f, 1.6f);
            lastWeatherFogIntensityFactor = intensityFactor;
            byte r = (byte)Math.Min(255, (int)(currentFogColor.R * intensityFactor));
            byte g = (byte)Math.Min(255, (int)(currentFogColor.G * intensityFactor));
            byte b = (byte)Math.Min(255, (int)(currentFogColor.B * intensityFactor));
            Color weatherAdjusted = new Color(r, g, b, currentFogColor.A);
            return weatherAdjusted;
        }
        catch
        {
            return currentFogColor;
        }
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = MathHelper.Clamp(t, 0f, 1f);
        byte r = (byte)MathHelper.Lerp(a.R, b.R, t);
        byte g = (byte)MathHelper.Lerp(a.G, b.G, t);
        byte bl = (byte)MathHelper.Lerp(a.B, b.B, t);
        return new Color(r, g, bl, a.A);
    }

    private Color ComposeParticleColor(FogParticle p, Color baseColor, Vector2 playerWorldCenter)
    {
        float fadeIn = MathHelper.Clamp(p.AgeSeconds / Constants.ParticleFadeInSeconds, 0f, 1f);
        float fadeOut = p.IsFadingOut
            ? MathHelper.Clamp(p.FadeOutSecondsLeft / Constants.ParticleFadeOutSeconds, 0f, 1f)
            : 1f;
        // Opacity affects
        float cellBreath = ComputeCellBreathOpacity(p.Position);
        float timeOfDay = Config.EnableTimeOfDayFog ? ComputeTimeOfDayOpacityMultiplier() : 1.0f;
        float lightAlphaMult = ComputeLightThinningMultiplier(p.Position);
        float dailyMult = GetDailyFogAlphaMultiplier();
        float a = (Config.ParticleStrength ? Constants.DefaultFogAlpha : Constants.DefaultFogAlphaWeak) * p.Alpha * fadeIn * fadeOut * cellBreath * timeOfDay * dailyMult;
        a = MathHelper.Clamp(a, 0f, 1f) * lightAlphaMult;
        return ApplyExplosionTintPerBlasts(baseColor, p.Position) * a;
    }

    private Color ComposeSmokeColor(FogParticle p, Color baseColor, Vector2 playerWorldCenter)
    {
        float fadeIn = MathHelper.Clamp(p.AgeSeconds / Constants.SmokeFadeInSeconds, 0f, 1f);
        fadeIn = fadeIn * fadeIn * (3f - 2f * fadeIn);
        float fadeToFloorT = MathHelper.Clamp(p.AgeSeconds / Constants.SmokeFadeToFloorSeconds, 0f, 1f);
        fadeToFloorT = fadeToFloorT * fadeToFloorT * (3f - 2f * fadeToFloorT);
        float lightAlphaMult = ComputeLightThinningMultiplier(p.Position);
        Color smokeTone = LerpColor(baseColor, Color.DimGray, 0.85f); ;
        float initialOpacity = (Config.ParticleStrength ? Constants.DefaultFogAlpha : Constants.DefaultFogAlphaWeak) * p.Alpha * fadeIn;
        float currentOpacity = MathHelper.Lerp(initialOpacity, Constants.SmokeMinAlpha, fadeToFloorT);
        float a = currentOpacity * lightAlphaMult;
        return ApplyExplosionTintPerBlasts(smokeTone, p.Position) * a;
    }

    private float GetDailyFogAlphaMultiplier()
    {
        return Config.EnableDailyRandomFog ? MathHelper.Clamp(dailyFogStrength, 0f, 4f) : 1f;
    }

    private float ComputeLightThinningMultiplier(Vector2 worldPosition)
    {
        float warmth = ComputeLightWarmth(worldPosition);
        return MathHelper.Clamp(1f - warmth * (Config.LightThinningStrength ? Constants.LightThinningStrength : Constants.LightThinningStrengthWeak), 0f, 1f);
    }

    private float ComputeLightWarmth(Vector2 worldPosition)
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
            t = t * t * (3f - 2f * t);
            // per-light flicker (windy shimmer): 1 ± 0.2
            float phase = time * 3.0f;
            float flicker = 1f + 0.05f * (float)Math.Sin(phase);
            brighten += t * 1.95f * flicker;
        }
        return MathHelper.Clamp(brighten, 0f, 1.0f);
    }

    // Global opacity multiplier based on time of day
    private float ComputeTimeOfDayOpacityMultiplier()
    {
        try
        {
            float multiplier = 1f;
            // Time-based burn-off: lowest at midday, full at dawn/dusk/night
            int tod = Game1.timeOfDay; // e.g., 600..2600
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
        catch
        {
            return 1f;
        }
    }

    // Breathing opacity per cell (based on world position)
    private float ComputeCellBreathOpacity(Vector2 worldPosition)
    {
        float cellSize = Math.Max(1f, Constants.FogTileSize);
        // Derive a stable cell id by quantizing world position
        int col = (int)Math.Floor(worldPosition.X / cellSize);
        int row = (int)Math.Floor(worldPosition.Y / cellSize);
        // Simple hash to desync phases per cell
        float jitter = Utils.Hash01(col * 73856093 ^ row * 19349663);
        float phase = breathBasePhase + jitter * MathHelper.TwoPi * Constants.BreathDesync;
        float s = (float)Math.Sin(phase);
        // Map to [1 - A, 1 + A], then clamp to [0, 1.5] and finally to [0,1]
        float mult = 1f + Constants.BreathAmplitude * s;
        // Since we apply to alpha, clamp to [0,1]
        return MathHelper.Clamp(mult, 0f, 1f);
    }

    private Color ApplyExplosionTintPerBlasts(Color inputColor, Vector2 worldPosition)
    {
        if (explosionFlashInfos == null || explosionFlashInfos.Count == 0)
            return inputColor;
        float strongestInfluence = 0f;
        Color explosionColor = Color.OrangeRed;
        foreach (var flash in explosionFlashInfos)
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
}