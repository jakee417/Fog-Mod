#nullable enable
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        private void ResetGrouse()
        {
            grouse.Clear();
            spawnedTreePositions.Clear();
            nextGrouseId = 1;
            lastPlayerLocation = null;
        }

        private void SpawnGrouseInTrees()
        {
            if (Game1.currentLocation == null || grouse.Count >= GrouseMaxPerLocation)
                return;

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
                AnimationTimer = 0f,
                Alpha = 1.0f,
                OriginalY = treePosition.Y
            };
            grouse.Add(newGrouse);
            spawnedTreePositions.Add(treePosition);
        }

        private void UpdateGrouse(float deltaSeconds)
        {
            for (int i = grouse.Count - 1; i >= 0; i--)
            {
                var g = grouse[i];
                g.StateTimer += deltaSeconds;

                switch (g.State)
                {
                    case GrouseState.Perched:
                        UpdateGrousePerched(g, false);
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
                UpdateGrouseAnimation(ref g, deltaSeconds);

                // Potential grouse cleanup
                if ((g.State == GrouseState.Flying && IsGrouseOffScreen(g)) || g.Alpha <= 0f)
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

        private void UpdateGrousePerched(Grouse g, bool fromMultiplayerSync)
        {
            Vector2 playerPos = Game1.player.getStandingPosition();
            // Either someone triggered a new flush or we are syncing from another player.
            if (fromMultiplayerSync || Vector2.Distance(g.Position, playerPos) < GrouseDetectionRadius)
            {
                if (!fromMultiplayerSync)
                {
                    var flushInfo = new GrouseFlushInfo
                    {
                        LocationName = Game1.currentLocation?.NameOrUniqueName,
                        GrouseId = g.GrouseId,
                        Timestamp = Game1.currentGameTime.TotalGameTime.Ticks
                    };
                    SendGrouseFlushMessage(flushInfo);
                }
                g.State = GrouseState.Surprised;
                g.StateTimer = 0f;
                g.Velocity = Vector2.Zero;
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

        private void UpdateGrouseKnockedDown(Grouse g, float deltaSeconds)
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
                // Landed - stop falling and start fading
                g.Velocity = Vector2.Zero;
                g.Position = new Vector2(g.Position.X, g.OriginalY + GrouseFallDistance);

                // Start fading after landing
                if (g.StateTimer > GrouseFallDistance / 150f) // Rough estimate of fall time
                {
                    float timeSinceLanding = g.StateTimer - (GrouseFallDistance / 150f);
                    float fadeProgress = timeSinceLanding / GrouseFadeOutDuration;
                    g.Alpha = Math.Max(0f, 1.0f - fadeProgress);
                }
            }
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
                GrouseState.KnockedDown => 0f, // No animation when knocked down
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

        private void KnockDownGrouse(int grouseId)
        {
            for (int i = 0; i < grouse.Count; i++)
            {
                var g = grouse[i];
                if (g.GrouseId == grouseId)
                {
                    g.State = GrouseState.KnockedDown;
                    g.StateTimer = 0f;
                    // Preserve the grouse's current momentum but add some downward force
                    g.Velocity = new Vector2(g.Velocity.X * 0.8f, Math.Max(g.Velocity.Y + 100f, 150f));
                    g.FlightHeight = 0f;
                    g.Alpha = 1.0f;
                    Vector2 impactPosition = g.Position;
                    g.OriginalY = impactPosition.Y;
                    Vector2 screenPosition = Game1.GlobalToLocal(Game1.viewport, g.Position);
                    screenPosition.Y -= GrouseSpriteHeight * g.Scale / 2f;
                    g.DamageFlashTimer = GrouseDamageFlashDuration;
                    g.Smoke = new CollisionSmoke { Position = screenPosition };
                    grouse[i] = g;
                    PlayGrouseKnockdownSound(g);
                    break;
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

        private void PlayGrouseKnockdownSound(Grouse g)
        {
            if (g.State == GrouseState.KnockedDown)
                Game1.playSound("hitEnemy");
        }
    }
}
