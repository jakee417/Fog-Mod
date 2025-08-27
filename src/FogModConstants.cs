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
        private const string ExplosionMessageType = "Explosion";
        private const string GrouseFlushMessageType = "GrouseFlush";
        private const int MaximumSmokeParticlesPerCell = 10;
        private const float SmokeFadeInSeconds = 0.8f;
        private const float SmokeSpeed = 3.5f;
        private const float SmokeFadeToFloorSeconds = 8.0f;
        private const float SmokeMinAlpha = 0.05f;
        private const float SmokeGrowthSeconds = 8.0f;
        private const float DebugRingThickness = 2.0f;

        // Grouse constants
        private const float GrouseDetectionRadius = 128f; // How close player needs to be to flush grouse
        private const float GrouseFlushSpeed = 150f; // Speed during takeoff
        private const float GrouseExitSpeed = 400f; // Speed when leaving screen (increased for quick exit)
        private const float GrouseFlushDuration = 3.0f; // How long the flush animation lasts (3 seconds for heavy flapping)
        private const float GrouseFlyingDuration = 8f; // How long it flies around before exiting
        private const float GrouseSpawnChance = 0.15f; // Chance per tree to spawn a grouse
        private const float GrouseScale = 1.0f; // Visual scale of the grouse
        private const int GrouseMaxPerLocation = 3; // Maximum grouse per location
        private const float GrouseFlappingSoundInterval = 0.3f; // How often to play flapping sound during flush (seconds)

    // Grouse sprite constants (2x2 grid from grouse.png)
    private const int GrouseSpriteWidth = 16; // Width of each frame
    private const int GrouseSpriteHeight = 16; // Height of each frame
    private const int GrouseSpriteColumns = 2; // Number of columns in sprite sheet
    private const int GrouseSpriteRows = 2; // Number of rows in sprite sheet
    }
}