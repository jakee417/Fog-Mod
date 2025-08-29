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
            public required bool IsFogDay { get; init; }
            public required float DailyFogStrength { get; init; }
            public required float ProbabilityOfFogForADay { get; init; }
            public required float ProbabilityOfFogRoll { get; init; }
        }

        private struct FogParticle
        {
            public required Texture2D Texture { get; init; }
            public required float Scale { get; init; }
            public required float Rotation { get; init; }
            public required Vector2 Position;
            public required Vector2 Velocity;
            public required float Alpha;
            public required float AgeSeconds;
            public required bool IsFadingOut;
            public required float FadeOutSecondsLeft;
        }

        public struct CellOccupancy
        {
            public required int[,] Counts;
            public required List<int>[,] Indices;
        }

        private struct LightInfo
        {
            public required Vector2 Position { get; init; }
            public required float RadiusPixels { get; init; }
        }

        private class Grouse
        {
            public required Vector2 Position { get; init; }
        }

        private struct Grouse
        {
            public required int GrouseId { get; init; }
            public required Vector2 TreePosition { get; init; }
            public required Vector2 Position;
            public required Vector2 Velocity;
            public required GrouseState State;
            public required float StateTimer;
            public required float Scale;
            public required float Rotation;
            public required float FlightHeight;
            public required bool FacingLeft;
            public Vector2 GetExitDirection
            {
                get
                {
                    return FacingLeft ? new Vector2(-1, 0) : new Vector2(1, 0);
                }
            }
            public required float FlightTimer;
            public required bool HasPlayedFlushSound;
            public required bool HasBeenSpotted;
            public required int AnimationFrame;
            public required float AnimationTimer;
            public required float Alpha;
            public required float OriginalY;
            public required float? DamageFlashTimer;
            public required CollisionSmoke? Smoke;
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


