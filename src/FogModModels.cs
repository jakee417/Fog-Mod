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
        }

        private struct FogParticle
        {
            public Vector2 Position { get; set; }
            public Vector2 Velocity { get; set; }
            public float Scale { get; init; }
            public float Rotation { get; init; }
            public float Alpha { get; set; }
            public float AgeSeconds { get; set; }
            public Texture2D Texture { get; init; }
            public bool IsFadingOut { get; set; }
            public float FadeOutSecondsLeft { get; set; }
            public float FadeOutTotalSeconds { get; set; }
        }

        public struct CellOccupancy
        {
            public int[,] Counts;
            public List<int>[,] Indices;
        }

        private struct LightInfo
        {
            public Vector2 Position;
            public float RadiusPixels;
        }

        private class CollisionSmoke
        {
            public Vector2 Position { get; set; }

        }

        private class Grouse
        {
            public int GrouseId { get; init; }
            public Vector2 Position { get; set; }
            public Vector2 Velocity { get; set; }
            public Vector2 TreePosition { get; init; }
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
            public float Alpha { get; set; }
            public float OriginalY { get; set; }
            public float? DamageFlashTimer { get; set; }
            public Vector2 GetExitDirection => FacingLeft ? new Vector2(-1, 0) : new Vector2(1, 0);
            public CollisionSmoke? Smoke { get; set; }
            public bool HasDroppedEgg { get; set; }
        }

        private enum GrouseState
        {
            Perched,
            Surprised,
            Flushing,
            Flying,
            KnockedDown
        }
    }
}


