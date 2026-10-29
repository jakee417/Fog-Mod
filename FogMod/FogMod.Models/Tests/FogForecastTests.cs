#nullable enable
using FogMod.Models;
using Xunit;

namespace FogMod.Tests;

public class FogForecastTests
{
    [Fact]
    public void Constructor_AssignsAllFields()
    {
        var forecast = new FogForecast(
            isFogDay: true,
            dailyFogStrength: 1.1f,
            probabilityOfFogForADay: 0.2f,
            probabilityOfFogRoll: 0.15f);
        Assert.True(forecast.IsFogDay);
        Assert.Equal(1.1f, forecast.DailyFogStrength);
        Assert.Equal(0.2f, forecast.ProbabilityOfFogForADay);
        Assert.Equal(0.15f, forecast.ProbabilityOfFogRoll);
    }
}
