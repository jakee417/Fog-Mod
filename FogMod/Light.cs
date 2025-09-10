#nullable enable
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using FogMod.Models;

namespace FogMod;

public partial class FogMod : Mod
{
    private List<LightInfo> lightSources = new List<LightInfo>();

    private void RefreshLightSources()
    {
        lightSources.Clear();
        try
        {
            var loc = Game1.currentLocation;
            if (loc == null) return;
            var dict = loc.sharedLights;
            if (dict == null) return;
            foreach (var ls in dict.Values)
            {
                if (ls == null) continue;
                Vector2 rawPos = ls.position.Value;
                Vector2 pos = (rawPos.X < 100f && rawPos.Y < 100f)
                    ? rawPos * Game1.tileSize + new Vector2(32f, 32f)
                    : rawPos;
                float radiusPixels = ls.radius.Value * Game1.tileSize; // tiles to pixels
                lightSources.Add(new LightInfo(position: pos, radiusPixels: radiusPixels));
            }
        }
        catch { }
    }
}