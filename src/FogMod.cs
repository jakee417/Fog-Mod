#nullable enable
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
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public ModConfig Config { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public Random Random = new Random();
        private static readonly Vector2 globalWindDirection = new Vector2(WeatherDebris.globalWind, 0f);
        private static readonly Color DefaultFogColor = Color.LightGray;
        public GenericModConfigMenu.IGenericModConfigMenuApi? gmcmApi { get; set; }
        internal static FogMod? Instance;
        private bool isFogDay = false;
        private float probabilityOfFogForADay = 0.05f;
        private float probabilityOfFogRoll = 0.0f;
        private List<FogParticle> floatingParticles = new List<FogParticle>();
        private CellOccupancy fogCellOccupancy;
        private List<ExplosionFlashInfo> explosionFlashInfos = new List<ExplosionFlashInfo>();
        private List<FogParticle> explosionSmokeParticles = new List<FogParticle>();
        private CellOccupancy smokeCellOccupancy;
        private List<LightInfo> lightSources = new List<LightInfo>();
        public List<Texture2D>? cloudTextures { get; set; }
        public Texture2D? whitePixel { get; set; }
        public Texture2D? grouseTexture { get; set; }
        public Texture2D? surprisedTexture { get; set; }
        public Texture2D? damageTexture { get; set; }
        private FogGrid grid;
        private float time;
        private float breathBasePhase;
        private float dailyFogStrength = 0f;
        private float lastWeatherFogIntensityFactor = 1f;
        private GameLocation? lastLocation = null;
        private List<Grouse> grouse = new List<Grouse>();
        private HashSet<string> lastActiveLocationNames = new HashSet<string>();

        public override void Entry(IModHelper helper)
        {
            // Test log that should definitely appear
            Monitor.Log($"🌫️ Fog Mod (v{this.ModManifest.Version}) is loading! 🌫️", LogLevel.Alert);

            // Load config
            Config = Helper.ReadConfig<ModConfig>();

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

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Register with Generic Mod Config Menu using the typed API
            GenericModConfigMenu.IGenericModConfigMenuApi? gmcmApi = Helper.ModRegistry.GetApi<GenericModConfigMenu.IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
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

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            InitializeDailyFogStrength();
        }

        private void InitializeDailyFogStrength()
        {
            if (!Context.IsWorldReady)
                return;

            if (!Config.EnableDailyRandomFog)
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
            return new FogForecast(
                isFogDay: isFogDay,
                dailyFogStrength: dailyFogStrength,
                probabilityOfFogForADay: probabilityOfFogForADay,
                probabilityOfFogRoll: probabilityOfFogRoll
            );
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

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            // Update grid snapshot
            grid = new FogGrid(
                cellSize: FogTileSize,
                bufferCells: DefaultFogGridBufferCells
            );

            float deltaSeconds = (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;
            time += deltaSeconds;
            breathBasePhase = time * (MathHelper.TwoPi / BreathPeriodSeconds);

            // Reset fog if we transitioned to a new location
            if (Game1.currentLocation != lastLocation || lastLocation == null)
                ResetAllParticlesOnLocationChange();
            RefreshLightSources();
            UpdateExplosionSmokeParticles(deltaSeconds);
            if (isFogDay && Game1.currentLocation.IsOutdoors)
                UpdateFloatingFogParticles(deltaSeconds);
            UpdateExplosionFlashInfos(deltaSeconds);

            // Update grouse
            if (Config.EnableGrouseCritters && Game1.currentLocation.IsOutdoors)
            {
                SpawnGrouseInTrees();
                if (Context.IsMainPlayer)
                    BroadcastExistingGrouse();
                UpdateGrouse(deltaSeconds);
            }
            lastLocation = Game1.currentLocation;
        }

        private void ResetAllParticlesOnLocationChange()
        {
            ResetFogParticles();
            ResetExplosionSmokeParticles();
            ResetGrouse();
        }

        private void OnRendered(object? sender, RenderedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.currentLocation == null) return;

            // DrawDebugFogGrid(e.SpriteBatch);

            if (Config.DebugShowInfo)
                DrawDebugInfo(e.SpriteBatch);

            Color fogColor = GetEffectiveFogColor();

            if (Config.EnableGrouseCritters && Game1.currentLocation.IsOutdoors)
                DrawGrouse(e.SpriteBatch);

            DrawExplosionFlashes(e.SpriteBatch);
            DrawExplosionSmokeParticles(e.SpriteBatch, fogColor);
            if (isFogDay && Game1.currentLocation.IsOutdoors)
                DrawFloatingFogParticles(e.SpriteBatch, fogColor);
        }

        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            try
            {
                if (e.FromPlayerID == Game1.player.UniqueMultiplayerID)
                    return;

                string? currentLocation = Game1.currentLocation?.NameOrUniqueName;
                switch (e.Type)
                {
                    case MessageType.Explosion:
                        var explosionData = e.ReadAs<ExplosionFlashInfo>();
                        if (explosionData.LocationName == currentLocation)
                            HandleExplosionFromMessage(explosionData);
                        break;
                    case MessageType.GrouseFlush:
                        var flushData = e.ReadAs<GrouseFlushInfo>();
                        if (flushData.LocationName == currentLocation)
                            HandleGrouseFlushFromMessage(flushData);
                        break;
                    case MessageType.GrouseKnockdown:
                        var knockdownData = e.ReadAs<GrouseKnockdownInfo>();
                        if (knockdownData.LocationName == currentLocation)
                            HandleGrouseKnockdownFromMessage(knockdownData);
                        break;
                    case MessageType.ItemDrop:
                        var itemDropData = e.ReadAs<ItemDropInfo>();
                        if (itemDropData.LocationName == currentLocation)
                            HandleItemDropFromMessage(itemDropData);
                        break;
                    case MessageType.GrouseSpawn:
                        var spawnData = e.ReadAs<GrouseSpawnInfo>();
                        if (spawnData.LocationName == currentLocation)
                            HandleGrouseSpawnFromMessage(spawnData);
                        break;
                    case MessageType.GrouseLocationRemoval:
                        var removalData = e.ReadAs<GrouseLocationRemovalInfo>();
                        HandleGrouseLocationRemovalFromMessage(removalData);
                        break;
                    default:
                        Monitor.Log($"OnModMessageReceived: Unknown message type '{e.Type}' from mod '{e.FromModID}'", LogLevel.Warn);
                        break;
                }
            }
            catch
            {
                FogMod.Instance?.Monitor.Log($"OnModMessageReceived failed - FromModID: {e.FromModID}, Type: {e.Type}, ThisModID: {this.ModManifest.UniqueID}, IsMainPlayer: {Context.IsMainPlayer}", LogLevel.Error);
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // Debug hotkey: G to spawn grouse at main player's location.
            if (e.Button == SButton.G && Config.EnableGrouseCritters && Game1.currentLocation.IsOutdoors && Context.IsMainPlayer)
            {
                Vector2 playerPosition = Game1.player.getStandingPosition();
                FarmerHelper.raiseHands(Game1.player);
                Vector2 spawnPosition = playerPosition + new Vector2(0, -Game1.player.FarmerSprite.SpriteHeight * 2.5f);
                SpawnGrouse(spawnPosition, Game1.currentLocation.NameOrUniqueName);
                Game1.addHUDMessage(new HUDMessage("Grouse Released!", 2));
            }
        }
    }
}