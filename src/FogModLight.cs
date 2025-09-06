#nullable enable
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace FogMod;

public partial class FogMod : Mod
{
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
                // position is NetVector2; use Value
                Vector2 rawPos = ls.position.Value;
                // Heuristic: small values (tile indices) need conversion to world pixels.
                // Convert tile coords -> pixels and center inside tile (tile*64 + 32).
                Vector2 pos = (rawPos.X < 100f && rawPos.Y < 100f)
                    ? rawPos * 64f + new Vector2(32f, 32f)
                    : rawPos;
                float radiusPixels = ls.radius.Value * 64f; // tiles to pixels
                lightSources.Add(new LightInfo(position: pos, radiusPixels: radiusPixels));
            }
        }
        catch { }
    }
}