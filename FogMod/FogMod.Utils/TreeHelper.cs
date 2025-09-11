#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FogMod.Utils;

public static class TreeHelper
{
    private static Dictionary<string, Dictionary<Tuple<float, float>, Tree>> cache = new();
    private static List<_Leaf> leaves = new();

    private class _Leaf : Leaf
    {
        internal float initialY;
        internal Texture2D texture;

        public _Leaf(Vector2 position, float rotationRate, int type, float yVelocity, float initialY, Texture2D texture)
            : base(position, rotationRate, type, yVelocity)
        {
            this.initialY = initialY;
            this.texture = texture;
        }
    }

    public static void ResetLeaves()
    {
        leaves.Clear();
    }

    public static void ClearCache()
    {
        cache.Clear();
        ResetLeaves();
    }

    public static Vector2 GetGrouseSpawnPosition(Tree tree)
    {
        Rectangle renderBounds = tree.getRenderBounds();
        int seed = renderBounds.X * 1000 + renderBounds.Y * 1000;
        var treeRng = new Random(seed);
        float minHeightFromTop = renderBounds.Height / 4f;  // 1/4 from top = 3/4 height
        float maxHeightFromTop = renderBounds.Height / 2f;  // 1/2 from top = 1/2 height
        float yOffset = (float)(treeRng.NextDouble() * (maxHeightFromTop - minHeightFromTop) + minHeightFromTop);
        float xVariance = (float)(treeRng.NextDouble() - 0.5) * (renderBounds.Width * 0.1f);
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

    public static List<Tree> GetAvailableTrees(IEnumerable<GameLocation> locations)
    {
        var availableTrees = new List<Tree>();
        foreach (var location in locations)
        {
            availableTrees.AddRange(GetAvailableTreePositions(location));
        }
        return availableTrees;
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

    public static void TriggerFallingLeaves(Tree tree, Vector2 position, int numLeaves = 10)
    {
        if (tree != null && tree.IsLeafy() && tree.texture.Value is Texture2D texture)
        {
            Random rng = new Random((int)(position.X + position.Y));
            for (int i = 0; i < numLeaves; i++)
            {
                Vector2 spawnPos = new Vector2(
                    position.X + rng.Next(-32, 32),
                    position.Y - rng.Next(0, 64)
                );
                float yVelocity = rng.Next(10, 40) / 10f;
                int type = rng.Next(4);
                float rotationRate = rng.Next(-10, 10) / 100f;
                leaves.Add(new _Leaf(spawnPos, rotationRate, type, yVelocity, position.Y, texture));
            }
        }
    }

    public static void UpdateLeaves()
    {
        for (int i = leaves.Count - 1; i >= 0; i--)
        {
            var leaf = leaves[i];
            leaf.position.Y -= leaf.yVelocity - 4.5f;
            leaf.yVelocity = Math.Max(0f, leaf.yVelocity - 0.01f);
            leaf.rotation += leaf.rotationRate;
            if (leaf.position.Y >= leaf.initialY + 64f)
            {
                leaves.RemoveAt(i);
            }
        }
    }

    public static void DrawLeaves(SpriteBatch spriteBatch)
    {
        foreach (var leaf in leaves)
        {
            Rectangle sourceRect = new Rectangle(16 + leaf.type % 2 * 8, 112 + leaf.type / 2 * 8, 8, 8);
            Vector2 drawPos = Game1.GlobalToLocal(Game1.viewport, leaf.position);
            spriteBatch.Draw(leaf.texture, drawPos, sourceRect, Color.White, leaf.rotation, Vector2.Zero, 4f, SpriteEffects.None, 0.99f);
        }
    }
}