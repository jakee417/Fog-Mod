#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using System.Collections.Generic;
using Netcode;
using System;
using StardewValley.TerrainFeatures;
using StardewValley;
using StardewValley.Projectiles;

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


        public class NetGrouse : Projectile
        {
            // Static variables
            public static readonly int[] wingPattern = { 0, 1, 2, 3, 4, 3, 2, 1 };


            public readonly NetInt grouseId = new NetInt();
            public readonly NetString location = new NetString();
            public readonly NetVector2 treePosition = new NetVector2();
            public readonly NetVector2 spawnPosition = new NetVector2();
            public readonly NetBool launchedByFarmer = new NetBool();
            public readonly NetVector2 velocity = new NetVector2();
            public readonly NetEnum<GrouseState> state = new NetEnum<GrouseState>();
            public readonly NetFloat stateTimer = new NetFloat();
            public readonly NetFloat scale = new NetFloat();
            public readonly NetFloat flightHeight = new NetFloat();
            public readonly NetBool facingLeft = new NetBool();
            public readonly NetFloat flightTimer = new NetFloat();
            public readonly NetBool hasPlayedFlushSound = new NetBool();
            public readonly NetBool hasPlayedKnockedDownSound = new NetBool();
            public readonly NetBool hasBeenSpotted = new NetBool();
            public readonly NetInt totalCycles = new NetInt();
            public readonly NetInt animationFrame = new NetInt();
            public readonly NetFloat animationTimer = new NetFloat();
            public readonly NetBool isHiding = new NetBool();
            public readonly NetFloat originalY = new NetFloat();
            public readonly NetBool hasDamageFlashTimer = new NetBool();
            public readonly NetFloat damageFlashTimer = new NetFloat();
            public readonly NetBool hasSmoke = new NetBool();
            public readonly NetVector2 smokePosition = new NetVector2();
            public readonly NetBool hasDroppedEgg = new NetBool();
            public readonly NetFloat hideTransitionProgress = new NetFloat();
            public readonly NetBool isTransitioning = new NetBool();

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

            public bool LaunchedByFarmer
            {
                get => launchedByFarmer.Value;
                private set => launchedByFarmer.Value = value;
            }

            public bool FacingLeft
            {
                get => facingLeft.Value;
                private set => facingLeft.Value = value;
            }

            // Mutable Property wrappers
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
                get => rotation;
                set => rotation = value;
            }

            public float FlightHeight
            {
                get => flightHeight.Value;
                set => flightHeight.Value = value;
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

            public bool HasPlayedKnockedDownSound
            {
                get => hasPlayedKnockedDownSound.Value;
                set => hasPlayedKnockedDownSound.Value = value;
            }

            public bool HasBeenSpotted
            {
                get => hasBeenSpotted.Value;
                set => hasBeenSpotted.Value = value;
            }

            public int TotalCycles
            {
                get => totalCycles.Value;
                set => totalCycles.Value = value;
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

            public bool IsHiding
            {
                get => isHiding.Value;
                set => isHiding.Value = value;
            }

            public float Alpha
            {
                get
                {
                    if (State == GrouseState.Perched && Game1.currentLocation is GameLocation location)
                    {
                        Tree? tree = TreeHelper.GetTreeFromId(location, TreePosition);
                        return tree?.alpha ?? alpha.Value;
                    }
                    return alpha.Value;
                }
                set => alpha.Value = value;
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

            public Vector2? Smoke
            {
                get => hasSmoke.Value ? smokePosition.Value : null;
                set
                {
                    hasSmoke.Value = value.HasValue;
                    if (value.HasValue)
                        smokePosition.Value = value.Value;
                }
            }

            public bool HasDroppedEgg
            {
                get => hasDroppedEgg.Value;
                set => hasDroppedEgg.Value = value;
            }

            public float HideTransitionProgress
            {
                get => hideTransitionProgress.Value;
                set => hideTransitionProgress.Value = value;
            }

            public bool IsTransitioning
            {
                get => isTransitioning.Value;
                set => isTransitioning.Value = value;
            }

            // Computed properties
            public Vector2 GetExitDirection => FacingLeft ? new Vector2(-1, 0) : new Vector2(1, 0);

            public bool NewAnimationFrame => AnimationTimer == 0f;

            public bool ReadyToBeRemoved => Alpha <= 0f;

            // Constructors
            protected override void InitNetFields()
            {
                base.InitNetFields();
                NetFields
                    .SetOwner(this)
                    .AddField(grouseId, "grouseId")
                    .AddField(location, "location")
                    .AddField(treePosition, "treePosition")
                    .AddField(spawnPosition, "spawnPosition")
                    .AddField(launchedByFarmer, "launchedByFarmer")
                    .AddField(velocity, "velocity")
                    .AddField(state, "state")
                    .AddField(stateTimer, "stateTimer")
                    .AddField(scale, "scale")
                    .AddField(flightHeight, "flightHeight")
                    .AddField(facingLeft, "facingLeft")
                    .AddField(flightTimer, "flightTimer")
                    .AddField(hasPlayedFlushSound, "hasPlayedFlushSound")
                    .AddField(hasPlayedKnockedDownSound, "hasPlayedKnockedDownSound")
                    .AddField(hasBeenSpotted, "hasBeenSpotted")
                    .AddField(totalCycles, "totalCycles")
                    .AddField(animationFrame, "animationFrame")
                    .AddField(animationTimer, "animationTimer")
                    .AddField(isHiding, "isHiding")
                    .AddField(alpha, "alpha")
                    .AddField(originalY, "originalY")
                    .AddField(hasDamageFlashTimer, "hasDamageFlashTimer")
                    .AddField(damageFlashTimer, "damageFlashTimer")
                    .AddField(hasSmoke, "hasSmoke")
                    .AddField(smokePosition, "smokePosition")
                    .AddField(hasDroppedEgg, "hasDroppedEgg")
                    .AddField(hideTransitionProgress, "hideTransitionProgress")
                    .AddField(isTransitioning, "isTransitioning");
            }

            public NetGrouse()
            {
                InitNetFields();
                Velocity = Vector2.Zero;
                State = GrouseState.Perched;
                StateTimer = 0f;
                Scale = GrouseScale;
                FlightHeight = 0f;
                FlightTimer = 0f;
                HasPlayedFlushSound = false;
                HasPlayedKnockedDownSound = false;
                HasBeenSpotted = false;
                AnimationFrame = 0;
                AnimationTimer = 0f;
                IsHiding = false;
                Alpha = 1.0f;
                DamageFlashTimer = null;
                Smoke = null;
                HasDroppedEgg = false;
                HideTransitionProgress = 0f;
                IsTransitioning = false;
                TotalCycles = 0;
            }

            public NetGrouse(int grouseId, Vector2 treePosition, Vector2 position, bool facingLeft, bool launchedByFarmer) : this()
            {
                GrouseId = grouseId;
                TreePosition = treePosition;
                LaunchedByFarmer = launchedByFarmer;
                Position = position;
                FacingLeft = facingLeft;
                OriginalY = position.Y;
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

            // Nerf the projectile built-in functions to make sure we have no side effects.
            public override void behaviorOnCollisionWithPlayer(GameLocation location, Farmer player)
            { }

            public override void behaviorOnCollisionWithTerrainFeature(TerrainFeature t, Vector2 tileLocation, GameLocation location)
            { }

            public override void behaviorOnCollisionWithOther(GameLocation location)
            { }

            public override void behaviorOnCollisionWithMonster(NPC n, GameLocation location)
            { }

            public override void updatePosition(GameTime time)
            { }

            public override bool update(GameTime time, GameLocation location)
            {
                return false;
            }

            protected override bool ShouldApplyCollisionLocally(GameLocation location)
            {
                return false;
            }

            protected override void updateTail(GameTime time)
            { }

            public override bool isColliding(GameLocation location, out Character target, out TerrainFeature terrainFeature)
            {
                target = null!;
                terrainFeature = null!;
                return false;
            }

            public override Rectangle getBoundingBox()
            {
                int width = (int)(GrouseSpriteWidth * Scale);
                int height = (int)(GrouseSpriteHeight * Scale);
                return new Rectangle(
                    (int)(Position.X - width / 2),
                    (int)(Position.Y - height / 2),
                    width,
                    height
                );
            }

            public override void draw(SpriteBatch b)
            {
                FogMod.Instance?.DrawSingleGrouse(spriteBatch: b, g: this);
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


