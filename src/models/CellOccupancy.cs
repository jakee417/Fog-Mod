#nullable enable
using System.Collections.Generic;

namespace FogMod;

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
}