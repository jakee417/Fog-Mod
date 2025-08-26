using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewValley.Objects;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        private static readonly Vector2 globalWindDirection = new Vector2(WeatherDebris.globalWind, 0f);
        private static readonly Color DefaultFogColor = Color.LightGray;
        private GenericModConfigMenu.IGenericModConfigMenuApi gmcmApi;
        internal static FogMod Instance { get; private set; }
        private ModConfig config;
        private Random random;
        private bool isFogDay = false;
        private float probabilityOfFogForADay = 0.05f;
        private float probabilityOfFogRoll = 0.0f;
        private List<FogParticle> floatingParticles = new List<FogParticle>();
        private CellOccupancy fogCellOccupancy;
        private List<ExplosionFlashInfo> explosionFlashInfos = new List<ExplosionFlashInfo>();
        private List<FogParticle> explosionSmokeParticles = new List<FogParticle>();
        private CellOccupancy smokeCellOccupancy;
        private List<LightInfo> lightSources = new List<LightInfo>();
        private List<Texture2D> cloudTextures;
        private Texture2D whitePixel;
        private Texture2D grouseTexture;
        private FogGrid grid;
        private float time = 0f;
        private float breathBasePhase = 0f;
        private float dailyFogStrength = 0f;
        private float lastWeatherFogIntensityFactor = 1f;
        private GameLocation currentLocation = null;
        private List<Grouse> grouse = new List<Grouse>();
        private HashSet<Vector2> spawnedTreePositions = new HashSet<Vector2>();
        private int nextGrouseId = 1;
        private string lastPlayerLocation = "";

        public override void Entry(IModHelper helper)
        {
            // Test log that should definitely appear
            Monitor.Log($"🌫️ Fog Mod (v{this.ModManifest.Version}) is loading! 🌫️", LogLevel.Alert);

            // Initialize random number generator
            random = new Random();

            // Load config
            config = Helper.ReadConfig<ModConfig>();

            // Subscribe to events
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.Rendered += OnRendered;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;

            // Harmony patches
            Instance = this;
            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.explode), new Type[] { typeof(Vector2), typeof(int), typeof(Farmer), typeof(bool), typeof(int), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(FogMod), nameof(OnBombExplodedPostfix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(TV), nameof(TV.proceedToNextScene)),
                prefix: new HarmonyMethod(typeof(FogMod), nameof(ProceedToNextScenePrefix)),
                postfix: new HarmonyMethod(typeof(FogMod), nameof(ProceedToNextScenePostfix))
            );
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // Register with Generic Mod Config Menu using the typed API
            gmcmApi = Helper.ModRegistry.GetApi<GenericModConfigMenu.IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcmApi != null)
            {
                Monitor.Log("Generic Mod Config Menu API found! Registering options...", LogLevel.Info);
                RegisterModConfig(gmcmApi);
            }

            // Normalize global wind direction once
            if (globalWindDirection.LengthSquared() > 0f)
                globalWindDirection.Normalize();

            try
            {
                cloudTextures = new List<Texture2D>();
                string[] names = new[] { "Cloud1.png", "Cloud2.png", "Cloud3.png" };
                foreach (var name in names)
                {
                    try
                    {
                        var tex = Helper.ModContent.Load<Texture2D>($"assets/{name}");
                        if (tex != null)
                            cloudTextures.Add(tex);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to load cloud textures: {ex.Message}", LogLevel.Trace);
            }
            try
            {
                whitePixel = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
                whitePixel.SetData(new[] { Color.White });
            }
            catch { }

            try
            {
                grouseTexture = Helper.ModContent.Load<Texture2D>("assets/Brown Chicken.png");
                Monitor.Log("Successfully loaded grouse texture", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to load grouse texture: {ex.Message}", LogLevel.Warn);
            }
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            InitializeDailyFogStrength();
        }

        private void InitializeDailyFogStrength()
        {
            if (!Context.IsWorldReady)
                return;

            if (!config.EnableDailyRandomFog)
            {
                isFogDay = true;
                dailyFogStrength = 1f;
                probabilityOfFogForADay = 1f;
                probabilityOfFogRoll = 0f;
                return;
            }

            int daysPlayed = (int)Game1.stats.DaysPlayed;
            FogForecast forecast = ComputeFogForecast(daysPlayed);
            isFogDay = forecast.IsFogDay;
            dailyFogStrength = forecast.DailyFogStrength;
            probabilityOfFogForADay = forecast.ProbabilityOfFogForADay;
            probabilityOfFogRoll = forecast.ProbabilityOfFogRoll;
        }

        private static FogForecast ComputeFogForecast(int daysPlayed)
        {
            // Use a deterministic per-day seed for both fog presence and strength
            int seed = daysPlayed ^ (int)(Game1.uniqueIDForThisGame & 0x7FFFFFFF);
            var rng = new Random(seed);
            float probabilityOfFogRoll = (float)rng.NextDouble();
            float probabilityOfFogForADay = ComputeProbabilityOfFogForADay();
            bool isFogDay = probabilityOfFogRoll <= probabilityOfFogForADay;
            float strengthRoll = (float)rng.NextDouble();
            float dailyFogStrength = MathHelper.Lerp(DailyRandomFogMin, DailyRandomFogMax, strengthRoll);
            return new FogForecast
            {
                IsFogDay = isFogDay,
                DailyFogStrength = dailyFogStrength,
                ProbabilityOfFogForADay = probabilityOfFogForADay,
                ProbabilityOfFogRoll = probabilityOfFogRoll
            };
        }

        private static float ComputeProbabilityOfFogForADay()
        {
            string season = Game1.currentSeason;
            float seasonalProbability;
            switch (season)
            {
                case "spring":
                    seasonalProbability = 0.12f;
                    break;
                case "summer":
                    seasonalProbability = 0.03f;
                    break;
                case "fall":
                case "autumn":
                    seasonalProbability = 0.14f;
                    break;
                case "winter":
                    seasonalProbability = 0.06f;
                    break;
                default:
                    seasonalProbability = 0.08f;
                    break;
            }
            return seasonalProbability;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            // Update grid snapshot
            grid = new FogGrid(FogTileSize, DefaultFogGridBufferCells);

            float deltaSeconds = (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;
            time += deltaSeconds;
            breathBasePhase = time * (MathHelper.TwoPi / BreathPeriodSeconds);

            // Reset fog if we transitioned to a new location
            if (Game1.currentLocation != null && Game1.currentLocation != currentLocation)
            {
                currentLocation = Game1.currentLocation;
                ResetAllParticlesOnLocationChange();
            }
            RefreshLightSources();
            UpdateExplosionSmokeParticles(deltaSeconds);
            if (isFogDay && Game1.currentLocation != null && Game1.currentLocation.IsOutdoors)
                UpdateFloatingFogParticles(deltaSeconds);
            UpdateExplosionFlashInfos(deltaSeconds);

            // Update grouse
            if (config.EnableGrouseCritters && Game1.currentLocation != null && Game1.currentLocation.IsOutdoors)
            {
                UpdateGrouse(deltaSeconds);
                SpawnGrouseInTrees();
            }
        }

        private void ResetAllParticlesOnLocationChange()
        {
            ResetFogParticles();
            ResetExplosionSmokeParticles();
            if (config.EnableGrouseCritters)
                ResetGrouse();
        }

        private void OnRendered(object sender, RenderedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            if (config.DebugLightRings)
                DrawDebugLightRings(e.SpriteBatch);

            if (config.DebugFogCells)
                DrawDebugFogGrid(e.SpriteBatch);

            if (config.DebugShowInfo)
                DrawDebugInfo(e.SpriteBatch);

            Color fogColor = GetEffectiveFogColor();
            DrawExplosionFlashes(e.SpriteBatch);
            DrawExplosionSmokeParticles(e.SpriteBatch, fogColor);
            if (isFogDay && Game1.currentLocation != null && Game1.currentLocation.IsOutdoors)
                DrawFloatingFogParticles(e.SpriteBatch, fogColor);

            // Draw grouse
            if (config.EnableGrouseCritters && Game1.currentLocation != null && Game1.currentLocation.IsOutdoors)
                DrawGrouse(e.SpriteBatch);
        }

        private void OnModMessageReceived(object sender, ModMessageReceivedEventArgs e)
        {
            try
            {
                if (e.Type == ExplosionMessageType)
                {
                    if (Context.IsMainPlayer)
                        return;
                    var data = e.ReadAs<ExplosionFlashInfo>();
                    if (data.LocationName == Game1.currentLocation?.NameOrUniqueName)
                        HandleExplosion(data.LocationName, data.CenterWorld, data.RadiusPixels);
                }
                else if (e.Type == GrouseFlushMessageType)
                {
                    if (Context.IsMainPlayer)
                        return;
                    var data = e.ReadAs<GrouseFlushInfo>();
                    if (data.LocationName == Game1.currentLocation?.NameOrUniqueName)
                        HandleGrouseFlushFromMessage(data);
                }
            }
            catch
            {
                FogMod.Instance.Monitor.Log($"OnModMessageReceived failed - FromModID: {e.FromModID}, Type: {e.Type}, ThisModID: {this.ModManifest.UniqueID}, IsMainPlayer: {Context.IsMainPlayer}", LogLevel.Error);
            }
        }

        public class ModConfig
        {
            public bool EnableDailyRandomFog { get; set; } = true;
            public bool EnableWeatherBasedFog { get; set; } = true;
            public bool EnableTimeOfDayFog { get; set; } = true;
            public bool ParticleStrength { get; set; } = true;
            public bool LightThinningStrength { get; set; } = true;
            public bool DebugShowInfo { get; set; } = false;
            public bool DebugLightRings { get; set; } = false;
            public bool DebugFogCells { get; set; } = false;
            public bool DebugFogBlack { get; set; } = false;

            // Experimental Features
            public bool EnableGrouseCritters { get; set; } = false;
        }
    }
}