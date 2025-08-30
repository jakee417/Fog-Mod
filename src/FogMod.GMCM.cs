#nullable enable
using System;
using StardewModdingAPI;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        public class ModConfig
        {
            public bool EnableDailyRandomFog { get; set; } = true;
            public bool EnableWeatherBasedFog { get; set; } = true;
            public bool EnableTimeOfDayFog { get; set; } = true;
            public bool ParticleStrength { get; set; } = true;
            public bool LightThinningStrength { get; set; } = true;
            public bool DebugShowInfo { get; set; } = false;
            public bool EnableGrouseCritters { get; set; } = false;
        }

        private void RegisterModConfig(GenericModConfigMenu.IGenericModConfigMenuApi configMenu)
        {
            try
            {
                configMenu.Register(
                    ModManifest,
                    () => { Config = new ModConfig(); Monitor.Log("Config reset to defaults", LogLevel.Info); },
                            () => { Helper.WriteConfig(Config); Monitor.Log("Config saved", LogLevel.Info); },
                            titleScreenOnly: false
                        );

                configMenu.AddSectionTitle(
                    ModManifest,
                    () => "Fog Clouds",
                    () => "Configure fog cloud settings"
                );

                configMenu.AddBoolOption(
                    ModManifest,
                    () => Config.EnableDailyRandomFog,
                    value =>
                    {
                        Config.EnableDailyRandomFog = value;
                        InitializeDailyFogStrength();
                        ResetFogParticles();
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

                configMenu.AddBoolOption(
                    ModManifest,
                    () => Config.ParticleStrength,
                    value => Config.ParticleStrength = value,
                    () => "Particle Strength",
                    () => "Set the strength of the particle effect high/low"
                );

                configMenu.AddBoolOption(
                    ModManifest,
                    () => Config.LightThinningStrength,
                    value => Config.LightThinningStrength = value,
                    () => "Light Thinning Strength",
                    () => "Set the strength of the light thinning effect high/low"
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
                Monitor.Log($"Failed to register with Generic Mod Config Menu: {ex.Message}", LogLevel.Warn);
                Monitor.Log($"Exception details: {ex}", LogLevel.Debug);
            }
        }
    }
}