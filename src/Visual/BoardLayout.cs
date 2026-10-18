using System.Numerics;
using Raylib_cs;

namespace WaveFunctionCollapse.Visual;

public readonly record struct BoardLayout(Rectangle Card, Vector2 Origin, float TileSize, int Columns, int Rows)
{
    public static BoardLayout Create(int width, int height, int columns, int rows)
    {
        if (width < 1100 || height < 760 || columns <= 0 || rows <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var card = new Rectangle(24, 108, width - 372, height - 240);
        float tile = Math.Min((card.Width - 32) / columns, (card.Height - 88) / rows);
        return new(card, new(card.X + (card.Width - columns * tile) / 2,
            card.Y + 48 + (card.Height - 88 - rows * tile) / 2), tile, columns, rows);
    }

    public Vector2 ToScreen(double x, double y) => Origin + new Vector2((float)x, (float)y) * TileSize;

    public (int X, int Y)? HitTest(Vector2 mouse)
    {
        var local = (mouse - Origin) / TileSize;
        if (local.X < 0 || local.Y < 0 || local.X >= Columns || local.Y >= Rows) return null;
        return ((int)local.X, (int)local.Y);
    }
}
