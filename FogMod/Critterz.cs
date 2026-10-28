#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using Netcode;
using System.Linq;
using FogMod.Models;
using FogMod.Utils;


namespace FogMod;

public partial class FogMod : Mod
{
    public Texture2D? critterzTexture { get; set; }

    // Everything static the player collides with. Characters and farmers are
    // excluded: they move, and the player anchors the flood fill.
    private static readonly CollisionMask WalkableCheck =
        CollisionMask.Buildings | CollisionMask.Flooring | CollisionMask.Furniture |
        CollisionMask.Objects | CollisionMask.TerrainFeatures | CollisionMask.LocationSpecific;

    private void InitializeCritterz()
    {
        ClearCritterzFromAllLocations();
        SpawnCritterzInBushes(outdoorLocations);
    }

    private void ClearCritterzFromAllLocations()
    {
        foreach (GameLocation loc in outdoorLocations)
        {
            loc.characters.RemoveWhere(p => p is Critterz);
        }
    }

    private static Dictionary<string, HashSet<Vector2>> reachabilityCache = new();

    public static void ClearReachabilityCache()
    {
        reachabilityCache.Clear();
    }

    // Tiles the player can walk to, flooded from the player's position and
    // every warp entrance into the location. Blocked uses the engine's own
    // query, so map tiles, trees, objects, buildings, and clumps all count.
    // Cached per day.
    private HashSet<Vector2> GetReachableTiles(GameLocation location)
    {
        string key = location.NameOrUniqueName;
        if (reachabilityCache.TryGetValue(key, out var cached))
            return cached;
        HashSet<Vector2> reachable = ComputeReachableTiles(location);
        reachabilityCache[key] = reachable;
        return reachable;
    }

    private HashSet<Vector2> ComputeReachableTiles(GameLocation location)
    {
        var reachable = new HashSet<Vector2>();
        try
        {
            int width = location.Map.Layers[0].LayerWidth;
            int height = location.Map.Layers[0].LayerHeight;

            var queue = new Queue<Vector2>();
            void TryAdd(Vector2 tile)
            {
                if (tile.X < 0 || tile.Y < 0 || tile.X >= width || tile.Y >= height)
                    return;
                if (location.IsTileBlockedBy(tile, WalkableCheck, WalkableCheck, false))
                    return;
                if (reachable.Add(tile))
                    queue.Enqueue(tile);
            }

            // The player is standing somewhere reachable.
            if (Game1.player?.currentLocation?.NameOrUniqueName == location.NameOrUniqueName)
                TryAdd(Game1.player.Tile);
            // Otherwise the player arrives through a warp from elsewhere.
            foreach (GameLocation other in Game1.locations)
            {
                foreach (Warp warp in other.warps)
                {
                    if (warp.TargetName == location.NameOrUniqueName)
                        TryAdd(new Vector2(warp.TargetX, warp.TargetY));
                }
            }

            int guard = 0;
            while (queue.Count > 0 && guard++ < 40000)
            {
                Vector2 tile = queue.Dequeue();
                TryAdd(tile + new Vector2(1, 0));
                TryAdd(tile + new Vector2(-1, 0));
                TryAdd(tile + new Vector2(0, 1));
                TryAdd(tile + new Vector2(0, -1));
            }
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to compute reachable tiles for {location.NameOrUniqueName}: {ex.Message}", LogLevel.Trace);
            reachable.Clear();
        }
        return reachable;
    }

    public static List<Critterz> GetAllCritterz()
    {
        List<Critterz> allCritterz = new List<Critterz>();
        foreach (GameLocation loc in outdoorLocations)
        {
            allCritterz.AddRange(loc.characters.Where(c => c is Critterz).Cast<Critterz>());
        }
        return allCritterz;
    }

    public Critterz? GetCritterzById(int critterzId)
    {
        return GetAllCritterz().FirstOrDefault(r => r.CritterzId == critterzId);
    }

    private void SpawnCritterzInBushes(IEnumerable<GameLocation> locations)
    {
        foreach (GameLocation loc in locations)
        {
            if (loc.IsOutdoors)
                TrySpawnCritterz(loc);
        }
    }

    // Preallocate hidden Critterz inside bushes, grouse-perch style.
    public void TrySpawnCritterz(GameLocation location)
    {
        if (location is Farm || location is StardewValley.Locations.Desert)
            return;
        if (location.largeTerrainFeatures == null || location.largeTerrainFeatures.Count == 0)
            return;

        int critterzCount = location.characters.Count(c => c is Critterz);
        if (critterzCount >= Constants.CritterzMaxPerLocation)
            return;

        HashSet<Vector2> reachable = GetReachableTiles(location);
        List<Bush> bushes = BushHelper.GetAvailableBushes(location)
            .Where(b => HasReachableExit(b.Tile, reachable))
            .OrderBy(_ => Game1.random.Next())
            .ToList();
        foreach (Bush bush in bushes)
        {
            if (critterzCount >= Constants.CritterzMaxPerLocation)
                break;
            if (Game1.random.NextDouble() >= Constants.CritterzSpawnChance)
                continue;
            SpawnCritterz(location, bush.Tile, Game1.random.NextBool(), bush.Tile);
            critterzCount++;
        }
    }

    private Critterz SpawnCritterz(GameLocation location, Vector2 tile, bool flip, Vector2 homeBush)
    {
        int critterzId = Utilities.GetDeterministicId(
            locationSeed: location.NameOrUniqueName.GetHashCode(),
            daySeed: (int)Game1.stats.DaysPlayed,
            position: tile,
            salt: (int)Random.NextInt64()
        );
        // Skin only: squirrel, bunny (grey/white by season), or raccoon row.
        int skin = Game1.random.Next(3) switch
        {
            0 => Constants.SquirrelRow,
            2 => Constants.RaccoonRow,
            _ => location.IsWinterHere() ? Constants.WhiteBunnyRow : Constants.GreyBunnyRow,
        };
        Critterz newCritterz = new Critterz(
            critterzId: critterzId,
            location: location,
            tilePosition: tile,
            flip: flip,
            homeBush: homeBush,
            skin: skin
        );
        location.characters.Add(newCritterz);
        return newCritterz;
    }

    public Bush? NearestReachableBush(GameLocation location, Vector2 fromTile)
    {
        HashSet<Vector2> reachable = GetReachableTiles(location);
        List<Bush> bushes = BushHelper.GetAvailableBushes(location)
            .Where(b => HasReachableExit(b.Tile, reachable))
            .ToList();
        if (bushes.Count == 0)
            return null;
        return bushes.OrderBy(b => Vector2.DistanceSquared(b.Tile, fromTile)).First();
    }

    // Bush tiles block movement (engine truth), so a bush is usable when a
    // Critterz can burst out onto neighboring reachable ground.
    private bool HasReachableExit(Vector2 bushTile, HashSet<Vector2> reachable)
    {
        return reachable.Contains(bushTile + new Vector2(1, 0))
            || reachable.Contains(bushTile + new Vector2(-1, 0))
            || reachable.Contains(bushTile + new Vector2(0, 1))
            || reachable.Contains(bushTile + new Vector2(0, -1));
    }

    // Any client can spook a sitting Critterz; the synced state transition is
    // picked up by whoever updates that location, which selects the
    // destination and plays the burst there.
    public static void TriggerCritterz(Critterz r)
    {
        if (r.State == CritterzState.Sitting)
            r.State = CritterzState.Running;
    }

    private void UpdateCritterz(float deltaSeconds)
    {
        if (Game1.currentLocation is GameLocation location && GetNPCsAtCurrentLocation() is NetCollection<NPC> npc)
        {
            foreach (var r in npc.OfType<Critterz>())
            {
                UpdateCritterz(r, location, deltaSeconds);
            }
            npc.RemoveWhere(p => p is Critterz r && r.RemoveCritterzForPosition());
        }
    }

    private void UpdateCritterz(Critterz r, GameLocation location, float deltaSeconds)
    {
        switch (r.State)
        {
            case CritterzState.Sitting:
                // Hidden in the bush until its bush is disturbed.
                break;
            case CritterzState.Running:
                UpdateRunningCritterz(r, location, deltaSeconds);
                break;
        }
    }

    private void UpdateRunningCritterz(Critterz r, GameLocation location, float deltaSeconds)
    {
        if (r.Path == null)
        {
            if (SelectDestination(r, location) is (Vector2 bush, List<Vector2> path))
            {
                r.DestBush = bush;
                r.Path = path;
                BurstFromBush(r, location);
            }
            else
            {
                // Nowhere to run: duck back into hiding, silently.
                r.State = CritterzState.Sitting;
            }
            return;
        }
        if (r.Path.Count == 0)
        {
            ArriveCritterz(r, location);
            return;
        }

        Vector2 target = r.Path[0];
        Vector2 toTarget = target - r.Position;
        float stepLength = Constants.CritterzRunSpeed * deltaSeconds;
        if (toTarget.X != 0f)
            r.Flip = toTarget.X < 0f;
        if (toTarget.Length() <= stepLength)
        {
            r.Position = target;
            r.Path.RemoveAt(0);
            // Random zap on turns to keep the run interesting, not every hop.
            if (r.Path.Count > 0 && Game1.random.NextDouble() < Constants.CritterzTurnSoundChance)
                location.playSound(Constants.CritterzTurnAudioCueId, r.TilePosition);
            if (r.Path.Count == 0)
                ArriveCritterz(r, location);
        }
        else
        {
            r.Position += toTarget / toTarget.Length() * stepLength;
        }
    }

    private void BurstFromBush(Critterz r, GameLocation location)
    {
        if (BushHelper.GetAvailableBushes(location).FirstOrDefault(b => b.Tile == r.HomeBush) is Bush bush)
        {
            bush.shake(bush.Tile, true);
            BushHelper.TriggerBushLeaves(bush);
        }
    }

    private void ArriveCritterz(Critterz r, GameLocation location)
    {
        Vector2 dest = r.DestBush ?? r.HomeBush;
        // Rustle while still Running so the shake hook skips this Critterz.
        if (BushHelper.GetAvailableBushes(location).FirstOrDefault(b => b.Tile == dest) is Bush bush)
        {
            bush.shake(bush.Tile, true);
            BushHelper.TriggerBushLeaves(bush);
        }
        r.Path = null;
        r.HomeBush = dest;
        r.DestBush = null;
        r.State = CritterzState.Sitting;
    }

    // Pick a destination bush grouse-style (farther bushes preferred), with
    // a legal path to it.
    private (Vector2 bush, List<Vector2> path)? SelectDestination(Critterz r, GameLocation location)
    {
        HashSet<Vector2> reachable = GetReachableTiles(location);
        HashSet<Vector2> occupiedBushes = location.characters
            .OfType<Critterz>()
            .Where(other => other.CritterzId != r.CritterzId)
            .Select(other => other.HomeBush)
            .ToHashSet();
        List<Bush> candidates = BushHelper.GetAvailableBushes(location)
            .Where(b => b.Tile != r.HomeBush && HasReachableExit(b.Tile, reachable) && !occupiedBushes.Contains(b.Tile))
            .ToList();
        if (candidates.Count == 0)
            return null;

        Vector2 startTile = r.TilePosition;
        for (int attempt = 0; attempt < Constants.CritterzDestinationTries && candidates.Count > 0; attempt++)
        {
            Bush bush = PickWeightedBush(candidates, r.Position);
            candidates.Remove(bush);
            if (FindCritterzPath(startTile, bush.Tile, reachable) is List<Vector2> tilePath)
                return (bush.Tile, ExpandPathWithZigZag(r.Position, tilePath));
        }
        return null;
    }

    private Bush PickWeightedBush(List<Bush> candidates, Vector2 fromPosition)
    {
        var weighted = candidates
            .Select(bush =>
            {
                float distance = Vector2.Distance(fromPosition, bush.Tile * Game1.tileSize);
                return (bush, weight: distance * distance);
            })
            .OrderBy(_ => Game1.random.Next())
            .ToList();
        float totalWeight = weighted.Sum(t => t.weight);
        if (totalWeight == 0f)
            return candidates[0];
        float roll = (float)Game1.random.NextDouble() * totalWeight;
        float cumulative = 0f;
        foreach (var (bush, weight) in weighted)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return bush;
        }
        return candidates[0];
    }

    // BFS over reachable tiles; 4-directional so paths never clip corners.
    // Endpoint bush tiles are exempt (Critterz burst out of / dive into
    // bushes), but paths never route through other bushes.
    private List<Vector2>? FindCritterzPath(Vector2 startTile, Vector2 goalTile, HashSet<Vector2> reachable)
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
    private List<Vector2> ExpandPathWithZigZag(Vector2 startPixel, List<Vector2> tilePath)
    {
        List<Vector2> waypoints = new List<Vector2>();
        Vector2 current = startPixel;
        int side = Game1.random.NextBool() ? 1 : -1;
        foreach (Vector2 tile in tilePath)
        {
            Vector2 target = tile * Game1.tileSize + new Vector2(32f, 32f);
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
}
