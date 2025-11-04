#nullable enable
using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using FogMod.Models;
using FogMod.Utils;

namespace FogMod;

public partial class FogMod : Mod
{
    // Bomb
    public static void OnBombExplodedPostfix(GameLocation __instance, Vector2 tileLocation, int radius, Farmer who, bool damageFarmers, int damage_amount, bool destroyObjects)
    {
        try
        {
            if (FogMod.Instance == null) return;
            Vector2 center = tileLocation * Game1.tileSize + new Vector2(32f, 32f);
            float radiusPx = Math.Max(Game1.tileSize, radius * Game1.tileSize * 2f);
            string location = __instance?.NameOrUniqueName ?? "Unknown";
            ExplosionFlashInfo info = new ExplosionFlashInfo(
                locationName: location,
                centerWorld: center,
                radiusPixels: radiusPx,
                timeLeft: Constants.ExplosionFlashDurationSeconds
            );
            Utils.Multiplayer.SendMessage(info);
            FogMod.HandleExplosion(info);
        }
        catch
        {
            FogMod.Instance?.Monitor.Log($"OnBombExplodedPostfix failed - IsMainPlayer: {Context.IsMainPlayer}, Location: {__instance?.Name}", LogLevel.Error);
        }
    }

    public static void HandleExplosion(ExplosionFlashInfo info)
    {
        FogMod.Instance?.explosionFlashInfos.Add(info);
        if (Config.EnableExplosionSmoke)
            FogMod.Instance?.SpawnExplosionSmoke(info.CenterWorld, info.RadiusPixels);
    }

    // TV Weather Report
    public static void ProceedToNextScenePrefix(TV __instance, out int __state)
    {
        try
        {
            __state = __instance.GetType().GetField("currentChannel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(__instance) is int v ? v : 0;
        }
        catch
        {
            __state = 0;
        }
    }

    public static void ProceedToNextScenePostfix(TV __instance, int __state)
    {
        try
        {
            int newChannel = __instance.GetType().GetField("currentChannel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(__instance) is int v ? v : 0;

            // Only run the weather postfix when we transitioned from the weather channel (2)
            // to channel 0 (TV turned off / finished). This avoids replacing the weather dialogue.
            if (__state == 2 && newChannel == 0)
            {
                OnWeatherForecastPostfix();
            }
        }
        catch { }
    }

    public static void OnWeatherForecastPostfix()
    {
        try
        {
            int daysPlayed = (int)Game1.stats.DaysPlayed + 1;
            FogForecast forecast = FogMod.ComputeFogForecast(daysPlayed);
            string text = "And no fog!";
            // Either the player wants fog everyday or it gets forecasted.
            if (!FogMod.Config.EnableDailyRandomFog is bool enableDailyRandomFog && enableDailyRandomFog || forecast.IsFogDay)
            {
                string playersName = Game1.player.Name;
                text = $"And bad news! the fog's getting thicker... and {playersName}'s getting larger!";
            }
            Game1.drawObjectDialogue(Game1.parseText(text));
        }
        catch { }
    }

    // Grouse Surprising
    public static void OnTreePerformToolActionPostfix(Tree __instance, Tool t, int explosion, Vector2 tileLocation)
    {
        try
        {
            FogMod.HandleGrouseSurprise(__instance);
        }
        catch (Exception ex)
        {
            FogMod.Instance?.Monitor.Log($"OnTreePerformToolActionPostfix failed: {ex.Message}", LogLevel.Error);
        }
    }

    public static void OnTreeShakePostfix(Tree __instance, Vector2 tileLocation, bool doEvenIfStillShaking)
    {
        try
        {
            FogMod.HandleGrouseSurprise(__instance);
        }
        catch (Exception ex)
        {
            FogMod.Instance?.Monitor.Log($"OnTreeShakePostfix failed: {ex.Message}", LogLevel.Error);
        }
    }

    public static void HandleGrouseSurprise(Tree tree)
    {
        if (FogMod.Instance == null || !FogMod.Config.EnableGrouseCritters)
            return;

        Vector2 position = TreeHelper.GetTreePosition(tree);
        if (FogMod.Instance.GetNPCsAtCurrentLocation() is NetCollection<NPC> npc)
            foreach (NPC p in npc)
            {
                if (p is Grouse g && g.State == GrouseState.Perched)
                {
                    if (g.TreePosition == position)
                    {
                        if (Utils.Multiplayer.IsAbleToUpdateOwnWorld())
                            g.State = GrouseState.Surprised;
                        else
                            Utils.Multiplayer.SendMessage(
                                new GrouseEventInfo(
                                    grouseId: g.GrouseId,
                                    _event: GrouseEventInfo.EventType.Flushed,
                                    timestamp: DateTime.UtcNow.Ticks
                                )
                            );
                        break;
                    }
                }
            }
    }

    public static bool OnSlingshotPerformFirePrefix(StardewValley.Tools.Slingshot __instance, GameLocation location, Farmer who)
    {
        try
        {
            // Not our multi-slingshot, run original
            if (__instance.ItemId != Constants.GrouseRewardItemName)
                return true;

            StardewValley.Object obj = __instance.attachments[0];
            if (obj == null)
            {
                Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:Slingshot.cs.14254"));
                __instance.canPlaySound = true;
                return false;
            }

            int backArmDistance = __instance.GetBackArmDistance(who);
            Vector2 shootOrigin = __instance.GetShootOrigin(who);
            Vector2 aimPosition = __instance.AdjustForHeight(new Vector2(__instance.aimPos.X, __instance.aimPos.Y));
            Vector2 baseVelocity = Utility.getVelocityTowardPoint(
                shootOrigin,
                aimPosition,
                (float)(15 + Game1.random.Next(4, 6)) * (1f + who.buffs.WeaponSpeedMultiplier)
            );

            if (backArmDistance > 4 && !__instance.canPlaySound)
            {
                // Determine how many projectiles based on ammo and config
                int maxPellets = FogMod.Config.MultiSlingShotPellets ? Constants.GrouseRewardItemMaxPellets : 1;
                int availableAmmo = obj.Stack;
                int pelletsToFire = Math.Min(maxPellets, availableAmmo);

                // Generate spread pattern
                List<int> spreadDirections = new List<int>();
                if (pelletsToFire == 1)
                    spreadDirections.Add(0); // Center only
                else if (pelletsToFire == 2)
                {
                    spreadDirections.Add(-1); // Left
                    spreadDirections.Add(1);  // Right
                }
                else // 3 or more
                {
                    spreadDirections.Add(0);  // Center
                    spreadDirections.Add(1);  // Right
                    spreadDirections.Add(-1); // Left
                }

                List<(StardewValley.Object ammo, float angle)> projectiles = new List<(StardewValley.Object, float)>();
                foreach (int i in spreadDirections)
                {
                    StardewValley.Object obj2 = (StardewValley.Object)obj.getOne();
                    float spreadAngle = i * 0.261799f; // ~15 degrees
                    projectiles.Add((obj2, spreadAngle));

                    if (obj.ConsumeStack(1) == null)
                    {
                        __instance.attachments[0] = null;
                        break;
                    }
                }

                foreach (var (obj2, spreadAngle) in projectiles)
                {
                    float cos = (float)Math.Cos(spreadAngle);
                    float sin = (float)Math.Sin(spreadAngle);
                    Vector2 velocityTowardPoint = new Vector2(
                        baseVelocity.X * cos - baseVelocity.Y * sin,
                        baseVelocity.X * sin + baseVelocity.Y * cos
                    );

                    // Use Galaxy Slingshot damage (4x)
                    float damageMultiplier = 4f;
                    int ammoDamage = __instance.GetAmmoDamage(obj2);
                    string ammoCollisionSound = __instance.GetAmmoCollisionSound(obj2);
                    var ammoCollisionBehavior = __instance.GetAmmoCollisionBehavior(obj2);

                    if (!Game1.options.useLegacySlingshotFiring)
                    {
                        velocityTowardPoint.X *= -1f;
                        velocityTowardPoint.Y *= -1f;
                    }

                    location.projectiles.Add(
                        new StardewValley.Projectiles.BasicProjectile(
                            (int)(damageMultiplier * (float)(ammoDamage + Game1.random.Next(-(ammoDamage / 2), ammoDamage + 2)) * (1f + who.buffs.AttackMultiplier)),
                            -1, 0, 0,
                            (float)(Math.PI / (double)(64f + (float)Game1.random.Next(-63, 64))),
                            0f - velocityTowardPoint.X,
                            0f - velocityTowardPoint.Y,
                            shootOrigin - new Vector2(32f, 32f),
                            ammoCollisionSound,
                            null, null,
                            explode: false,
                            damagesMonsters: true,
                            location, who,
                            ammoCollisionBehavior,
                            obj2.ItemId
                        )
                        {
                            IgnoreLocationCollision = Game1.currentLocation.currentEvent != null || Game1.currentMinigame != null
                        }
                    );
                }
            }

            __instance.canPlaySound = true;
            // Skip original method
            return false;
        }
        catch (Exception ex)
        {
            FogMod.Instance?.Monitor.Log($"OnSlingshotPerformFirePrefix failed: {ex.Message}", LogLevel.Error);
            // Run original on error
            return true;
        }
    }

    // Galaxy Slingshot Multi-Shot Tooltip
    public static void OnSlingshotGetHoverBoxTextPostfix(StardewValley.Tools.Slingshot __instance, Item hoveredItem, ref string? __result)
    {
        try
        {
            // Only apply to Galaxy Slingshot
            if (__instance.ItemId != "34" || __result == null)
                return;

            // Keep base result for attachable items
            if (hoveredItem is StardewValley.Object obj && __instance.canThisBeAttached(obj))
                return;

            if (hoveredItem == null && __instance.attachments?[0] != null)
            {
                int maxPellets = FogMod.Config.MultiSlingShotPellets ? Constants.GrouseRewardItemMaxPellets : 1;
                int availableAmmo = __instance.attachments[0].Stack;
                int willFire = Math.Min(maxPellets, availableAmmo);
                string plural = willFire > 1 ? "s" : "";
                __result = $"{__result}\nMulti-Shot: {willFire} {__instance.attachments[0].DisplayName}{plural}";
            }
        }
        catch (Exception ex)
        {
            FogMod.Instance?.Monitor.Log($"OnSlingshotGetHoverBoxTextPostfix failed: {ex.Message}", LogLevel.Error);
        }
    }
}