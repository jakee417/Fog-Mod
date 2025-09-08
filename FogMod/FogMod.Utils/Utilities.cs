#nullable enable
using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace FogMod.Utils;

public class Utilities
{
    internal static Vector2 GetPlayerWorldCenter()
    {
        var c = Game1.player.GetBoundingBox().Center;
        return new Vector2(c.X, c.Y);
    }

    internal static float Hash01(int v)
    {
        unchecked
        {
            uint x = (uint)v;
            x ^= x >> 17;
            x *= 0xed5ad4bbU;
            x ^= x >> 11;
            x *= 0xac4c1b51U;
            x ^= x >> 15;
            x *= 0x31848babU;
            x ^= x >> 14;
            return (x & 0x00FFFFFF) / (float)0x01000000; // [0,1)
        }
    }

    internal static bool DeterministicBool(Vector2 position, int variant)
    {
        int seed = (int)(position.X * 1000 + position.Y * 1000 + variant);
        var rng = new Random(seed);
        return rng.NextDouble() < 0.5;
    }

    internal static Vector2 ApplyMomentumThruTurn(Vector2 targetPosition, float targetSpeed, Vector2 currentPosition, Vector2 currentVelocity, float turnFactor)
    {
        Vector2 direction = Vector2.Normalize(targetPosition - currentPosition);
        return Vector2.Lerp(currentVelocity, direction * targetSpeed, turnFactor);
    }

    public static int GetDeterministicId(int locationSeed, int daySeed, Vector2 position, int? salt)
    {
        int baseId = (locationSeed.GetHashCode() ^ daySeed ^ (int)(position.X * 1000 + position.Y * 1000)) & 0x7FFFFFFF;
        return salt.HasValue ? baseId ^ salt.Value : baseId;
    }
}