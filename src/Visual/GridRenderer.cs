using Raylib_cs;
using WaveFunctionCollapse.Simulation;
using WaveFunctionCollapse.Simulation.Environment;

namespace WaveFunctionCollapse.Visual;

public sealed class GridRenderer
{
    public void Draw(SimulationWorld world, BoardLayout board, bool showPerception)
    {
        Theme.Card(board.Card);
        Theme.TextAt("ECOSSISTEMA / WFC", (int)board.Card.X + 20, (int)board.Card.Y + 17, 18);
        for (int y = 0; y < world.Environment.Height; y++)
        for (int x = 0; x < world.Environment.Width; x++)
        {
            var floor = world.Environment.Sample(x, y);
            var position = board.ToScreen(x, y);
            var rect = new Rectangle(position.X, position.Y, board.TileSize - 1, board.TileSize - 1);
            Raylib.DrawRectangleRec(rect, TileColor(floor));
            if (floor.Toxin > 0)
                Raylib.DrawLine((int)(position.X + 5), (int)(position.Y + 5),
                    (int)(position.X + board.TileSize - 5), (int)(position.Y + board.TileSize - 5), Theme.Red);
        }
        if (showPerception)
        {
            int radius = world.Agent.Genome.PerceptionRange;
            int ax = (int)world.Agent.X, ay = (int)world.Agent.Y;
            for (int y = 0; y < board.Rows; y++)
            for (int x = 0; x < board.Columns; x++)
                if (Math.Abs((long)x - ax) + Math.Abs((long)y - ay) <= radius)
                {
                    var p = board.ToScreen(x, y);
                    Raylib.DrawRectangleLinesEx(new(p.X, p.Y, board.TileSize - 1, board.TileSize - 1), 1,
                        new Color(98, 226, 210, 90));
                }
        }
        var hovered = board.HitTest(Raylib.GetMousePosition());
        if (hovered is { } tile)
        {
            var p = board.ToScreen(tile.X, tile.Y);
            Raylib.DrawRectangleLinesEx(new(p.X, p.Y, board.TileSize - 1, board.TileSize - 1), 2, Theme.Text);
        }
        Theme.TextAt("AZUL agua   VERDE nutrientes   ROSA toxina   CLARO percepcao",
            (int)board.Card.X + 20, (int)(board.Card.Y + board.Card.Height - 28), 15, Theme.Muted);
    }

    private static Color TileColor(EnvironmentSample sample)
    {
        if (sample.Toxin > 0) return new(100, 43, 70);
        if (Math.Abs(sample.Temperature - 25) > 15) return new(137, 85, 49);
        double nutrition = Math.Clamp((sample.Glucose * 4 + sample.Lipids * 9) / 96, 0, 1);
        if (sample.Density < 0.75) return new(22, 64 + (int)(nutrition * 35), 89 + (int)(nutrition * 40));
        return new(28 + (int)(nutrition * 10), 58 + (int)(nutrition * 65), 57 + (int)(nutrition * 32));
    }
}
