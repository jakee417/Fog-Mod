#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Netcode;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Tools;
using StardewModdingAPI;
using System.Collections.Generic;

namespace FogMod.Models;

public enum CritterzState
{
    Sitting,
    Running,
}

// Experimental shootable Critterz. The loop mirrors the grouse: Critterz sit
// at bushes, a trigger picks a destination bush, and they run a legal path
// to it — except the destination needs pathfinding instead of flight.
public class Critterz : Monster
{
    // MARK: Netcode variables
    public readonly NetInt critterzId = new NetInt();
    public readonly NetEnum<CritterzState> state = new NetEnum<CritterzState>();
    public readonly NetBool flipped = new NetBool();
    public readonly NetInt skin = new NetInt();
    public readonly NetVector2 homeBush = new NetVector2();

    // MARK: Immutable Property Wrappers
    public int CritterzId
    {
        get => critterzId.Value;
        protected set => critterzId.Value = value;
    }

    // MARK: Mutable Property wrappers
    public CritterzState State
    {
        get => state.Value;
        set => state.Value = value;
    }

    public bool Flip
    {
        get => flipped.Value;
        set => flipped.Value = value;
    }

    public int Skin
    {
        get => skin.Value;
        protected set => skin.Value = value;
    }

    public int BaseFrame
    {
        get => Skin * Constants.CritterRowLength;
    }

    public Vector2 HomeBush
    {
        get => homeBush.Value;
        set => homeBush.Value = value;
    }

    public int SittingFrame
    {
        get => BaseFrame;
    }

    public List<string> ObjectToDrop
    {
        get => new List<string>(objectsToDrop);
        set => objectsToDrop.Set(value);
    }

    public Vector2 TilePosition
    {
        get => new Vector2((float)Math.Floor(Position.X / Game1.tileSize), (float)Math.Floor(Position.Y / Game1.tileSize));
    }

    public override bool IsVillager => false;

    public override Stack<Dialogue> CurrentDialogue
    {
        get
        {
            // Disable all dialogue since the Critterz is not a villager
            Stack<Dialogue> currentDialogue = new Stack<Dialogue>();
            return currentDialogue;
        }
        set
        { }
    }

    // MARK: Non-synced fields
    public List<Vector2>? Path;
    public Vector2? DestBush;

    // MARK: Constructors

    public Critterz() : base()
    {
        // Compatibility with https://www.nexusmods.com/stardewvalley/mods/22672
        modData.Add("Junimatic.NotScary", "true");
        displayName = Constants.CritterzName;
        initNetFields();
        Slipperiness = 0;
        IsWalkingTowardPlayer = false;
        collidesWithOtherCharacters.Value = false;
        farmerPassesThrough = true;
        DamageToFarmer = 0;
        MaxHealth = Constants.CritterzMaxHealth;
        Health = MaxHealth;
        missChance.Value = Constants.CritterzMissRate;
        resilience.Value = 0;
        ExperienceGained = 0;
        mineMonster.Value = false;
        reloadSprite();
        Reset();
    }

    public Critterz(int critterzId, GameLocation location, Vector2 tilePosition, bool flip, Vector2 homeBush, int skin) : this()
    {
        Name = Constants.CritterzName;
        displayName = Constants.CritterzName;
        CritterzId = critterzId;
        currentLocation = location;
        // Hidden at the center of the home bush tile.
        Position = tilePosition * Game1.tileSize + new Vector2(32f, 32f);
        Flip = flip;
        FacingDirection = flip ? 3 : 1;
        HomeBush = homeBush;
        Skin = skin;
        reloadSprite();
        ObjectToDrop = GetCritterzLootToDrop();
    }

    // MARK: Monster Overrides
    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields
            .SetOwner(this)
            .AddField(critterzId, "critterzId")
            .AddField(state, "state")
            .AddField(flipped, "flipped")
            .AddField(skin, "skin")
            .AddField(homeBush, "homeBush");

        state.fieldChangeEvent += (NetEnum<CritterzState> field, CritterzState oldValue, CritterzState newValue) =>
        {
            // Farmhands never run the update loop, so rebuild the animation
            // from synced state on transitions.
            if (newValue == CritterzState.Running)
                SetHopAnimation();
            else
                SetSittingFrame();
        };
    }

    public override void update(GameTime time, GameLocation location)
    { }

    public override void reloadSprite(bool onlyAppearance = false)
    {
        Sprite = new AnimatedSprite();
        Sprite.spriteTexture = FogMod.Instance?.critterzTexture;
        if (Sprite.spriteTexture == null)
            FogMod.Instance?.Monitor.Log("Critterz texture is missing - make sure every player installed the same mod version including assets/critterz.png.", LogLevel.Warn);
        Sprite.SpriteWidth = 32;
        Sprite.SpriteHeight = 32;
        Sprite.SourceRect = new Rectangle(0, 0, Sprite.SpriteWidth, Sprite.SpriteHeight);
        Sprite.loop = true;
        if (State == CritterzState.Running)
            SetHopAnimation();
        else
            SetSittingFrame();
        HideShadow = false;
    }

    public override int takeDamage(int damage, int xTrajectory, int yTrajectory, bool isBomb, double addedPrecision, Farmer who)
    {
        int actualDamage = Math.Max(1, damage - (int)resilience.Value);
        // Same weapon rules as the grouse: slingshots and bombs only, and
        // hidden Critterz can't be hit, like perched grouse.
        bool legalWeapons = (who.CurrentTool is Slingshot) || isBomb;
        if (State == CritterzState.Sitting || !legalWeapons)
            actualDamage = -1;
        else if (Game1.random.NextDouble() < missChance.Value - missChance.Value * addedPrecision)
            actualDamage = -1;
        else
        {
            Vector2 spriteCenter = Position - new Vector2(64f, 64f);

            Health -= actualDamage;
            setTrajectory(xTrajectory / 2, yTrajectory / 2);
            Color puffColor = Color.LightGray;

            // Death effects
            if (Health <= 0)
            {
                if (currentLocation is GameLocation loc)
                {
                    loc.playSound("magma_sprite_hit", TilePosition);
                    TemporaryAnimatedSprite poof = new TemporaryAnimatedSprite(28, spriteCenter, puffColor, 6)
                    {
                        interval = 50f
                    };
                    Utility.makeTemporarySpriteJuicier(poof, loc, 2);
                }
            }
            else
            {
                if (currentLocation is GameLocation loc)
                {
                    loc.playSound("hitEnemy", TilePosition);
                    TemporaryAnimatedSprite poof = new TemporaryAnimatedSprite(28, spriteCenter, puffColor, 3)
                    {
                        interval = 50f
                    };
                    Utility.makeTemporarySpriteJuicier(poof, loc, 1);
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
            FogMod.Instance?.Monitor.Log($"Critterz {CritterzId} health <= 0 and will be removed.", LogLevel.Trace);
            return true;
        }
        return false;
    }

    public override Rectangle GetBoundingBox()
    {
        // Mirror Critter.getBoundingBox(0, 0).
        return new Rectangle(
            (int)Position.X - 32,
            (int)Position.Y - 16,
            64,
            32
        );
    }

    public override void draw(SpriteBatch b)
    {
        // Character.sprite is a net field, but AnimatedSprite only syncs
        // textureName (plus width/height) and re-resolves pixels with
        // ContentManager.Load on arrival. A ModContent texture has no content
        // name, so farmhands always deserialize with spriteTexture == null
        // (shadow still draws; the engine calls DrawShadow separately).
        // Repair it locally: spriteTexture is a plain field, so this touches
        // no net state. The grouse sidesteps this by never drawing via Sprite.
        if (Sprite != null && Sprite.spriteTexture == null)
        {
            Sprite.spriteTexture = FogMod.Instance?.critterzTexture;
            if (State == CritterzState.Running)
                SetHopAnimation();
            else
                SetSittingFrame();
        }
        if (Sprite?.spriteTexture == null)
            return;
        // Coy: fully hidden inside the bush until triggered.
        if (State == CritterzState.Sitting)
            return;

        // Vanilla critters animate in update(); the mod drives movement from
        // the update tick instead, so animate here like the grouse does. This
        // also keeps farmhands animating without running the update loop.
        Sprite.animateOnce(Game1.currentGameTime);

        // Mirror Critter.draw (Critterz never leave y offsets at 0).
        Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, Position + new Vector2(-64f, -128f));
        Sprite.draw(
            b,
            screenPos,
            Position.Y / 10000f + Position.X / 1000000f,
            0,
            0,
            Color.White,
            Flip,
            4f,
            0f,
            false
        );
    }

    public override void DrawShadow(SpriteBatch b)
    {
        if (State == CritterzState.Sitting)
            return;
        // Mirror the Critter.draw shadow with zero y offsets.
        Vector2 shadowPosition = Game1.GlobalToLocal(Game1.viewport, Position + new Vector2(0f, -4f));
        b.Draw(
            Game1.shadowTexture,
            shadowPosition,
            Game1.shadowTexture.Bounds,
            Color.White,
            0f,
            new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y),
            3f,
            SpriteEffects.None,
            (Position.Y - 1f) / 10000f
        );
    }

    public override void resetForNewDay(int dayOfMonth)
    { }

    // MARK: Functions
    public void Reset()
    {
        State = CritterzState.Sitting;
        Path = null;
        DestBush = null;
        invincibleCountdown = 0;
    }

    public void SetHopAnimation()
    {
        if (Sprite?.spriteTexture == null)
            return;
        // Full Critterz row: sitting -> jumping -> sitting.
        Sprite.setCurrentAnimation(new List<FarmerSprite.AnimationFrame>
        {
            new FarmerSprite.AnimationFrame(BaseFrame + 0, 70),
            new FarmerSprite.AnimationFrame(BaseFrame + 1, 50),
            new FarmerSprite.AnimationFrame(BaseFrame + 2, 50),
            new FarmerSprite.AnimationFrame(BaseFrame + 3, 90),
            new FarmerSprite.AnimationFrame(BaseFrame + 4, 50),
            new FarmerSprite.AnimationFrame(BaseFrame + 5, 50),
            new FarmerSprite.AnimationFrame(BaseFrame + 6, 70),
        });
        Sprite.loop = true;
    }

    public void SetSittingFrame()
    {
        if (Sprite?.spriteTexture == null)
            return;
        // A one-frame looping animation renders as a static sitting frame.
        Sprite.setCurrentAnimation(new List<FarmerSprite.AnimationFrame>
        {
            new FarmerSprite.AnimationFrame(SittingFrame, 100),
        });
        Sprite.loop = true;
    }

    // Kill loot, pre-rolled at spawn into the vanilla objectsToDrop list
    // (dropped by GameLocation.monsterDrop, like the grouse snacks). One
    // universal table: the critter stays generic, the loot carries the theme.
    protected static List<string> GetCritterzLootToDrop()
    {
        List<string> loot = new List<string>();
        float luck = (float)Game1.player.DailyLuck;
        // Common forage/trash mix: cave carrot, hazelnut, acorn, pine cone,
        // trash, driftwood, broken glasses, broken CD, soggy newspaper.
        string[] commons = { "78", "408", "309", "311", "168", "169", "170", "171", "172" };
        int maxCommons = luck > 0.0f ? 3 : 2;
        int numCommons = FogMod.Random.Next(0, maxCommons);
        for (int i = 0; i < numCommons; i++)
            loot.Add(commons[FogMod.Random.Next(commons.Length)]);
        // Uncommon rabbit product.
        if (FogMod.Random.NextDouble() < (luck > 0.0f ? 0.15 : 0.10))
            loot.Add("440"); // Wool
        // Rare chase item (cf. Serpent 0.8%).
        if (FogMod.Random.NextDouble() < (luck > 0.0f ? 0.05 : 0.03))
            loot.Add("446"); // Rabbit's Foot
        return loot;
    }

    public bool RemoveCritterzForPosition()
    {
        // Pathing keeps Critterz in bounds; this is only a backstop.
        if (Game1.currentLocation is GameLocation location && IsCritterzOffLocation(this, location))
        {
            FogMod.Instance?.Monitor.Log($"Critterz {CritterzId} is off location and will be removed.", LogLevel.Trace);
            return true;
        }
        return false;
    }

    public static bool IsCritterzOffLocation(Critterz r, GameLocation location)
    {
        // Mirror the vanilla Critter.update out-of-bounds check.
        Rectangle locationBounds = new Rectangle(0, 0, location.Map.Layers[0].LayerWidth * Game1.tileSize, location.Map.Layers[0].LayerHeight * Game1.tileSize);
        return r.Position.X < -128f
            || r.Position.Y < -128f
            || r.Position.X > locationBounds.Width
            || r.Position.Y > locationBounds.Height;
    }

}
