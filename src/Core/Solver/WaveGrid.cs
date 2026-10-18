using System.Numerics;

namespace WaveFunctionCollapse.Core.Solver;

/// <summary>Read-only public view of row-major superposition domains.</summary>
public sealed class WaveGrid
{
    private readonly ulong[] domains;
    public int Width { get; }
    public int Height { get; }
    internal int Length => domains.Length;

    internal WaveGrid(int width, int height, ulong initialMask)
    {
        Width = width;
        Height = height;
        domains = new ulong[checked(width * height)];
        Array.Fill(domains, initialMask);
    }

    public ulong GetPossibilities(int x, int y) => domains[GetIndex(x, y)];
    public int GetPossibilityCount(int x, int y) => BitOperations.PopCount(GetPossibilities(x, y));

    public int? GetCollapsedTileId(int x, int y)
    {
        ulong mask = GetPossibilities(x, y);
        return BitOperations.PopCount(mask) == 1 ? BitOperations.TrailingZeroCount(mask) : null;
    }

    internal int GetIndex(int x, int y)
    {
        if ((uint)x >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
        return y * Width + x;
    }

    internal ulong this[int index] { get => domains[index]; set => domains[index] = value; }
}
