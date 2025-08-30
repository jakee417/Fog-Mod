#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using System.Collections.Generic;
using System;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        private void SpawnExplosionSmoke(Vector2 centerWorld, float radiusPixels)
        {
            try
            {
                if (cloudTextures == null || cloudTextures.Count == 0)
                    return;

                int count = Math.Max(24, Math.Min(220, (int)(radiusPixels * 0.45f)));
                // Start from current occupancy so we can cap per-tile spawns
                var occupancy = ComputeSmokeCellOccupancy();
                int[,] counts = occupancy.Counts;
                for (int i = 0; i < count; i++)
                {
                    float ang = (float)(Random.NextDouble() * MathHelper.TwoPi);
                    // uniform area distribution
                    float r = (float)Math.Sqrt(Random.NextDouble()) * Math.Max(8f, radiusPixels);
                    Vector2 pos = centerWorld + new Vector2((float)Math.Cos(ang) * r, (float)Math.Sin(ang) * r);

                    // Respect per-cell smoke cap before spawning
                    (int row, int col) = grid.GetCellFromPosition(pos);
                    if (col >= 0 && col < grid.ExtCols && row >= 0 && row < grid.ExtRows && counts[col, row] >= MaximumSmokeParticlesPerCell)
                        continue;

                    // velocity: outward + wind + jitter
                    Vector2 outward = r > 1e-3f ? Vector2.Normalize(pos - centerWorld) : new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                    Vector2 v = outward * SmokeSpeed * (0.4f + (float)Random.NextDouble() * 0.7f);
                    Vector2 wind = globalWindDirection * SmokeSpeed * (0.4f + (float)Random.NextDouble() * 0.6f);
                    // slight upward drift like heavy smoke rising slowly
                    Vector2 rise = new Vector2(0f, -SmokeSpeed * MathHelper.Lerp(0.05f, 0.15f, (float)Random.NextDouble()));
                    Vector2 jitter = new Vector2(((float)Random.NextDouble() - 0.5f) * SmokeSpeed, ((float)Random.NextDouble() - 0.5f) * SmokeSpeed) * 0.25f;
                    Vector2 vel = v + wind + rise + jitter;

                    // choose texture
                    Texture2D tex = cloudTextures[Random.Next(cloudTextures.Count)];
                    float scale = MathHelper.Lerp(DefaultFloatingScaleMin, DefaultFloatingScaleMax, (float)Random.NextDouble());
                    // smoke is chunkier and grows; start a bit smaller
                    scale *= MathHelper.Lerp(0.6f, 1.1f, (float)Random.NextDouble());
                    float alpha = MathHelper.Lerp(0.35f, 0.7f, (float)Random.NextDouble());
                    var particle = new FogParticle
                    {
                        Position = pos,
                        Velocity = vel,
                        Scale = scale,
                        Rotation = 0f,
                        Alpha = alpha,
                        AgeSeconds = 0f,
                        Texture = tex,
                        IsFadingOut = false,
                        FadeOutSecondsLeft = 0f,
                        FadeOutTotalSeconds = 0f
                    };
                    explosionSmokeParticles.Add(particle);

                    // Increment occupancy for the tile we just spawned into
                    if (col >= 0 && col < grid.ExtCols && row >= 0 && row < grid.ExtRows)
                        counts[col, row] += 1;
                }
            }
            catch { }
        }

        public void ResetExplosionSmokeParticles()
        {
            explosionSmokeParticles = new List<FogParticle>();
            smokeCellOccupancy = new CellOccupancy();
        }

        private void UpdateExplosionFlashInfos(float deltaSeconds)
        {
            // Update explosion flash infos
            for (int i = explosionFlashInfos.Count - 1; i >= 0; i--)
            {
                var ef = explosionFlashInfos[i];
                ef.TimeLeft = Math.Max(0f, ef.TimeLeft - deltaSeconds);
                if (ef.TimeLeft <= 0f)
                {
                    explosionFlashInfos.RemoveAt(i);
                    continue;
                }
                explosionFlashInfos[i] = ef;
            }
        }

        private void UpdateExplosionSmokeParticles(float deltaSeconds)
        {
            // Update explosion smoke particles
            if (explosionSmokeParticles != null && explosionSmokeParticles.Count > 0)
                explosionSmokeParticles = RemoveUnusedParticles(explosionSmokeParticles, grid, deltaSeconds, false);

            // Update occupancy snapshot for smoke particles
            smokeCellOccupancy = ComputeSmokeCellOccupancy();
            RemoveExtraSmokeOverTarget(smokeCellOccupancy);
        }

        private CellOccupancy ComputeSmokeCellOccupancy()
        {
            int[,] counts = new int[grid.ExtCols, grid.ExtRows];
            List<int>[,] indices = new List<int>[grid.ExtCols, grid.ExtRows];
            for (int i = 0; i < explosionSmokeParticles.Count; i++)
            {
                var p = explosionSmokeParticles[i];
                (int row, int col) = grid.GetCellFromPosition(p.Position);
                if (col < 0 || col >= grid.ExtCols || row < 0 || row >= grid.ExtRows)
                    continue;
                counts[col, row]++;
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

        private void RemoveExtraSmokeOverTarget(CellOccupancy occupancy)
        {
            for (int row = 0; row < grid.ExtRows; row++)
            {
                for (int col = 0; col < grid.ExtCols; col++)
                {
                    var list = occupancy.Indices != null ? occupancy.Indices[col, row] : null;
                    int eligible = list != null ? list.Count : 0;
                    int extra = eligible - MaximumSmokeParticlesPerCell;
                    if (extra <= 0)
                        continue;

                    if (list == null || list.Count == 0)
                        continue;

                    list.Sort((a, b) => explosionSmokeParticles[b].AgeSeconds.CompareTo(explosionSmokeParticles[a].AgeSeconds));
                    int toFade = Math.Min(extra, list.Count);
                    for (int k = 0; k < toFade; k++)
                    {
                        int idx = list[k];
                        var p = explosionSmokeParticles[idx];
                        if (!p.IsFadingOut)
                        {
                            p.IsFadingOut = true;
                            p.FadeOutTotalSeconds = ParticleFadeOutSeconds;
                            p.FadeOutSecondsLeft = ParticleFadeOutSeconds;
                            explosionSmokeParticles[idx] = p;
                        }
                    }
                }
            }
        }
    }
}


