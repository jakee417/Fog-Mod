#nullable enable
using System;
using Microsoft.Xna.Framework;
using StardewValley;

namespace FogMod;

public readonly struct FogGrid
{
    public int Cols { get; }
    public int Rows { get; }
    public Vector2 ExtOriginWorld { get; }
    public int ExtCols { get; }
    public int ExtRows { get; }
    public float CellSize { get; }
    public int BufferCells { get; }

    public FogGrid(float cellSize, int bufferCells)
    {
        CellSize = Math.Max(1f, cellSize);
        BufferCells = Math.Max(0, bufferCells);

        int width = Math.Max(1, Game1.viewport.Width);
        int height = Math.Max(1, Game1.viewport.Height);
        Vector2 viewportTopLeftWorld = new Vector2(Game1.viewport.X, Game1.viewport.Y);
        Cols = Math.Max(1, (int)Math.Ceiling(width / CellSize));
        Rows = Math.Max(1, (int)Math.Ceiling(height / CellSize));
        int gridLeftIndex = (int)Math.Floor(viewportTopLeftWorld.X / CellSize) - BufferCells;
        int gridTopIndex = (int)Math.Floor(viewportTopLeftWorld.Y / CellSize) - BufferCells;
        ExtOriginWorld = new Vector2(gridLeftIndex * cellSize, gridTopIndex * cellSize);
        ExtCols = Cols + BufferCells * 2;
        ExtRows = Rows + BufferCells * 2;
    }

    public Rectangle GetExtendedBounds()
    {
        int left = (int)Math.Floor(ExtOriginWorld.X);
        int top = (int)Math.Floor(ExtOriginWorld.Y);
        int width = (int)Math.Ceiling(ExtCols * CellSize);
        int height = (int)Math.Ceiling(ExtRows * CellSize);
        return new Rectangle(left, top, width, height);
    }

    public Tuple<int, int> GetCellFromPosition(Vector2 position)
    {
        int col = (int)Math.Floor((position.X - ExtOriginWorld.X) / CellSize);
        int row = (int)Math.Floor((position.Y - ExtOriginWorld.Y) / CellSize);
        return new Tuple<int, int>(row, col);
    }
}