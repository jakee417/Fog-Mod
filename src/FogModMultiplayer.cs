#nullable enable
using Microsoft.Xna.Framework;
using StardewValley;
using StardewModdingAPI;
using System;
using StardewValley.Locations;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        private class MessageType
        {
            public const string Explosion = "Explosion";
            public const string GrouseFlush = "GrouseFlush";
            public const string GrouseKnockdown = "GrouseKnockdown";
            public const string ItemDrop = "ItemDrop";
        }

        private struct ExplosionFlashInfo
        {
            public string LocationName { get; init; }
            public Vector2 CenterWorld { get; init; }
            public float RadiusPixels { get; init; }
            public float TimeLeft { get; set; }

            public ExplosionFlashInfo(string locationName, Vector2 centerWorld, float radiusPixels, float timeLeft)
            {
                LocationName = locationName;
                CenterWorld = centerWorld;
                RadiusPixels = radiusPixels;
                TimeLeft = timeLeft;
            }
        }

        private struct GrouseFlushInfo
        {
            public string? LocationName { get; init; }
            public int GrouseId { get; init; }
            public long Timestamp { get; init; }

            public GrouseFlushInfo(string? locationName, int grouseId, long timestamp)
            {
                LocationName = locationName;
                GrouseId = grouseId;
                Timestamp = timestamp;
            }
        }

        private struct GrouseKnockdownInfo
        {
            public string? LocationName { get; init; }
            public int GrouseId { get; init; }
            public long Timestamp { get; init; }

            public GrouseKnockdownInfo(string? locationName, int grouseId, long timestamp)
            {
                LocationName = locationName;
                GrouseId = grouseId;
                Timestamp = timestamp;
            }
        }

        private struct ItemDropInfo
        {
            public string? LocationName { get; init; }
            public Vector2 Position { get; init; }
            public string ItemId { get; init; }
            public int Quantity { get; init; }
            public long Timestamp { get; init; }

            public ItemDropInfo(string? locationName, Vector2 position, string itemId, int quantity, long timestamp)
            {
                LocationName = locationName;
                Position = position;
                ItemId = itemId;
                Quantity = quantity;
                Timestamp = timestamp;
            }
        }
    }

    public partial class FogMod : Mod
    {
        // Bomb Explosions
        private void SendExplosionMessage(ExplosionFlashInfo msg)
        {
            try
            {
                Helper.Multiplayer.SendMessage(msg, MessageType.Explosion);
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
            Helper.Multiplayer.SendMessage(flushInfo, MessageType.GrouseFlush);
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
                var knockDownInfo = new GrouseKnockdownInfo(locationName: locationName, grouseId: grouseId, timestamp: Game1.currentGameTime?.TotalGameTime.Ticks ?? 0);
                Helper.Multiplayer.SendMessage(knockDownInfo, MessageType.GrouseKnockdown);
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
                Helper.Multiplayer.SendMessage(itemDropInfo, MessageType.ItemDrop);
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
    }
}
