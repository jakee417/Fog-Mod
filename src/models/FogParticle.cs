#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FogMod;

public struct FogParticle
{
    public Texture2D? Texture { get; init; }
    public float Scale { get; init; }
    public float Rotation { get; init; }
    public Vector2 Position;
    public Vector2 Velocity;
    public float Alpha;
    public float AgeSeconds;
    public bool IsFadingOut;
    public float FadeOutSecondsLeft;

    public FogParticle(
        Texture2D texture,
        float scale,
        float rotation,
        Vector2 position,
        Vector2 velocity,
        float alpha,
        float ageSeconds,
        bool isFadingOut,
        float fadeOutSecondsLeft
    )
    {
        Texture = texture;
        Scale = scale;
        Rotation = rotation;
        Position = position;
        Velocity = velocity;
        Alpha = alpha;
        AgeSeconds = ageSeconds;
        IsFadingOut = isFadingOut;
        FadeOutSecondsLeft = fadeOutSecondsLeft;
    }
}