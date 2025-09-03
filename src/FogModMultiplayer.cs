#nullable enable
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using System;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        private class MessageType
        {
            public const string Explosion = "Explosion";
            public const string GrouseEvent = "GrouseEvent";
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

        private struct GrouseEventInfo
        {
            public int GrouseId { get; init; }
            public string Event { get; init; }
            public long Timestamp { get; init; }

            public GrouseEventInfo(int grouseId, string _event, long timestamp)
            {
                GrouseId = grouseId;
                Event = _event;
                Timestamp = timestamp;
            }

            public class EventType
            {
                public const string Flushed = "Flushed";
                public const string KnockedDown = "KnockedDown";
            }
        }
    }

    public partial class FogMod : Mod
    {
        private void SendMessage<T>(T message)
        {
            string messageType = "";
            try
            {
                if (message is ExplosionFlashInfo)
                {
                    messageType = MessageType.Explosion;
                }
                else if (message is GrouseEventInfo)
                {
                    messageType = MessageType.GrouseEvent;
                }
                else
                {
                    Monitor.Log($"🚀 Unknown message type: {message?.GetType().Name}", LogLevel.Warn);
                    return;
                }
                Helper.Multiplayer.SendMessage(message, messageType, new[] { ModManifest.UniqueID });
            }
            catch (Exception ex)
            {
                Monitor.Log($"🚀 Error broadcasting {messageType} message: {ex.Message}", LogLevel.Error);
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

        private void HandleGrouseEventFromMessage(GrouseEventInfo msg)
        {
            try
            {
                if (GetGrouseById(msg.GrouseId) is NetGrouse g)
                {
                    switch (msg.Event)
                    {
                        case GrouseEventInfo.EventType.Flushed:
                            if (g.State == GrouseState.Perched)
                                SurpriseGrouse(g);
                            return;
                        case GrouseEventInfo.EventType.KnockedDown:
                            if (g.State != GrouseState.KnockedDown)
                                KnockDownGrouse(g);
                            return;
                    }
                    Monitor.Log($"🚀 Unknown grouse event: {msg.Event}", LogLevel.Warn);
                }
                Monitor.Log($"🚀 Grouse not found: {msg.GrouseId}", LogLevel.Warn);
            }
            catch (Exception ex)
            {
                Monitor.Log($"🚀 Error handling grouse event message: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
