#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace FogMod.Utils;

// Pure critter rules (pathing, skin, loot), extracted from the FogMod and
// Critterz partials so they can run under dotnet test. Callers keep thin
// wrappers that read game state (Game1, location) and delegate here;
// behavior is unchanged.
public static class CritterzLogic
{
    // BFS over reachable tiles; 4-directional so paths never clip corners.
    // Endpoint bush tiles are exempt (Critterz burst out of / dive into
    // bushes), but paths never route through other bushes.
    public static List<Vector2>? FindPath(Vector2 startTile, Vector2 goalTile, HashSet<Vector2> reachable)
    {
        if (startTile == goalTile)
            return new List<Vector2>();
        var prev = new Dictionary<Vector2, Vector2> { [startTile] = startTile };
        var queue = new Queue<Vector2>();
        queue.Enqueue(startTile);
        bool found = false;
        int guard = 0;
        while (queue.Count > 0 && guard++ < 40000)
        {
            Vector2 current = queue.Dequeue();
            if (current == goalTile)
            {
                found = true;
                break;
            }
            TryStep(current + new Vector2(1, 0));
            TryStep(current + new Vector2(-1, 0));
            TryStep(current + new Vector2(0, 1));
            TryStep(current + new Vector2(0, -1));

            void TryStep(Vector2 next)
            {
                if ((reachable.Contains(next) || next == goalTile) && !prev.ContainsKey(next))
                {
                    prev[next] = current;
                    queue.Enqueue(next);
                }
            }
        }
        if (!found)
            return null;
        List<Vector2> path = new List<Vector2>();
        Vector2 step = goalTile;
        while (step != startTile)
        {
            path.Add(step);
            step = prev[step];
        }
        path.Reverse();
        return path;
    }

    // Expand tile centers into pixel waypoints. The sprite only faces
    // left/right, so vertical legs split into alternating 45-degree hops
    // (inside the 60-degree cone around the x-axis), which reads as a
    // zig-zag instead of straight up/down movement.
    public static List<Vector2> ExpandPathWithZigZag(Vector2 startPixel, List<Vector2> tilePath, int tileSize, int firstSide)
    {
        List<Vector2> waypoints = new List<Vector2>();
        Vector2 current = startPixel;
        int side = firstSide;
        foreach (Vector2 tile in tilePath)
        {
            Vector2 target = tile * tileSize + new Vector2(32f, 32f);
            if (Math.Abs(target.X - current.X) < 0.5f && Math.Abs(target.Y - current.Y) > 0.5f)
            {
                Vector2 mid = new Vector2(
                    current.X + side * Constants.CritterzZigZagAmplitudePx,
                    (current.Y + target.Y) / 2f
                );
                waypoints.Add(mid);
                side = -side;
            }
            waypoints.Add(target);
            current = target;
        }
        return waypoints;
    }

    // Distance-squared weighted pick over pixel positions (farther
    // preferred), with a shuffle so equal weights still vary. Returns the
    // picked index. RNG call order matches the original bush picker.
    public static int PickWeightedIndex(IReadOnlyList<Vector2> pixelPositions, Vector2 fromPosition, Random rng)
    {
        var weighted = pixelPositions
            .Select((pos, index) =>
            {
                float distance = Vector2.Distance(fromPosition, pos);
                return (index, weight: distance * distance);
            })
            .OrderBy(_ => rng.Next())
            .ToList();
        float totalWeight = weighted.Sum(t => t.weight);
        if (totalWeight == 0f)
            return 0;
        float roll = (float)rng.NextDouble() * totalWeight;
        float cumulative = 0f;
        foreach (var (index, weight) in weighted)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return index;
        }
        return 0;
    }

    // Skin only: squirrel, bunny (grey/white by season), or raccoon row.
    public static int PickSkin(int roll, bool isWinter)
    {
        return roll switch
        {
            0 => Constants.SquirrelRow,
            2 => Constants.RaccoonRow,
            _ => isWinter ? Constants.WhiteBunnyRow : Constants.GreyBunnyRow,
        };
    }

    // Kill loot, pre-rolled at spawn into the vanilla objectsToDrop list
    // (dropped by GameLocation.monsterDrop, like the grouse snacks). The
    // critter stays generic, but the loot is skin-specific: bunnies drop
    // rabbit goods, squirrels drop nuts, raccoons drop trash.
    public static List<string> RollLootToDrop(int skin, bool lucky, Random rng)
    {
        List<string> loot = new List<string>();
        int maxCommons = lucky ? 3 : 2;
        switch (skin)
        {
            case Constants.GreyBunnyRow:
            case Constants.WhiteBunnyRow:
                for (int i = 0, n = rng.Next(0, maxCommons); i < n; i++)
                    loot.Add("78"); // Cave Carrot
                if (rng.NextDouble() < (lucky ? 0.15 : 0.10))
                    loot.Add("440"); // Wool
                // Rare chase item (cf. Serpent 0.8%).
                if (rng.NextDouble() < (lucky ? 0.05 : 0.03))
                    loot.Add("446"); // Rabbit's Foot
                break;
            case Constants.SquirrelRow:
                string[] nuts = { "408", "309", "311" }; // Hazelnut, acorn, pine cone
                for (int i = 0, n = rng.Next(0, maxCommons); i < n; i++)
                    loot.Add(nuts[rng.Next(nuts.Length)]);
                break;
            case Constants.RaccoonRow:
            default:
                string[] trash = { "168", "169", "170", "171", "172" };
                for (int i = 0, n = rng.Next(0, maxCommons); i < n; i++)
                    loot.Add(trash[rng.Next(trash.Length)]);
                break;
        }
        return loot;
    }
}
