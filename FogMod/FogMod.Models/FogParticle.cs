#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FogMod.Models;

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

    public static List<FogParticle> RemoveUnusedParticles(List<FogParticle> particles, Grid grid, float deltaSeconds, bool removeOffscreen)
    {
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var p = particles[i];
            p.Position += p.Velocity * deltaSeconds;
            p.AgeSeconds += deltaSeconds;
            if (removeOffscreen && !grid.GetExtendedBounds().Contains(new Point((int)p.Position.X, (int)p.Position.Y)))
            {
                particles.RemoveAt(i);
                continue;
            }
            if (p.IsFadingOut)
            {
                p.FadeOutSecondsLeft -= deltaSeconds;
                if (p.FadeOutSecondsLeft <= 0f)
                {
                    particles.RemoveAt(i);
                    continue;
                }
            }
            particles[i] = p;
        }
        return particles;
    }
}