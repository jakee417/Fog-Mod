#nullable enable
using Xunit;

namespace FogMod.Tests;

public class ModConfigTests
{
    [Fact]
    public void Defaults_AreAllEnabled()
    {
        var config = new GMCM.GMCM.ModConfig();
        Assert.True(config.EnableFog);
        Assert.True(config.EnableFogBank);
        Assert.True(config.EnableDailyRandomFog);
        Assert.True(config.EnableWeatherBasedFog);
        Assert.True(config.EnableTimeOfDayFog);
        Assert.True(config.EnableExplosionSmoke);
        Assert.True(config.EnableGrouseCritters);
        Assert.True(config.EnableCritterz);
        Assert.True(config.MultiSlingShotPellets);
    }
}
