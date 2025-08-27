using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
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
                    // Force flush this grouse to synchronize with other players
                    g.State = GrouseState.Flushing;
                    g.StateTimer = 0f;
                    g.Velocity = Vector2.Zero;
                    g.HasPlayedFlushSound = true; // Don't play sound again since host already sent message
                    g.LastFlappingSoundTime = 0f;

                    // Face the direction it will exit
                    Vector2 exitDirection = GetDeterministicExitDirection(g.TreePosition);
                    g.FacingLeft = exitDirection.X < 0;

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

        private float DeterministicFloat(Vector2 position, int variant, float min = 0f, float max = 1f)
        {
            int seed = (int)(position.X * 1000 + position.Y * 1000 + variant);
            var rng = new Random(seed);
            return min + (float)rng.NextDouble() * (max - min);
        }

        private Vector2 GetDeterministicExitDirection(Vector2 treePosition)
        {
            // Use deterministic random to pick left or right (only directions that make sense visually)
            // This ensures all players see the grouse exit in the same direction
            int seed = (int)(treePosition.X * 1000 + treePosition.Y * 1000 + 999); // +999 for exit direction variant
            var rng = new Random(seed);
            bool goLeft = rng.NextDouble() < 0.5;

            return goLeft ? new Vector2(-1, 0) : new Vector2(1, 0);
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
            var availableTrees = GetAvailableTreePositions();

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
                {
                    SpawnGrouseAtTree(treePos);
                }
            }
        }

        private List<Vector2> GetAvailableTreePositions()
        {
            var availableTrees = new List<Vector2>();

            if (Game1.currentLocation?.terrainFeatures == null)
                return availableTrees;

            foreach (var pair in Game1.currentLocation.terrainFeatures.Pairs)
            {
                if (pair.Value is Tree tree && tree.growthStage.Value >= Tree.treeStage)
                {
                    Vector2 treePos = pair.Key * 64f; // Convert tile to pixel coordinates
                    if (!spawnedTreePositions.Contains(treePos))
                    {
                        availableTrees.Add(treePos);
                    }
                }
            }

            return availableTrees;
        }

        private void SpawnGrouseAtTree(Vector2 treePosition)
        {
            var newGrouse = new Grouse
            {
                GrouseId = nextGrouseId++,
                Position = treePosition + new Vector2(32f, -32f), // Offset to sit in tree canopy
                TreePosition = treePosition,
                Velocity = Vector2.Zero,
                State = GrouseState.Perched,
                StateTimer = 0f,
                Scale = GrouseScale,
                Rotation = 0f,
                FlightHeight = 0f,
                FacingLeft = DeterministicBool(treePosition, 1), // Deterministic facing direction
                FlightTimer = 0f,
                TotalFlightTime = GrouseFlyingDuration + DeterministicFloat(treePosition, 2, -2f, 2f), // Deterministic variation
                HasPlayedFlushSound = false,
                LastFlappingSoundTime = 0f,
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
            float distanceToPlayer = Vector2.Distance(g.Position, playerPos);

            if (distanceToPlayer < GrouseDetectionRadius)
            {
                // Flush the grouse!
                g.State = GrouseState.Flushing;
                g.StateTimer = 0f;

                // Start with minimal velocity - the bird will build momentum during flush
                g.Velocity = Vector2.Zero;
                
                // Face the direction it will exit
                Vector2 exitDirection = GetDeterministicExitDirection(g.TreePosition);
                g.FacingLeft = exitDirection.X < 0;

                // Play the initial flush sound
                if (!g.HasPlayedFlushSound)
                {
                    Game1.playSound("crow");  // Using crow sound as it's similar to a bird cry
                    g.HasPlayedFlushSound = true;
                    g.LastFlappingSoundTime = 0f;

                    // Send multiplayer message to synchronize flush across all players
                    SendGrouseFlushMessage(g.TreePosition);
                }
            }
        }

        private void UpdateGrouseFlushing(Grouse g, float deltaSeconds)
        {
            float flushProgress = g.StateTimer / GrouseFlushDuration;

            // Play flapping sounds during the flush
            g.LastFlappingSoundTime += deltaSeconds;
            if (g.LastFlappingSoundTime >= GrouseFlappingSoundInterval)
            {
                // Use different sounds based on flush intensity
                if (flushProgress < 0.8f)
                {
                    // Heavy flapping phase - use a wing flap sound
                    Game1.playSound("batScreech"); // This gives a good wing-beating effect
                }
                else
                {
                    // Lighter sounds as it prepares to fly away
                    Game1.playSound("fishSlap"); // Quick whoosh sound
                }
                g.LastFlappingSoundTime = 0f;
            }

            if (g.StateTimer >= GrouseFlushDuration)
            {
                g.State = GrouseState.Flying;
                g.StateTimer = 0f;
                g.FlightTimer = 0f;

                // Transition to full flight speed in the same direction
                // Exit direction was already established at start of flush
                Vector2 exitDirection = GetDeterministicExitDirection(g.TreePosition);
                g.Velocity = exitDirection * GrouseExitSpeed;
                g.FacingLeft = g.Velocity.X < 0;
            }
            else
            {
                // Determine flight direction at start of flush (for realistic behavior)
                Vector2 exitDirection = GetDeterministicExitDirection(g.TreePosition);
                g.FacingLeft = exitDirection.X < 0;

                // Build momentum in the exit direction over the flush duration
                float momentumProgress = flushProgress; // Linear build-up
                float currentSpeed = MathHelper.Lerp(GrouseFlushSpeed * 0.3f, GrouseFlushSpeed, momentumProgress);

                // Wing flapping gets more intense as flush progresses
                float flapIntensity = MathHelper.Lerp(20f, 35f, flushProgress);

                // Move in exit direction with increasing speed + wing flapping variation
                float flappingVariation = (float)Math.Sin(g.StateTimer * 15f) * 0.15f; // Slight variation
                Vector2 baseVelocity = exitDirection * currentSpeed;

                // Add some vertical flapping motion for realism
                float verticalFlapping = (float)Math.Sin(g.StateTimer * 12f) * 10f;

                g.Velocity = new Vector2(
                    baseVelocity.X * (1f + flappingVariation),
                    baseVelocity.Y + verticalFlapping
                );
            }
        }

        private void UpdateGrouseFlying(Grouse g, float deltaSeconds)
        {
            g.FlightTimer += deltaSeconds;

            // Simple bobbing motion while flying off screen - this affects visual height only
            float bobAmount = (float)Math.Sin(g.FlightTimer * 4f) * 10f;
            g.FlightHeight = bobAmount;

            // Maintain consistent flight trajectory - no velocity changes
            // The velocity was set when entering Flying state and doesn't change
            // This creates a straight-line flight path to exit the screen

            g.FacingLeft = g.Velocity.X < 0;
        }

        private void UpdateGrouseAnimation(ref Grouse g, float deltaSeconds)
        {
            g.AnimationTimer += deltaSeconds;

            // Different animation speeds based on state
            float animationSpeed = g.State switch
            {
                GrouseState.Perched => 0.5f,    // Slow idle animation
                GrouseState.Flushing => 8f,     // Fast flapping during flush
                GrouseState.Flying => 6f,       // Medium speed for flying
                _ => 1f
            };

            if (g.AnimationTimer >= 1f / animationSpeed)
            {
                g.AnimationTimer = 0f;

                // Choose animation frames based on state
                int maxFrame = g.State switch
                {
                    GrouseState.Perched => 1,        // Use frames 0-1 for idle
                    GrouseState.Flushing => 6,       // Use frames 0-6 for dramatic flapping
                    GrouseState.Flying => 3,         // Use frames 0-3 for flying
                    _ => 0
                };

                g.AnimationFrame = (g.AnimationFrame + 1) % (maxFrame + 1);
            }
        }

        private bool IsGrouseOffScreen(Grouse g)
        {
            Rectangle viewport = Game1.graphics.GraphicsDevice.Viewport.Bounds;
            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, g.Position);

            return screenPos.X < -100 || screenPos.X > viewport.Width + 100 ||
                   screenPos.Y < -100 || screenPos.Y > viewport.Height + 100;
        }

        private void DrawGrouse(SpriteBatch spriteBatch)
        {
            if (grouse.Count == 0)
                return;

            foreach (var g in grouse)
            {
                // Only draw if not perched (perched grouse are hidden in trees)
                if (g.State == GrouseState.Perched)
                    continue;

                DrawSingleGrouse(spriteBatch, g);
            }
        }
    }
}
