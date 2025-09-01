#nullable enable
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley.TerrainFeatures;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FogMod
{
    public partial class FogMod : Mod
    {

        private void InitializeGrouse()
        {
            grouse.Clear();
            SpawnGrouseInTrees(outdoorLocations);
        }

        private void SpawnGrouseInTrees(IEnumerable<GameLocation> locations)
        {
            if (!Context.IsMainPlayer)
                return;

            int spawnedGrouse = 0;
            int numLocations = 0;
            foreach (GameLocation loc in locations)
            {
                if (loc.IsOutdoors)
                {
                    List<Tree> availableTrees = TreeHelper.GetAvailableTreePositions(loc);
                    spawnedGrouse += SpawnGrouseForTrees(availableTrees, loc.NameOrUniqueName);
                    numLocations++;
                }
            }
        }

        private int SpawnGrouseForTrees(List<Tree> availableTrees, string locationName)
        {
            int locationSeed = locationName.GetHashCode();
            int daySeed = (int)Game1.stats.DaysPlayed;
            var locationRng = new Random(locationSeed ^ daySeed);
            int grouseCount = grouse.Where(g => g.Location == locationName).Count();
            foreach (var tree in availableTrees)
            {
                if (grouseCount >= GrouseMaxPerLocation)
                    break;

                if (locationRng.NextDouble() < GrouseSpawnChance)
                {
                    Vector2 treePosition = TreeHelper.GetTreePosition(tree);
                    Vector2 spawnPosition = TreeHelper.GetGrouseSpawnPosition(tree);
                    SpawnGrouse(treePosition, spawnPosition, locationName, null);
                    grouseCount++;
                }
            }
            return grouseCount;
        }

        private void SpawnGrouse(Vector2 treePosition, Vector2 spawnPosition, string locationName, int? salt)
        {
            int grouseId = NetGrouse.GetDeterministicId(
                locationSeed: locationName.GetHashCode(),
                daySeed: (int)Game1.stats.DaysPlayed,
                treePosition: treePosition,
                salt: salt
            );
            if (grouse.Any(g => g.GrouseId == grouseId))
                return;
            NetGrouse newGrouse = new NetGrouse(
                grouseId: grouseId,
                locationName: locationName,
                treePosition: treePosition,
                spawnPosition: spawnPosition,
                facingLeft: DeterministicBool(spawnPosition, 1)
            );
            grouse.Add(newGrouse);
        }

        private void UpdateGrouse(float deltaSeconds)
        {
            foreach (var g in grouse)
            {
                g.StateTimer += deltaSeconds;

                switch (g.State)
                {
                    case GrouseState.Perched:
                        UpdateGrousePerched(g);
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

                    case GrouseState.KnockedDown:
                        UpdateGrouseKnockedDown(g, deltaSeconds);
                        break;
                }
                g.Position += g.Velocity * deltaSeconds;
                UpdateGrouseAnimation(g, deltaSeconds);
            }
            grouse.RemoveWhere(RemoveGrouse);
        }

        private void UpdateGrousePerched(NetGrouse g)
        {
            Vector2 playerPos = Game1.player.getStandingPosition();

            if (Game1.isWarping || Game1.fadeToBlack || Game1.globalFade)
                return;

            if (Vector2.Distance(g.Position, playerPos) < GrouseDetectionRadius)
            {
                g.State = GrouseState.Surprised;
                g.Velocity = Vector2.Zero;
            }
        }

        private void UpdateGrouseSurprised(NetGrouse g, float deltaSeconds)
        {
            g.Velocity = Vector2.Zero;

            if (g.StateTimer >= GrouseSurprisedDuration)
            {
                // Transition to flushing after being surprised
                g.State = GrouseState.Flushing;
                // The flying sprite is slightly smaller.
                g.Scale *= 1.2f;
            }
        }

        private void UpdateGrouseFlushing(NetGrouse g, float deltaSeconds)
        {
            float flushProgress = g.StateTimer / GrouseFlushDuration;

            if (flushProgress >= 1f)
            {
                g.State = GrouseState.Flying;
                g.FlightTimer = 0f;
                g.Velocity = g.GetExitDirection * GrouseExitSpeed;
            }
            else
            {
                float momentumProgress = flushProgress;
                float currentSpeed = MathHelper.Lerp(GrouseFlushSpeed * 0.3f, GrouseFlushSpeed, momentumProgress);
                float bobX = (float)Math.Sin(g.StateTimer * 15f) * 0.15f;
                float bobY = (float)Math.Sin(g.StateTimer * 12f) * GrouseBobAmplitude;
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
            float bobAmount = (float)Math.Sin(g.FlightTimer * 4f) * GrouseBobAmplitude;
            g.FlightHeight = bobAmount;
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
            float fallProgress = (g.Position.Y - g.OriginalY) / GrouseFallDistance;
            if (fallProgress < 1.0f)
                g.Velocity = new Vector2(g.Velocity.X, g.Velocity.Y + 500f * deltaSeconds);
            else
            {
                // Landed - stop falling and drop egg (only once)
                Vector2 landingPosition = new Vector2(g.Position.X, g.OriginalY + GrouseFallDistance);
                if (!g.HasDroppedEgg)
                {
                    DropEggAtLanding(landingPosition, g.GrouseId);
                    g.HasDroppedEgg = true;
                }
                g.Velocity = Vector2.Zero;
                g.Position = landingPosition;
                // Start fading
                if (g.StateTimer > GrouseFallDistance / 150f)
                {
                    float timeSinceLanding = g.StateTimer - (GrouseFallDistance / 150f);
                    float fadeProgress = timeSinceLanding / GrouseFadeOutDuration;
                    g.Alpha = Math.Max(0f, 1.0f - fadeProgress);
                }
            }
        }

        private void UpdateGrouseAnimation(NetGrouse g, float deltaSeconds)
        {
            g.AnimationTimer += deltaSeconds;
            float animationSpeed = g.ComputeAnimationSpeed();
            if (animationSpeed > 0f && g.AnimationTimer >= 1f / animationSpeed)
            {
                g.AnimationTimer = 0f;
                switch (g.State)
                {
                    case GrouseState.Perched:
                        // Cycle through top sitting: sitting left (0) → sitting left (1)
                        g.AnimationFrame = (g.AnimationFrame + 1) % 2;
                        break;
                    case GrouseState.Surprised:
                        // Cycle through top row once: 0→1→2→3→4, then stay at 4
                        if (g.HasBeenSpotted && g.AnimationFrame < 4)
                        {
                            g.AnimationFrame++;
                            PlaySurpriseSound(g);
                        }
                        break;
                    case GrouseState.Flushing:
                    case GrouseState.Flying:
                        // Smooth wing cycle: 0→1→2→3→2→1→0→1→2→3...
                        g.AnimationFrame = (g.AnimationFrame + 1) % NetGrouse.wingPattern.Length;
                        PlayWingBeatSound(g);
                        break;
                    case GrouseState.KnockedDown:
                        g.AnimationFrame = 2;
                        break;
                }
            }
        }

        private bool RemoveGrouse(NetGrouse g)
        {
            bool offscreen = (g.State == GrouseState.Flushing || g.State == GrouseState.Flying) && IsGrouseOffScreen(g);
            return offscreen || g.ReadyToBeRemoved;
        }

        private void KnockDownGrouse(NetGrouse g)
        {
            g.State = GrouseState.KnockedDown;
            g.AnimationFrame = 2;
            g.Velocity = new Vector2(g.Velocity.X * 0.8f, Math.Max(g.Velocity.Y + 100f, 150f));
            g.FlightHeight = 0f;
            g.Alpha = 1.0f;
            Vector2 impactPosition = g.Position;
            g.OriginalY = impactPosition.Y;
            Vector2 screenPosition = Game1.GlobalToLocal(Game1.viewport, g.Position);
            screenPosition.Y -= GrouseSpriteHeight * g.Scale / 2f;
            g.DamageFlashTimer = GrouseDamageFlashDuration;
            g.Smoke = new CollisionSmoke(position: screenPosition);
            DropFeatherAtImpact(impactPosition, g.GrouseId);
            PlayGrouseKnockdownSound(g);
        }

        private bool IsGrouseOffScreen(NetGrouse g)
        {
            return !grid.GetExtendedBounds().Contains(new Point((int)g.Position.X, (int)g.Position.Y));
        }

        private void PlaySurpriseSound(NetGrouse g)
        {
            if (!g.HasPlayedFlushSound && g.AnimationFrame == 4)
            {
                Game1.playSound("crow");
                g.HasPlayedFlushSound = true;
            }
        }

        private void PlayWingBeatSound(NetGrouse g)
        {
            if (g.AnimationFrame == 3)
                Game1.playSound("fishSlap");
        }

        private void PlayGrouseKnockdownSound(NetGrouse g)
        {
            if (g.State == GrouseState.KnockedDown)
                Game1.playSound("hitEnemy");
        }

        private void DropFeatherAtImpact(Vector2 impactPosition, int grouseId)
        {
            var deterministicRng = new Random(grouseId);
            bool shouldDropFeather = deterministicRng.NextDouble() < GrouseFeatherDropChance;
            if (shouldDropFeather)
            {
                string featherItemId = "444";
                var itemDropInfo = new ItemDropInfo(
                    locationName: Game1.currentLocation?.NameOrUniqueName,
                    position: impactPosition,
                    itemId: featherItemId,
                    quantity: 1,
                    timestamp: Game1.currentGameTime?.TotalGameTime.Ticks ?? 0
                );
                SendItemDropMessage(itemDropInfo);
                // Only create the item drop on the main player to avoid duplicates
                if (Context.IsMainPlayer)
                    CreateItemDrop(impactPosition, featherItemId, 1);
            }
        }

        private void DropEggAtLanding(Vector2 landingPosition, int grouseId)
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
            var itemDropInfo = new ItemDropInfo(
                locationName: Game1.currentLocation?.NameOrUniqueName,
                position: landingPosition,
                itemId: eggItemId,
                quantity: 1,
                timestamp: Game1.currentGameTime?.TotalGameTime.Ticks ?? 0
            );
            SendItemDropMessage(itemDropInfo);
            // Only create the item drop on the main player to avoid duplicates
            if (Context.IsMainPlayer)
                CreateItemDrop(landingPosition, eggItemId, 1);
        }
    }
}
