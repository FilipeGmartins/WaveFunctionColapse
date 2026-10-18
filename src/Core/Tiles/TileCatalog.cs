using System.Numerics;

namespace WaveFunctionCollapse.Core.Tiles;

/// <summary>Dense catalog indices are the tile IDs. This implementation supports 1–64 tiles.</summary>
public sealed class TileCatalog
{
    private readonly TileDefinition[] tiles;
    private readonly double[] weights;
    private readonly ulong[,] compatible;
    public int Count => tiles.Length;
    public TileDefinition this[int tileId] => tiles[tileId];
    public ulong AllMask => Count == 64 ? ulong.MaxValue : (1UL << Count) - 1;

    public TileCatalog(IEnumerable<TileDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        tiles = definitions.ToArray();
        if (tiles.Length is < 1 or > 64)
            throw new ArgumentException("Provide between 1 and 64 tiles.", nameof(definitions));
        if (tiles.Any(t => t is null)) throw new ArgumentException("Tiles cannot be null.", nameof(definitions));
        if (tiles.Select(t => t.Name).Distinct(StringComparer.Ordinal).Count() != tiles.Length)
            throw new ArgumentException("Tile names must be unique.", nameof(definitions));
        double maxWeight = tiles.Max(t => t.Weight);
        weights = tiles.Select(t => t.Weight / maxWeight).ToArray();
        if (weights.Any(w => w == 0))
            throw new ArgumentException("Weight range is too large to normalize.", nameof(definitions));
        compatible = new ulong[4, Count];
        for (int d = 0; d < 4; d++)
        for (int a = 0; a < Count; a++)
        for (int b = 0; b < Count; b++)
            if (tiles[a].GetSocket((Direction)d) == tiles[b].GetSocket(((Direction)d).Opposite()))
                compatible[d, a] |= 1UL << b;
    }

    public ulong GetCompatibleMask(int tileId, Direction direction) => compatible[(int)direction, tileId];

    internal double Weight(int tileId) => weights[tileId];

    internal ulong SupportedNeighbors(ulong mask, Direction direction)
    {
        ulong support = 0;
        while (mask != 0)
        {
            int id = BitOperations.TrailingZeroCount(mask);
            support |= compatible[(int)direction, id];
            mask &= mask - 1;
        }
        return support;
    }
}
