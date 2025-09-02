#nullable enable
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FogMod
{
    public static class TreeHelper
    {
        private static Dictionary<string, Dictionary<Tuple<float, float>, Tree>> cache = new();

        public static void ClearCache()
        {
            cache.Clear();
        }

        public static Vector2 GetGrouseSpawnPosition(Tree tree)
        {
            Rectangle renderBounds = tree.getRenderBounds();
            int seed = renderBounds.X * 1000 + renderBounds.Y * 1000;
            var treeRng = new System.Random(seed);
            float minHeightFromTop = renderBounds.Height / 8f;  // 1/8 from top = 7/8 height
            float maxHeightFromTop = renderBounds.Height / 2f;  // 1/2 from top = 1/2 height
            float yOffset = (float)(treeRng.NextDouble() * (maxHeightFromTop - minHeightFromTop) + minHeightFromTop);
            float xVariance = (float)(treeRng.NextDouble() - 0.5) * (renderBounds.Width * 0.2f);
            return new Vector2(
                renderBounds.X + renderBounds.Width / 2f + xVariance,
                renderBounds.Y + yOffset
            );
        }

        public static Vector2 GetTreePosition(Tree tree)
        {
            return tree.Tile;
        }

        private static bool GetLegalTree(Tree tree)
        {
            return tree.growthStage.Value >= Tree.treeStage && tree.stump.Value == false && tree.IsLeafy();
        }

        public static List<Tree> GetAvailableTreePositions(GameLocation location)
        {
            string locationKey = location.NameOrUniqueName;

            var availableTrees = new List<Tree>();
            cache[locationKey] = new Dictionary<Tuple<float, float>, Tree>();

            if (location.terrainFeatures == null)
                return availableTrees;

            foreach (var pair in location.terrainFeatures.Pairs)
            {
                if (pair.Value is Tree tree && GetLegalTree(tree))
                {
                    availableTrees.Add(tree);
                    cache[locationKey].Add(Tuple.Create(tree.Tile.X, tree.Tile.Y), tree);
                }
            }
            return availableTrees;
        }

        public static Tree? GetTreeFromId(GameLocation location, Vector2 tile)
        {
            if (cache.TryGetValue(location.NameOrUniqueName, out var locationCache))
            {
                if (locationCache.TryGetValue(Tuple.Create(tile.X, tile.Y), out var tree))
                    return tree;
            }

            // fall back in case location has not been cached
            List<Tree> trees = GetAvailableTreePositions(location);
            Tree? result = trees.FirstOrDefault(t => t.Tile == tile);
            return result;
        }
    }
}
