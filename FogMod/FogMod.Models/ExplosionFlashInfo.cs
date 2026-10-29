#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace FogMod.Models;

public struct ExplosionFlashInfo
{
    public string LocationName { get; init; }
    public Vector2 CenterWorld { get; init; }
    public float RadiusPixels { get; init; }
    public float TimeLeft { get; set; }

    public ExplosionFlashInfo(string locationName, Vector2 centerWorld, float radiusPixels, float timeLeft)
    {
        LocationName = locationName;
        CenterWorld = centerWorld;
        RadiusPixels = radiusPixels;
        TimeLeft = timeLeft;
    }

    public static void TickFlashes(List<ExplosionFlashInfo> flashes, float deltaSeconds)
    {
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            var ef = flashes[i];
            ef.TimeLeft = Math.Max(0f, ef.TimeLeft - deltaSeconds);
            if (ef.TimeLeft <= 0f)
            {
                flashes.RemoveAt(i);
                continue;
            }
            flashes[i] = ef;
        }
    }
}