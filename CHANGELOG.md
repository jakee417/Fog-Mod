# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.4.2] - 2025-09-26

### Changed
- Compatibility with Junimatic mod
- Removed all dialogue from the grouse

### Technical
- set `IsVillager` to false

## [0.4.1] - 2025-09-26

### Changed
- Added changelog and update keys


## [0.4] - 2025-09-26

### Added
- Void grouse variant with 20% spawn chance, featuring gray feathers and void egg drops
- New void grouse texture asset (`grouse_void.png`)

### Changed
- Grouse now randomly spawn as either normal or void variants
- Void grouse drop void eggs (305) instead of regular eggs
- Updated grouse death and hit effects to use appropriate feather colors (gray for void, brown for normal)

### Technical
- Added `TextureName` property and `IsVoid` boolean to Grouse class for variant handling
- Implemented dynamic texture loading based on grouse type
- Updated Grouse constructor to accept texture name parameter
- Modified drawing functions to use instance-specific textures instead of hardcoded references
- Added `grouseVoidTexture` property to FogMod class

## [0.3] - 2025-09-21

### Added
- Automated release build script (`build_release.sh`) that handles version updating, building, folder renaming, and zip creation
- Proper sprite initialization for grouse entities to improve compatibility with other mods
- New demo GIF showcasing extended grouse behavior

### Changed
- Renamed mod from "Fog Mod" to "Foggy Grouse" to better reflect its features
- Updated mod description to include both grouse and fog effects
- Changed UniqueID to `jakeetaylor.FoggyGrouse` for better identification
- Improved build configuration with `VersionPrefix` for flexible versioning

### Fixed
- Issue where grouse effects (leaves spawning, explosions) occurred in locations where the player was not present
- Added location synchronization checks to prevent multiplayer desync issues

### Technical
- Added `IsColocatedWithPlayer` utility methods for location validation
- Updated `Grouse.cs` to check player location before triggering effects
- Modified `Multiplayer.cs` to use new utility for message handling

## [0.2] - 2025-09-20

### Added
- Enhanced leaf effects when grouse are perched, now triggering 3 falling leaves per event
- Additional demo assets including fog demo GIF and MOV, bomb demo updates, grouse demo variations, and release screenshot

### Changed
- Improved GMCM configuration descriptions for better clarity on fog layers and modifiers
- Simplified multi-slingshot directions to reduce complexity
- Removed startup log message for cleaner initialization

### Performance
- Optimized debug information rendering by updating only every 8 frames to reduce performance overhead

### Technical
- Updated Grouse.cs to include leaf falling mechanics when perched
- Modified Debug.cs to cache debug text and reduce frame-by-frame updates

## [0.1] - 2025-09-17

### Added
- Core fog system with floating particles and fog banks
- Grouse spawning system with tree-based AI and behaviors (perched, flying, surprised states)
- Multiplayer synchronization for grouse events and explosions
- GMCM integration for fog configuration (strength, weather effects, time of day modifiers)
- Multi-slingshot weapon that fires in multiple directions
- Debug overlay with detailed game state information
- Nexus Mods documentation and installation guides
- Demo videos and GIFs showcasing fog and grouse mechanics

### Changed
- Grouse gameplay balanced for harder difficulty (health, speed, spawn rates)
- Fog effects respond to weather conditions and time of day
- Grouse drop eggs as loot with probability system

### Technical
- Implemented Harmony patches for game integration
- Added custom rendering system for fog particles and grouse
- Created utility classes for multiplayer, tree management, and location handling
- Refactored visibility and alpha blending systems
- Added explosion effects with particle systems
- Implemented light effects and color tinting