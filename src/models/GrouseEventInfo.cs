#nullable enable

namespace FogMod;

public struct GrouseEventInfo
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