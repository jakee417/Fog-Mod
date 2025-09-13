#nullable enable
using System;
using StardewModdingAPI;

namespace FogMod.GMCM;

public static class GMCM
{
    public static ModConfig Config = FogMod.Config;
    public static IManifest? ModManifest = FogMod.Instance?.ModManifest;
    public static IModHelper? Helper = FogMod.Instance?.Helper;
    public static IMonitor? Monitor = FogMod.Instance?.Monitor;

    public class ModConfig
    {
        public bool EnableFog { get; set; } = true;
        public bool EnableFogBank { get; set; } = true;
        public bool EnableDailyRandomFog { get; set; } = true;
        public bool EnableWeatherBasedFog { get; set; } = true;
        public bool EnableTimeOfDayFog { get; set; } = true;
        public bool EnableExplosionSmoke { get; set; } = true;
        public bool EnableGrouseCritters { get; set; } = true;
        public SButton GrouseToggleKey { get; set; } = SButton.G;
        public bool DebugShowInfo { get; set; } = false;
    }

    public static void RegisterModConfig(IGenericModConfigMenuApi configMenu)
    {
        try
        {
            if (Config == null)
                return;

            configMenu.Register(
                ModManifest,
                () => { Config = new ModConfig(); Monitor?.Log("Config reset to defaults", LogLevel.Info); },
                () => { Helper?.WriteConfig(Config); Monitor?.Log("Config saved", LogLevel.Info); },
                titleScreenOnly: false
            );

            configMenu.AddSectionTitle(
                ModManifest,
                () => "Fog",
                () => "Configure fog settings"
            );

            configMenu.AddBoolOption(
                ModManifest,
                () => Config.EnableFog,
                value => Config.EnableFog = value,
                () => "Enable Fog Clouds",
                () => "Show fog cloud effects (individual fog particles)"
            );

            configMenu.AddBoolOption(
                ModManifest,
                () => Config.EnableFogBank,
                value => Config.EnableFogBank = value,
                () => "Enable Fog Bank",
                () => "Show fog bank effect (same as fog in infested mine level)."
            );

            configMenu.AddBoolOption(
                ModManifest,
                () => Config.EnableDailyRandomFog,
                value =>
                {
                    Config.EnableDailyRandomFog = value;
                    FogMod.Instance?.InitializeDailyFogStrength();
                    FogMod.Instance?.ResetFogParticles();
                },
                () => "Daily Random Fog Strength",
                () => "Enable or disable daily random fog strength based on the season."
            );

            configMenu.AddBoolOption(
                ModManifest,
                () => Config.EnableWeatherBasedFog,
                value => Config.EnableWeatherBasedFog = value,
                () => "Weather Based Fog Effect",
                () => "Enable or disable weather-based fog effect (rain, storms, snow, etc.)"
            );

            configMenu.AddBoolOption(
                ModManifest,
                () => Config.EnableTimeOfDayFog,
                value => Config.EnableTimeOfDayFog = value,
                () => "Time of Day Fog Effect",
                () => "Enable or disable time of day fog effect (daylight, night, etc.)"
            );

            configMenu.AddSectionTitle(
                ModManifest,
                () => "Explosions",
                () => "Configure explosion smoke settings"
            );

            configMenu.AddBoolOption(
                ModManifest,
                () => Config.EnableExplosionSmoke,
                value => Config.EnableExplosionSmoke = value,
                () => "Enable Explosion Smoke",
                () => "Show smoke particles when explosions occur."
            );

            configMenu.AddSectionTitle(
                ModManifest,
                () => "Grouse",
                () => "Configure grouse settings"
            );

            configMenu.AddBoolOption(
                ModManifest,
                () => Config.EnableGrouseCritters,
                value => Config.EnableGrouseCritters = value,
                () => "Enable Grouse Critters",
                () => "Spawn grouse birds in trees that flush when you get close. Requires restart to take full effect."
            );


            configMenu.AddKeybind(
                ModManifest,
                () => Config.GrouseToggleKey,
                value => Config.GrouseToggleKey = value,
                () => "Grouse Spawn Hotkey",
                () => "The key used to spawn a grouse."
            );

            configMenu.AddSectionTitle(
                ModManifest,
                () => "Debug Options",
                () => "Options for debugging and development purposes."
            );

            configMenu.AddBoolOption(
                ModManifest,
                () => Config.DebugShowInfo,
                value => Config.DebugShowInfo = value,
                () => "Show Debugging Information",
                () => "Display debugging information in the top-left corner"
            );
        }
        catch (Exception ex)
        {
            Monitor?.Log($"Failed to register with Generic Mod Config Menu: {ex.Message}", LogLevel.Warn);
            Monitor?.Log($"Exception details: {ex}", LogLevel.Debug);
        }
    }
}