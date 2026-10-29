#nullable enable
using System;
using System.Collections.Generic;

namespace FogMod.Models;

public struct CellOccupancy
{
    public int[,] Counts;
    public List<int>[,] Indices;

    public CellOccupancy(int[,] counts, List<int>[,] indices)
    {
        Counts = counts;
        Indices = indices;
    }

    public bool IsValid => Counts != null && Indices != null;

    public static CellOccupancy ComputeCellOccupancy(List<FogParticle> particles, Grid grid)
    {
        int[,] counts = new int[grid.ExtCols, grid.ExtRows];
        List<int>[,] indices = new List<int>[grid.ExtCols, grid.ExtRows];

        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
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
        return new CellOccupancy(
            counts: counts,
            indices: indices
        );
    }

    // Fade the oldest extras over the smoke target, cell by cell.
    public void FadeExtraSmoke(List<FogParticle> smoke, int extCols, int extRows)
    {
        for (int row = 0; row < extRows; row++)
        {
            for (int col = 0; col < extCols; col++)
            {
                var list = Indices != null ? Indices[col, row] : null;
                int eligible = list != null ? list.Count : 0;
                int extra = eligible - Constants.MaximumSmokeParticlesPerCell;
                if (extra <= 0)
                    continue;

                if (list == null || list.Count == 0)
                    continue;

                list.Sort((a, b) => smoke[b].AgeSeconds.CompareTo(smoke[a].AgeSeconds));
                int toFade = Math.Min(extra, list.Count);
                for (int k = 0; k < toFade; k++)
                {
                    int idx = list[k];
                    var p = smoke[idx];
                    if (!p.IsFadingOut)
                    {
                        p.IsFadingOut = true;
                        p.FadeOutSecondsLeft = Constants.ParticleFadeOutSeconds;
                        smoke[idx] = p;
                    }
                }
            }
        }
    }
}