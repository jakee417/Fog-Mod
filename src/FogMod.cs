using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Projectiles;
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
        private Texture2D surprisedTexture;
        private Texture2D damageTexture;
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
        private static readonly int[] wingPattern = { 0, 1, 2, 3, 2, 1 };

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
            helper.Events.Input.ButtonPressed += OnButtonPressed;

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
            harmony.Patch(
                original: AccessTools.Method(typeof(Projectile), nameof(Projectile.update)),
                postfix: new HarmonyMethod(typeof(FogMod), nameof(OnProjectileUpdatePostfix))
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
                grouseTexture = Helper.ModContent.Load<Texture2D>("assets/grouse.png");
                Monitor.Log("Successfully loaded grouse texture", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to load grouse texture: {ex.Message}", LogLevel.Warn);
            }

            try
            {
                grouseTexture = Helper.ModContent.Load<Texture2D>("assets/grouse.png");
                Monitor.Log("Successfully loaded grouse texture", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to load grouse texture: {ex.Message}", LogLevel.Warn);
            }

            try
            {
                surprisedTexture = Helper.ModContent.Load<Texture2D>("assets/surprised.png");
                Monitor.Log("Successfully loaded surprised texture", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to load surprised texture: {ex.Message}", LogLevel.Warn);
            }

            try
            {
                damageTexture = Helper.ModContent.Load<Texture2D>("assets/damage.png");
                Monitor.Log("Successfully loaded damage texture", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to load surprised texture: {ex.Message}", LogLevel.Warn);
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

            // DrawDebugFogGrid(e.SpriteBatch);

            if (config.DebugShowInfo)
                DrawDebugInfo(e.SpriteBatch);

            Color fogColor = GetEffectiveFogColor();

            if (config.EnableGrouseCritters && Game1.currentLocation != null && Game1.currentLocation.IsOutdoors)
                DrawGrouse(e.SpriteBatch);

            DrawExplosionFlashes(e.SpriteBatch);
            DrawExplosionSmokeParticles(e.SpriteBatch, fogColor);
            if (isFogDay && Game1.currentLocation != null && Game1.currentLocation.IsOutdoors)
                DrawFloatingFogParticles(e.SpriteBatch, fogColor);
        }

        private void OnModMessageReceived(object sender, ModMessageReceivedEventArgs e)
        {
            try
            {
                if (!Enum.TryParse<MessageType>(e.Type, ignoreCase: true, out MessageType messageType))
                {
                    FogMod.Instance.Monitor.Log($"Unknown message type: {e.Type}", LogLevel.Warn);
                    return;
                }
                bool fromAnotherPlayer = e.FromPlayerID != Game1.player.UniqueMultiplayerID;
                string currentLocation = Game1.currentLocation?.NameOrUniqueName;
                switch (messageType)
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

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // Debug hotkey: G to spawn grouse at player location
            if (e.Button == SButton.G && Context.IsPlayerFree && config.EnableGrouseCritters)
            {
                Vector2 playerPosition = Game1.player.getStandingPosition();
                if (Context.IsMainPlayer)
                {
                    FarmerHelper.raiseHands(Game1.player);
                    Vector2 spawnPosition = playerPosition + new Vector2(0, -Game1.player.FarmerSprite.SpriteHeight * 2.5f);
                    SpawnGrouseAtTree(spawnPosition);
                    Game1.addHUDMessage(new HUDMessage("Grouse Released!", 2));
                }
            }
        }
    }
}