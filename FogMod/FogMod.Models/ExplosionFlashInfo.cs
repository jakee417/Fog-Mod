#nullable enable
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
}