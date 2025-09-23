#nullable enable

namespace FogMod;

public static class Constants
{
    public const int FogTileSize = 128;
    public const int DefaultFogGridBufferCells = 3;
    public const float FogCloudScale = 2.0f;
    public const float DefaultFloatingScaleMin = 0.4f;
    public const float DefaultFloatingScaleMax = 1.4f;
    public const float DefaultFogAlpha = 0.3f;
    public const int MinimumFogParticlesPerCell = 1;
    public const int MaximumFogParticlesPerCell = 3;
    public const float LightThinningStrength = 0.75f;
    public const float DailyRandomFogMin = 0.8f;
    public const float DailyRandomFogMax = 1.2f;
    public const float FloatingParticleSpeed = 10f;
    public const float ParticleFadeInSeconds = 1.0f;
    public const float ParticleFadeOutSeconds = 5.0f;
    public const float BreathAmplitude = 0.35f;
    public const float BreathPeriodSeconds = 24f;
    public const float BreathDesync = 0.5f;
    public const float ExplosionFlashDurationSeconds = 0.75f;
    public const int MaximumSmokeParticlesPerCell = 10;
    public const float SmokeFadeInSeconds = 0.8f;
    public const float SmokeSpeed = 3.5f;
    public const float SmokeFadeToFloorSeconds = 8.0f;
    public const float SmokeMinAlpha = 0.05f;
    public const float SmokeGrowthSeconds = 8.0f;
    public const float DebugRingThickness = 2.0f;

    // Grouse constants
    public const string GrouseName = "Grouse";
    public const string GrouseAudioCueId = "FogMod_Grouse";
    public const string GrouseRewardItemName = "FogMod_MultiSlingshot";
    public const string GrouseTextureName = "grouse";
    public const string GrouseVoidTextureName = "grouse_void";
    public const float GrouseVoidSpawnChance = 0.2f;
    public const int GrouseQuestGoal = 100;
    public const float GrouseFlushSpeed = 200f;
    public const float GrouseBobAmplitude = 10f;
    public const float GrouseExitSpeed = 400f;
    public const float GrouseSurprisedDuration = 2.0f;
    public const float GrouseFlushDuration = 2.0f;
    public const float GrouseSpawnChance = 0.1f;
    public const float GrouseScale = 4f;
    public const int GrouseHidingCycles = 40;
    public const int GrouseMaxPerLocation = 1;
    public const int GrouseSpriteWidth = 16;
    public const int GrouseSpriteHeight = 16;
    public const float GrouseTransitionDuration = 0.3f;
    public const float SurprisedSpriteScale = 4f;
    public const float GrouseLandingDistanceThreshold = 512f;
    public const float GrouseMissRate = 0.1f;
    public const int GrouseMaxHealth = 30;
}