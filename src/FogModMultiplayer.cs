#nullable enable
using Microsoft.Xna.Framework;
using StardewValley;
using StardewModdingAPI;
using System;
using System.Linq;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        private class MessageType
        {
            public const string Explosion = "Explosion";
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
                // Only create item drops on the host to prevent duplicates
                if (Context.IsMainPlayer)
                    CreateItemDrop(itemDropInfo.Position, itemDropInfo.ItemId, itemDropInfo.Quantity);
            }
            catch (Exception ex)
            {
                Monitor.Log($"HandleGrouseItemDropFromMessage failed: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
