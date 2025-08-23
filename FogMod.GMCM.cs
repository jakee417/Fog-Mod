using System;
using StardewModdingAPI;

namespace FogMod
{
    public partial class FogMod : Mod

    {
        private void RegisterModConfig(GenericModConfigMenu.IGenericModConfigMenuApi configMenu)
        {
            try
            {
                configMenu.Register(
                    ModManifest,
                    () => { config = new ModConfig(); Monitor.Log("Config reset to defaults", LogLevel.Info); },
                            () => { Helper.WriteConfig(config); Monitor.Log("Config saved", LogLevel.Info); },
                            titleScreenOnly: false
                        );

                configMenu.AddSectionTitle(
                    ModManifest,
                    () => "Fog Clouds",
                    () => "Configure fog cloud settings"
                );

                configMenu.AddBoolOption(
                    ModManifest,
                    () => config.EnableDailyRandomFog,
                    value =>
                    {
                        config.EnableDailyRandomFog = value;
                        InitializeDailyFogStrength();
                        ResetFogParticles();
                    },
                    () => "Daily Random Fog Strength",
                    () => "Enable or disable daily random fog strength based on the season."
                );

                configMenu.AddBoolOption(
                    ModManifest,
                    () => config.EnableWeatherBasedFog,
                    value => config.EnableWeatherBasedFog = value,
                    () => "Weather Based Fog Effect",
                    () => "Enable or disable weather-based fog effect (rain, storms, snow, etc.)"
                );

                configMenu.AddBoolOption(
                    ModManifest,
                    () => config.EnableTimeOfDayFog,
                    value => config.EnableTimeOfDayFog = value,
                    () => "Time of Day Fog Effect",
                    () => "Enable or disable time of day fog effect (daylight, night, etc.)"
                );

                configMenu.AddSectionTitle(
                    ModManifest,
                    () => "Particle Settings",
                    () => "Configure particle settings"
                );

                configMenu.AddBoolOption(
                    ModManifest,
                    () => config.ParticleStrength,
                    value => config.ParticleStrength = value,
                    () => "Particle Strength",
                    () => "Set the strength of the particle effect high/low"
                );

                configMenu.AddBoolOption(
                    ModManifest,
                    () => config.LightThinningStrength,
                    value => config.LightThinningStrength = value,
                    () => "Light Thinning Strength",
                    () => "Set the strength of the light thinning effect high/low"
                );

                // Debug
                configMenu.AddSectionTitle(
                    ModManifest,
                    () => "Debug",
                    () => "Developer options"
                );

                configMenu.AddBoolOption(
                    ModManifest,
                    () => config.DebugLightRings,
                    value => config.DebugLightRings = value,
                    () => "Draw Light Rings",
                    () => "Draw debug rings around light sources"
                );

                configMenu.AddBoolOption(
                    ModManifest,
                    () => config.DebugFogCells,
                    value => config.DebugFogCells = value,
                    () => "Draw Fog Cells",
                    () => "Draw a red grid overlay for fog cells"
                );

                configMenu.AddBoolOption(
                    ModManifest,
                    () => config.DebugFogBlack,
                    value => config.DebugFogBlack = value,
                    () => "Force Black Fog",
                    () => "Override fog color to solid black for debugging"
                );

                configMenu.AddBoolOption(
                    ModManifest,
                    () => config.DebugShowInfo,
                    value => config.DebugShowInfo = value,
                    () => "Show Debugging Information",
                    () => "Display debugging information in the top-left corner"
                );
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to register with Generic Mod Config Menu: {ex.Message}", LogLevel.Warn);
                Monitor.Log($"Exception details: {ex}", LogLevel.Debug);
            }
        }
    }
}


