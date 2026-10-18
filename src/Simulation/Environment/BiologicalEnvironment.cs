using WaveFunctionCollapse.Core.Solver;
using WaveFunctionCollapse.Core.Tiles;

namespace WaveFunctionCollapse.Simulation.Environment;

public readonly record struct EnvironmentSample(int X, int Y, int TileId, double Glucose, double Lipids,
    double Toxin, string? ToxinType, double Density, double Temperature);

public sealed class BiologicalEnvironment
{
    private readonly BiologicalTile[] tiles;
    private readonly int[] tileIds;
    private readonly double[] glucose;
    private readonly double[] lipids;
    public int Width { get; }
    public int Height { get; }
    public int MapSeed { get; }

    public BiologicalEnvironment(WfcResult map, TileCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(catalog);
        if (!map.IsSuccess) throw new ArgumentException("A successful WFC map is required.", nameof(map));
        Width = map.Grid.Width;
        Height = map.Grid.Height;
        MapSeed = map.Seed;
        int length = checked(Width * Height);
        tiles = new BiologicalTile[length];
        tileIds = new int[length];
        glucose = new double[length];
        lipids = new double[length];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int id = map.Grid.GetCollapsedTileId(x, y) ?? throw new ArgumentException("Uncollapsed map.", nameof(map));
            if ((uint)id >= (uint)catalog.Count || catalog[id] is not BiologicalTile tile)
                throw new ArgumentException("Every map tile must reference a biological tile in the supplied catalog.", nameof(catalog));
            int index = y * Width + x;
            tiles[index] = tile;
            tileIds[index] = id;
            glucose[index] = tile.Glucose;
            lipids[index] = tile.Lipids;
        }
    }

    public EnvironmentSample Sample(int x, int y)
    {
        int i = Index(x, y);
        var tile = tiles[i];
        return new(x, y, tileIds[i], glucose[i], lipids[i], tile.Toxin, tile.ToxinType, tile.Density, tile.Temperature);
    }

    // Rates are constrained to ATP storage before extraction, avoiding waste at a full energy reserve.
    internal double Absorb(int x, int y, double glucoseRequest, double lipidRequest, double capacity)
    {
        int i = Index(x, y);
        double g = Math.Min(glucose[i], Math.Min(glucoseRequest, capacity / 4));
        double l = Math.Min(lipids[i], Math.Min(lipidRequest, Math.Max(0, capacity - g * 4) / 9));
        glucose[i] -= g;
        lipids[i] -= l;
        return g * 4 + l * 9;
    }

    private int Index(int x, int y)
    {
        if ((uint)x >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
        return y * Width + x;
    }
}
