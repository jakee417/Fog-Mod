using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        private void ResetGrouse()
        {
            grouse.Clear();
            spawnedTreePositions.Clear();
            nextGrouseId = 1; // Reset ID counter
            lastPlayerLocation = ""; // Reset location tracking
        }

        // Multiplayer synchronization methods
        private void HandleGrouseFlushFromMessage(GrouseFlushInfo flushInfo)
        {
            // Find grouse at the specified tree position and force flush it
            for (int i = 0; i < grouse.Count; i++)
            {
                var g = grouse[i];
                if (Vector2.Distance(g.TreePosition, flushInfo.TreePosition) < 32f && g.State == GrouseState.Perched)
                {
                    // Force surprise this grouse to synchronize with other players
                    g.State = GrouseState.Surprised;
                    g.StateTimer = 0f;
                    g.Velocity = Vector2.Zero;
                    g.HasPlayedFlushSound = false;
                    g.HasBeenSpotted = false;
                    g.FacingLeft = DeterministicBool(g.TreePosition, 1);
                    grouse[i] = g;
                    break;
                }
            }
        }

        private void SendGrouseFlushMessage(Vector2 treePosition)
        {
            if (!Context.IsMainPlayer) return;

            var flushInfo = new GrouseFlushInfo
            {
                LocationName = Game1.currentLocation?.NameOrUniqueName,
                TreePosition = treePosition,
                Timestamp = Game1.currentGameTime.TotalGameTime.Ticks
            };

            Helper.Multiplayer.SendMessage(flushInfo, GrouseFlushMessageType);
        }

        // Generate deterministic random values based on position and seed
        private bool DeterministicBool(Vector2 position, int variant)
        {
            int seed = (int)(position.X * 1000 + position.Y * 1000 + variant);
            var rng = new Random(seed);
            return rng.NextDouble() < 0.5;
        }

        private void SpawnGrouseInTrees()
        {
            // Only the host should decide when to spawn grouse
            if (!Context.IsMainPlayer)
                return;

            if (Game1.currentLocation == null || grouse.Count >= GrouseMaxPerLocation)
                return;

            // Check if location changed to warrant new spawns
            string currentLocationName = Game1.currentLocation.NameOrUniqueName ?? "Unknown";
            bool locationChanged = currentLocationName != lastPlayerLocation;

            // Only spawn if location changed
            if (!locationChanged && lastPlayerLocation != "")
                return;

            // Update tracking variables
            lastPlayerLocation = currentLocationName;

            // Get all tree positions that don't already have grouse
            var availableTrees = TreeHelper.GetAvailableTreePositions(Game1.currentLocation, spawnedTreePositions);

            // Use deterministic spawning based on location and day
            string locationSeed = Game1.currentLocation.NameOrUniqueName ?? "Unknown";
            int daySeed = (int)Game1.stats.DaysPlayed;
            var locationRng = new Random(locationSeed.GetHashCode() ^ daySeed);

            // Randomly spawn grouse in some trees using deterministic random
            foreach (var treePos in availableTrees)
            {
                if (grouse.Count >= GrouseMaxPerLocation)
                    break;

                if (locationRng.NextDouble() < GrouseSpawnChance)
                    SpawnGrouseAtTree(treePos);
            }
        }

        private void SpawnGrouseAtTree(Vector2 treePosition)
        {
            // treePosition is calculated by TreeHelper using leaf positions or fallback logic
            var newGrouse = new Grouse
            {
                GrouseId = nextGrouseId++,
                Position = treePosition,
                TreePosition = treePosition,
                Velocity = Vector2.Zero,
                State = GrouseState.Perched,
                StateTimer = 0f,
                Scale = GrouseScale,
                Rotation = 0f,
                FlightHeight = 0f,
                FacingLeft = DeterministicBool(treePosition, 1),
                FlightTimer = 0f,
                HasPlayedFlushSound = false,
                HasBeenSpotted = false,
                AnimationFrame = 0,
                AnimationTimer = 0f
            };

            grouse.Add(newGrouse);
            spawnedTreePositions.Add(treePosition);
        }

        private void UpdateGrouse(float deltaSeconds)
        {
            Vector2 playerPos = Game1.player.getStandingPosition();

            for (int i = grouse.Count - 1; i >= 0; i--)
            {
                var g = grouse[i];
                g.StateTimer += deltaSeconds;

                switch (g.State)
                {
                    case GrouseState.Perched:
                        UpdateGrousePerched(g, playerPos, deltaSeconds);
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
                }

                // Update position
                g.Position += g.Velocity * deltaSeconds;

                // Update animation
                UpdateGrouseAnimation(ref g, deltaSeconds);

                // Remove if off screen and flying (since flying now means leaving)
                if (g.State == GrouseState.Flying && IsGrouseOffScreen(g))
                {
                    spawnedTreePositions.Remove(g.TreePosition);
                    grouse.RemoveAt(i);
                }
                else
                {
                    grouse[i] = g;
                }
            }
        }

        private void UpdateGrousePerched(Grouse g, Vector2 playerPos, float deltaSeconds)
        {
            if (Vector2.Distance(g.Position, playerPos) < GrouseDetectionRadius)
            {
                g.State = GrouseState.Surprised;
                g.StateTimer = 0f;
                g.Velocity = Vector2.Zero;
                SendGrouseFlushMessage(g.TreePosition);
            }
        }

        private void UpdateGrouseSurprised(Grouse g, float deltaSeconds)
        {
            g.Velocity = Vector2.Zero;

            if (g.StateTimer >= GrouseSurprisedDuration)
            {
                // Transition to flushing after being surprised
                g.State = GrouseState.Flushing;
                g.StateTimer = 0f;
                // The flying sprite is slightly smaller.
                g.Scale *= 1.2f;
            }
        }

        private void UpdateGrouseFlushing(Grouse g, float deltaSeconds)
        {
            float flushProgress = g.StateTimer / GrouseFlushDuration;

            if (flushProgress >= 1f)
            {
                g.State = GrouseState.Flying;
                g.StateTimer = 0f;
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

        private void UpdateGrouseFlying(Grouse g, float deltaSeconds)
        {
            g.FlightTimer += deltaSeconds;
            float bobAmount = (float)Math.Sin(g.FlightTimer * 4f) * GrouseBobAmplitude;
            g.FlightHeight = bobAmount;
        }

        private void UpdateGrouseAnimation(ref Grouse g, float deltaSeconds)
        {
            g.AnimationTimer += deltaSeconds;
            float animationSpeed = g.State switch
            {
                GrouseState.Perched => 0f,
                GrouseState.Surprised => 3f,
                GrouseState.Flushing => 36f,
                GrouseState.Flying => 12f,
                _ => 1f
            };

            if (animationSpeed > 0f && g.AnimationTimer >= 1f / animationSpeed)
            {
                g.AnimationTimer = 0f;
                if (g.State == GrouseState.Surprised)
                {
                    // Cycle through top row once: 0→1→2→3, then stay at 3
                    if (g.HasBeenSpotted && g.AnimationFrame < 3)
                    {
                        g.AnimationFrame++;
                        PlaySurpriseSound(g);
                    }
                }
                else if (g.State == GrouseState.Flushing || g.State == GrouseState.Flying)
                {
                    // Smooth wing cycle: 0→1→2→3→2→1→0→1→2→3...
                    g.AnimationFrame = (g.AnimationFrame + 1) % FogMod.wingPattern.Length;
                    PlayWingBeatSound(g);
                }
            }
        }

        private bool IsGrouseOffScreen(Grouse g)
        {
            return !grid.GetExtendedBounds().Contains(new Point((int)g.Position.X, (int)g.Position.Y));
        }

        private void PlaySurpriseSound(Grouse g)
        {
            if (!g.HasPlayedFlushSound && g.AnimationFrame == 3)
            {
                Game1.playSound("crow");
                g.HasPlayedFlushSound = true;
            }
        }

        private void PlayWingBeatSound(Grouse g)
        {
            if (g.AnimationFrame == 2)
                Game1.playSound("fishSlap");
        }
    }
}
