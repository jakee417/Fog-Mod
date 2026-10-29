#nullable enable
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using FogMod.Models;
using FogMod.Utils;

namespace FogMod;

public partial class FogMod : Mod
{
    public static Color GetEffectiveFogColor()
    {
        Color fog = FogAtLocation();
        if (Config.EnableWeatherBasedFog)
            fog = AdjustFogForWeather(fog);

        return fog;
    }

    public static Color FogAtLocation()
    {
        try
        {
            var loc = Game1.currentLocation;
            Color target = DefaultFogColor;

            if (loc == null) return target;

            // Desert
            if (loc is StardewValley.Locations.Desert)
            {
                target = Color.SaddleBrown;
            }
            // Deep forest (Secret Woods)
            else if (loc is StardewValley.Locations.Woods)
            {
                target = Color.ForestGreen;
            }
            // Town / Railroad tend to stone/urban
            else if (loc is StardewValley.Locations.Town || loc is StardewValley.Locations.Railroad)
            {
                target = Color.DarkGray;
            }
            // High elevation areas become white
            else if (loc is StardewValley.Locations.Mountain)
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

    public static Color AdjustFogForWeather(Color currentFogColor)
    {
        try
        {

            float intensityFactor = GetWeatherIntensityFactor();
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

    public static float GetWeatherIntensityFactor()
    {
        float intensityFactor = 1f;
        if (Game1.isRaining)
            intensityFactor += 0.25f;
        if (Game1.isLightning)
            intensityFactor += 0.15f;
        if (Game1.isSnowing)
            intensityFactor += 0.20f;
        if (Game1.isDebrisWeather)
            intensityFactor += 0.05f;

        return MathHelper.Clamp(intensityFactor, 0.4f, 1.6f);
    }

    public static Color LerpColor(Color a, Color b, float t)
    {
        return FogMath.LerpColor(a, b, t);
    }

    public Color ComposeParticleColor(FogParticle p, Color baseColor)
    {
        // Opacity affects
        float cellBreath = ComputeCellBreathOpacity(p.Position);
        float timeOfDay = Config.EnableTimeOfDayFog ? ComputeTimeOfDayOpacityMultiplier() : 1.0f;
        float lightAlphaMult = ComputeLightThinningMultiplier(p.Position);
        float dailyMult = GetDailyFogAlphaMultiplier();
        float a = FogMath.ParticleAlpha(p.Alpha, p.AgeSeconds, p.IsFadingOut, p.FadeOutSecondsLeft,
            cellBreath, timeOfDay, dailyMult, lightAlphaMult);
        return ApplyExplosionTintPerBlasts(baseColor, p.Position) * a;
    }

    public Color ComposeSmokeColor(FogParticle p, Color baseColor)
    {
        float lightAlphaMult = ComputeLightThinningMultiplier(p.Position);
        Color smokeTone = LerpColor(baseColor, Color.DimGray, 0.85f);
        float a = FogMath.SmokeAlpha(p.Alpha, p.AgeSeconds, lightAlphaMult);
        return ApplyExplosionTintPerBlasts(smokeTone, p.Position) * a;
    }

    public float GetDailyFogAlphaMultiplier()
    {
        return FogMath.DailyFogAlphaMultiplier(Config.EnableDailyRandomFog, dailyFogStrength);
    }

    public float ComputeLightThinningMultiplier(Vector2 worldPosition)
    {
        return FogMath.LightThinningMultiplier(ComputeLightWarmth(worldPosition));
    }

    public float ComputeLightWarmth(Vector2 worldPosition)
    {
        return FogMath.LightWarmth(worldPosition, lightSources, time);
    }

    // Global opacity multiplier based on time of day
    public static float ComputeTimeOfDayOpacityMultiplier()
    {
        try
        {
            return FogMath.TimeOfDayOpacity(Game1.timeOfDay); // e.g., 600..2600
        }
        catch
        {
            return 1f;
        }
    }

    // Breathing opacity per cell (based on world position)
    public float ComputeCellBreathOpacity(Vector2 worldPosition)
    {
        return FogMath.CellBreathOpacity(worldPosition, breathBasePhase);
    }

    public Color ApplyExplosionTintPerBlasts(Color inputColor, Vector2 worldPosition)
    {
        return FogMath.ApplyExplosionTint(inputColor, worldPosition, explosionFlashInfos);
    }
}