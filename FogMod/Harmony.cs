#nullable enable
using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System;
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

    // Grouse Reward
    public static bool OnItemRegistryCreatePrefix(string itemId, int amount, int quality, bool allowNull, ref Item __result)
    {
        try
        {
            if (itemId == Constants.GrouseRewardItemName)
            {
                var multiSlingshot = new MultiSlingshot();
                if (amount != 1)
                {
                    multiSlingshot.Stack = amount;
                    multiSlingshot.FixStackSize();
                }
                if (quality != 0)
                {
                    multiSlingshot.Quality = quality;
                    multiSlingshot.FixQuality();
                }
                __result = multiSlingshot;
                return false;
            }
        }
        catch (Exception ex)
        {
            FogMod.Instance?.Monitor.Log($"OnItemRegistryCreatePrefix failed for item {itemId}: {ex.Message}", LogLevel.Error);
        }
        return true;
    }
}