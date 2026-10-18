using WaveFunctionCollapse.Core.Solver;
using WaveFunctionCollapse.Core.Tiles;

// Dependency-free regression executable: a failed assertion returns a nonzero exit code.
var cases = new (string Name, Action Run)[]
{
    ("Shannon entropy follows weights", () =>
    {
        var c = new TileCatalog([Uniform("A", 1), Uniform("B", 3)]);
        Near(ShannonEntropy.Calculate(3, c), -0.25 * Math.Log(0.25) - 0.75 * Math.Log(0.75));
        Near(ShannonEntropy.Calculate(1, c), 0);
        Throws<ArgumentOutOfRangeException>(() => ShannonEntropy.Calculate(0, c));
        Throws<ArgumentOutOfRangeException>(() => ShannonEntropy.Calculate(4, c));
    }),
    ("Alternating sockets propagate from a pin", () =>
    {
        var c = Alternating();
        var r = new WfcSolver(c).Solve(11, 8, 7, pins: [new(0, 0, 0)]);
        Require(r.IsSuccess, "Expected a valid checkerboard.");
        for (int y = 0; y < r.Grid.Height; y++)
        for (int x = 0; x < r.Grid.Width; x++)
            Require(r.Grid.GetCollapsedTileId(x, y) == (x + y) % 2, "Propagation broke parity.");
        CheckEdges(r.Grid, c);
    }),
    ("Generated grids obey directional sockets across seeds", () =>
    {
        var c = new TileCatalog([
            Tile("Ground", 1, 1, 1, 1), Tile("Water", 0, 0, 0, 0),
            Tile("North", 0, 1, 1, 1), Tile("East", 1, 0, 1, 1),
            Tile("South", 1, 1, 0, 1), Tile("West", 1, 1, 1, 0)
        ]);
        for (int seed = 0; seed < 30; seed++)
        {
            var r = new WfcSolver(c).Solve(12, 9, seed, maxAttempts: 20);
            Require(r.IsSuccess, $"No map for seed {seed}.");
            CheckEdges(r.Grid, c);
        }
    }),
    ("Identical seeds reproduce the grid", () =>
    {
        var solver = new WfcSolver(new TileCatalog([Uniform("A", 1), Uniform("B", 2)]));
        var a = solver.Solve(15, 10, 123);
        var b = solver.Solve(15, 10, 123);
        for (int y = 0; y < 10; y++)
        for (int x = 0; x < 15; x++)
            Require(a.Grid.GetPossibilities(x, y) == b.Grid.GetPossibilities(x, y), "Seed mismatch.");
    }),
    ("Conflicting pins produce an empty domain", () =>
    {
        var r = new WfcSolver(Alternating()).Solve(2, 1, 0, maxAttempts: 3,
            pins: [new(0, 0, 0), new(1, 0, 0)]);
        Require(r.Status == WfcStatus.Contradiction && r.Attempts == 3, "Expected bounded failure.");
        var p = r.ContradictionAt!.Value;
        Require(r.Grid.GetPossibilities(p.X, p.Y) == 0, "Missing contradiction domain.");
        var duplicate = new WfcSolver(Alternating()).Solve(1, 1, 0,
            pins: [new(0, 0, 0), new(0, 0, 1)]);
        Require(!duplicate.IsSuccess, "Conflicting pins at the same coordinate were ignored.");
    }),
    ("Initial propagation detects an impossible catalog", () =>
    {
        var c = new TileCatalog([Tile("Impossible horizontally", 0, 1, 0, 2)]);
        Require(!new WfcSolver(c).Solve(2, 1, 42).IsSuccess, "Unchecked initial adjacency.");
        Require(new WfcSolver(c).Solve(1, 1, 42).IsSuccess, "Open boundary must allow a single tile.");
    }),
    ("All 64 bits including tile 63 are supported", () =>
    {
        var c = new TileCatalog(Enumerable.Range(0, 64).Select(i => Uniform($"T{i}", 1)));
        Require(c.AllMask == ulong.MaxValue, "64-bit full mask overflow.");
        Near(ShannonEntropy.Calculate(c.AllMask, c), Math.Log(64));
        var r = new WfcSolver(c).Solve(2, 2, 1, pins: [new(1, 1, 63)]);
        Require(r.IsSuccess && r.Grid.GetCollapsedTileId(1, 1) == 63, "High bit lost.");
    }),
    ("Weighted sampling respects a large probability difference", () =>
    {
        var c = new TileCatalog([Uniform("Rare", 1), Uniform("Common", 9)]);
        var solver = new WfcSolver(c);
        int common = 0;
        for (int seed = 0; seed < 1000; seed++)
            if (solver.Solve(1, 1, seed).Grid.GetCollapsedTileId(0, 0) == 1) common++;
        Require(common is > 850 and < 950, $"Unexpected weighted count: {common}.");
    }),
    ("Inputs and biological conditions are validated", () =>
    {
        Throws<ArgumentException>(() => new TileCatalog([]));
        Throws<ArgumentException>(() => new TileCatalog(Enumerable.Range(0, 65).Select(i => Uniform($"T{i}", 1))));
        Throws<ArgumentOutOfRangeException>(() => Uniform("NaN", double.NaN));
        Throws<ArgumentException>(() => new BiologicalTile("Toxin", 1, new(0), new(0), new(0), new(0), toxin: 1));
        Throws<ArgumentOutOfRangeException>(() => new BiologicalTile("Cold", 1, new(0), new(0), new(0), new(0), temperature: -300));
        var solver = new WfcSolver(Alternating());
        Throws<ArgumentOutOfRangeException>(() => solver.Solve(0, 2, 0));
        Throws<ArgumentOutOfRangeException>(() => solver.Solve(2, 2, 0, maxAttempts: 0));
        Throws<ArgumentOutOfRangeException>(() => solver.Solve(2, 2, 0, pins: [new(2, 0, 0)]));
        Throws<ArgumentOutOfRangeException>(() => solver.Solve(2, 2, 0, pins: [new(0, 0, 2)]));
    }),
    ("Cancellation is honored", () =>
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        Throws<OperationCanceledException>(() => new WfcSolver(Alternating())
            .Solve(10, 10, 0, cancellationToken: source.Token));
    })
};

int failures = 0;
foreach (var test in cases)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception error) { failures++; Console.Error.WriteLine($"FAIL {test.Name}: {error.Message}"); }
}
Console.WriteLine($"{cases.Length - failures}/{cases.Length} passed");
return failures == 0 ? 0 : 1;

static TileDefinition Uniform(string name, double weight) => new(name, weight, new(0), new(0), new(0), new(0));
static TileDefinition Tile(string name, int n, int e, int s, int w) => new(name, 1, new(n), new(e), new(s), new(w));
static TileCatalog Alternating() => new([Tile("A", 0, 0, 1, 1), Tile("B", 1, 1, 0, 0)]);
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Near(double actual, double expected) => Require(Math.Abs(actual - expected) < 1e-12, $"{actual} != {expected}");
static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}
static void CheckEdges(WaveGrid grid, TileCatalog catalog)
{
    for (int y = 0; y < grid.Height; y++)
    for (int x = 0; x < grid.Width; x++)
    {
        int id = grid.GetCollapsedTileId(x, y) ?? throw new Exception("Uncollapsed cell.");
        if (x + 1 < grid.Width)
        {
            int right = grid.GetCollapsedTileId(x + 1, y)!.Value;
            Require(catalog[id].East == catalog[right].West, "East/West socket mismatch.");
        }
        if (y + 1 < grid.Height)
        {
            int down = grid.GetCollapsedTileId(x, y + 1)!.Value;
            Require(catalog[id].South == catalog[down].North, "South/North socket mismatch.");
        }
    }
}
