using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using System;
using System.Collections.Generic;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        private void InitializeFloatingFogParticles()
        {
            floatingParticles = new List<FogParticle>();
            grid = new FogGrid(FogTileSize, DefaultFogGridBufferCells);
            for (int row = 0; row < grid.ExtRows; row++)
            {
                for (int col = 0; col < grid.ExtCols; col++)
                {
                    for (int n = 0; n < MinimumFogParticlesPerCell; n++)
                    {
                        floatingParticles.Add(CreateParticleInCell(col, row, grid.ExtOriginWorld));
                    }
                }
            }
        }

        public void ResetFogParticles()
        {
            floatingParticles = new List<FogParticle>();
            fogCellOccupancy = new CellOccupancy
            {
                Counts = new int[0, 0],
                Indices = new List<int>[0, 0]
            };
        }

        private FogParticle CreateParticleInCell(int col, int row, Vector2 viewportTopLeftWorld)
        {
            float speed = FloatingParticleSpeed * (0.9f + (float)random.NextDouble() * 0.2f);
            Vector2 baseDir = globalWindDirection;
            Vector2 velocity;
            float angleJitter = ((float)random.NextDouble() - 0.5f) * MathHelper.ToRadians(12f);
            float cos = (float)Math.Cos(angleJitter);
            float sin = (float)Math.Sin(angleJitter);
            Vector2 jitteredDir = new Vector2(
                baseDir.X * cos - baseDir.Y * sin,
                baseDir.X * sin + baseDir.Y * cos
            );
            if (jitteredDir.LengthSquared() > 0.0000001f)
                jitteredDir.Normalize();
            else if (baseDir.LengthSquared() > 0.0000001f)
                jitteredDir = Vector2.Normalize(baseDir);
            else
                jitteredDir = Vector2.Zero;
            velocity = jitteredDir * speed;
            float cellX = viewportTopLeftWorld.X + col * FogTileSize;
            float cellY = viewportTopLeftWorld.Y + row * FogTileSize;
            float x = cellX + (float)random.NextDouble() * FogTileSize;
            float y = cellY + (float)random.NextDouble() * FogTileSize;
            float scale = DefaultFloatingScaleMin + (float)random.NextDouble() * Math.Max(0.01f, DefaultFloatingScaleMax - DefaultFloatingScaleMin);
            float alpha = 0.35f + (float)random.NextDouble() * 0.35f;
            Texture2D chosenTex = null;
            if (cloudTextures != null && cloudTextures.Count > 0)
            {
                int idx = random.Next(cloudTextures.Count);
                chosenTex = cloudTextures[idx];
            }
            return new FogParticle
            {
                Position = new Vector2(x, y),
                Velocity = velocity,
                Scale = scale,
                Rotation = 0f,
                Alpha = Math.Max(0.05f, Math.Min(0.6f, alpha)),
                AgeSeconds = 0f,
                Texture = chosenTex,
                IsFadingOut = false,
                FadeOutSecondsLeft = ParticleFadeOutSeconds,
            };
        }

        private void UpdateFloatingFogParticles(float deltaSeconds)
        {
            if (floatingParticles == null || floatingParticles.Count == 0)
                InitializeFloatingFogParticles();
            floatingParticles = RemoveUnusedParticles(floatingParticles, grid, deltaSeconds, true);
            var occupancy = ComputeCellOccupancy();
            occupancy.Counts = PopulateCellsUnderTarget(occupancy.Counts);
            RemoveFogOverTarget(occupancy);
            fogCellOccupancy = occupancy;
        }

        private static List<FogParticle> RemoveUnusedParticles(List<FogParticle> particles, FogGrid grid, float deltaSeconds, bool removeOffscreen)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Position += p.Velocity * deltaSeconds;
                p.AgeSeconds += deltaSeconds;
                if (removeOffscreen && !grid.GetExtendedBounds().Contains(new Point((int)p.Position.X, (int)p.Position.Y)))
                {
                    particles.RemoveAt(i);
                    continue;
                }
                if (p.IsFadingOut)
                {
                    p.FadeOutSecondsLeft -= deltaSeconds;
                    if (p.FadeOutSecondsLeft <= 0f)
                    {
                        particles.RemoveAt(i);
                        continue;
                    }
                }
                particles[i] = p;
            }
            return particles;
        }

        private CellOccupancy ComputeCellOccupancy()
        {
            int[,] counts = new int[grid.ExtCols, grid.ExtRows];
            List<int>[,] indices = new List<int>[grid.ExtCols, grid.ExtRows];

            for (int i = 0; i < floatingParticles.Count; i++)
            {
                var p = floatingParticles[i];
                (int row, int col) = grid.GetCellFromPosition(p.Position);
                if (col < 0 || col >= grid.ExtCols || row < 0 || row >= grid.ExtRows)
                    continue;

                // Count all particles
                counts[col, row]++;

                // Build selection pool excluding those already fading out
                if (!p.IsFadingOut)
                {
                    var list = indices[col, row];
                    if (list == null)
                    {
                        list = new List<int>();
                        indices[col, row] = list;
                    }
                    list.Add(i);
                }
            }
            return new CellOccupancy { Counts = counts, Indices = indices };
        }

        private int[,] PopulateCellsUnderTarget(int[,] counts)
        {
            for (int row = 0; row < grid.ExtRows; row++)
            {
                for (int col = 0; col < grid.ExtCols; col++)
                {
                    int target = MinimumFogParticlesPerCell;
                    int need = target - counts[col, row];
                    for (int k = 0; k < need; k++)
                    {
                        floatingParticles.Add(
                            CreateParticleInCell(col, row, grid.ExtOriginWorld)
                        );
                        counts[col, row]++;
                    }
                }
            }
            return counts;
        }

        private void RemoveFogOverTarget(CellOccupancy occupancy)
        {
            for (int row = 0; row < grid.ExtRows; row++)
            {
                for (int col = 0; col < grid.ExtCols; col++)
                {
                    int target = MaximumFogParticlesPerCell;
                    int eligible = occupancy.Indices[col, row] != null ? occupancy.Indices[col, row].Count : 0;
                    int extra = eligible - target;
                    if (extra <= 0)
                        continue;

                    var list = occupancy.Indices[col, row];
                    if (list == null || list.Count == 0)
                        continue;

                    list.Sort((a, b) => floatingParticles[b].AgeSeconds.CompareTo(floatingParticles[a].AgeSeconds));
                    int toFade = Math.Min(extra, list.Count);
                    for (int k = 0; k < toFade; k++)
                    {
                        int idx = list[k];
                        var p = floatingParticles[idx];
                        if (!p.IsFadingOut)
                        {
                            p.IsFadingOut = true;
                            floatingParticles[idx] = p;
                        }
                    }
                }
            }
        }
    }
}


