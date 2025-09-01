#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using System.Collections.Generic;
using Netcode;
using System;

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

        public struct CollisionSmoke : IComparable
        {
            public Vector2 Position { get; init; }

            public CollisionSmoke(Vector2 position)
            {
                Position = position;
            }

            public int CompareTo(object? obj)
            {
                if (obj is CollisionSmoke other)
                {
                    return Position.GetHashCode().CompareTo(other.Position.GetHashCode());
                }
                return 0;
            }
        }

        public class NetGrouse : INetObject<NetFields>
        {
            // Static variables
            public static readonly int[] wingPattern = { 0, 1, 2, 3, 4, 3, 2, 1 };

            public NetFields NetFields { get; } = new NetFields("Grouse");

            // Core identity and location
            public NetInt grouseId = new NetInt();
            public NetString location = new NetString();
            public NetVector2 treePosition = new NetVector2();
            public NetVector2 spawnPosition = new NetVector2();

            // Dynamic state
            public NetVector2 position = new NetVector2();
            public NetVector2 velocity = new NetVector2();
            public NetEnum<GrouseState> state = new NetEnum<GrouseState>();
            public NetFloat stateTimer = new NetFloat();

            // Visual properties
            public NetFloat scale = new NetFloat();
            public NetFloat rotation = new NetFloat();
            public NetFloat flightHeight = new NetFloat();
            public NetBool facingLeft = new NetBool();

            // Animation and timers
            public NetFloat flightTimer = new NetFloat();
            public NetBool hasPlayedFlushSound = new NetBool();
            public NetBool hasBeenSpotted = new NetBool();
            public NetInt animationFrame = new NetInt();
            public NetFloat animationTimer = new NetFloat();
            public NetFloat alpha = new NetFloat();
            public NetFloat originalY = new NetFloat();

            // Optional properties (using nullable pattern)
            public NetBool hasDamageFlashTimer = new NetBool();
            public NetFloat damageFlashTimer = new NetFloat();

            // Smoke collision
            public NetBool hasSmoke = new NetBool();
            public NetVector2 smokePosition = new NetVector2();

            // State flags
            public NetBool hasDroppedEgg = new NetBool();

            // Property wrappers for clean access (following SDV pattern)
            // Immutable properties - can only be set during construction
            public int GrouseId
            {
                get => grouseId.Value;
                private set => grouseId.Value = value;
            }

            public string Location
            {
                get => location.Value;
                private set => location.Value = value;
            }

            public Vector2 TreePosition
            {
                get => treePosition.Value;
                private set => treePosition.Value = value;
            }

            public Vector2 SpawnPosition
            {
                get => spawnPosition.Value;
                private set => spawnPosition.Value = value;
            }

            // Mutable properties - can be changed during gameplay
            public Vector2 Position
            {
                get => position.Value;
                set => position.Value = value;
            }

            public Vector2 Velocity
            {
                get => velocity.Value;
                set => velocity.Value = value;
            }

            public GrouseState State
            {
                get => state.Value;
                set
                {
                    state.Value = value;
                    ResetStateAnimations();
                }
            }

            public float StateTimer
            {
                get => stateTimer.Value;
                set => stateTimer.Value = value;
            }

            public float Scale
            {
                get => scale.Value;
                set => scale.Value = value;
            }

            public float Rotation
            {
                get => rotation.Value;
                set => rotation.Value = value;
            }

            public float FlightHeight
            {
                get => flightHeight.Value;
                set => flightHeight.Value = value;
            }

            public bool FacingLeft
            {
                get => facingLeft.Value;
                set => facingLeft.Value = value;
            }

            public float FlightTimer
            {
                get => flightTimer.Value;
                set => flightTimer.Value = value;
            }

            public bool HasPlayedFlushSound
            {
                get => hasPlayedFlushSound.Value;
                set => hasPlayedFlushSound.Value = value;
            }

            public bool HasBeenSpotted
            {
                get => hasBeenSpotted.Value;
                set => hasBeenSpotted.Value = value;
            }

            public int AnimationFrame
            {
                get => animationFrame.Value;
                set => animationFrame.Value = value;
            }

            public float AnimationTimer
            {
                get => animationTimer.Value;
                set => animationTimer.Value = value;
            }

            public float Alpha
            {
                get => alpha.Value;
                set => alpha.Value = value;
            }

            public bool ReadyToBeRemoved
            {
                get => alpha.Value <= 0f;
            }

            public float OriginalY
            {
                get => originalY.Value;
                set => originalY.Value = value;
            }

            public float? DamageFlashTimer
            {
                get => hasDamageFlashTimer.Value ? damageFlashTimer.Value : null;
                set
                {
                    hasDamageFlashTimer.Value = value.HasValue;
                    if (value.HasValue)
                        damageFlashTimer.Value = value.Value;
                }
            }

            public CollisionSmoke? Smoke
            {
                get => hasSmoke.Value ? new CollisionSmoke(smokePosition.Value) : null;
                set
                {
                    hasSmoke.Value = value.HasValue;
                    if (value.HasValue)
                        smokePosition.Value = value.Value.Position;
                }
            }

            public bool HasDroppedEgg
            {
                get => hasDroppedEgg.Value;
                set => hasDroppedEgg.Value = value;
            }

            // Computed properties
            public Vector2 GetExitDirection => FacingLeft ? new Vector2(-1, 0) : new Vector2(1, 0);

            public NetGrouse()
            {
                // Set owner before adding fields to avoid warnings
                NetFields.SetOwner(this);

                NetFields
                    .AddField(grouseId)
                    .AddField(location)
                    .AddField(treePosition)
                    .AddField(spawnPosition)
                    .AddField(position)
                    .AddField(velocity)
                    .AddField(state)
                    .AddField(stateTimer)
                    .AddField(scale)
                    .AddField(rotation)
                    .AddField(flightHeight)
                    .AddField(facingLeft)
                    .AddField(flightTimer)
                    .AddField(hasPlayedFlushSound)
                    .AddField(hasBeenSpotted)
                    .AddField(animationFrame)
                    .AddField(animationTimer)
                    .AddField(alpha)
                    .AddField(originalY)
                    .AddField(hasDamageFlashTimer)
                    .AddField(damageFlashTimer)
                    .AddField(hasSmoke)
                    .AddField(smokePosition)
                    .AddField(hasDroppedEgg);
            }

            public NetGrouse(int grouseId, string locationName, Vector2 treePosition, Vector2 spawnPosition, bool facingLeft) : this()
            {
                GrouseId = grouseId;
                Location = locationName;
                TreePosition = treePosition;
                SpawnPosition = spawnPosition;
                Position = spawnPosition;
                Velocity = Vector2.Zero;
                State = GrouseState.Perched;
                StateTimer = 0f;
                Scale = GrouseScale;
                Rotation = 0f;
                FlightHeight = 0f;
                FlightTimer = 0f;
                HasPlayedFlushSound = false;
                HasBeenSpotted = false;
                AnimationFrame = 0;
                AnimationTimer = 0f;
                Alpha = 1.0f;
                OriginalY = spawnPosition.Y;
                DamageFlashTimer = null;
                Smoke = null;
                HasDroppedEgg = false;
            }

            // Public functions
            public static int GetDeterministicId(int locationSeed, int daySeed, Vector2 treePosition, int? salt)
            {
                int baseId = (locationSeed.GetHashCode() ^ daySeed ^ (int)(treePosition.X * 1000 + treePosition.Y * 1000)) & 0x7FFFFFFF;
                return salt.HasValue ? baseId ^ salt.Value : baseId;
            }

            public void ResetStateAnimations()
            {
                StateTimer = 0f;
                AnimationTimer = 0f;
            }

            public float ComputeAnimationSpeed()
            {
                float animationSpeed = State switch
                {
                    GrouseState.Perched => 0.5f,
                    GrouseState.Surprised => 3f,
                    GrouseState.Flushing => 36f,
                    GrouseState.Flying => 12f,
                    GrouseState.KnockedDown => 0f, // No animation when knocked down
                    _ => 1f
                };
                return animationSpeed;
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


