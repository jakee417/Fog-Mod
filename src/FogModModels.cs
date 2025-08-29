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
            public required bool IsFogDay;
            public required float DailyFogStrength;
            public required float ProbabilityOfFogForADay;
            public required float ProbabilityOfFogRoll;
        }

        private struct FogParticle
        {
            public required Vector2 Position;
            public required Vector2 Velocity;
            public required float Scale;
            public required float Rotation;
            public required float Alpha;
            public required float AgeSeconds;
            public required Texture2D Texture;
            public required bool IsFadingOut;
            public required float FadeOutSecondsLeft;
        }

        public struct CellOccupancy
        {
            public required int[,] Counts;
            public required List<int>[,] Indices;
        }

        private struct ExplosionFlashInfo
        {
            public required string LocationName;
            public required Vector2 CenterWorld;
            public required float RadiusPixels;
            public required float TimeLeft;
        }

        private struct LightInfo
        {
            public required Vector2 Position;
            public required float RadiusPixels;
        }

        private struct GrouseFlushInfo
        {
            public required string LocationName;
            public required int GrouseId;
            public required Vector2 TreePosition;
            public required long Timestamp;
        }

        private struct GrouseKnockdownInfo
        {
            public Vector2 Position { get; set; }
        }

        private class Grouse
        {
            public int GrouseId { get; set; } // Unique identifier for multiplayer sync
            public Vector2 Position { get; set; }
            public Vector2 Velocity { get; set; }
            public Vector2 TreePosition { get; set; }
            public GrouseState State { get; set; }
            public float StateTimer { get; set; }
            public float Scale { get; set; }
            public float Rotation { get; set; }
            public float FlightHeight { get; set; }
            public bool FacingLeft { get; set; }
            public float FlightTimer { get; set; }
            public bool HasPlayedFlushSound { get; set; }
            public bool HasBeenSpotted { get; set; }
            public int AnimationFrame { get; set; }
            public float AnimationTimer { get; set; }
            public float Alpha { get; set; } // For fade out effect when knocked down
            public float OriginalY { get; set; } // Y position when knocked down for fall distance calculation
            public float DamageFlashTimer { get; set; }
            public Vector2 GetExitDirection => FacingLeft ? new Vector2(-1, 0) : new Vector2(1, 0);
            public CollisionSmoke Smoke { get; set; }
        }

        private enum GrouseState
        {
            Perched,    // Sitting in tree, invisible until player gets close
            Surprised,  // Brief moment of being startled before flush
            Flushing,   // Quick takeoff animation (3 seconds)
            Flying,     // Flying off screen and will be removed
            KnockedDown // Hit by projectile, falling to ground
        }
    }
}


