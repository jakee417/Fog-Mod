#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Netcode;
using StardewValley.TerrainFeatures;
using StardewValley;
using FogMod.Utils;
using StardewValley.Monsters;

namespace FogMod.Models;

public enum GrouseState
{
    Perched,
    Surprised,
    Flushing,
    Flying,
    Landing,
    KnockedDown
}

public class NetGrouse : Monster
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
    public readonly NetVector2 targetTreePosition = new NetVector2();
    public readonly NetBool isHiding = new NetBool();
    public readonly NetInt hideSoundTimer = new NetInt();

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

    public bool LaunchedByFarmer
    {
        get => launchedByFarmer.Value;
        private set => launchedByFarmer.Value = value;
    }

    // Mutable Property wrappers
    public Vector2 TreePosition
    {
        get => treePosition.Value;
        set => treePosition.Value = value;
    }

    public Vector2 TilePosition
    {
        get => new Vector2((float)Math.Floor(Position.X / Game1.tileSize), (float)Math.Floor(Position.Y / Game1.tileSize));
    }

    public Vector2 Velocity
    {
        get => new Vector2(xVelocity, yVelocity);
        set
        {
            xVelocity = value.X;
            yVelocity = value.Y;
        }
    }

    public GrouseState State
    {
        get => state.Value;
        set
        {
            ResetStateAnimations();
            state.Value = value;
            switch (state.Value)
            {
                case GrouseState.Perched:
                case GrouseState.Surprised:
                    Velocity = Vector2.Zero;
                    break;
                case GrouseState.Flushing:
                case GrouseState.Flying:
                case GrouseState.Landing:
                    break;
                case GrouseState.KnockedDown:
                    Velocity = new Vector2(Velocity.X * 0.8f, Math.Max(Velocity.Y + 100f, 150f));
                    FlightHeight = 0f;
                    Alpha = 1.0f;
                    Vector2 screenPosition = Game1.GlobalToLocal(Game1.viewport, Position);
                    screenPosition.Y -= Constants.GrouseSpriteHeight * Scale / 2f;
                    DamageFlashTimer = Constants.GrouseDamageFlashDuration;
                    Smoke = screenPosition;
                    AnimationFrame = 2;
                    FogMod.DropFeatherAtImpact(Position, LocationName, GrouseId);
                    break;
            }

        }
    }

    public float FlightHeight
    {
        get => flightHeight.Value;
        set => flightHeight.Value = value;
    }

    internal bool IsHiding
    {
        get => isHiding.Value;
        set => isHiding.Value = value;
    }

    internal int HideSoundTimer
    {
        get => hideSoundTimer.Value;
        set => hideSoundTimer.Value = value;
    }

    public Vector2? TargetTreePosition
    {
        get
        {
            return targetTreePosition.Value == Vector2.Zero ? null : targetTreePosition.Value;
        }
        set
        {
            if (value is Vector2 newValue)
            {
                targetTreePosition.Value = newValue;
            }
            else
            {
                targetTreePosition.Value = Vector2.Zero;
            }
        }
    }

    public float Alpha
    {
        get
        {
            if (State == GrouseState.Perched && Game1.currentLocation is GameLocation location)
            {
                Tree? tree = TreeHelper.GetTreeFromId(location, TreePosition);
                return tree?.alpha ?? alpha;
            }
            return alpha;
        }
        set => alpha = value;
    }

    public float AnimationSpeed
    {
        get
        {
            return State switch
            {
                GrouseState.Perched => 0.5f,
                GrouseState.Surprised => 3f,
                GrouseState.Flushing => 36f,
                GrouseState.Flying => 12f,
                GrouseState.Landing => 36f,
                GrouseState.KnockedDown => 0f,
                _ => 1f
            };
        }
        set { }
    }

    public bool FacingLeft
    {
        get
        {
            return Velocity.X < 0;
        }
        set { }
    }

    // Non-synced fields
    internal float AnimationTimer;
    internal float StateTimer;
    internal float FlightTimer;
    internal int AnimationFrame;
    internal float HideTransitionProgress;
    internal bool IsTransitioning;
    internal int TotalCycles = 0;
    internal float FallProgress;
    internal float? DamageFlashTimer;
    internal bool HasPlayedFlushSound;
    internal bool HasPlayedKnockedDownSound;
    internal Vector2? Smoke;
    internal bool HasDroppedEgg;
    internal float alpha;
    internal TexturePack Textures;


    // Computed properties
    public Vector2 GetExitDirection => FacingLeft ? new Vector2(-1, 0) : new Vector2(1, 0);

    public bool NewAnimationFrame => AnimationTimer == 0f;

    // Constructors
    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields
            .SetOwner(this)
            .AddField(grouseId, "grouseId")
            .AddField(locationName, "locationName")
            .AddField(treePosition, "treePosition")
            .AddField(launchedByFarmer, "launchedByFarmer")
            .AddField(state, "state")
            .AddField(flightHeight, "flightHeight")
            .AddField(targetTreePosition, "targetTreePosition")
            .AddField(isHiding, "isHiding")
            .AddField(hideSoundTimer, "hideSoundTimer");
    }

    public NetGrouse()
    {
        initNetFields();
        Reset();
    }

    public void Reset()
    {
        Velocity = Vector2.Zero;
        State = GrouseState.Perched;
        StateTimer = 0f;
        Scale = Constants.GrouseScale;
        FlightHeight = 0f;
        FlightTimer = 0f;
        HasPlayedFlushSound = false;
        HasPlayedKnockedDownSound = false;
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
        TargetTreePosition = null;
        HideSoundTimer = 0;
        Health = 1;
    }

    public NetGrouse(int grouseId, TexturePack textures, string locationName, Vector2 treePosition, Vector2 position, bool launchedByFarmer) : this()
    {
        GrouseId = grouseId;
        LocationName = locationName;
        TreePosition = treePosition;
        LaunchedByFarmer = launchedByFarmer;
        Position = position;
        FallProgress = 0f;
        Textures = textures;
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

    public override void draw(SpriteBatch b)
    {
        DrawAGrouse(spriteBatch: b);
    }

    public override void DrawShadow(SpriteBatch b)
    {
        if (Alpha <= 0f)
            return;

        Vector2 shadowPosition = Game1.GlobalToLocal(Game1.viewport, Position + new Vector2(0, GetBoundingBox().Height / 2));
        float shadowScale = Scale * 0.5f;
        Color shadowColor = Color.Gray * Alpha * 0.5f;
        b.Draw(
            Game1.shadowTexture,
            shadowPosition,
            Game1.shadowTexture.Bounds,
            shadowColor,
            0f,
            new Vector2(Game1.shadowTexture.Width / 2f, Game1.shadowTexture.Height / 2f),
            shadowScale,
            SpriteEffects.None,
            Math.Max(0f, StandingPixel.Y / 10000f) - 1E-06f
        );
    }

    public override void MovePosition(GameTime time, xTile.Dimensions.Rectangle viewport, GameLocation currentLocation)
    { }

    public override bool isInvincible()
    {
        // We will use our own damage handling.
        return true;
    }

    public override int takeDamage(int damage, int xTrajectory, int yTrajectory, bool isBomb, double addedPrecision, Farmer who)
    {
        return 0;
    }

    public override bool ShouldMonsterBeRemoved()
    {
        return RemoveGrouse(this, Game1.currentLocation);
    }

    internal static bool RemoveGrouse(NetGrouse g, GameLocation location)
    {
        bool offLocation = (g.State == GrouseState.Flushing || g.State == GrouseState.Flying ||
                               (g.State == GrouseState.Landing && g.StateTimer > 30f)) && IsGrouseOffLocation(g, location);
        return offLocation || g.Health <= 0;
    }

    private static bool IsGrouseOffLocation(NetGrouse g, GameLocation location)
    {
        Rectangle locationBounds = new Rectangle(0, 0, location.Map.Layers[0].LayerWidth * Game1.tileSize, location.Map.Layers[0].LayerHeight * Game1.tileSize);
        return !locationBounds.Contains(new Point((int)g.Position.X, (int)g.Position.Y));
    }



    public struct TexturePack
    {
        public Texture2D? GrouseTexture;
        public Texture2D? DamageTexture;
        public Texture2D? SurprisedTexture;

        public TexturePack(Texture2D? grouseTexture, Texture2D? damageTexture, Texture2D? surprisedTexture)
        {
            GrouseTexture = grouseTexture;
            DamageTexture = damageTexture;
            SurprisedTexture = surprisedTexture;
        }
    }

    internal void DrawAGrouse(SpriteBatch spriteBatch)
    {
        float deltaSeconds = (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;
        UpdateGrouseAnimationState(deltaSeconds);
        PlayGrouseNoise();

        if (!Utility.isOnScreen(Position, Game1.tileSize))
            return;

        if (State == GrouseState.Perched)
            DrawPerchedGrouse(spriteBatch, this);
        else
            DrawMovingGrouse(spriteBatch, this);
    }

    public void UpdateGrouseAnimationState(float deltaSeconds)
    {
        AnimationTimer += deltaSeconds;
        if (AnimationSpeed > 0f && AnimationTimer >= 1f / AnimationSpeed)
        {
            AnimationTimer = 0f;
            switch (State)
            {
                case GrouseState.Perched:
                    // Cycle through top sitting: sitting left (0) → sitting left (1)
                    AnimationFrame = (AnimationFrame + 1) % 2;
                    UpdateGrouseHidingLogic();
                    break;
                case GrouseState.Surprised:
                    // Cycle through top row once: 0→1→2→3→4, then stay at 4
                    if (AnimationFrame < 4)
                        AnimationFrame++;
                    break;
                case GrouseState.Flushing:
                case GrouseState.Flying:
                case GrouseState.Landing:
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
                    HideTransitionProgress += deltaSeconds / Constants.GrouseTransitionDuration;
                    if (HideTransitionProgress >= 1f)
                    {
                        HideTransitionProgress = 1f;
                        IsTransitioning = false;
                    }
                    break;
            }
        }
    }

    internal void UpdateGrouseHidingLogic()
    {
        int exposedCycles = 4;
        int hideCycle = (TotalCycles + GrouseId) % Constants.GrouseHidingCycles;
        bool shouldHide = hideCycle >= exposedCycles;

        // Start transition if hiding state changed
        if (IsHiding != shouldHide)
        {
            IsTransitioning = true;
            HideTransitionProgress = 0f;
            HideSoundTimer = shouldHide ? FogMod.Random.Next(0, Constants.GrouseHidingCycles - exposedCycles) : 0;
            IsHiding = shouldHide;
        }
        else
            HideSoundTimer--;
    }

    internal void PlayGrouseNoise()
    {
        if (Game1.currentLocation is GameLocation loc)
        {
            switch (State)
            {
                case GrouseState.Perched:
                    if (IsTransitioning && NewAnimationFrame)
                        loc.localSound("leafrustle", TilePosition);
                    else if (IsHiding && HideSoundTimer == 0)
                    {
                        loc.localSound(Constants.GrouseAudioCueId, TilePosition);
                        // This will push us past 0 which disables further sounds until next cycle
                        HideSoundTimer--;
                    }
                    break;
                case GrouseState.Surprised:
                    if (AnimationFrame == 4 && !HasPlayedFlushSound)
                    {
                        loc.localSound("crow", TilePosition);
                        HasPlayedFlushSound = true;
                    }
                    else if (!LaunchedByFarmer && NewAnimationFrame && AnimationFrame < 2)
                    {
                        loc.localSound("leafrustle", TilePosition);
                    }
                    break;
                case GrouseState.Flushing:
                case GrouseState.Flying:
                case GrouseState.Landing:
                    if (AnimationFrame == 3 && NewAnimationFrame)
                        loc.localSound("fishSlap", TilePosition);
                    break;
                case GrouseState.KnockedDown:
                    if (!HasPlayedKnockedDownSound)
                    {
                        loc.localSound("hitEnemy", TilePosition);
                        HasPlayedKnockedDownSound = true;
                    }
                    break;
            }
        }
    }

    private void DrawPerchedGrouse(SpriteBatch spriteBatch, NetGrouse g)
    {
        if (Textures.GrouseTexture == null || g.Alpha <= 0f)
            return;

        // Calculate hide/show animation
        float hideProgress = g.IsHiding ? 1f : 0f;
        if (g.IsTransitioning)
        {
            hideProgress = g.IsHiding ? g.HideTransitionProgress : (1f - g.HideTransitionProgress);
        }

        // If completely hidden, don't draw
        if (hideProgress >= 1f)
            return;

        Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, g.Position);
        screenPos.Y += g.FlightHeight;

        // Calculate vertical offset and sprite height based on hide progress
        const int maxSpriteHeight = 7;  // Full grouse sprite height
        const float popUpDistance = 8f;  // How far up the grouse pops when appearing

        // Interpolate sprite height and position
        float currentSpriteHeight = MathHelper.Lerp(maxSpriteHeight, 0f, hideProgress);
        float verticalOffset = MathHelper.Lerp(-popUpDistance, 0f, hideProgress);

        // Apply vertical offset to screen position
        screenPos.Y += verticalOffset;

        SpriteEffects effects = g.FacingLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        int frameX = g.AnimationFrame % 2;
        Rectangle sourceRect = new Rectangle(
            frameX * Constants.GrouseSpriteWidth,
            0,
            Constants.GrouseSpriteWidth,
            (int)Math.Max(1, currentSpriteHeight)
        );
        Vector2 origin = new Vector2(Constants.GrouseSpriteWidth / 2f, Constants.GrouseSpriteHeight);
        spriteBatch.Draw(
            Textures.GrouseTexture,
            position: screenPos,
            sourceRectangle: sourceRect,
            color: Color.White * g.Alpha,
            rotation: 0f,
            origin: origin,
            scale: g.Scale,
            effects: effects,
            layerDepth: 0.85f
        );
    }

    private void DrawMovingGrouse(SpriteBatch spriteBatch, NetGrouse g)
    {
        if (Textures.GrouseTexture == null || g.Alpha <= 0f)
            return;

        Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, g.Position);
        screenPos.Y += g.FlightHeight;

        int frameX = 0;
        int frameY = 0;
        SpriteEffects effects = g.FacingLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        float scale = g.Scale;

        switch (g.State)
        {
            case GrouseState.Surprised:
                frameX = g.AnimationFrame;
                frameY = 0;
                break;
            case GrouseState.Flushing:
            case GrouseState.Flying:
            case GrouseState.Landing:
                frameX = NetGrouse.wingPattern[g.AnimationFrame % NetGrouse.wingPattern.Length];
                frameY = 1;
                scale *= 1.2f;
                break;
            case GrouseState.KnockedDown:
                frameX = g.AnimationFrame;
                frameY = 1;
                scale *= 1.2f;
                break;
        }
        Rectangle sourceRect = new Rectangle(
            frameX * Constants.GrouseSpriteWidth,
            frameY * Constants.GrouseSpriteHeight,
            Constants.GrouseSpriteWidth,
            Constants.GrouseSpriteHeight
        );
        Vector2 origin = new Vector2(Constants.GrouseSpriteWidth / 2f, Constants.GrouseSpriteHeight);
        spriteBatch.Draw(
            Textures.GrouseTexture,
            position: screenPos,
            sourceRectangle: sourceRect,
            color: Color.White * g.Alpha,
            rotation: 0f,
            origin: origin,
            scale: scale,
            effects: effects,
            layerDepth: 0.85f
        );

        if (Textures.DamageTexture is Texture2D damageTexture && g.DamageFlashTimer is float damageFlashTimer && damageFlashTimer > 0f && g.Smoke is Vector2 smoke)
        {
            float ratio = damageFlashTimer / Constants.GrouseDamageFlashDuration;
            int damageFrameY = 0;
            int damageFrameX = 0;
            if (ratio < 0.33)
                damageFrameX = 2;
            else if (ratio > 0.33 && ratio < 0.66)
                damageFrameX = 1;
            Rectangle damageRect = new Rectangle(
                damageFrameX * Constants.DamageSpriteWidth,
                damageFrameY * Constants.DamageSpriteHeight,
                Constants.DamageSpriteWidth,
                Constants.DamageSpriteHeight
            );
            spriteBatch.Draw(
                damageTexture,
                position: smoke,
                sourceRectangle: damageRect,
                color: Color.White * ratio,
                rotation: (float)Math.Sin(ratio * 2.0f * Math.PI),
                origin: new Vector2(Constants.DamageSpriteWidth / 2f, Constants.DamageSpriteHeight / 2f),
                scale: 1.5f,
                effects: SpriteEffects.None,
                layerDepth: 0.86f
            );
        }
        if (Textures.SurprisedTexture is Texture2D surprisedTexture && g.State == GrouseState.Surprised && g.AnimationFrame == 4)
        {
            Vector2 surprisedPos = screenPos;
            surprisedPos.Y -= Constants.GrouseSpriteHeight * scale * 1.02f;
            spriteBatch.Draw(
                surprisedTexture,
                position: surprisedPos,
                sourceRectangle: null,
                color: Color.White,
                rotation: 0f,
                origin: new Vector2(surprisedTexture.Width / 2f, surprisedTexture.Height),
                scale: Constants.SurprisedSpriteScale,
                effects: SpriteEffects.None,
                layerDepth: 0.86f
            );
        }
    }
}