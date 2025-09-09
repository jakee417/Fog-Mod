#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewValley.Objects;
using System.Linq;
using Netcode;
using StardewValley.TerrainFeatures;
using System.IO;
using StardewValley.GameData;
using FogMod.Models;
using FogMod.Utils;

namespace FogMod;

public partial class FogMod : Mod
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public static GMCM.GMCM.ModConfig Config { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public static Random Random = new Random();
    private static readonly Vector2 globalWindDirection = new Vector2(WeatherDebris.globalWind, 0f);
    private static readonly Color DefaultFogColor = Color.LightGray;
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
    private FogGrid grid;
    private float time;
    private float breathBasePhase;
    private float dailyFogStrength = 0f;
    private float lastWeatherFogIntensityFactor = 1f;
    private readonly IEnumerable<GameLocation> outdoorLocations = Game1.locations.Where(loc => loc.IsOutdoors);

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        // Test log that should definitely appear
        Monitor.Log($"🌫️ Fog Mod (v{this.ModManifest.Version}) is loading! 🌫️", LogLevel.Alert);

        // Load config
        Config = Helper.ReadConfig<GMCM.GMCM.ModConfig>();

        // Subscribe to events
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.Multiplayer.ModMessageReceived += Utils.Multiplayer.OnModMessageReceived;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.Player.Warped += OnWarped;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Display.Rendered += OnRendered;

        // Harmony patches
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
            original: AccessTools.Method(typeof(Tree), nameof(Tree.performToolAction), new Type[] { typeof(Tool), typeof(int), typeof(Vector2) }),
            postfix: new HarmonyMethod(typeof(FogMod), nameof(OnTreePerformToolActionPostfix))
        );
        harmony.Patch(
            original: AccessTools.Method(typeof(Tree), nameof(Tree.shake), new Type[] { typeof(Vector2), typeof(bool) }),
            postfix: new HarmonyMethod(typeof(FogMod), nameof(OnTreeShakePostfix))
        );
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        if (Helper.ModRegistry.GetApi<GMCM.IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu") is GMCM.IGenericModConfigMenuApi gmcmApi)
            GMCM.GMCM.RegisterModConfig(gmcmApi);

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
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/AudioChanges"))
        {

            e.LoadFrom(
                () =>
                {
                    string grouseAudioPath = Path.Combine(Helper.DirectoryPath, "assets", "grouse.wav");

                    if (!File.Exists(grouseAudioPath))
                    {
                        Monitor.Log($"Grouse audio file not found at: {grouseAudioPath}", LogLevel.Warn);
                        return new Dictionary<string, AudioCueData>();
                    }
                    AudioCueData cue = new AudioCueData
                    {
                        Id = Constants.GrouseAudioCueId,
                        FilePaths = new List<string> { grouseAudioPath },
                        Category = "Sound",
                        StreamedVorbis = false,
                        Looped = false,
                        UseReverb = false
                    };
                    return new Dictionary<string, AudioCueData>
                    {
                        { Constants.GrouseAudioCueId, cue }
                    };
                },
                AssetLoadPriority.Medium
            );
        }
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        InitializeDailyFogStrength();
        TreeHelper.ClearCache();
        if (Context.IsMainPlayer)
            InitializeGrouse();
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (!Context.IsWorldReady) return;
        if (e.IsLocalPlayer)
            ResetAllParticlesOnLocationChange();
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady) return;

        // Update grid snapshot
        grid = new FogGrid(
            cellSize: Constants.FogTileSize,
            bufferCells: Constants.DefaultFogGridBufferCells
        );

        float deltaSeconds = (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;
        time += deltaSeconds;
        breathBasePhase = time * (MathHelper.TwoPi / Constants.BreathPeriodSeconds);

        RefreshLightSources();
        UpdateExplosionSmokeParticles(deltaSeconds);
        if (isFogDay && Game1.currentLocation != null && Game1.currentLocation.IsOutdoors)
            UpdateFloatingFogParticles(deltaSeconds);
        UpdateExplosionFlashInfos(deltaSeconds);

        // Update grouse
        if (Config.EnableGrouseCritters && Utils.Multiplayer.IsAbleToUpdateOwnWorld())
        {
            TreeHelper.UpdateLeaves();
            UpdateGrouse(deltaSeconds);
        }
    }

    private void ResetAllParticlesOnLocationChange()
    {
        ResetFogParticles();
        ResetExplosionSmokeParticles();
    }

    private void OnRendered(object? sender, RenderedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.currentLocation == null)
            return;

        // DrawDebugFogGrid(e.SpriteBatch);

        if (Config.DebugShowInfo)
            DrawDebugInfo(e.SpriteBatch);

        Color fogColor = GetEffectiveFogColor();

        TreeHelper.DrawLeaves(e.SpriteBatch);
        DrawExplosionFlashes(e.SpriteBatch);
        DrawExplosionSmokeParticles(e.SpriteBatch, fogColor);
        if (isFogDay && Game1.currentLocation.IsOutdoors)
            DrawFloatingFogParticles(e.SpriteBatch, fogColor);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (e.Button == SButton.G && Config.EnableGrouseCritters && Game1.currentLocation.IsOutdoors)
        {
            if (GetNPCsAtCurrentLocation() is NetCollection<NPC> npc)
            {
                Vector2 playerPosition = Game1.player.getStandingPosition();
                Utils.Multiplayer.SendMessage(new GrouseEventInfo(
                    grouseId: -1,
                    _event: GrouseEventInfo.EventType.Released,
                    timestamp: DateTime.UtcNow.Ticks
                ));
                FarmerHelper.raiseHands(Game1.player);
                Vector2 spawnPosition = playerPosition + new Vector2(0, -Game1.player.FarmerSprite.SpriteHeight * 2.5f);
                int salt = (int)Random.NextInt64();
                NetGrouse g = SpawnGrouse(
                    npc: npc,
                    treePosition: spawnPosition,
                    spawnPosition: spawnPosition,
                    location: Game1.currentLocation,
                    salt: salt,
                    launchedByFarmer: true
                );
                g.State = GrouseState.Surprised;
                Game1.addHUDMessage(new HUDMessage("Grouse Released!", 2));
            }
        }
    }
}