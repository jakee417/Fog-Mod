#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using System;

namespace FogMod;

public partial class FogMod : Mod
{
    public Texture2D? scattergunTexture { get; set; }
    private Texture2D? armsBaseTexture;
    private Texture2D? armsRecoloredTexture;

    // Sprite source rectangles from weapon.png (32x32, 4 cells of 16x16)
    private static readonly Rectangle ScattergunSourceIcon = new(0, 0, 16, 16);
    private static readonly Rectangle ScattergunSourceRight = new(16, 0, 16, 16);
    private static readonly Rectangle ScattergunSourceDown = new(0, 16, 16, 16);
    private static readonly Rectangle ScattergunSourceUp = new(16, 16, 16, 16);

    // Arm source rectangles from BowArms.png (112x64)
    // Using the bow's "fully charged" arm poses since scattergun is always ready
    // Down: back arm pulling back, front arm forward
    private static readonly Rectangle ArmDownBack = new(32, 0, 16, 32);
    private static readonly Rectangle ArmDownFront = new(0, 32, 16, 32);
    // Up: back arm raised, front arm forward
    private static readonly Rectangle ArmUpBack = new(96, 0, 16, 32);
    private static readonly Rectangle ArmUpFront = new(0, 32, 16, 32);
    // Sideways: back arm and front arm extended
    private static readonly Rectangle ArmSideBack = new(48, 32, 16, 32);
    private static readonly Rectangle ArmSideFront = new(96, 32, 16, 32);

    // Placeholder skin/sleeve tones used in BowArms.png
    private static readonly Color SkinDarkTone = new(107, 0, 58);
    private static readonly Color SkinMediumTone = new(224, 107, 101);
    private static readonly Color SkinLightTone = new(249, 174, 137);
    private static readonly Color SleeveDarkTone = new(80, 80, 80);
    private static readonly Color SleeveMediumTone = new(135, 135, 135);
    private static readonly Color SleeveLightTone = new(154, 154, 154);

    internal static bool SuppressSlingshotDraw = false;

    /// Returns true if the given item ID is the scattergun (Galaxy Slingshot).
    public static bool IsScattergunId(string? itemId)
    {
        return itemId == Constants.GrouseRewardItemName;
    }

    public static bool IsGalaxyScattergun(Farmer who)
    {
        return who.UsingTool
            && who.CurrentTool is Slingshot slingshot
            && IsScattergunId(slingshot.ItemId);
    }

    internal void RecolorArmsTexture(Farmer farmer)
    {
        if (armsBaseTexture == null) return;

        Color[] data = new Color[armsBaseTexture.Width * armsBaseTexture.Height];
        armsBaseTexture.GetData(data);

        // Get farmer skin tones
        Texture2D skinColors = Helper.GameContent.Load<Texture2D>("Characters/Farmer/skinColors");
        Color[] skinData = new Color[skinColors.Width * skinColors.Height];
        skinColors.GetData(skinData);
        int which = Math.Clamp(farmer.skin.Value, 0, skinColors.Height - 1);
        Color skinDark = skinData[which * 3 % (skinColors.Height * 3)];
        Color skinMed = skinData[which * 3 % (skinColors.Height * 3) + 1];
        Color skinLight = skinData[which * 3 % (skinColors.Height * 3) + 2];

        // Get shirt color for sleeves
        bool isSleeveless = !farmer.ShirtHasSleeves();
        Color shirtColor = farmer.GetShirtColor();
        try
        {
            farmer.GetDisplayShirt(out var shirtTex, out var shirtIdx);
            Color[] shirtData = new Color[shirtTex.Bounds.Width * shirtTex.Bounds.Height];
            shirtTex.GetData(shirtData);
            int idx = shirtIdx * 8 / 128 * 32 * shirtTex.Bounds.Width + shirtIdx * 8 % 128 + shirtTex.Width * 4;
            if (idx - FarmerRenderer.shirtsTexture.Width * 2 >= 0 &&
                idx - FarmerRenderer.shirtsTexture.Width * 2 < shirtData.Length)
            {
                shirtColor = Utility.MakeCompletelyOpaque(
                    Utility.MultiplyColor(shirtData[idx - FarmerRenderer.shirtsTexture.Width * 2], farmer.GetShirtColor()));
            }
        }
        catch { }

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == SkinLightTone) data[i] = skinLight;
            else if (data[i] == SkinMediumTone) data[i] = skinMed;
            else if (data[i] == SkinDarkTone) data[i] = skinDark;

            if (data[i] == SleeveLightTone)
                data[i] = isSleeveless ? skinLight : Utility.MultiplyColor(SleeveLightTone, shirtColor);
            else if (data[i] == SleeveMediumTone)
                data[i] = isSleeveless ? skinMed : Utility.MultiplyColor(SleeveMediumTone, shirtColor);
            else if (data[i] == SleeveDarkTone)
                data[i] = isSleeveless ? skinDark : Utility.MultiplyColor(SleeveDarkTone, shirtColor);
        }

        armsRecoloredTexture ??= new Texture2D(Game1.graphics.GraphicsDevice,
                                                armsBaseTexture.Width, armsBaseTexture.Height);
        armsRecoloredTexture.SetData(data);
    }

    private static float GetScattergunAimRotation(Farmer who)
    {
        if (who.CurrentTool is not Slingshot slingshot)
            return 0f;

        Point point = Utility.Vector2ToPoint(
            slingshot.AdjustForHeight(Utility.PointToVector2(slingshot.aimPos.Value)));
        Vector2 shootOrigin = slingshot.GetShootOrigin(who);
        float rot = (float)Math.Atan2(point.Y - shootOrigin.Y, point.X - shootOrigin.X)
                    + MathHelper.Pi;
        rot -= MathHelper.Pi;
        if (rot < 0f) rot += MathHelper.TwoPi;
        return rot;
    }

    /// <summary>
    /// Returns the raw atan2 angle (in [-π, π]) from the farmer's shoot origin to the aim point.
    /// Used for down/up rotation so the gun barrel points directly at the reticle.
    /// </summary>
    private static float GetRawAimAngle(Farmer who)
    {
        if (who.CurrentTool is not Slingshot slingshot)
            return 0f;

        Point point = Utility.Vector2ToPoint(
            slingshot.AdjustForHeight(Utility.PointToVector2(slingshot.aimPos.Value)));
        Vector2 shootOrigin = slingshot.GetShootOrigin(who);
        return (float)Math.Atan2(point.Y - shootOrigin.Y, point.X - shootOrigin.X);
    }

    // === FarmerRenderer.draw prefix — suppress vanilla slingshot rendering ===
    public static void OnFarmerRendererDrawPrefix(Farmer who)
    {
        SuppressSlingshotDraw = false;
        if (IsGalaxyScattergun(who))
        {
            SuppressSlingshotDraw = true;
            who.usingSlingshot = false;
        }
    }

    // === FarmerRenderer.draw postfix — restore state and draw scattergun + arms ===
    public static void OnFarmerRendererDrawPostfix(
        SpriteBatch b, Vector2 position, Vector2 origin,
        float layerDepth, Color overrideColor, float rotation, float scale,
        Farmer who, ref Vector2 ___positionOffset)
    {
        if (!SuppressSlingshotDraw)
            return;

        who.usingSlingshot = true;
        SuppressSlingshotDraw = false;

        DrawScattergunWithArms(who, b, layerDepth, position, ___positionOffset, origin,
                            rotation, scale, overrideColor);
    }

    private static void DrawScattergunWithArms(
        Farmer who, SpriteBatch b, float layerDepth,
        Vector2 position, Vector2 positionOffset, Vector2 origin,
        float rotation, float scale, Color overrideColor)
    {
        if (Instance?.scattergunTexture == null || Instance.armsRecoloredTexture == null)
            return;

        Texture2D gunTex = Instance.scattergunTexture;
        Texture2D armsTex = Instance.armsRecoloredTexture;
        Vector2 baseOffset = position + origin + positionOffset + who.armOffset;
        // Fixed base without armOffset — so the gun butt stays anchored
        Vector2 fixedBase = position + origin + positionOffset;
        float aimRot = GetScattergunAimRotation(who);
        float rawAngle = GetRawAimAngle(who);
        var cfg = FogMod.ScattergunConfig;
        float gunScale = cfg.GunScale * scale;
        float armScale = cfg.ArmScale * scale;

        SpriteEffects flipEffect = who.FacingDirection == Game1.left
            ? SpriteEffects.FlipVertically : SpriteEffects.None;
        float depth = layerDepth;

        switch (who.FacingDirection)
        {
            case Game1.down:
                {
                    var d = cfg.Down;
                    // Down sprite barrel naturally points at +Y (π/2).
                    // Rotate so barrel points directly at the reticle.
                    float gunRot = rawAngle - MathHelper.PiOver2;
                    Vector2 gunPivot = new Vector2(d.GunPivot.X, d.GunPivot.Y);

                    // Back arm
                    b.Draw(armsTex, baseOffset + Vector2.Zero,
                           ArmDownBack, overrideColor, rotation, origin,
                           armScale, SpriteEffects.None, NextDepth(ref depth));

                    // Scattergun — butt anchored at fixed position, barrel points at reticle
                    b.Draw(gunTex, fixedBase + new Vector2(d.GunOffset.X, d.GunOffset.Y),
                           ScattergunSourceDown, Color.White, gunRot, gunPivot,
                           gunScale, SpriteEffects.None, NextDepth(ref depth));

                    // Front arm
                    b.Draw(armsTex, baseOffset + Vector2.Zero,
                           ArmDownFront, overrideColor, rotation, origin,
                           armScale, SpriteEffects.None, NextDepth(ref depth));
                }
                break;

            case Game1.right:
            case Game1.left:
                {
                    Vector2 originOffset = new Vector2(0f, -16f);
                    var side = who.FacingDirection == Game1.left ? cfg.Left : cfg.Right;
                    Vector2 specialOffset = new Vector2(side.SpecialOffset.X, side.SpecialOffset.Y);
                    float gunOffsetY = who.FacingDirection == Game1.left ? 1 : -1;

                    // Back arm
                    b.Draw(armsTex, baseOffset + specialOffset,
                           ArmSideBack, overrideColor, aimRot,
                           origin + originOffset, armScale,
                           flipEffect, 5.9E-05f);

                    // Scattergun — rotated toward aim, origin at left-center of sprite
                    float sideRotAdj = MathHelper.ToRadians(cfg.SideRotationOffset) * gunOffsetY;
                    Vector2 gunOrigin = new Vector2(0, ScattergunSourceRight.Height / 2f)
                                        - new Vector2(side.GunOriginOffset.X, side.GunOriginOffset.Y * gunOffsetY);
                    b.Draw(gunTex, baseOffset + specialOffset,
                           ScattergunSourceRight, Color.White, aimRot + sideRotAdj,
                           gunOrigin, gunScale,
                           flipEffect, NextDepth(ref depth));

                    // Front arm
                    b.Draw(armsTex, baseOffset + specialOffset,
                           ArmSideFront, overrideColor, aimRot,
                           new Vector2(0, ArmSideFront.Height / 2f),
                           armScale, flipEffect, NextDepth(ref depth));
                }
                break;

            case Game1.up:
                {
                    var u = cfg.Up;
                    // Up sprite barrel naturally points at -Y (-π/2).
                    // Rotate so barrel points directly at the reticle.
                    float gunRot = rawAngle + MathHelper.PiOver2;
                    Vector2 gunPivot = new Vector2(u.GunPivot.X, u.GunPivot.Y);

                    // Back arm
                    b.Draw(armsTex, baseOffset + Vector2.Zero,
                           ArmUpBack, overrideColor, rotation, origin,
                           armScale, SpriteEffects.None, depth - 0.001f);

                    // Scattergun — butt anchored at fixed position, barrel rotates
                    b.Draw(gunTex, fixedBase + new Vector2(u.GunOffset.X, u.GunOffset.Y),
                           ScattergunSourceUp, Color.White, gunRot, gunPivot,
                           gunScale, SpriteEffects.None, depth - 0.001f);

                    // Front arm
                    b.Draw(armsTex, baseOffset + Vector2.Zero,
                           ArmUpFront, overrideColor, rotation, origin,
                           armScale, SpriteEffects.None, depth - 0.001f);
                }
                break;
        }
    }

    private static float NextDepth(ref float depth)
    {
        depth += 1E-05f;
        return depth;
    }

    // === Called from Harmony.cs after projectiles fire ===
    internal static void OnScattergunFired(Vector2 shootOrigin, Vector2 aimPosition)
    {
        var cfg = FogMod.ScattergunConfig;

        // Play custom scattergun sound
        Game1.currentLocation?.playSound(Constants.ScattergunAudioCueId, Game1.player.Tile);

        // Spawn smoke halfway between barrel and reticle, drifting toward aim
        Vector2 smokePos = Vector2.Lerp(shootOrigin, aimPosition, 0.5f);
        Vector2 aimDir = aimPosition - shootOrigin;
        if (aimDir.LengthSquared() > 1e-3f)
            aimDir.Normalize();
        Instance?.SpawnScattergunSmoke(smokePos, cfg.SmokeRadius, cfg.SmokeCount, aimDir);

        // Orange muzzle flash
        string locationName = Game1.currentLocation?.NameOrUniqueName ?? "Unknown";
        var flash = new Models.ExplosionFlashInfo(
            locationName: locationName,
            centerWorld: smokePos,
            radiusPixels: cfg.SmokeRadius * 2f,
            timeLeft: Constants.ExplosionFlashDurationSeconds * 0.4f
        );
        Instance?.explosionFlashInfos.Add(flash);
    }

    // === Slingshot.tickUpdate prefix — suppress drawback sound, allow instant fire ===
    // Set canPlaySound = false BEFORE the original runs. This prevents the
    // "pullItemFromWater" sound from playing (it only plays when canPlaySound is true).
    // The original still runs (returns true) so aim position and other state updates normally.
    // canPlaySound staying false means the fire condition (!canPlaySound) is immediately true.
    // We play a click sound once when the player first starts aiming.
    private static bool _wasAiming = false;
    public static void OnSlingshotTickUpdatePrefix(Slingshot __instance, ref bool ___canPlaySound)
    {
        if (!IsScattergunId(__instance.ItemId))
            return;

        // Detect the very first frame of a new aim (player wasn't aiming last tick)
        Farmer who = Game1.player;
        bool isAiming = who.usingSlingshot;
        if (isAiming && !_wasAiming)
        {
            Game1.currentLocation?.playSound(Constants.ClickAudioCueId, Game1.player.Tile);
        }
        _wasAiming = isAiming;

        ___canPlaySound = false;
    }

    // === Slingshot.drawInMenu prefix — show scattergun icon in menus/toolbar ===
    public static bool OnSlingshotDrawInMenuPrefix(
        Slingshot __instance, SpriteBatch spriteBatch, Vector2 location,
        float scaleSize, float transparency, float layerDepth,
        StackDrawType drawStackNumber, Color color, bool drawShadow)
    {
        if (!IsScattergunId(__instance.ItemId) || Instance?.scattergunTexture == null)
            return true;

        spriteBatch.Draw(
            Instance.scattergunTexture,
            location + new Vector2(32f, 32f),
            ScattergunSourceIcon,
            color * transparency,
            0f,
            new Vector2(8f, 8f),
            scaleSize * 4f,
            SpriteEffects.None,
            layerDepth
        );

        // Draw ammo count like vanilla slingshot
        if (drawStackNumber != StackDrawType.Hide && __instance.attachments[0] is StardewValley.Object ammo)
        {
            Utility.drawTinyDigits(
                ammo.Stack,
                spriteBatch,
                location + new Vector2(64 - Utility.getWidthOfTinyDigitString(ammo.Stack, 3f * scaleSize) - 3f * scaleSize,
                                       64f - 18f * scaleSize + 2f),
                3f * scaleSize,
                1f,
                Color.White
            );
        }

        return false;
    }
}
