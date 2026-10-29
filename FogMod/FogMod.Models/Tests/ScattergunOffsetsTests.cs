#nullable enable
using FogMod.Models;
using Xunit;

namespace FogMod.Tests;

public class ScattergunOffsetsTests
{
    [Fact]
    public void Defaults_MatchTuning()
    {
        var offsets = new ScattergunOffsets();
        Assert.Equal(4f, offsets.GunScale);
        Assert.Equal(4f, offsets.ArmScale);
        Assert.Equal(24, offsets.SmokeCount);
        Assert.Equal(24f, offsets.SmokeRadius);
        Assert.Equal(2f, offsets.SideRotationOffset);
        Assert.InRange(offsets.SpreadAngle, 0.2617f, 0.2619f);
        Assert.NotNull(offsets.Down);
        Assert.NotNull(offsets.Up);
        Assert.NotNull(offsets.Left);
        Assert.NotNull(offsets.Right);
    }
}
