#nullable enable
using Microsoft.Xna.Framework;

namespace FogMod.Models;

public struct LightInfo
{
    public Vector2 Position { get; init; }
    public float RadiusPixels { get; init; }

    public LightInfo(Vector2 position, float radiusPixels)
    {
        Position = position;
        RadiusPixels = radiusPixels;
    }
}