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
            // Fallback to calculated position in tree canopy area
            return GetFallbackGrousePosition(tree);
        }



        private static Vector2 GetFallbackGrousePosition(Tree tree)
        {
            Rectangle renderBounds = tree.getRenderBounds();

            // Position at exact center of render bounds (matching the orange/black debug dot)
            return new Vector2(
                renderBounds.X + renderBounds.Width / 2f,   // Horizontal center
                renderBounds.Y + renderBounds.Height / 4f   // Vertical center
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
