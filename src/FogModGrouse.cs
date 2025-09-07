#nullable enable
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley.TerrainFeatures;
using StardewValley;
using System;
using System.Collections.Generic;
using Netcode;
using StardewValley.Projectiles;
using System.Linq;

namespace FogMod;

public partial class FogMod : Mod
{
    private void InitializeGrouse()
    {
        foreach (GameLocation loc in outdoorLocations)
        {
            loc.projectiles.RemoveWhere(p => p is NetGrouse);
        }
        SpawnGrouseInTrees(outdoorLocations);
    }

    public NetCollection<Projectile>? GetProjectilesAtCurrentLocation()
    {
        if (GetProjectileForLocation(Game1.currentLocation) is NetCollection<Projectile> projectiles)
        {
            return projectiles;
        }
        return null;
    }

    private NetCollection<Projectile>? GetProjectileForLocation(GameLocation? location)
    {
        if (location is GameLocation loc && loc.projectiles is NetCollection<Projectile> grouse)
        {
            return grouse;
        }
        return null;
    }

    private List<NetGrouse> GetAllGrouse()
    {
        List<NetGrouse> allGrouse = new List<NetGrouse>();
        foreach (GameLocation loc in outdoorLocations)
        {
            allGrouse.AddRange(loc.projectiles.Where(p => p is NetGrouse).Cast<NetGrouse>());
        }
        return allGrouse;
    }

    private NetGrouse? GetGrouseById(int grouseId)
    {
        return GetAllGrouse().FirstOrDefault(g => g.GrouseId == grouseId);
    }

    private void SpawnGrouseInTrees(IEnumerable<GameLocation> locations)
    {
        int numLocations = 0;
        foreach (GameLocation loc in locations)
        {
            if (loc.IsOutdoors && GetProjectileForLocation(loc) is NetCollection<Projectile> projectiles)
            {
                SpawnGrouseForTrees(projectiles, TreeHelper.GetAvailableTreePositions(loc), loc.NameOrUniqueName);
                numLocations++;
            }
        }
    }

    private void SpawnGrouseForTrees(NetCollection<Projectile> projectiles, List<Tree> availableTrees, string locationName)
    {
        int locationSeed = locationName.GetHashCode();
        int daySeed = (int)Game1.stats.DaysPlayed;
        var locationRng = new Random(locationSeed ^ daySeed);
        int grouseCount = projectiles.Count(p => p is NetGrouse);
        foreach (var tree in availableTrees)
        {
            if (grouseCount >= Constants.GrouseMaxPerLocation)
                break;

            if (locationRng.NextDouble() < Constants.GrouseSpawnChance)
            {
                Vector2 treePosition = TreeHelper.GetTreePosition(tree);
                Vector2 spawnPosition = TreeHelper.GetGrouseSpawnPosition(tree);
                _ = SpawnGrouse(
                    projectiles: projectiles,
                    treePosition: treePosition,
                    spawnPosition: spawnPosition,
                    locationName: locationName,
                    salt: null,
                    launchedByFarmer: false
                );
                grouseCount++;
            }
        }
    }

    private NetGrouse SpawnGrouse(NetCollection<Projectile> projectiles, Vector2 treePosition, Vector2 spawnPosition, string locationName, int? salt, bool launchedByFarmer)
    {
        int grouseId = NetGrouse.GetDeterministicId(
            locationSeed: locationName.GetHashCode(),
            daySeed: (int)Game1.stats.DaysPlayed,
            treePosition: treePosition,
            salt: salt
        );
        NetGrouse newGrouse = new NetGrouse(
            grouseId: grouseId,
            locationName: locationName,
            treePosition: treePosition,
            position: spawnPosition,
            facingLeft: Utils.DeterministicBool(treePosition, grouseId),
            launchedByFarmer: launchedByFarmer
        );
        projectiles.Add(newGrouse);
        return newGrouse;
    }

    private void UpdateGrouse(float deltaSeconds)
    {
        if (GetProjectilesAtCurrentLocation() is NetCollection<Projectile> projectiles)
        {
            foreach (var g in projectiles.Where(p => p is NetGrouse).Cast<NetGrouse>())
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

                    case GrouseState.KnockedDown:
                        UpdateGrouseKnockedDown(g, deltaSeconds);
                        break;
                }
                g.Position += g.Velocity * deltaSeconds;
            }
        }
    }

    private void UpdateGrouseSurprised(NetGrouse g, float deltaSeconds)
    {
        g.Velocity = Vector2.Zero;

        if (g.StateTimer >= Constants.GrouseSurprisedDuration)
        {
            g.State = GrouseState.Flushing;
            if (TreeHelper.GetTreeFromId(Game1.currentLocation, g.TreePosition) is Tree tree)
                tree.shake(tileLocation: g.TreePosition, doEvenIfStillShaking: true);
        }
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
            float bobY = (float)Math.Sin(g.StateTimer * 12f) * Constants.GrouseBobAmplitude;
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
        float bobAmount = (float)Math.Sin(g.FlightTimer * 4f) * Constants.GrouseBobAmplitude;
        g.FlightHeight = bobAmount;

        if (g.TargetTreePosition == null && SelectNewTree(g) is Tree targetTree)
        {
            Vector2 targetPosition = TreeHelper.GetGrouseSpawnPosition(targetTree);
            g.TargetTreePosition = TreeHelper.GetTreePosition(targetTree);
            g.Velocity = Utils.ApplyMomentumThruTurn(
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
            g.Velocity = Utils.ApplyMomentumThruTurn(
                targetPosition: targetPosition,
                targetSpeed: Constants.GrouseExitSpeed,
                currentPosition: g.Position,
                currentVelocity: g.Velocity,
                turnFactor: 2f * deltaSeconds
            );
            float currentDistance = Vector2.Distance(g.Position, targetPosition);
            if (currentDistance <= Constants.GrouseLandingDistanceThreshold)
                g.State = GrouseState.Landing;
        }
    }

    private void UpdateGrouseLanding(NetGrouse g, float deltaSeconds)
    {
        float bobAmount = (float)Math.Sin(g.FlightTimer * 4f) * Constants.GrouseBobAmplitude;
        g.FlightHeight = bobAmount;

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

        Vector2 currentPos = g.TreePosition * 64f;
        var weightedTrees = candidateTrees
            .Select(tree =>
            {
                Vector2 treePos = TreeHelper.GetTreePosition(tree) * 64f;
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

    private void UpdateGrouseKnockedDown(NetGrouse g, float deltaSeconds)
    {
        g.StateTimer += deltaSeconds;
        if (g.DamageFlashTimer > 0f)
        {
            g.DamageFlashTimer -= deltaSeconds;
            if (g.DamageFlashTimer < 0f)
                g.DamageFlashTimer = 0f;
        }
        g.FallProgress += deltaSeconds * g.Velocity.Y;
        float fallProgress = g.FallProgress / Constants.GrouseFallDistance;
        if (fallProgress < 1.0f)
            g.Velocity = new Vector2(g.Velocity.X, g.Velocity.Y + 500f * deltaSeconds);
        else
        {
            if (!g.HasDroppedEgg)
            {
                DropEggAtLanding(g.Position, g.LocationName, g.GrouseId);
                g.HasDroppedEgg = true;
            }
            g.Velocity = Vector2.Zero;
            if (g.StateTimer > Constants.GrouseFallDistance / 150f)
            {
                float timeSinceLanding = g.StateTimer - (Constants.GrouseFallDistance / 150f);
                float fadeProgress = timeSinceLanding / Constants.GrouseFadeOutDuration;
                g.Alpha = Math.Max(0f, 1.0f - fadeProgress);
            }
        }
    }

    internal bool RemoveGrouse(Projectile p, GameLocation location)
    {
        if (p is NetGrouse g)
        {
            bool offLocation = (g.State == GrouseState.Flushing || g.State == GrouseState.Flying ||
                               (g.State == GrouseState.Landing && g.StateTimer > 30f)) && IsGrouseOffLocation(g, location);
            return offLocation || g.ReadyToBeRemoved;
        }
        return false;
    }

    private bool IsGrouseOffLocation(NetGrouse g, GameLocation location)
    {
        Rectangle locationBounds = new Rectangle(0, 0, location.Map.Layers[0].LayerWidth * 64, location.Map.Layers[0].LayerHeight * 64);
        return !locationBounds.Contains(new Point((int)g.Position.X, (int)g.Position.Y));
    }

    private void PlayGrouseNoise(NetGrouse g)
    {
        switch (g.State)
        {
            case GrouseState.Perched:
                if (g.IsTransitioning && g.NewAnimationFrame)
                    Game1.playSound("leafrustle");
                break;
            case GrouseState.Surprised:
                if (g.AnimationFrame == 4 && !g.HasPlayedFlushSound)
                {
                    Game1.playSound("crow");
                    g.HasPlayedFlushSound = true;
                }
                else if (!g.LaunchedByFarmer && g.NewAnimationFrame && g.AnimationFrame < 2)
                {
                    Game1.playSound("leafrustle");
                }
                break;
            case GrouseState.Flushing:
            case GrouseState.Flying:
            case GrouseState.Landing:
                if (g.AnimationFrame == 3 && g.NewAnimationFrame)
                    Game1.playSound("fishSlap");
                break;
            case GrouseState.KnockedDown:
                if (!g.HasPlayedKnockedDownSound)
                {
                    Game1.playSound("hitEnemy");
                    g.HasPlayedKnockedDownSound = true;
                }
                break;
        }
    }

    internal static void DropFeatherAtImpact(Vector2 impactPosition, string locationName, int grouseId)
    {
        var deterministicRng = new Random(grouseId);
        bool shouldDropFeather = deterministicRng.NextDouble() < Constants.GrouseFeatherDropChance;
        if (shouldDropFeather)
        {
            string featherItemId = "444";
            Utils.CreateItemDrop(impactPosition, locationName, featherItemId, 1);
        }
    }

    internal static void DropEggAtLanding(Vector2 landingPosition, string locationName, int grouseId)
    {
        var deterministicRng = new Random(grouseId);
        double roll = deterministicRng.NextDouble();
        // Basic brown egg
        string eggItemId = "180";
        // Golden egg
        if (roll < 0.01)
            eggItemId = "928";
        // Large Brown egg
        else if (roll >= 0.01 && roll < 0.06)
            eggItemId = "182";
        // Fried egg
        else if (roll >= 0.06 && roll < 0.1)
            eggItemId = "194";
        Utils.CreateItemDrop(landingPosition, locationName, eggItemId, 1);
    }
}
