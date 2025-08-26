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

                    // Use deterministic facing direction based on tree position
                    g.FacingLeft = DeterministicBool(g.TreePosition, 0);

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
                LastFlappingSoundTime = 0f
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

                    case GrouseState.Exiting:
                        UpdateGrouseExiting(g, deltaSeconds);
                        break;
                }

                // Update position
                g.Position += g.Velocity * deltaSeconds;

                // Remove if off screen and exiting
                if (g.State == GrouseState.Exiting && IsGrouseOffScreen(g))
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
                g.FacingLeft = DeterministicBool(g.TreePosition, 3); // Use deterministic facing for consistency

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

                // Don't immediately overwrite velocity - let it transition smoothly from flush
                // Only adjust if the velocity is too low or in a bad direction
                float currentSpeed = g.Velocity.Length();
                if (currentSpeed < GrouseFlyingSpeed * 0.7f)
                {
                    // If velocity is too low, give it a boost in a random direction
                    float angle = (float)(random.NextDouble() * MathHelper.TwoPi);
                    g.Velocity = new Vector2(
                        (float)Math.Cos(angle) * GrouseFlyingSpeed,
                        (float)Math.Sin(angle) * GrouseFlyingSpeed * 0.3f
                    );
                }
                else if (currentSpeed > GrouseFlyingSpeed * 1.5f)
                {
                    // If going too fast, scale it down but maintain direction
                    g.Velocity = Vector2.Normalize(g.Velocity) * (GrouseFlyingSpeed * 1.2f);
                }
                // If speed is in good range, keep the existing velocity (maintain flush momentum)
                g.FacingLeft = g.Velocity.X < 0;
            }
            else
            {
                // Heavy wing flapping phase with momentum building
                // Early flush: heavy flapping, little movement (bird building energy)
                // Late flush: rapid acceleration away from tree

                // Wing flapping gets more intense as flush progresses
                float flapIntensity = MathHelper.Lerp(20f, 35f, flushProgress);
                g.Rotation = (float)Math.Sin(g.StateTimer * flapIntensity) * 0.4f;

                // Velocity builds up more gradually over the 3 seconds
                float momentumCurve = flushProgress * flushProgress; // Quadratic curve for more gradual acceleration

                if (flushProgress < 0.8f)
                {
                    // Extended heavy flapping phase (2.4 seconds of 3 seconds)
                    float verticalIntensity = (float)Math.Sin(g.StateTimer * 12f) * 25f * (1f - flushProgress * 0.5f);
                    // Ensure minimum horizontal movement even in early phase
                    float minHorizontalSpeed = 15f;
                    float horizontalSpeed = minHorizontalSpeed + (25f * momentumCurve);
                    g.Velocity = new Vector2(
                        (g.FacingLeft ? -1f : 1f) * horizontalSpeed,
                        -25f - verticalIntensity
                    );
                }
                else
                {
                    // Final phase: choose direction and accelerate away (0.6 seconds)
                    if (flushProgress >= 0.8f && flushProgress < 0.85f)
                    {
                        // Pick deterministic direction for exit based on tree position
                        float randomDirection = DeterministicFloat(g.TreePosition, 4, 0f, MathHelper.TwoPi);
                        g.Velocity = new Vector2(
                            (float)Math.Cos(randomDirection) * GrouseFlushSpeed * 0.8f,
                            (float)Math.Sin(randomDirection) * GrouseFlushSpeed * 0.5f - 15f
                        );
                        g.FacingLeft = g.Velocity.X < 0;
                    }
                    else
                    {
                        // Accelerate in chosen direction
                        float exitMultiplier = MathHelper.Lerp(0.8f, 1.5f, (flushProgress - 0.85f) / 0.15f);
                        g.Velocity *= exitMultiplier;
                    }
                }
            }
        }

        private void UpdateGrouseFlying(Grouse g, float deltaSeconds)
        {
            g.FlightTimer += deltaSeconds;

            // Flying pattern - slight bobbing and direction changes
            float bobAmount = (float)Math.Sin(g.FlightTimer * 3f) * 15f;
            g.FlightHeight = bobAmount;

            // Reduce frequency of direction changes and make them more gradual
            // Use deterministic timing based on grouse ID and flight time
            float changeThreshold = 0.005f;
            int timeSeed = (int)(g.FlightTimer * 100) + g.GrouseId * 1000;
            var flightRng = new Random(timeSeed);
            if (flightRng.NextDouble() < changeThreshold)
            {
                float newAngle = DeterministicFloat(g.TreePosition, (int)(g.FlightTimer * 10), 0f, MathHelper.TwoPi);
                Vector2 newVelocity = new Vector2(
                    (float)Math.Cos(newAngle) * GrouseFlyingSpeed,
                    (float)Math.Sin(newAngle) * GrouseFlyingSpeed * 0.3f
                );

                // Smoothly interpolate to new direction instead of instant change
                g.Velocity = Vector2.Lerp(g.Velocity, newVelocity, 0.1f);
                g.FacingLeft = g.Velocity.X < 0;
            }

            // Wing flapping animation
            g.Rotation = (float)Math.Sin(g.FlightTimer * 8f) * 0.15f;

            // Check if it's time to exit
            if (g.FlightTimer >= g.TotalFlightTime)
            {
                g.State = GrouseState.Exiting;
                g.StateTimer = 0f;

                // Head toward edge of screen
                Rectangle viewport = Game1.graphics.GraphicsDevice.Viewport.Bounds;
                Vector2 screenCenter = new Vector2(viewport.Width / 2f, viewport.Height / 2f);
                Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, g.Position);

                // Choose closest edge to exit toward
                Vector2 exitDirection;
                if (Math.Abs(screenPos.X - screenCenter.X) > Math.Abs(screenPos.Y - screenCenter.Y))
                {
                    // Exit left or right
                    exitDirection = screenPos.X < screenCenter.X ? new Vector2(-1, 0) : new Vector2(1, 0);
                }
                else
                {
                    // Exit top or bottom
                    exitDirection = screenPos.Y < screenCenter.Y ? new Vector2(0, -1) : new Vector2(0, 1);
                }

                g.Velocity = exitDirection * GrouseExitSpeed;
                g.FacingLeft = g.Velocity.X < 0;
            }
        }

        private void UpdateGrouseExiting(Grouse g, float deltaSeconds)
        {
            // Maintain faster wing flapping during exit for more dynamic appearance
            g.Rotation = (float)Math.Sin(g.StateTimer * 12f) * 0.2f;

            // Ensure the grouse maintains good exit speed
            float currentSpeed = g.Velocity.Length();
            if (currentSpeed < GrouseExitSpeed * 0.8f)
            {
                // Boost speed if it's getting too slow
                g.Velocity = Vector2.Normalize(g.Velocity) * GrouseExitSpeed;
            }

            // Optional: Add slight acceleration as it exits for more dramatic effect
            g.Velocity *= 1.01f; // Gradually accelerate by 1% per frame
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

        private void DrawSingleGrouse(SpriteBatch spriteBatch, Grouse g)
        {
            // Use a simple colored rectangle as the grouse sprite
            // In a real implementation, you'd want to use an actual grouse texture
            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, g.Position);
            screenPos.Y += g.FlightHeight; // Apply flight bobbing

            // Draw grouse body (brown rectangle) - made larger and more visible
            Rectangle grouseRect = new Rectangle(
                (int)screenPos.X - 12,
                (int)screenPos.Y - 8,
                24,
                16
            );

            Color grouseColor = Color.SaddleBrown;
            if (g.State == GrouseState.Flushing)
            {
                // More dramatic flashing during flush with wing flapping intensity
                float flushProgress = g.StateTimer / GrouseFlushDuration;
                float flapIntensity = MathHelper.Lerp(25f, 40f, flushProgress);
                float flashIntensity = (float)Math.Sin(g.StateTimer * flapIntensity) * 0.5f + 0.5f;
                grouseColor = Color.Lerp(Color.SaddleBrown, Color.Yellow, flashIntensity * 0.4f);

                // Add some panic coloring
                grouseColor = Color.Lerp(grouseColor, Color.Red, flushProgress * 0.3f);
            }

            // Draw body
            if (whitePixel != null)
            {
                spriteBatch.Draw(
                    whitePixel,
                    grouseRect,
                    null,
                    grouseColor,
                    g.Rotation,
                    Vector2.Zero,
                    g.FacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                    0.85f // Layer depth - above most things but below UI
                );
            }

            // Draw wing detail (lighter brown, larger rectangle)
            Rectangle wingRect = new Rectangle(
                grouseRect.X + (g.FacingLeft ? 3 : 9),
                grouseRect.Y + 2,
                12,
                6
            );

            // During flushing, make wings more prominent and animated
            if (g.State == GrouseState.Flushing)
            {
                float flushProgress = g.StateTimer / GrouseFlushDuration;
                float wingExpansion = (float)Math.Sin(g.StateTimer * MathHelper.Lerp(25f, 40f, flushProgress)) * 4f + 4f;
                wingRect.Width = (int)(12 + wingExpansion);
                wingRect.Height = (int)(6 + wingExpansion / 2f);
                wingRect.X = grouseRect.X + (g.FacingLeft ? (int)(3 - wingExpansion / 2f) : (int)(9 - wingExpansion / 2f));
                wingRect.Y = grouseRect.Y + (int)(2 - wingExpansion / 4f);
            }
            else if (g.State == GrouseState.Exiting)
            {
                // During exiting, also show dynamic wing beating for speed impression
                float wingExpansion = (float)Math.Sin(g.StateTimer * 12f) * 3f + 3f;
                wingRect.Width = (int)(12 + wingExpansion);
                wingRect.Height = (int)(6 + wingExpansion / 2f);
                wingRect.X = grouseRect.X + (g.FacingLeft ? (int)(3 - wingExpansion / 2f) : (int)(9 - wingExpansion / 2f));
                wingRect.Y = grouseRect.Y + (int)(2 - wingExpansion / 4f);
            }

            Color wingColor = Color.Lerp(grouseColor, Color.White, 0.3f);
            if (whitePixel != null)
            {
                spriteBatch.Draw(
                    whitePixel,
                    wingRect,
                    null,
                    wingColor,
                    g.Rotation,
                    Vector2.Zero,
                    g.FacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                    0.851f // Slightly above body
                );
            }

            // Add a bright outline to make it more visible
            Rectangle outlineRect = new Rectangle(
                grouseRect.X - 1,
                grouseRect.Y - 1,
                grouseRect.Width + 2,
                grouseRect.Height + 2
            );

            if (whitePixel != null)
            {
                // Draw outline in bright yellow/orange
                spriteBatch.Draw(
                    whitePixel,
                    new Rectangle(outlineRect.X, outlineRect.Y, outlineRect.Width, 1),
                    null,
                    Color.Orange,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    0.849f
                );
                spriteBatch.Draw(
                    whitePixel,
                    new Rectangle(outlineRect.X, outlineRect.Bottom - 1, outlineRect.Width, 1),
                    null,
                    Color.Orange,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    0.849f
                );
                spriteBatch.Draw(
                    whitePixel,
                    new Rectangle(outlineRect.X, outlineRect.Y, 1, outlineRect.Height),
                    null,
                    Color.Orange,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    0.849f
                );
                spriteBatch.Draw(
                    whitePixel,
                    new Rectangle(outlineRect.Right - 1, outlineRect.Y, 1, outlineRect.Height),
                    null,
                    Color.Orange,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    0.849f
                );
            }
        }
    }
}
