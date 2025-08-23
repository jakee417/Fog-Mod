using StardewModdingAPI;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        private const int FogTileSize = 128;
        private const int DefaultFogGridBufferCells = 3;
        private const float FogCloudScale = 3.0f;
        private const float DefaultFloatingScaleMin = 0.4f;
        private const float DefaultFloatingScaleMax = 1.4f;
        private const float DefaultFogAlpha = 0.6f;
        private const float DefaultFogAlphaWeak = 0.3f;
        private const int MinimumFogParticlesPerCell = 1;
        private const int MaximumFogParticlesPerCell = 3;
        private const float LightThinningStrength = 0.75f;
        private const float LightThinningStrengthWeak = 0.25f;
        private const float DailyRandomFogMin = 0.8f;
        private const float DailyRandomFogMax = 1.2f;
        private const float FloatingParticleSpeed = 10f;
        private const float ParticleFadeInSeconds = 1.0f;
        private const float ParticleFadeOutSeconds = 5.0f;
        private const float BreathAmplitude = 0.35f;
        private const float BreathPeriodSeconds = 24f;
        private const float BreathDesync = 0.5f;
        private const float ExplosionFlashDurationSeconds = 0.75f;
        private const int MaximumSmokeParticlesPerCell = 10;
        private const float SmokeFadeInSeconds = 0.8f;
        private const float SmokeSpeed = 3.5f;
        private const float SmokeFadeToFloorSeconds = 8.0f;
        private const float SmokeMinAlpha = 0.05f;
        private const float SmokeGrowthSeconds = 8.0f;
        private const float DebugRingThickness = 2.0f;
    }
}