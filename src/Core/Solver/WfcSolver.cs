using System.Numerics;
using WaveFunctionCollapse.Core.Tiles;

namespace WaveFunctionCollapse.Core.Solver;

/// <summary>Weighted WFC with open boundaries, socket propagation and bounded random restarts.</summary>
public sealed class WfcSolver(TileCatalog catalog)
{
    private readonly TileCatalog catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    public WfcResult Solve(int width, int height, int seed, int maxAttempts = 1,
        IEnumerable<TilePin>? pins = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);
        _ = checked(width * height);
        TilePin[] fixedTiles = pins?.ToArray() ?? [];
        foreach (var pin in fixedTiles)
        {
            if ((uint)pin.X >= (uint)width || (uint)pin.Y >= (uint)height)
                throw new ArgumentOutOfRangeException(nameof(pins), "Pin coordinates are outside the grid.");
            if ((uint)pin.TileId >= (uint)catalog.Count)
                throw new ArgumentOutOfRangeException(nameof(pins), "Unknown pinned tile ID.");
        }

        var random = new Random(seed);
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var grid = new WaveGrid(width, height, catalog.AllMask);
            var queue = new Queue<int>();
            foreach (var pin in fixedTiles)
            {
                int index = grid.GetIndex(pin.X, pin.Y);
                grid[index] &= 1UL << pin.TileId;
            }
            // Every domain must be checked, even before the first observation.
            for (int i = 0; i < grid.Length; i++) queue.Enqueue(i);

            int failure;
            while ((failure = Propagate(grid, queue, cancellationToken)) < 0)
            {
                int next = SelectMinimumEntropy(grid, random, cancellationToken);
                if (next < 0) return new(WfcStatus.Success, grid, seed, attempt, null);
                grid[next] = Choose(grid[next], random);
                queue.Enqueue(next);
            }

            if (attempt == maxAttempts)
                return new(WfcStatus.Contradiction, grid, seed, attempt,
                    new GridPosition(failure % width, failure / width));
        }
    }

    private int Propagate(WaveGrid grid, Queue<int> queue, CancellationToken token)
    {
        while (queue.TryDequeue(out int index))
        {
            token.ThrowIfCancellationRequested();
            if (grid[index] == 0) return index;
            int x = index % grid.Width, y = index / grid.Width;
            for (int d = 0; d < 4; d++)
            {
                int neighbor = (Direction)d switch
                {
                    Direction.North when y > 0 => index - grid.Width,
                    Direction.East when x + 1 < grid.Width => index + 1,
                    Direction.South when y + 1 < grid.Height => index + grid.Width,
                    Direction.West when x > 0 => index - 1,
                    _ => -1
                };
                if (neighbor < 0) continue;
                ulong reduced = grid[neighbor] & catalog.SupportedNeighbors(grid[index], (Direction)d);
                if (reduced == grid[neighbor]) continue;
                grid[neighbor] = reduced;
                if (reduced == 0) return neighbor;
                queue.Enqueue(neighbor);
            }
        }
        return -1;
    }

    private int SelectMinimumEntropy(WaveGrid grid, Random random, CancellationToken token)
    {
        int selected = -1, ties = 0;
        double minimum = double.PositiveInfinity;
        for (int i = 0; i < grid.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            if (BitOperations.PopCount(grid[i]) <= 1) continue;
            double entropy = ShannonEntropy.Calculate(grid[i], catalog);
            if (entropy < minimum - 1e-12)
            {
                minimum = entropy;
                selected = i;
                ties = 1;
            }
            else if (Math.Abs(entropy - minimum) <= 1e-12 && random.Next(++ties) == 0)
                selected = i;
        }
        return selected;
    }

    private ulong Choose(ulong mask, Random random)
    {
        double total = 0;
        ulong remaining = mask;
        while (remaining != 0)
        {
            total += catalog.Weight(BitOperations.TrailingZeroCount(remaining));
            remaining &= remaining - 1;
        }
        double target = random.NextDouble() * total;
        int last = 0;
        while (mask != 0)
        {
            last = BitOperations.TrailingZeroCount(mask);
            target -= catalog.Weight(last);
            if (target < 0) return 1UL << last;
            mask &= mask - 1;
        }
        return 1UL << last; // Floating-point rounding fallback.
    }
}
