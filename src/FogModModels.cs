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
            public required string LocationName;
            public required int GrouseId;
            public required Vector2 ProjectilePosition;
            public required long Timestamp;
        }

        private struct CollisionSmoke
        {
            public required Vector2 Position;
        }

        private struct Grouse
        {
            public required int GrouseId;
            public required Vector2 Position;
            public required Vector2 Velocity;
            public required Vector2 TreePosition;
            public required GrouseState State;
            public required float StateTimer;
            public required float Scale;
            public required float Rotation;
            public required float FlightHeight;
            public required bool FacingLeft;
            public required float FlightTimer;
            public required bool HasPlayedFlushSound;
            public required bool HasBeenSpotted;
            public required int AnimationFrame;
            public required float AnimationTimer;
            public required float Alpha;
            public required float OriginalY;
            public required float? DamageFlashTimer;
            public Vector2 GetExitDirection => FacingLeft ? new Vector2(-1, 0) : new Vector2(1, 0);
            public required CollisionSmoke? Smoke;
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


