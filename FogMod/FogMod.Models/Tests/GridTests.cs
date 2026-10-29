#nullable enable
using FogMod.Models;
using Microsoft.Xna.Framework;
using Xunit;

namespace FogMod.Tests;

public class GridTests
{
    [Fact]
    public void Constructor_DerivesCellsAndBufferedOrigin()
    {
        var grid = new Grid(1280, 720, 0, 0, 128f, 3);
        Assert.Equal(10, grid.Cols);
        Assert.Equal(6, grid.Rows);
        Assert.Equal(new Vector2(-384f, -384f), grid.ExtOriginWorld);
        Assert.Equal(16, grid.ExtCols);
        Assert.Equal(12, grid.ExtRows);
    }

    [Fact]
    public void Constructor_ClampsDegenerateInputs()
    {
        var grid = new Grid(640, 480, 0, 0, 0f, -2);
        Assert.Equal(1f, grid.CellSize);
        Assert.Equal(0, grid.BufferCells);
        Assert.Equal(grid.Cols, grid.ExtCols);
    }

    [Fact]
    public void ExtendedBounds_CoversBufferedCells()
    {
        var grid = new Grid(1280, 720, 0, 0, 128f, 3);
        Rectangle bounds = grid.GetExtendedBounds();
        Assert.Equal(new Rectangle(-384, -384, 2048, 1536), bounds);
    }

    [Fact]
    public void CellFromPosition_MapsThroughOrigin()
    {
        var grid = new Grid(256, 256, 0, 0, 128f, 0);
        Assert.Equal(new System.Tuple<int, int>(0, 0), grid.GetCellFromPosition(new Vector2(10f, 10f)));
        Assert.Equal(new System.Tuple<int, int>(0, 1), grid.GetCellFromPosition(new Vector2(200f, 10f)));
        Assert.Equal(new System.Tuple<int, int>(1, 1), grid.GetCellFromPosition(new Vector2(200f, 200f)));
    }
}
