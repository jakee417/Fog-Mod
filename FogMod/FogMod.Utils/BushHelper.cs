#nullable enable
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using System.Collections.Generic;
using System.Linq;

namespace FogMod.Utils;

public static class BushHelper
{

    private static Dictionary<string, List<Bush>> cache = new();

    public static List<Bush> GetAvailableBushes(GameLocation location)
    {
        var bushes = new List<Bush>();
        foreach (var feature in location.largeTerrainFeatures)
        {
            if (feature is Bush bush)
                bushes.Add(bush);
        }
        return bushes;
    }

    public static bool IsBushFruity(Bush bush)
    {
        return bush.inBloom() && bush.readyForHarvest() && bush.GetShakeOffItem() != null;
    }

    public static List<Bush> GetAvailableBushesWithFruit(GameLocation location)
    {
        if (cache.TryGetValue(location.NameOrUniqueName, out var cachedBushes))
            return cachedBushes;
        var allBushes = GetAvailableBushes(location);
        var bushes = allBushes.Where(b => IsBushFruity(b)).ToList();
        cache[location.NameOrUniqueName] = bushes;
        return bushes;
    }

    public static Bush? GetNearestBushWithFruitToTile(GameLocation location, Vector2 tile)
    {
        Bush? nearestBush = null;
        float nearestDist = float.MaxValue;
        foreach (var bush in GetAvailableBushesWithFruit(location))
        {
            float dist = Vector2.DistanceSquared(bush.Tile, tile);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestBush = bush;
            }
        }
        return nearestBush;
    }
}