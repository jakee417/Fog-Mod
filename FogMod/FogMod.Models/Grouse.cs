#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Netcode;
using StardewValley.TerrainFeatures;
using StardewValley;
using FogMod.Utils;
using StardewValley.Monsters;
using StardewValley.Tools;
using StardewModdingAPI;

namespace FogMod.Models;

public enum GrouseState
{
    Perched,
    Surprised,
    Flushing,
    Flying,
    Landing,
}

public class Grouse : Monster
{
    // MARK: Static variables
    public static readonly int[] wingPattern = { 0, 1, 2, 3, 4, 3, 2, 1 };

    // MARK: Netcode variables
    public readonly NetInt grouseId = new NetInt();
    public readonly NetVector2 treePosition = new NetVector2();
    public readonly NetBool launchedByFarmer = new NetBool();
    public readonly NetEnum<GrouseState> state = new NetEnum<GrouseState>();
    public readonly NetBool isHiding = new NetBool();

    // MARK: Immutable Property Wrappers
    public int GrouseId
    {
        get => grouseId.Value;
        protected set => grouseId.Value = value;
    }

    public bool LaunchedByFarmer
    {
        get => launchedByFarmer.Value;
        protected set => launchedByFarmer.Value = value;
    }

    // MARK: Mutable Property wrappers
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
        set => state.Value = value;
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
                _ => 1f
            };
        }
        set { }
    }

    public bool FacingLeft
    {
        get
        {
            switch (FacingDirection)
            {
                case 1:
                    lastDirection = 1;
                    return false;
                case 3:
                    lastDirection = 3;
                    return true;
            }
            return lastDirection == 3;
        }
        set { }
    }

    public string? ObjectToDrop
    {
        get => objectsToDrop.Count > 0 ? objectsToDrop[0] : null;
        set
        {
            objectsToDrop.Clear();
            if (value is string item)
                objectsToDrop.Add(item);
        }
    }

    public int AnimationFrame
    {
        get => animationFrame;
        set
        {
            if (Game1.currentLocation is GameLocation loc && value != animationFrame)
            {
                switch (State)
                {
                    case GrouseState.Perched:
                        break;
                    case GrouseState.Surprised:
                        if (Utils.Multiplayer.IsAbleToUpdateOwnWorld() && value == 4)
                            loc.playSound("crow", TilePosition);
                        break;
                    case GrouseState.Flushing:
                    case GrouseState.Flying:
                    case GrouseState.Landing:
                        if (value == 3)
                            loc.localSound("parrot_flap", TilePosition, 0);
                        break;
                }
            }
            animationFrame = value;
        }
    }

    public bool IsTransitioning
    {
        get => isTransitioning;
        set
        {
            if (Game1.currentLocation is GameLocation loc && value != isTransitioning)
            {
                switch (State)
                {
                    case GrouseState.Perched:
                        if (Utils.Multiplayer.IsAbleToUpdateOwnWorld() && value)
                            loc.playSound("leafrustle", TilePosition);
                        if (TreeHelper.GetTreeFromId(currentLocation, TreePosition) is Tree tree)
                            TreeHelper.TriggerFallingLeaves(
                                tree,
                                Position + new Vector2(0, Constants.GrouseSpriteHeight * (7 / 8)) * Scale,
                                numLeaves: 3
                            );
                        break;
                    case GrouseState.Surprised:
                        break;
                    case GrouseState.Flushing:
                    case GrouseState.Flying:
                    case GrouseState.Landing:
                        break;
                }
            }
            isTransitioning = value;
        }
    }

    public int HideSoundTimer
    {
        get => hideSoundTimer;
        set
        {
            if (Game1.currentLocation is GameLocation loc)
            {
                switch (State)
                {
                    case GrouseState.Perched:
                        if (Utils.Multiplayer.IsAbleToUpdateOwnWorld() && IsHiding && value == 0 && value != hideSoundTimer)
                            loc.playSound(Constants.GrouseAudioCueId, TilePosition);
                        break;
                    case GrouseState.Surprised:
                        break;
                    case GrouseState.Flushing:
                    case GrouseState.Flying:
                    case GrouseState.Landing:
                        break;
                }
            }
            hideSoundTimer = value;
        }
    }

    public uint CyclesInState
    {
        get => (uint)Math.Abs(totalCycles) % Constants.GrouseHidingCycles;
        set => totalCycles = (int)value;
    }

    public Vector2 GetExitDirection => FacingLeft ? new Vector2(-1, 0) : new Vector2(1, 0);


    // MARK: Non-synced fields
    public float AnimationTimer;
    public float StateTimer;
    public float FlightTimer;
    public float HideTransitionProgress;
    public Vector2? TargetTreePosition;

    // MARK: Non-synced protected fields
    protected int hideSoundTimer;
    protected bool isTransitioning;
    protected int animationFrame;
    protected int lastDirection = 1;
    protected float alpha;
    protected int totalCycles;

    // MARK: Constructors
    public Grouse() : base()
    {
        initNetFields();
        Slipperiness = 0;
        IsWalkingTowardPlayer = false;
        collidesWithOtherCharacters.Value = false;
        farmerPassesThrough = true;
        DamageToFarmer = 0;
        MaxHealth = Constants.GrouseMaxHealth;
        Health = MaxHealth;
        missChance.Value = Constants.GrouseMissRate;
        resilience.Value = 0;
        ExperienceGained = 0;
        mineMonster.Value = false;
        Reset();
    }

    public Grouse(int grouseId, GameLocation location, Vector2 treePosition, Vector2 position, bool launchedByFarmer) : this()
    {
        // Do not count launched grouse towards kill count
        Name = launchedByFarmer ? Constants.GrouseName + "_launched" : Constants.GrouseName;
        GrouseId = grouseId;
        currentLocation = location;
        TreePosition = treePosition;
        LaunchedByFarmer = launchedByFarmer;
        Position = position;
        FacingDirection = Utilities.DeterministicBool(Position, GrouseId) ? 3 : 1;
        ObjectToDrop = GetItemToDrop(launchedByFarmer);
        // Start on a random cycle to desync multiple grouse
        CyclesInState = (uint)GrouseId;
    }

    // MARK: Monster Overrides
    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields
            .SetOwner(this)
            .AddField(grouseId, "grouseId")
            .AddField(treePosition, "treePosition")
            .AddField(launchedByFarmer, "launchedByFarmer")
            .AddField(state, "state")
            .AddField(isHiding, "isHiding");

        treePosition.fieldChangeEvent += (NetVector2 field, Vector2 oldValue, Vector2 newValue) =>
        {
            Reset();
        };

        state.fieldChangeEvent += (NetEnum<GrouseState> field, GrouseState oldValue, GrouseState newValue) =>
        {
            ResetStateAnimations();
            switch (newValue)
            {
                case GrouseState.Perched:
                case GrouseState.Surprised:
                    Velocity = Vector2.Zero;
                    if (TreeHelper.GetTreeFromId(currentLocation, TreePosition) is Tree tree)
                    {
                        tree.Location.localSound("leafrustle", tree.Tile);
                        TreeHelper.TriggerFallingLeaves(tree, Position, numLeaves: 5);
                    }
                    break;
                case GrouseState.Flushing:
                case GrouseState.Flying:
                case GrouseState.Landing:
                    break;
            }
        };

        isHiding.fieldChangeEvent += (NetBool field, bool oldValue, bool newValue) =>
        {
            IsTransitioning = true;
            HideTransitionProgress = 0f;
            int availableCycles = Constants.GrouseHidingCycles - 4;
            HideSoundTimer = newValue ? FogMod.Random.Next(availableCycles * 3 / 8, availableCycles * 5 / 8) : -1;
        };
    }

    public override void update(GameTime time, GameLocation location)
    { }

    public override void reloadSprite(bool onlyAppearance = false)
    {
        Sprite = null;
        HideShadow = false;
    }

    public override int takeDamage(int damage, int xTrajectory, int yTrajectory, bool isBomb, double addedPrecision, Farmer who)
    {
        int actualDamage = Math.Max(1, damage - (int)resilience.Value);
        // No cherry picking...
        bool legalWeapons = (who.CurrentTool is Slingshot) || isBomb;
        if (State == GrouseState.Perched || State == GrouseState.Surprised || !legalWeapons)
            actualDamage = -1;
        else if (Game1.random.NextDouble() < missChance.Value - missChance.Value * addedPrecision)
            actualDamage = -1;
        else
        {
            Vector2 spriteCenter = Position - new Vector2(
                Constants.GrouseSpriteWidth * Scale / 2,
                Constants.GrouseSpriteHeight * Scale / 2
            );

            Health -= actualDamage;
            setTrajectory(xTrajectory / 2, yTrajectory / 2);

            // Death effects
            if (Health <= 0)
            {
                if (currentLocation is GameLocation loc)
                {
                    loc.playSound("magma_sprite_hit", TilePosition);
                    TemporaryAnimatedSprite featherAnimation = new TemporaryAnimatedSprite(28, spriteCenter, Color.SaddleBrown, 6)
                    {
                        interval = 50f
                    };
                    Utility.makeTemporarySpriteJuicier(featherAnimation, loc, 2);
                }
                if (isBomb)
                {
                    AddSmokePuffs(spriteCenter);
                    // Fry the whatever item we had
                    if (ObjectToDrop != null)
                        ObjectToDrop = "194";
                }
            }
            else
            {
                if (currentLocation is GameLocation loc)
                {
                    loc.playSound("hitEnemy", TilePosition);
                    TemporaryAnimatedSprite featherAnimation = new TemporaryAnimatedSprite(28, spriteCenter, Color.SaddleBrown, 3)
                    {
                        interval = 50f
                    };
                    Utility.makeTemporarySpriteJuicier(featherAnimation, loc, 1);
                }
            }
        }
        return actualDamage;
    }

    public override void drawAboveAllLayers(SpriteBatch b)
    { }

    public override void drawAboveAlwaysFrontLayer(SpriteBatch b)
    { }

    protected override void updateAnimation(GameTime time)
    { }

    public override void behaviorAtGameTick(GameTime time)
    { }

    public override void MovePosition(GameTime time, xTile.Dimensions.Rectangle viewport, GameLocation currentLocation)
    { }

    protected override void localDeathAnimation()
    { }

    protected override void sharedDeathAnimation()
    { }

    public override void shedChunks(int number, float scale)
    { }

    public override bool isInvincible()
    {
        return false;
    }

    public override void collisionWithFarmerBehavior()
    { }

    public override bool ShouldMonsterBeRemoved()
    {
        if (Health <= 0)
        {
            FogMod.Instance?.Monitor.Log($"Grouse {GrouseId} health <= 0 and will be removed.", LogLevel.Trace);
            return true;
        }
        return false;
    }

    public override Rectangle GetBoundingBox()
    {
        int scaledWidth = (int)(Constants.GrouseSpriteWidth * Scale);
        int scaledHeight = (int)(Constants.GrouseSpriteHeight * Scale);
        return new Rectangle(
            (int)Position.X - scaledWidth / 2,
            (int)Position.Y - scaledHeight,
            scaledWidth,
            scaledHeight
        );
    }

    public override void draw(SpriteBatch b)
    {
        float deltaSeconds = (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;
        UpdateGrouseAnimationState(deltaSeconds);
        if (State == GrouseState.Perched)
            DrawPerchedGrouse(b, this);
        else
            DrawMovingGrouse(b, this);
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

    public override void resetForNewDay(int dayOfMonth)
    { }

    // MARK: Functions
    public void Reset()
    {
        Velocity = Vector2.Zero;
        State = GrouseState.Perched;
        StateTimer = 0f;
        Scale = Constants.GrouseScale;
        FlightTimer = 0f;
        AnimationFrame = 0;
        AnimationTimer = 0f;
        IsHiding = false;
        Alpha = 1.0f;
        HideTransitionProgress = 0f;
        IsTransitioning = false;
        TargetTreePosition = null;
        HideSoundTimer = -1;
        invincibleCountdown = 0;
    }

    public bool RemoveGrouseForPosition()
    {
        bool isTargeting = TargetTreePosition is Vector2;
        bool isFlying = (State == GrouseState.Flushing || State == GrouseState.Flying || State == GrouseState.Landing) && !isTargeting;
        bool offLocation = isFlying && IsGrouseOffLocation(this, Game1.currentLocation);
        if (offLocation)
        {
            FogMod.Instance?.Monitor.Log($"Grouse {GrouseId} is off location and will be removed.", LogLevel.Trace);
            return true;
        }
        return false;
    }

    public static bool IsGrouseOffLocation(Grouse g, GameLocation location)
    {
        Rectangle locationBounds = new Rectangle(0, 0, location.Map.Layers[0].LayerWidth * Game1.tileSize, location.Map.Layers[0].LayerHeight * Game1.tileSize);
        return !locationBounds.Contains(new Point((int)g.Position.X, (int)g.Position.Y));
    }

    protected static string? GetItemToDrop(bool launchedByFarmer)
    {
        // No spamming grouse to get eggs
        if (launchedByFarmer)
            return null;

        double roll = FogMod.Random.NextDouble();
        string? eggItemId = null;
        // Daily luck
        float luck = (float)Game1.player.DailyLuck;
        // https://mateusaquino.github.io/stardewids/
        // Golden egg
        if (roll < luck + 0.01)
            eggItemId = "928";
        // Large egg
        else if (roll < luck + 0.1)
            eggItemId = "174";
        // Basic egg
        else if (roll < luck + 0.8)
            eggItemId = "176";
        return eggItemId;
    }

    // MARK: Update Animation Functions
    public void ResetStateAnimations()
    {
        StateTimer = 0f;
        AnimationTimer = 0f;
        AnimationFrame = 0;
        CyclesInState = 0;
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
                    AnimationFrame = (AnimationFrame + 1) % wingPattern.Length;
                    break;
            }
            CyclesInState++;
        }

        if (IsTransitioning)
        {
            HideTransitionProgress += deltaSeconds / Constants.GrouseTransitionDuration;
            if (HideTransitionProgress >= 1f)
            {
                HideTransitionProgress = 1f;
                IsTransitioning = false;
            }
        }
    }

    public void UpdateGrouseHidingLogic()
    {
        int exposedCycles = 4;
        bool shouldHide = CyclesInState >= exposedCycles;
        if (Utils.Multiplayer.IsAbleToUpdateOwnWorld() && IsHiding != shouldHide)
            IsHiding = shouldHide;
        HideSoundTimer--;
    }

    // MARK: Render Functions
    public void AddSmokePuffs(Vector2 position)
    {
        Utility.addSmokePuff(currentLocation, position, 0, 4f, 0.01f, 1f, 0.01f);
        Utility.addSmokePuff(currentLocation, position + new Vector2(32f, 16f), 400, 4f, 0.01f, 1f, 0.02f);
        Utility.addSmokePuff(currentLocation, position + new Vector2(-32f, -16f), 200, 4f, 0.01f, 1f, 0.02f);
        Utility.addSmokePuff(currentLocation, position + new Vector2(0f, 32f), 200, 4f, 0.01f, 1f, 0.01f);
        Utility.addSmokePuff(currentLocation, position, 0, 3f, 0.01f, 1f, 0.02f);
        Utility.addSmokePuff(currentLocation, position + new Vector2(21f, 16f), 500, 3f, 0.01f, 1f, 0.01f);
        Utility.addSmokePuff(currentLocation, position + new Vector2(-32f, -21f), 100, 3f, 0.01f, 1f, 0.02f);
        Utility.addSmokePuff(currentLocation, position + new Vector2(0f, 32f), 250, 3f, 0.01f, 1f, 0.01f);
    }

    public static void DrawPerchedGrouse(SpriteBatch spriteBatch, Grouse g)
    {
        if (FogMod.Instance?.grouseTexture == null || g.Alpha <= 0f)
            return;

        float hideProgress = g.IsHiding ? 1f : 0f;
        if (g.IsTransitioning)
        {
            hideProgress = g.IsHiding ? g.HideTransitionProgress : (1f - g.HideTransitionProgress);
        }
        if (hideProgress >= 1f)
            return;

        Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, g.Position);
        const int maxSpriteHeight = 7;
        const float popUpDistance = 8f;
        float currentSpriteHeight = MathHelper.Lerp(maxSpriteHeight, 0f, hideProgress);
        float verticalOffset = MathHelper.Lerp(-popUpDistance, 0f, hideProgress);
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
            FogMod.Instance?.grouseTexture,
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

    public static void DrawMovingGrouse(SpriteBatch spriteBatch, Grouse g)
    {
        if (FogMod.Instance?.grouseTexture == null || g.Alpha <= 0f)
            return;

        Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, g.Position);

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
                frameX = wingPattern[g.AnimationFrame % wingPattern.Length];
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
            FogMod.Instance?.grouseTexture,
            position: screenPos,
            sourceRectangle: sourceRect,
            color: Color.White * g.Alpha,
            rotation: 0f,
            origin: origin,
            scale: scale,
            effects: effects,
            layerDepth: 0.85f
        );
        if (g.State == GrouseState.Surprised && g.AnimationFrame == 4)
            DrawGrouseEmote(spriteBatch, screenPos, scale);
    }

    public static void DrawGrouseEmote(SpriteBatch spriteBatch, Vector2 screenPos, float scale)
    {
        if (FogMod.Instance?.surprisedTexture is Texture2D surprisedTexture)
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