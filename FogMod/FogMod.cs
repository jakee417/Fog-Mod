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
using StardewValley.GameData.Weapons;
using StardewValley.GameData.Shops;
using FogMod.Models;
using FogMod.Utils;

namespace FogMod;

public partial class FogMod : Mod
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public static GMCM.GMCM.ModConfig Config { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public static Random Random = new Random();
    public static readonly IEnumerable<GameLocation> outdoorLocations = Game1.locations.Where(loc => loc.IsOutdoors);
    public static readonly Vector2 globalWindDirection = new Vector2(WeatherDebris.globalWind, 0f);
    public static readonly Color DefaultFogColor = Color.LightGray;
    internal static FogMod? Instance;
    internal static ScattergunOffsets ScattergunConfig = new();
    public Grid grid;
    public float time;

    public override void Entry(IModHelper helper)
    {
        Instance = this;

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
        helper.Events.GameLoop.DayEnding += OnDayEnding;

        // Console commands
        helper.ConsoleCommands.Add("reload_scattergun", "Reload scattergun_offsets.json", (_, _) =>
        {
            LoadScattergunOffsets();
            Monitor.Log("Reloaded scattergun offsets.", LogLevel.Info);
        });

        helper.ConsoleCommands.Add(
            "set_grouse_kills",
            "Set the number of grouse killed for the current player. Usage: set_grouse_kills <count>",
            (cmd, args) =>
            {
                if (!Context.IsWorldReady)
                {
                    Monitor.Log("You must be in-game to use this command.", LogLevel.Warn);
                    return;
                }
                if (args.Length != 1 || !int.TryParse(args[0], out int count) || count < 0)
                {
                    Monitor.Log("Usage: set_grouse_kills <count> (must be a non-negative integer)", LogLevel.Info);
                    return;
                }
                int prev = Game1.stats.getMonstersKilled(Constants.GrouseName);
                Game1.stats.specificMonstersKilled[Constants.GrouseName] = count;
                bool hadRewardFlag = Game1.player.mailReceived.Contains(Constants.GrouseSlayerCompleteFlag);
                string gilFlag = $"Gil_{Constants.GrouseQuestName}";
                bool hadGilFlag = Game1.player.mailReceived.Contains(gilFlag);
                if (count < Constants.GrouseQuestGoal)
                {
                    if (hadRewardFlag)
                    {
                        Game1.player.mailReceived.Remove(Constants.GrouseSlayerCompleteFlag);
                        Monitor.Log($"Removed quest flag '{Constants.GrouseSlayerCompleteFlag}' because kills set below quest goal ({Constants.GrouseQuestGoal}).", LogLevel.Info);
                    }
                    if (hadGilFlag)
                    {
                        Game1.player.mailReceived.Remove(gilFlag);
                        Monitor.Log($"Removed Gil quest flag '{gilFlag}' because kills set below quest goal ({Constants.GrouseQuestGoal}).", LogLevel.Info);
                    }
                }
                Monitor.Log($"Set grouse kills to {count} for this player. Previous value was {prev}. Gil flag present: {(Game1.player.mailReceived.Contains(gilFlag) ? "YES" : "NO")}", LogLevel.Info);
            }
        );

        helper.ConsoleCommands.Add(
            "fogmod_debug",
            "Print FogMod debug info to the SMAPI log.",
            (cmd, args) =>
            {
                if (!Context.IsWorldReady)
                {
                    Monitor.Log("You must be in-game to use this command.", LogLevel.Warn);
                    return;
                }
                PrintDebugInfoToConsole();
            }
        );

        helper.ConsoleCommands.Add("spawn_grouse", "Spawn a debug grouse at the player", (_, _) =>
        {
            if (!Context.IsWorldReady)
            {
                Monitor.Log("You must be in-game to spawn a grouse.", LogLevel.Warn);
                return;
            }
            if (!Game1.currentLocation.IsOutdoors)
            {
                Monitor.Log("Must be outdoors to spawn a grouse.", LogLevel.Warn);
                return;
            }
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
                Grouse g = SpawnGrouse(
                    npc: npc,
                    treePosition: spawnPosition,
                    spawnPosition: spawnPosition,
                    location: Game1.currentLocation,
                    salt: salt,
                    launchedByFarmer: true
                );
                g.State = GrouseState.Surprised;
                Monitor.Log("Grouse spawned!", LogLevel.Info);
                Game1.addHUDMessage(new HUDMessage("Grouse Released!", 2));
            }
        });

        // Harmony patches
        var harmony = new Harmony(ModManifest.UniqueID);
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
        // Patch Galaxy Slingshot to add multi-shot behavior
        harmony.Patch(
            original: AccessTools.Method(typeof(StardewValley.Tools.Slingshot), nameof(StardewValley.Tools.Slingshot.PerformFire)),
            prefix: new HarmonyMethod(typeof(FogMod), nameof(OnSlingshotPerformFirePrefix))
        );
        harmony.Patch(
            original: AccessTools.Method(typeof(StardewValley.Tools.Slingshot), nameof(StardewValley.Tools.Slingshot.beginUsing),
                new[] { typeof(GameLocation), typeof(int), typeof(int), typeof(Farmer) }),
            postfix: new HarmonyMethod(typeof(FogMod), nameof(OnSlingshotBeginUsingPostfix))
        );
        harmony.Patch(
            original: AccessTools.Method(typeof(StardewValley.Tools.Slingshot), nameof(StardewValley.Tools.Slingshot.getHoverBoxText)),
            postfix: new HarmonyMethod(typeof(FogMod), nameof(OnSlingshotGetHoverBoxTextPostfix))
        );
        // Patch FarmerRenderer.draw to replace Galaxy Slingshot visuals with scattergun
        harmony.Patch(
            original: AccessTools.Method(typeof(FarmerRenderer), nameof(FarmerRenderer.draw),
                new[] { typeof(SpriteBatch), typeof(FarmerSprite.AnimationFrame), typeof(int), typeof(Rectangle), typeof(Vector2), typeof(Vector2), typeof(float), typeof(int), typeof(Color), typeof(float), typeof(float), typeof(Farmer) }),
            prefix: new HarmonyMethod(typeof(FogMod), nameof(OnFarmerRendererDrawPrefix)),
            postfix: new HarmonyMethod(typeof(FogMod), nameof(OnFarmerRendererDrawPostfix))
        );
        // Patch Slingshot.drawInMenu to show scattergun icon for Galaxy Slingshot
        harmony.Patch(
            original: AccessTools.Method(typeof(StardewValley.Tools.Slingshot), nameof(StardewValley.Tools.Slingshot.drawInMenu),
                new[] { typeof(SpriteBatch), typeof(Vector2), typeof(float), typeof(float), typeof(float), typeof(StackDrawType), typeof(Color), typeof(bool) }),
            prefix: new HarmonyMethod(typeof(FogMod), nameof(OnSlingshotDrawInMenuPrefix))
        );
        // Patch Slingshot.tickUpdate — suppress drawback sound while scattergun uses slingshot warmup timing
        harmony.Patch(
            original: AccessTools.Method(typeof(StardewValley.Tools.Slingshot), nameof(StardewValley.Tools.Slingshot.tickUpdate),
                new[] { typeof(GameTime), typeof(Farmer) }),
            prefix: new HarmonyMethod(typeof(FogMod), nameof(OnSlingshotTickUpdatePrefix))
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
            grouseVoidTexture = Helper.ModContent.Load<Texture2D>("assets/grouse_void.png");
            Monitor.Log("Successfully loaded grouse alt texture", LogLevel.Trace);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to load grouse alt texture: {ex.Message}", LogLevel.Warn);
        }

        try
        {
            grouseGoldenTexture = Helper.ModContent.Load<Texture2D>("assets/grouse_gold.png");
            Monitor.Log("Successfully loaded grouse golden texture", LogLevel.Trace);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to load grouse golden texture: {ex.Message}", LogLevel.Warn);
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

        LoadScattergunOffsets();

        try
        {
            scattergunTexture = Helper.ModContent.Load<Texture2D>("assets/weapon.png");
            Monitor.Log("Successfully loaded scattergun texture", LogLevel.Trace);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to load scattergun texture: {ex.Message}", LogLevel.Warn);
        }

        try
        {
            armsBaseTexture = Helper.ModContent.Load<Texture2D>("assets/arms.png");
            Monitor.Log("Successfully loaded arms texture", LogLevel.Trace);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to load arms texture: {ex.Message}", LogLevel.Warn);
        }
    }

    private void LoadScattergunOffsets()
    {
        try
        {
            string path = Path.Combine(Helper.DirectoryPath, "assets", "scattergun_offsets.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var offsets = System.Text.Json.JsonSerializer.Deserialize<ScattergunOffsets>(json);
                if (offsets != null)
                    ScattergunConfig = offsets;
                Monitor.Log("Loaded scattergun offsets from JSON.", LogLevel.Trace);
            }
            else
            {
                Monitor.Log("scattergun_offsets.json not found, using defaults.", LogLevel.Warn);
            }
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to load scattergun offsets: {ex.Message}", LogLevel.Warn);
        }
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/AudioChanges"))
        {

            e.LoadFrom(
                () =>
                {
                    var audioCues = new Dictionary<string, AudioCueData>();


                    // Register Grouse sound
                    string grouseAudioPath = Path.Combine(Helper.DirectoryPath, "assets", "grouse.wav");
                    if (File.Exists(grouseAudioPath))
                    {
                        audioCues[Constants.GrouseAudioCueId] = new AudioCueData
                        {
                            Id = Constants.GrouseAudioCueId,
                            FilePaths = new List<string> { grouseAudioPath },
                            Category = "Sound",
                            StreamedVorbis = false,
                            Looped = false,
                            UseReverb = false
                        };
                    }
                    else
                    {
                        Monitor.Log($"Grouse audio file not found at: {grouseAudioPath}", LogLevel.Warn);
                    }

                    // Register Scattergun sound (weapon.wav)
                    string scattergunAudioPath = Path.Combine(Helper.DirectoryPath, "assets", "weapon.wav");
                    if (File.Exists(scattergunAudioPath))
                    {
                        audioCues[Constants.ScattergunAudioCueId] = new AudioCueData
                        {
                            Id = Constants.ScattergunAudioCueId,
                            FilePaths = new List<string> { scattergunAudioPath },
                            Category = "Sound",
                            StreamedVorbis = false,
                            Looped = false,
                            UseReverb = false
                        };
                    }
                    else
                    {
                        Monitor.Log($"Scattergun audio file not found at: {scattergunAudioPath}", LogLevel.Warn);
                    }

                    string clickAudioPath = Path.Combine(Helper.DirectoryPath, "assets", "click.wav");
                    if (File.Exists(clickAudioPath))
                    {
                        audioCues[Constants.ClickAudioCueId] = new AudioCueData
                        {
                            Id = Constants.ClickAudioCueId,
                            FilePaths = new List<string> { clickAudioPath },
                            Category = "Sound",
                            StreamedVorbis = false,
                            Looped = false,
                            UseReverb = false
                        };
                    }
                    else
                    {
                        Monitor.Log($"Click audio file not found at: {clickAudioPath}", LogLevel.Warn);
                    }

                    return audioCues;
                },
                AssetLoadPriority.Medium
            );
        }
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Weapons"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, WeaponData>().Data;
                if (data.TryGetValue(Constants.GrouseRewardItemName, out var weapon))
                {
                    weapon.DisplayName = "Scattergun";
                    weapon.Description = "A powerful scattergun that fires multiple pellets.";
                }
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, ShopData>().Data;
                // Marlon's shop context is "AdventureGuild"
                if (data.TryGetValue("AdventureShop", out var shop))
                {
                    bool alreadyPresent = shop.Items.Any(i => i.ItemId == $"(W){Constants.GrouseRewardItemName}");
                    bool hasGrouseReward = Game1.player.mailReceived.Contains(Constants.GrouseSlayerCompleteFlag);
                    if (!alreadyPresent && hasGrouseReward)
                    {
                        shop.Items.Add(new ShopItemData
                        {
                            ItemId = $"(W){Constants.GrouseRewardItemName}",
                            Price = 50000, // Set your desired price
                            AvailableStock = -1 // Infinite stock
                        });
                    }
                }
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/MonsterSlayerQuests"))
        {
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, MonsterSlayerQuestData>().Data;
                data[Constants.GrouseQuestName] = new MonsterSlayerQuestData
                {
                    DisplayName = "Grouse",
                    Targets = new List<string> { Constants.GrouseName },
                    Count = Constants.GrouseQuestGoal,
                    RewardItemId = $"(W){Constants.GrouseRewardItemName}",
                    RewardDialogue = "Well done! You've proven yourself quite the grouse hunter. These elusive birds hide among the trees in the fog. Your persistence has paid off - here's a Scattergun with multi-shot capabilities!",
                    RewardFlag = Constants.GrouseSlayerCompleteFlag
                };
            });
        }
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        InitializeDailyFogStrength();
        TreeHelper.ClearCache();
        if (Context.IsWorldReady)
            RecolorArmsTexture(Game1.player);
        if (Context.IsMainPlayer && Config.EnableGrouseCritters)
            InitializeGrouse();
    }

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        ClearGrouseFromAllLocations();
        ResetAllParticlesOnLocationChange();
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (e.IsLocalPlayer)
        {
            ResetAllParticlesOnLocationChange();
            RecolorArmsTexture(Game1.player);
        }
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        grid = new Grid(
            cellSize: Constants.FogTileSize,
            bufferCells: Constants.DefaultFogGridBufferCells
        );
        float deltaSeconds = (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;
        time += deltaSeconds;
        breathBasePhase = time * (MathHelper.TwoPi / Constants.BreathPeriodSeconds);
        RefreshLightSources();
        UpdateExplosionFlashInfos(deltaSeconds);
        UpdateExplosionSmokeParticles(deltaSeconds);
        if (isFogDay && Game1.currentLocation != null && Game1.currentLocation.IsOutdoors)
            UpdateFogBank();
        if (isFogDay && Game1.currentLocation != null && Game1.currentLocation.IsOutdoors)
            UpdateFloatingFogParticles(deltaSeconds);
        TreeHelper.UpdateLeaves();
        if (Utils.Multiplayer.IsAbleToUpdateOwnWorld())
            UpdateGrouse(deltaSeconds);
    }

    private void ResetAllParticlesOnLocationChange()
    {
        ResetFogParticles();
        ResetExplosionSmokeParticles();
        TreeHelper.ResetLeaves();
    }

    private void OnRendered(object? sender, RenderedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.currentLocation == null)
            return;
        Color fogColor = GetEffectiveFogColor();
        TreeHelper.DrawLeaves(e.SpriteBatch);
        if (Config.EnableExplosionSmoke)
            DrawExplosionSmokeParticles(e.SpriteBatch, fogColor);
        if (Config.EnableFogBank && isFogDay && Game1.currentLocation.IsOutdoors)
            DrawFogBank(e.SpriteBatch, fogColor);
        if (Config.EnableFog && isFogDay && Game1.currentLocation.IsOutdoors)
            DrawFloatingFogParticles(e.SpriteBatch, fogColor);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
    }
}