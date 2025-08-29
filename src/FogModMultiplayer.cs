#nullable enable
using Microsoft.Xna.Framework;
using StardewValley;
using StardewModdingAPI;
using System;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        private enum MessageType
        {
            Explosion,
            GrouseFlush,
            GrouseKnockdown
        }

        private struct ExplosionFlashInfo
        {
            public required string LocationName { get; init; }
            public required Vector2 CenterWorld { get; init; }
            public required float RadiusPixels { get; init; }
            public required float TimeLeft { get; set; }
        }

        private struct GrouseFlushInfo
        {
            public required string? LocationName { get; init; }
            public required int GrouseId { get; init; }
            public required long Timestamp { get; init; }
        }

        private struct GrouseKnockdownInfo
        {
            public required string? LocationName { get; init; }
            public required int GrouseId { get; init; }
            public required long Timestamp { get; init; }
        }

        private struct ItemDropInfo
        {
            public required string? LocationName { get; init; }
            public required Vector2 Position { get; init; }
            public required string ItemId { get; init; }
            public required int Quantity { get; init; }
            public required long Timestamp { get; init; }
        }
    }

    public partial class FogMod : Mod
    {
        // Bomb Explosions
        private void SendExplosionMessage(ExplosionFlashInfo msg)
        {
            try
            {
                Helper.Multiplayer.SendMessage(msg, MessageType.Explosion.ToString());
            }
            catch (Exception ex)
            {
                Monitor.Log($"🚀 Error broadcasting explosion: {ex.Message}", LogLevel.Error);
            }
        }

        private void HandleExplosionFromMessage(ExplosionFlashInfo msg)
        {
            try
            {
                FogMod.Instance?.HandleExplosion(msg);
            }
            catch (Exception ex)
            {
                Monitor.Log($"🚀 Error handling explosion message: {ex.Message}", LogLevel.Error);
            }
        }

        // Grouse Flush
        private void SendGrouseFlushMessage(GrouseFlushInfo flushInfo)
        {
            Helper.Multiplayer.SendMessage(flushInfo, MessageType.GrouseFlush.ToString());
        }

        private void HandleGrouseFlushFromMessage(GrouseFlushInfo flushInfo)
        {
            try
            {
                for (int i = 0; i < grouse.Count; i++)
                {
                    var g = grouse[i];
                    if (g.GrouseId == flushInfo.GrouseId)
                    {
                        FogMod.Instance?.UpdateGrousePerched(g, true);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"HandleGrouseFlushFromMessage failed: {ex.Message}", LogLevel.Error);
            }
        }

        // Grouse Knockdown
        public void SendGrouseKnockdownMessage(int grouseId, Vector2 projectilePosition, string? locationName)
        {
            try
            {
                // Any player can broadcast grouse knockdowns when they hit one
                var knockdownInfo = new GrouseKnockdownInfo
                {
                    LocationName = locationName,
                    GrouseId = grouseId,
                    Timestamp = Game1.currentGameTime?.TotalGameTime.Ticks ?? 0
                };
                Helper.Multiplayer.SendMessage(knockdownInfo, MessageType.GrouseKnockdown.ToString());
            }
            catch (Exception ex)
            {
                Monitor.Log($"BroadcastGrouseKnockdown failed: {ex.Message}", LogLevel.Error);
            }
        }

        private void HandleGrouseKnockdownFromMessage(GrouseKnockdownInfo knockdownInfo)
        {
            try
            {
                FogMod.Instance?.KnockDownGrouse(knockdownInfo.GrouseId);
            }
            catch (Exception ex)
            {
                Monitor.Log($"HandleGrouseKnockdownFromMessage failed: {ex.Message}", LogLevel.Error);
            }
        }

        // Item Drop Handling
        private void SendItemDropMessage(ItemDropInfo itemDropInfo)
        {
            try
            {
                Helper.Multiplayer.SendMessage(itemDropInfo, MessageType.ItemDrop.ToString());
            }
            catch (Exception ex)
            {
                Monitor.Log($"SendGrouseItemDropMessage failed: {ex.Message}", LogLevel.Error);
            }
        }

        private void HandleItemDropFromMessage(ItemDropInfo itemDropInfo)
        {
            try
            {
                CreateItemDrop(itemDropInfo.Position, itemDropInfo.ItemId, itemDropInfo.Quantity);
            }
            catch (Exception ex)
            {
                Monitor.Log($"HandleGrouseItemDropFromMessage failed: {ex.Message}", LogLevel.Error);
            }
        }

        private void CreateItemDrop(Vector2 position, string itemId, int quantity)
        {
            var item = new StardewValley.Object(itemId, quantity);
            Game1.currentLocation.debris.Add(new StardewValley.Debris(
                item,
                position,
                Game1.player.getStandingPosition()
            ));
        }
    }
}
