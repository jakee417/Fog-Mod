#nullable enable
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley.TerrainFeatures;
using StardewValley;
using System;
using System.Collections.Generic;
using Netcode;
using System.Linq;
using FogMod.Models;
using FogMod.Utils;

namespace FogMod;

public partial class FogMod : Mod
{
    private void InitializeGrouse()
    {
        foreach (GameLocation loc in outdoorLocations)
        {
            loc.characters.RemoveWhere(p => p is NetGrouse);
        }
        SpawnGrouseInTrees(outdoorLocations);
    }

    public NetCollection<NPC>? GetNPCsAtCurrentLocation()
    {
        if (GetNPCsForLocation(Game1.currentLocation) is NetCollection<NPC> npc)
        {
            return npc;
        }
        return null;
    }

    private NetCollection<NPC>? GetNPCsForLocation(GameLocation? location)
    {
        if (location is GameLocation loc && loc.characters is NetCollection<NPC> npc)
        {
            return npc;
        }
        return null;
    }

    private List<NetGrouse> GetAllGrouse()
    {
        List<NetGrouse> allGrouse = new List<NetGrouse>();
        foreach (GameLocation loc in outdoorLocations)
        {
            allGrouse.AddRange(loc.characters.Where(c => c is NetGrouse).Cast<NetGrouse>());
        }
        return allGrouse;
    }

    internal NetGrouse? GetGrouseById(int grouseId)
    {
        return GetAllGrouse().FirstOrDefault(g => g.GrouseId == grouseId);
    }

    private void SpawnGrouseInTrees(IEnumerable<GameLocation> locations)
    {
        int numLocations = 0;
        foreach (GameLocation loc in locations)
        {
            if (loc.IsOutdoors && GetNPCsForLocation(loc) is NetCollection<NPC> npc)
            {
                SpawnGrouseForTrees(npc, TreeHelper.GetAvailableTreePositions(loc), loc);
                numLocations++;
            }
        }
    }

    private void SpawnGrouseForTrees(NetCollection<NPC> npc, List<Tree> availableTrees, GameLocation location)
    {
        int locationSeed = location.NameOrUniqueName.GetHashCode();
        int daySeed = (int)Game1.stats.DaysPlayed;
        var locationRng = new Random(locationSeed ^ daySeed);
        int grouseCount = npc.Count(c => c is NetGrouse);
        foreach (var tree in availableTrees)
        {
            if (grouseCount >= Constants.GrouseMaxPerLocation)
                break;

            if (locationRng.NextDouble() < Constants.GrouseSpawnChance)
            {
                Vector2 treePosition = TreeHelper.GetTreePosition(tree);
                Vector2 spawnPosition = TreeHelper.GetGrouseSpawnPosition(tree);
                _ = SpawnGrouse(
                    npc: npc,
                    treePosition: treePosition,
                    spawnPosition: spawnPosition,
                    location: location,
                    salt: null,
                    launchedByFarmer: false
                );
                grouseCount++;
            }
        }
    }

    private NetGrouse SpawnGrouse(NetCollection<NPC> npc, Vector2 treePosition, Vector2 spawnPosition, GameLocation location, int? salt, bool launchedByFarmer)
    {
        int grouseId = Utilities.GetDeterministicId(
            locationSeed: location.NameOrUniqueName.GetHashCode(),
            daySeed: (int)Game1.stats.DaysPlayed,
            position: treePosition,
            salt: salt
        );
        NetGrouse newGrouse = new NetGrouse(
            grouseId: grouseId,
            location: location,
            treePosition: treePosition,
            position: spawnPosition,
            launchedByFarmer: launchedByFarmer
        );
        npc.Add(newGrouse);
        return newGrouse;
    }

    private void UpdateGrouse(float deltaSeconds)
    {
        if (GetNPCsAtCurrentLocation() is NetCollection<NPC> npc)
        {
            foreach (var g in npc.OfType<NetGrouse>())
            {
                UpdateGrouse(g, deltaSeconds);
            }
        }
    }

    internal void UpdateGrouse(NetGrouse g, float deltaSeconds)
    {
        g.StateTimer += deltaSeconds;

        switch (g.State)
        {
            case GrouseState.Perched:
                break;

            case GrouseState.Surprised:
                UpdateGrouseSurprised(g, deltaSeconds);
                break;

            case GrouseState.Flushing:
                UpdateGrouseFlushing(g, deltaSeconds);
                break;

            case GrouseState.Flying:
                UpdateGrouseFlying(g, deltaSeconds);
                break;

            case GrouseState.Landing:
                UpdateGrouseLanding(g, deltaSeconds);
                break;
        }
        g.Position += g.Velocity * deltaSeconds;
        if (g.Velocity.X != 0f)
            g.FacingDirection = g.Velocity.X > 0f ? 1 : 3;
    }

    private void UpdateGrouseSurprised(NetGrouse g, float deltaSeconds)
    {
        g.Velocity = Vector2.Zero;

        if (g.StateTimer >= Constants.GrouseSurprisedDuration)
            g.State = GrouseState.Flushing;
    }

    private void UpdateGrouseFlushing(NetGrouse g, float deltaSeconds)
    {
        float flushProgress = g.StateTimer / Constants.GrouseFlushDuration;

        if (flushProgress >= 1f)
        {
            g.State = GrouseState.Flying;
            g.FlightTimer = 0f;
            Vector2 targetVelocity = g.GetExitDirection * Constants.GrouseExitSpeed;
            g.Velocity = Vector2.Lerp(g.Velocity, targetVelocity, 0.8f);
        }
        else
        {
            float currentSpeed = MathHelper.Lerp(Constants.GrouseFlushSpeed * 0.3f, Constants.GrouseFlushSpeed, flushProgress);
            float bobX = (float)Math.Sin(g.StateTimer * 15f) * 0.15f;
            float bobY = (float)Math.Sin(g.StateTimer * 12f) * Constants.GrouseBobAmplitude * 4;
            Vector2 baseVelocity = g.GetExitDirection * currentSpeed;
            g.Velocity = new Vector2(
                baseVelocity.X * (1f + bobX),
                baseVelocity.Y + bobY
            );
        }
    }

    private void UpdateGrouseFlying(NetGrouse g, float deltaSeconds)
    {
        g.FlightTimer += deltaSeconds;
        float bobY = (float)Math.Sin(g.StateTimer * 12f) * Constants.GrouseBobAmplitude;
        g.yVelocity += bobY;

        if (g.TargetTreePosition == null && SelectNewTree(g) is Tree targetTree)
        {
            Vector2 targetPosition = TreeHelper.GetGrouseSpawnPosition(targetTree);
            g.TargetTreePosition = TreeHelper.GetTreePosition(targetTree);
            g.Velocity = Utilities.ApplyMomentumThruTurn(
                targetPosition: targetPosition,
                targetSpeed: Constants.GrouseExitSpeed,
                currentPosition: g.Position,
                currentVelocity: g.Velocity,
                turnFactor: 1f * deltaSeconds
            );
        }
        else if (g.TargetTreePosition is Vector2 landTarget && TreeHelper.GetTreeFromId(Game1.currentLocation, landTarget) is Tree landingTree)
        {
            Vector2 targetPosition = TreeHelper.GetGrouseSpawnPosition(landingTree);
            g.Velocity = Utilities.ApplyMomentumThruTurn(
                targetPosition: targetPosition,
                targetSpeed: Constants.GrouseExitSpeed,
                currentPosition: g.Position,
                currentVelocity: g.Velocity,
                turnFactor: 2f * deltaSeconds
            );
            if (Vector2.Distance(g.Position, targetPosition) <= Constants.GrouseLandingDistanceThreshold)
                g.State = GrouseState.Landing;
        }
    }

    private void UpdateGrouseLanding(NetGrouse g, float deltaSeconds)
    {
        if (g.TargetTreePosition is Vector2 target && TreeHelper.GetTreeFromId(Game1.currentLocation, target) is Tree targetTree)
        {
            Vector2 targetPosition = TreeHelper.GetGrouseSpawnPosition(targetTree);
            float distanceToTarget = Vector2.Distance(g.Position, targetPosition);
            Vector2 direction = Vector2.Normalize(targetPosition - g.Position);
            float slowdownFactor = Math.Min(1f, distanceToTarget / 50f);
            float targetSpeed = Constants.GrouseFlushSpeed * slowdownFactor;
            g.Velocity = direction * Math.Max(targetSpeed, 20f);
            if (distanceToTarget < 32f)
            {
                g.TreePosition = TreeHelper.GetTreePosition(targetTree);
                g.Position = targetPosition;
                targetTree.shake(tileLocation: g.TreePosition, doEvenIfStillShaking: true);
                g.Reset();
            }
        }
        else
        {
            g.TargetTreePosition = null;
            g.State = GrouseState.Flying;
        }
    }

    private Tree? SelectNewTree(NetGrouse g)
    {
        Tree? currentTree = TreeHelper.GetTreeFromId(Game1.currentLocation, g.TreePosition);
        List<Tree> availableTrees = TreeHelper.GetAvailableTreePositions(Game1.currentLocation);
        if (availableTrees.Count == 0)
            return null;

        var occupiedTrees = GetAllGrouse()
            .Where(otherG => otherG.GrouseId != g.GrouseId)
            .Select(otherG => otherG.TreePosition)
            .ToHashSet();

        var candidateTrees = availableTrees
            .Where(tree => tree != currentTree && !occupiedTrees.Contains(TreeHelper.GetTreePosition(tree)))
            .ToList();

        if (candidateTrees.Count == 0)
            return null;

        Vector2 currentPos = g.TreePosition * Game1.tileSize;
        var weightedTrees = candidateTrees
            .Select(tree =>
            {
                Vector2 treePos = TreeHelper.GetTreePosition(tree) * Game1.tileSize;
                float distance = Vector2.Distance(currentPos, treePos);
                float weight = distance * distance;
                return (tree, weight);
            })
            .OrderBy(x => Game1.random.Next())
            .ToList();

        float totalWeight = weightedTrees.Sum(t => t.weight);
        if (totalWeight == 0)
            return candidateTrees[0];

        float randomValue = (float)Game1.random.NextDouble() * totalWeight;
        float cumulativeWeight = 0f;
        foreach (var (tree, weight) in weightedTrees)
        {
            cumulativeWeight += weight;
            if (randomValue <= cumulativeWeight)
                return tree;
        }

        return candidateTrees[0];
    }
}
