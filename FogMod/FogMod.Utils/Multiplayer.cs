#nullable enable
using StardewModdingAPI;
using StardewValley;
using System;
using FogMod.Models;
using StardewModdingAPI.Events;


namespace FogMod.Utils;

public static class Multiplayer
{
    static FogMod? Instance = FogMod.Instance;

    private class MessageType
    {
        public const string Explosion = "Explosion";
        public const string GrouseEvent = "GrouseEvent";
    }

    public static bool IsAbleToUpdateOwnWorld()
    {
        if (Game1.IsMasterGame)
            return true;

        if (Game1.currentLocation is GameLocation localLocation && GetHost()?.currentLocation is GameLocation hostLocation)
        {
            return localLocation != hostLocation;
        }
        return true;
    }

    private static Farmer? GetHost()
    {
        foreach (Farmer farmer in Game1.getOnlineFarmers())
        {
            if (Instance?.Helper.Multiplayer.GetConnectedPlayer(farmer.UniqueMultiplayerID) is IMultiplayerPeer peer)
            {
                if (peer.IsHost)
                    return farmer;
            }
        }
        return null;
    }

    public static Farmer? GetFarmerFromUniqueId(long uniqueId)
    {
        foreach (Farmer farmer in Game1.getOnlineFarmers())
        {
            if (farmer.UniqueMultiplayerID == uniqueId)
                return farmer;
        }
        return null;
    }

    public static void SendMessage<T>(T message)
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
                Instance?.Monitor.Log($"🚀 Unknown message type: {message?.GetType().Name}", LogLevel.Warn);
                return;
            }
            if (Instance?.ModManifest.UniqueID is string uniqueId)
                Instance?.Helper.Multiplayer.SendMessage(message, messageType, new[] { uniqueId });
        }
        catch (Exception ex)
        {
            Instance?.Monitor.Log($"🚀 Error broadcasting {messageType} message: {ex.Message}", LogLevel.Error);
        }
    }

    public static void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        try
        {
            if (e.FromPlayerID == Game1.player.UniqueMultiplayerID)
                return;

            string? currentLocation = Game1.currentLocation?.NameOrUniqueName;
            switch (e.Type)
            {
                case MessageType.Explosion:
                    var explosionData = e.ReadAs<ExplosionFlashInfo>();
                    if (explosionData.LocationName == currentLocation)
                        HandleExplosionFromMessage(explosionData);
                    break;
                case MessageType.GrouseEvent:
                    var grouseEventData = e.ReadAs<GrouseEventInfo>();
                    HandleGrouseEventFromMessage(e.FromPlayerID, grouseEventData);
                    break;
                default:
                    Instance?.Monitor.Log($"OnModMessageReceived: Unknown message type '{e.Type}' from mod '{e.FromModID}'", LogLevel.Warn);
                    break;
            }
        }
        catch
        {
            FogMod.Instance?.Monitor.Log($"OnModMessageReceived failed - FromModID: {e.FromModID}, Type: {e.Type}, IsMainPlayer: {Context.IsMainPlayer}", LogLevel.Error);
        }
    }

    private static void HandleExplosionFromMessage(ExplosionFlashInfo msg)
    {
        try
        {
            FogMod.HandleExplosion(msg);
        }
        catch (Exception ex)
        {
            Instance?.Monitor.Log($"🚀 Error handling explosion message: {ex.Message}", LogLevel.Error);
        }
    }

    private static void HandleGrouseEventFromMessage(long fromPlayerId, GrouseEventInfo msg)
    {
        try
        {
            switch (msg.Event)
            {
                case GrouseEventInfo.EventType.Flushed:
                    if (Context.IsMainPlayer && Instance?.GetGrouseById(msg.GrouseId) is NetGrouse g && g.State == GrouseState.Perched)
                        g.State = GrouseState.Surprised;
                    return;
                case GrouseEventInfo.EventType.Released:
                    if (GetFarmerFromUniqueId(fromPlayerId) is Farmer farmer)
                        FarmerHelper.raiseHands(farmer);
                    return;
            }
            Instance?.Monitor.Log($"🚀 Unknown grouse event: {msg.Event}", LogLevel.Warn);
        }
        catch (Exception ex)
        {
            Instance?.Monitor.Log($"🚀 Error handling grouse event message: {ex.Message}", LogLevel.Error);
        }
    }
}