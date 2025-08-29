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

            public FogForecast(bool isFogDay, float dailyFogStrength, float probabilityOfFogForADay, float probabilityOfFogRoll)
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

            public FogParticle(Texture2D texture, float scale, float rotation, Vector2 position, Vector2 velocity, float alpha, float ageSeconds, bool isFadingOut, float fadeOutSecondsLeft)
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
            public int[,]? Counts;
            public List<int>[,]? Indices;

            public CellOccupancy(int width, int height)
            {
                Counts = new int[width, height];
                Indices = new List<int>[width, height];
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


