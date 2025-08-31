#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using System.Collections.Generic;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        private struct FogForecast
        {
            public bool IsFogDay { get; init; }
            public float DailyFogStrength { get; init; }
            public float ProbabilityOfFogForADay { get; init; }
            public float ProbabilityOfFogRoll { get; init; }

            public FogForecast(
                bool isFogDay,
                float dailyFogStrength,
                float probabilityOfFogForADay,
                float probabilityOfFogRoll
            )
            {
                IsFogDay = isFogDay;
                DailyFogStrength = dailyFogStrength;
                ProbabilityOfFogForADay = probabilityOfFogForADay;
                ProbabilityOfFogRoll = probabilityOfFogRoll;
            }
        }

        private struct FogParticle
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

        public struct CellOccupancy
        {
            public int[,] Counts;
            public List<int>[,] Indices;

            public CellOccupancy(int[,] counts, List<int>[,] indices)
            {
                Counts = counts;
                Indices = indices;
            }

            public bool IsValid => Counts != null && Indices != null;
        }

        private struct LightInfo
        {
            public Vector2 Position { get; init; }
            public float RadiusPixels { get; init; }

            public LightInfo(Vector2 position, float radiusPixels)
            {
                Position = position;
                RadiusPixels = radiusPixels;
            }
        }

        private struct CollisionSmoke
        {
            public Vector2 Position { get; init; }

            public CollisionSmoke(Vector2 position)
            {
                Position = position;
            }
        }

        private class Grouse
        {
            public static readonly int[] wingPattern = { 0, 1, 2, 3, 4, 3, 2, 1 };
            public int GrouseId { get; init; }
            public string Location { get; init; }
            public Vector2 TreePosition { get; init; }
            public Vector2 SpawnPosition { get; init; }
            public Vector2 Position;
            public Vector2 Velocity;
            public GrouseState State;
            public float StateTimer;
            public float Scale;
            public float Rotation;
            public float FlightHeight;
            public bool FacingLeft;
            public float FlightTimer;
            public bool HasPlayedFlushSound;
            public bool HasBeenSpotted;
            public int AnimationFrame;
            public float AnimationTimer;
            public float Alpha;
            public float OriginalY;
            public float? DamageFlashTimer;
            public Vector2 GetExitDirection => FacingLeft ? new Vector2(-1, 0) : new Vector2(1, 0);
            public CollisionSmoke? Smoke;
            public bool HasDroppedEgg;

            public Grouse(
                string location,
                Vector2 position,
                Vector2 velocity,
                Vector2 treePosition,
                Vector2 spawnPosition,
                GrouseState state,
                float stateTimer,
                int grouseId,
                float scale,
                float rotation,
                float flightHeight,
                bool facingLeft,
                float flightTimer,
                bool hasPlayedFlushSound,
                bool hasBeenSpotted,
                int animationFrame,
                float animationTimer,
                float alpha,
                float originalY,
                float? damageFlashTimer,
                CollisionSmoke? smoke,
                bool hasDroppedEgg
            )
            {
                Location = location;
                Position = position;
                Velocity = velocity;
                TreePosition = treePosition;
                SpawnPosition = spawnPosition;
                State = state;
                StateTimer = stateTimer;
                GrouseId = grouseId;
                Scale = scale;
                Rotation = rotation;
                FlightHeight = flightHeight;
                FacingLeft = facingLeft;
                FlightTimer = flightTimer;
                HasPlayedFlushSound = hasPlayedFlushSound;
                HasBeenSpotted = hasBeenSpotted;
                AnimationFrame = animationFrame;
                AnimationTimer = animationTimer;
                Alpha = alpha;
                OriginalY = originalY;
                DamageFlashTimer = damageFlashTimer;
                Smoke = smoke;
                HasDroppedEgg = hasDroppedEgg;
            }

            public static int GetDeterministicId(int locationSeed, int daySeed, Vector2 treePosition)
            {
                return (locationSeed.GetHashCode() ^ daySeed ^ (int)(treePosition.X * 1000 + treePosition.Y * 1000)) & 0x7FFFFFFF;
            }
        }

        public enum GrouseState
        {
            Perched,
            Surprised,
            Flushing,
            Flying,
            KnockedDown
        }
    }
}


