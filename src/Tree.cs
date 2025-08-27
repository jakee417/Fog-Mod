using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using System.Collections.Generic;
using System.Reflection;
using StardewModdingAPI;

namespace FogMod
{
    public static class TreeHelper
    {
        public static Vector2 GetGrouseSpawnPosition(Tree tree)
        {
            try
            {
                // Try to use average leaf position for most realistic placement
                Vector2? leafAveragePosition = GetAverageLeafPosition(tree);
                if (leafAveragePosition.HasValue)
                {
                    return leafAveragePosition.Value;
                }
            }
            catch
            {
                FogMod.Instance.Monitor.Log("Failed to get average leaf position for tree.", LogLevel.Info);

            }

            // Fallback to calculated position in tree canopy area
            return GetFallbackGrousePosition(tree);
        }

        private static Vector2? GetAverageLeafPosition(Tree tree)
        {
            // Use reflection to access private leaves field
            var leavesField = typeof(Tree).GetField("leaves", BindingFlags.NonPublic | BindingFlags.Instance);
            if (leavesField == null)
                return null;

            var leaves = leavesField.GetValue(tree) as System.Collections.IList;
            if (leaves == null || leaves.Count == 0)
                return null;

            Vector2 leafSum = Vector2.Zero;
            int validLeafCount = 0;

            foreach (var leaf in leaves)
            {
                var positionField = leaf.GetType().GetField("position", BindingFlags.Public | BindingFlags.Instance);
                if (positionField != null)
                {
                    var position = (Vector2)positionField.GetValue(leaf);
                    leafSum += position;
                    validLeafCount++;
                }
            }

            if (validLeafCount > 0)
            {
                return leafSum / validLeafCount;
            }

            return null;
        }

        private static Vector2 GetFallbackGrousePosition(Tree tree)
        {
            Rectangle renderBounds = tree.getRenderBounds();

            // Position at top 3/4 height, centered horizontally in render bounds
            return new Vector2(
                renderBounds.X + renderBounds.Width / 2f,      // Horizontal center
                renderBounds.Y + renderBounds.Height * 0.25f   // Top 3/4 (25% down from top)
            );
        }

        public static List<Vector2> GetAvailableTreePositions(GameLocation location, HashSet<Vector2> spawnedTreePositions)
        {
            var availableTrees = new List<Vector2>();

            if (location?.terrainFeatures == null)
                return availableTrees;

            foreach (var pair in location.terrainFeatures.Pairs)
            {
                if (pair.Value is Tree tree && tree.growthStage.Value >= Tree.treeStage)
                {
                    Vector2 treePos = GetGrouseSpawnPosition(tree);

                    if (!spawnedTreePositions.Contains(treePos))
                    {
                        availableTrees.Add(treePos);
                    }
                }
            }

            return availableTrees;
        }
    }
}
