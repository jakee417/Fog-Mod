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
        private static void OnBombExplodedPostfix(GameLocation __instance, Vector2 tileLocation, int radius, Farmer who, bool damageFarmers, int damage_amount, bool destroyObjects)
        {
            try
            {
                if (FogMod.Instance == null) return;
                Vector2 center = tileLocation * 64f + new Vector2(32f, 32f);
                float radiusPx = Math.Max(64f, radius * 64f * 2f);
                string location = __instance?.NameOrUniqueName ?? "Unknown";
                FogMod.Instance.HandleExplosion(location, center, radiusPx);
            }
            catch
            {
                FogMod.Instance.Monitor.Log($"OnBombExplodedPostfix failed - IsMainPlayer: {Context.IsMainPlayer}, Location: {__instance?.Name}", LogLevel.Error);
            }
        }

        private void HandleExplosion(string location, Vector2 center, float radiusPx)
        {
            ExplosionFlashInfo info = new()
            {
                LocationName = location,
                CenterWorld = center,
                RadiusPixels = radiusPx,
                TimeLeft = ExplosionFlashDurationSeconds
            };
            FogMod.Instance.explosionFlashInfos.Add(info);
            FogMod.Instance.SpawnExplosionSmoke(center, radiusPx);
            if (Context.IsMainPlayer)
                FogMod.Instance.BroadcastExplosion(info);
        }

        private void BroadcastExplosion(ExplosionFlashInfo msg)
        {
            try
            {
                Helper.Multiplayer.SendMessage(msg, ExplosionMessageType);
            }
            catch (Exception ex)
            {
                Monitor.Log($"🚀 Error broadcasting explosion: {ex.Message}", LogLevel.Error);
            }
        }

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
                if (!FogMod.Instance.config.EnableDailyRandomFog || forecast.IsFogDay)
                {
                    string playersName = Game1.player.Name;
                    text = $"And bad news! the fog's getting thicker... and {playersName}'s getting larger!";
                }
                Game1.drawObjectDialogue(Game1.parseText(text));
            }
            catch { }
        }

        private static void OnProjectileUpdatePostfix(Projectile __instance, GameLocation location)
        {
            try
            {
                if (FogMod.Instance == null || !FogMod.Instance.config.EnableGrouseCritters) return;

                // Check if this projectile hit a grouse
                Vector2 projectilePos = __instance.position.Value;
                projectilePos.X += 8f * 4;
                projectilePos.Y += 8f * 4;
                foreach (var grouse in FogMod.Instance.grouse)
                {
                    // Only check grouse that are flying (can be hit)
                    if (grouse.State != GrouseState.Flushing && grouse.State != GrouseState.Flying) continue;

                    // Calculate distance between projectile and grouse
                    Vector2 grousePos = grouse.Position;
                    grousePos.Y += grouse.FlightHeight; // Account for flight height
                    grousePos.Y -= GrouseSpriteHeight * grouse.Scale / 2f;
                    float distance = Vector2.Distance(projectilePos, grousePos);

                    // If projectile is close enough to the grouse (hit detection)
                    if (distance < 32f) // 32 pixels hit radius
                    {
                        // Knock the grouse down, preserving its current momentum
                        FogMod.Instance.KnockDownGrouse(grouse.GrouseId);
                        break; // Only hit one grouse per projectile
                    }
                }
            }
            catch (Exception ex)
            {
                FogMod.Instance.Monitor.Log($"OnProjectileUpdatePostfix failed: {ex.Message}", LogLevel.Error);
            }
        }
    }
}


