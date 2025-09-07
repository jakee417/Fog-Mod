#nullable enable
using StardewModdingAPI;
using StardewValley;
using System;

namespace FogMod;

public partial class FogMod : Mod
{
    private class MessageType
    {
        public const string Explosion = "Explosion";
        public const string GrouseEvent = "GrouseEvent";
    }

    private bool IsAbleToUpdateOwnWorld()
    {
        if (Game1.IsMasterGame)
            return true;

        if (Game1.currentLocation is GameLocation localLocation && GetHost()?.currentLocation is GameLocation hostLocation)
        {
            return localLocation != hostLocation;
        }
        return true;
    }

    private Farmer? GetHost()
    {
        foreach (Farmer farmer in Game1.getOnlineFarmers())
        {
            if (Helper.Multiplayer.GetConnectedPlayer(farmer.UniqueMultiplayerID) is IMultiplayerPeer peer)
            {
                if (peer.IsHost)
                    return farmer;
            }
        }
        return null;
    }

    public void SendMessage<T>(T message)
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
            FogMod.HandleExplosion(msg);
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
                            g.State = GrouseState.Surprised;
                        return;
                    case GrouseEventInfo.EventType.KnockedDown:
                        if (g.State != GrouseState.KnockedDown)
                            g.State = GrouseState.KnockedDown;
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