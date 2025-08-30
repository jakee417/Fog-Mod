#nullable enable
using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        private static Vector2 GetPlayerWorldCenter()
        {
            var c = Game1.player.GetBoundingBox().Center;
            return new Vector2(c.X, c.Y);
        }

        private static float Hash01(int v)
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

        private bool DeterministicBool(Vector2 position, int variant)
        {
            int seed = (int)(position.X * 1000 + position.Y * 1000 + variant);
            var rng = new Random(seed);
            return rng.NextDouble() < 0.5;
        }
    }
}