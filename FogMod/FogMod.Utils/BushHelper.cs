#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FogMod.Utils;

public static class BushHelper
{
    public static void TriggerBushLeaves(Bush bush, int numLeaves = 5)
    {
        Rectangle bushRect = bush.sourceRect.Value;
        if (bushRect.Width < 8 || bushRect.Height < 8)
            return;
        Texture2D texture;
        try
        {
            texture = Game1.content.Load<Texture2D>("TileSheets\\bushes");
        }
        catch
        {
            return;
        }
        // Sample green pixels from the middle of the bush itself, so the
        // leaves always match the bush's season.
        Rectangle leafRect = new Rectangle(
            bushRect.X + bushRect.Width / 2 - 4,
            bushRect.Y + bushRect.Height / 2 - 4,
            8,
            8
        );
        Vector2 center = bush.Tile * Game1.tileSize + new Vector2(32f, 32f);
        Random rng = new Random((int)(center.X + center.Y));
        for (int i = 0; i < numLeaves; i++)
        {
            Vector2 spawnPos = new Vector2(
                center.X + rng.Next(-32, 32),
                center.Y - rng.Next(0, 64)
            );
            float yVelocity = rng.Next(10, 40) / 10f;
            float rotationRate = rng.Next(-10, 10) / 100f;
            TreeHelper.leaves.Add(new TreeHelper._Leaf(spawnPos, rotationRate, 0, yVelocity, center.Y, texture, leafRect));
        }
    }


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