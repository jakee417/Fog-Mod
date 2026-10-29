#nullable enable
using FogMod.Models;
using Xunit;

namespace FogMod.Tests;

public class GrouseEventInfoTests
{
    [Fact]
    public void Constructor_AssignsAllFields()
    {
        var info = new GrouseEventInfo(9, GrouseEventInfo.EventType.Flushed, 12345L);
        Assert.Equal(9, info.GrouseId);
        Assert.Equal("Flushed", info.Event);
        Assert.Equal(12345L, info.Timestamp);
    }

    [Fact]
    public void EventTypes_AreDistinct()
    {
        Assert.NotEqual(GrouseEventInfo.EventType.Flushed, GrouseEventInfo.EventType.Released);
    }
}
