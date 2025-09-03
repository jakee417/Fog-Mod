#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using System.Collections.Generic;
using Netcode;
using StardewValley.TerrainFeatures;
using StardewValley;
using StardewValley.Projectiles;
using xTile.Dimensions;

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

            // Netcode variables
            public readonly NetInt grouseId = new NetInt();
            public readonly NetString locationName = new NetString();
            public readonly NetVector2 treePosition = new NetVector2();
            public readonly NetBool launchedByFarmer = new NetBool();
            public readonly NetEnum<GrouseState> state = new NetEnum<GrouseState>();
            public readonly NetFloat flightHeight = new NetFloat();
            public readonly NetBool facingLeft = new NetBool();

            // Immutable Property Wrappers
            public int GrouseId
            {
                get => grouseId.Value;
                private set => grouseId.Value = value;
            }

            public string LocationName
            {
                get => locationName.Value;
                private set => locationName.Value = value;
            }

            public Vector2 TreePosition
            {
                get => treePosition.Value;
                private set => treePosition.Value = value;
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
                get => new Vector2(xVelocity.Value, yVelocity.Value);
                set
                {
                    xVelocity.Value = value.X;
                    yVelocity.Value = value.Y;
                }
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

            public float FlightHeight
            {
                get => flightHeight.Value;
                set => flightHeight.Value = value;
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

            // Non-synced fields
            internal float Scale;
            internal float AnimationTimer { get; set; }
            internal float StateTimer { get; set; }
            internal float FlightTimer { get; set; }
            internal int AnimationFrame { get; set; }
            internal float HideTransitionProgress { get; set; }
            internal bool IsTransitioning { get; set; }
            internal int TotalCycles { get; set; }
            internal float OriginalY { get; set; }
            internal float? DamageFlashTimer;
            internal bool HasPlayedFlushSound;
            internal bool HasPlayedKnockedDownSound;
            internal bool HasBeenSpotted;
            internal bool IsHiding;
            internal Vector2? Smoke;
            internal bool HasDroppedEgg;


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
                    .AddField(locationName, "locationName")
                    .AddField(treePosition, "treePosition")
                    .AddField(launchedByFarmer, "launchedByFarmer")
                    .AddField(state, "state")
                    .AddField(flightHeight, "flightHeight")
                    .AddField(facingLeft, "facingLeft")
                    .AddField(alpha, "alpha");
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

            public NetGrouse(int grouseId, string locationName, Vector2 treePosition, Vector2 position, bool facingLeft, bool launchedByFarmer) : this()
            {
                GrouseId = grouseId;
                LocationName = locationName;
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
                AnimationFrame = 0;
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

            public override void draw(SpriteBatch b)
            {
                FogMod.Instance?.DrawSingleGrouse(spriteBatch: b, g: this);
            }

            public void UpdateGrouseAnimationState(float deltaSeconds)
            {
                AnimationTimer += deltaSeconds;
                float animationSpeed = ComputeAnimationSpeed();
                if (animationSpeed > 0f && AnimationTimer >= 1f / animationSpeed)
                {
                    AnimationTimer = 0f;
                    switch (State)
                    {
                        case GrouseState.Perched:
                            // Cycle through top sitting: sitting left (0) → sitting left (1)
                            AnimationFrame = (AnimationFrame + 1) % 2;
                            // Determine hiding state - vary per bird
                            int hideCycle = (TotalCycles + GrouseId) % 10;
                            bool wasHiding = IsHiding;
                            bool shouldHide = hideCycle >= 4;

                            // Start transition if hiding state changed
                            if (wasHiding != shouldHide)
                            {
                                IsTransitioning = true;
                                HideTransitionProgress = 0f;
                            }
                            IsHiding = shouldHide;
                            break;
                        case GrouseState.Surprised:
                            // Cycle through top row once: 0→1→2→3→4, then stay at 4
                            if (HasBeenSpotted && AnimationFrame < 4)
                                AnimationFrame++;
                            break;
                        case GrouseState.Flushing:
                        case GrouseState.Flying:
                            // Smooth wing cycle: 0→1→2→3→2→1→0→1→2→3...
                            AnimationFrame = (AnimationFrame + 1) % NetGrouse.wingPattern.Length;
                            break;
                        case GrouseState.KnockedDown:
                            AnimationFrame = 2;
                            break;
                    }
                    TotalCycles++;
                }

                // Update hide/show transition if needed
                if (IsTransitioning)
                {
                    switch (State)
                    {
                        case GrouseState.Perched:
                            HideTransitionProgress += deltaSeconds / GrouseTransitionDuration;
                            if (HideTransitionProgress >= 1f)
                            {
                                HideTransitionProgress = 1f;
                                IsTransitioning = false;
                            }
                            break;
                    }
                }
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


