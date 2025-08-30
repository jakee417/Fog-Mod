#nullable enable
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Projectiles;
using System;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        // Bomb
        private static void OnBombExplodedPostfix(GameLocation __instance, Vector2 tileLocation, int radius, Farmer who, bool damageFarmers, int damage_amount, bool destroyObjects)
        {
            try
            {
                if (FogMod.Instance == null) return;
                Vector2 center = tileLocation * 64f + new Vector2(32f, 32f);
                float radiusPx = Math.Max(64f, radius * 64f * 2f);
                string location = __instance?.NameOrUniqueName ?? "Unknown";
                ExplosionFlashInfo info = new()
                {
                    LocationName = location,
                    CenterWorld = center,
                    RadiusPixels = radiusPx,
                    TimeLeft = ExplosionFlashDurationSeconds
                };
                FogMod.Instance.SendExplosionMessage(info);
                FogMod.Instance.HandleExplosion(info);
            }
            catch
            {
                FogMod.Instance?.Monitor.Log($"OnBombExplodedPostfix failed - IsMainPlayer: {Context.IsMainPlayer}, Location: {__instance?.Name}", LogLevel.Error);
            }
        }

        private void HandleExplosion(ExplosionFlashInfo info)
        {

            FogMod.Instance?.explosionFlashInfos.Add(info);
            FogMod.Instance?.SpawnExplosionSmoke(info.CenterWorld, info.RadiusPixels);
        }

        // TV Weather Report
        private static void ProceedToNextScenePrefix(TV __instance, out int __state)
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

        private static void ProceedToNextScenePostfix(TV __instance, int __state)
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

        private static void OnWeatherForecastPostfix()
        {
            try
            {
                int daysPlayed = (int)Game1.stats.DaysPlayed + 1;
                FogMod.FogForecast forecast = FogMod.ComputeFogForecast(daysPlayed);
                string text = "And no fog!";
                // Either the player wants fog everyday or it gets forecasted.
                if (!FogMod.Instance?.Config.EnableDailyRandomFog is bool enableDailyRandomFog && enableDailyRandomFog || forecast.IsFogDay)
                {
                    string playersName = Game1.player.Name;
                    text = $"And bad news! the fog's getting thicker... and {playersName}'s getting larger!";
                }
                Game1.drawObjectDialogue(Game1.parseText(text));
            }
            catch { }
        }

        // Projectile Update
        private static void OnProjectileUpdatePostfix(Projectile __instance, GameLocation location)
        {
            try
            {
                if (FogMod.Instance == null || !FogMod.Instance.Config.EnableGrouseCritters)
                    return;

                Vector2 projectilePos = __instance.position.Value;
                // Adjust for projectile scale.
                projectilePos.X += 8f * 4f;
                projectilePos.Y += 8f * 4f;
                foreach (var grouse in FogMod.Instance.grouse)
                {
                    if (grouse.State != GrouseState.Flushing && grouse.State != GrouseState.Flying)
                        continue;

                    Vector2 grousePos = grouse.Position;
                    grousePos.Y += grouse.FlightHeight;
                    grousePos.Y -= GrouseSpriteHeight * grouse.Scale / 2f;
                    float distance = Vector2.Distance(projectilePos, grousePos);
                    if (distance < GrouseCollisionRadius)
                    {
                        FogMod.Instance.SendGrouseKnockdownMessage(grouse.GrouseId, projectilePos, location?.NameOrUniqueName);
                        FogMod.Instance.KnockDownGrouse(grouse.GrouseId);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                FogMod.Instance?.Monitor.Log($"OnProjectileUpdatePostfix failed: {ex.Message}", LogLevel.Error);
            }
        }
    }
}


