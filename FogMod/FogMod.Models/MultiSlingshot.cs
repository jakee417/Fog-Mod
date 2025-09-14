#nullable enable

using Microsoft.Xna.Framework.Input;
using StardewValley.Tools;
using StardewValley;
using Microsoft.Xna.Framework;
using StardewValley.Projectiles;
using System;
using System.Collections.Generic;

namespace FogMod.Models;

public class MultiSlingshot : Slingshot
{
    public static readonly List<int> directions = new List<int> { 0, 1, -1, 2, -2 };

    public struct MultiShot
    {
        public StardewValley.Object obj2;
        public float spreadAngle;
    }

    public MultiSlingshot() : base("34")
    { }

    protected override string loadDisplayName()
    {
        return "Multi-Slingshot";
    }

    public override string? getHoverBoxText(Item hoveredItem)
    {
        if (base.getHoverBoxText(hoveredItem) is string desc)
        {
            if (hoveredItem is StardewValley.Object obj && canThisBeAttached(obj))
                return desc;
            if (hoveredItem == null && attachments?[0] != null)
                return $"{desc}\nShoots {directions.Count} {attachments[0].DisplayName}s.";
        }
        return null;
    }

    public override void PerformFire(GameLocation location, Farmer who)
    {
        StardewValley.Object obj = attachments[0];
        if (obj != null)
        {
            updateAimPos();
            int x = aimPos.X;
            int y = aimPos.Y;
            int backArmDistance = GetBackArmDistance(who);
            Vector2 shootOrigin = GetShootOrigin(who);
            Vector2 baseVelocity = Utility.getVelocityTowardPoint(
                GetShootOrigin(who),
                AdjustForHeight(new Vector2(x, y)), (float)(15 + Game1.random.Next(4, 6)) * (1f + who.buffs.WeaponSpeedMultiplier)
            );
            if (backArmDistance > 4 && !canPlaySound)
            {
                List<MultiShot> projectiles = new List<MultiShot>();
                foreach (int i in directions)
                {
                    StardewValley.Object obj2 = (StardewValley.Object)obj.getOne();
                    if (obj.ConsumeStack(1) == null)
                    {
                        attachments[0] = null;
                        break;
                    }
                    float spreadAngle = i * 0.261799f;
                    projectiles.Add(new MultiShot { obj2 = obj2, spreadAngle = spreadAngle });
                }

                foreach (MultiShot shot in projectiles)
                {
                    StardewValley.Object obj2 = shot.obj2;
                    float spreadAngle = shot.spreadAngle;
                    float cos = (float)Math.Cos(spreadAngle);
                    float sin = (float)Math.Sin(spreadAngle);
                    Vector2 velocityTowardPoint = new Vector2(
                        baseVelocity.X * cos - baseVelocity.Y * sin,
                        baseVelocity.X * sin + baseVelocity.Y * cos
                    );

                    string text = base.ItemId;
                    float num = ((text == "33") ? 2f : ((!(text == "34")) ? 1f : 4f));
                    int ammoDamage = GetAmmoDamage(obj2);
                    string ammoCollisionSound = GetAmmoCollisionSound(obj2);
                    BasicProjectile.onCollisionBehavior ammoCollisionBehavior = GetAmmoCollisionBehavior(obj2);
                    if (!Game1.options.useLegacySlingshotFiring)
                    {
                        velocityTowardPoint.X *= -1f;
                        velocityTowardPoint.Y *= -1f;
                    }

                    location.projectiles.Add(
                        new BasicProjectile(
                            (int)(num * (float)(ammoDamage + Game1.random.Next(-(ammoDamage / 2), ammoDamage + 2)) * (1f + who.buffs.AttackMultiplier)),
                            -1,
                            0,
                            0,
                            (float)(Math.PI / (double)(64f + (float)Game1.random.Next(-63, 64))),
                            0f - velocityTowardPoint.X,
                            0f - velocityTowardPoint.Y,
                            shootOrigin - new Vector2(32f, 32f),
                            ammoCollisionSound,
                            null,
                            null,
                            explode: false,
                            damagesMonsters: true,
                            location,
                            who,
                            ammoCollisionBehavior,
                            obj2.ItemId
                        )
                        {
                            IgnoreLocationCollision = Game1.currentLocation.currentEvent != null || Game1.currentMinigame != null
                        }
                    );
                }
            }
        }
        else
        {
            Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:Slingshot.cs.14254"));
        }

        canPlaySound = true;
    }

    protected void updateAimPos()
    {
        if (lastUser == null || !lastUser.IsLocalPlayer)
        {
            return;
        }

        Point point = Game1.getMousePosition();
        if (Game1.options.gamepadControls && !Game1.lastCursorMotionWasMouse)
        {
            Vector2 vector = Game1.oldPadState.ThumbSticks.Left;
            if (vector.Length() < 0.25f)
            {
                vector.X = 0f;
                vector.Y = 0f;
                if (Game1.oldPadState.DPad.Down == ButtonState.Pressed)
                {
                    vector.Y = -1f;
                }
                else if (Game1.oldPadState.DPad.Up == ButtonState.Pressed)
                {
                    vector.Y = 1f;
                }

                if (Game1.oldPadState.DPad.Left == ButtonState.Pressed)
                {
                    vector.X = -1f;
                }

                if (Game1.oldPadState.DPad.Right == ButtonState.Pressed)
                {
                    vector.X = 1f;
                }

                if (vector.X != 0f && vector.Y != 0f)
                {
                    vector.Normalize();
                    vector *= 1f;
                }
            }

            Vector2 shootOrigin = GetShootOrigin(lastUser);
            if (!Game1.options.useLegacySlingshotFiring && vector.Length() < 0.25f)
            {
                switch (lastUser.FacingDirection)
                {
                    case 3:
                        vector = new Vector2(-1f, 0f);
                        break;
                    case 1:
                        vector = new Vector2(1f, 0f);
                        break;
                    case 0:
                        vector = new Vector2(0f, 1f);
                        break;
                    case 2:
                        vector = new Vector2(0f, -1f);
                        break;
                }
            }

            point = Utility.Vector2ToPoint(shootOrigin + new Vector2(vector.X, 0f - vector.Y) * 600f);
            point.X -= Game1.viewport.X;
            point.Y -= Game1.viewport.Y;
        }

        int x = point.X + Game1.viewport.X;
        int y = point.Y + Game1.viewport.Y;
        aimPos.X = x;
        aimPos.Y = y;
    }
}