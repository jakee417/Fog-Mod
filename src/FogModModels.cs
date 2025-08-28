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
            public bool IsFogDay;
            public float DailyFogStrength;
            public float ProbabilityOfFogForADay;
            public float ProbabilityOfFogRoll;
        }

        private class FogParticle
        {
            public Vector2 Position { get; set; }
            public Vector2 Velocity { get; set; }
            public float Scale { get; set; }
            public float Rotation { get; set; }
            public float Alpha { get; set; }
            public float AgeSeconds { get; set; }
            public Texture2D Texture { get; set; }
            public bool IsFadingOut { get; set; }
            public float FadeOutSecondsLeft { get; set; }
            public float FadeOutTotalSeconds { get; set; }
        }

        public struct CellOccupancy
        {
            public int[,] Counts;
            public List<int>[,] Indices;
        }

        private struct ExplosionFlashInfo
        {
            public string LocationName;
            public Vector2 CenterWorld;
            public float RadiusPixels;
            public float TimeLeft;
        }

        private struct LightInfo
        {
            public Vector2 Position;
            public float RadiusPixels;
        }

        private struct GrouseFlushInfo
        {
            public string LocationName;
            public Vector2 TreePosition;
            public long Timestamp; // Game time for synchronization
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

            // Computed property example
            public Vector2 GetExitDirection => FacingLeft ? new Vector2(-1, 0) : new Vector2(1, 0);
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


