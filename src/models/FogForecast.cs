#nullable enable

namespace FogMod;

public struct FogForecast
{
    public bool IsFogDay { get; init; }
    public float DailyFogStrength { get; init; }
    public float ProbabilityOfFogForADay { get; init; }
    public float ProbabilityOfFogRoll { get; init; }

    public FogForecast(
        bool isFogDay,
        float dailyFogStrength,
        float probabilityOfFogForADay,
        float probabilityOfFogRoll
    )
    {
        IsFogDay = isFogDay;
        DailyFogStrength = dailyFogStrength;
        ProbabilityOfFogForADay = probabilityOfFogForADay;
        ProbabilityOfFogRoll = probabilityOfFogRoll;
    }
}