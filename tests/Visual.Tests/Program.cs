using System.Numerics;
using System.Text.Json;
using WaveFunctionCollapse.Application;
using WaveFunctionCollapse.Simulation;
using WaveFunctionCollapse.Visual;

var cases = new (string Name, Action Run)[]
{
    ("Frame accumulation preserves fixed timestep", () =>
    {
        var clock = new PlaybackClock();
        int ticks = 0;
        for (int i = 0; i < 60; i++) ticks += clock.Advance(1.0 / 60, 0.1);
        Require(ticks == 10, $"Expected ten fixed ticks, got {ticks}.");
        clock.SetSpeed(8);
        ticks = 0;
        for (int i = 0; i < 60; i++) ticks += clock.Advance(1.0 / 60, 0.1);
        Require(ticks == 80, "Speed changed timestep or lost ticks.");
    }),
    ("Paused frames and old debt do not advance simulation", () =>
    {
        var clock = new PlaybackClock();
        Require(clock.Advance(0.09, 0.1) == 0, "Advanced too early.");
        clock.SetPaused(true);
        Require(clock.Advance(10, 0.1) == 0, "Paused clock advanced.");
        clock.SetPaused(false);
        Require(clock.Advance(0.02, 0.1) == 0, "Pause retained old debt.");
        Require(clock.Advance(0.08, 0.1) == 1, "Clock failed to resume.");
    }),
    ("Long stalls have bounded UI work", () =>
    {
        var clock = new PlaybackClock();
        clock.SetSpeed(32);
        Require(clock.Advance(100, 0.1) == 80, "Stall was not clamped.");
        Require(clock.Advance(100, 0.001) == 128, "Step budget was exceeded.");
        Require(clock.Advance(0, 0.001) == 0, "Unbounded debt remains.");
        Throws<ArgumentOutOfRangeException>(() => clock.SetSpeed(double.NaN));
        Throws<ArgumentOutOfRangeException>(() => clock.Advance(-1, 0.1));
    }),
    ("Board projection and picking agree across window sizes", () =>
    {
        foreach (var (width, height) in new[] { (1100, 760), (1280, 820), (1920, 1080) })
        {
            var board = BoardLayout.Create(width, height, 40, 20);
            Require(board.TileSize > 0, "Invalid grid scale.");
            for (int y = 0; y < 20; y++)
            for (int x = 0; x < 40; x++)
                Require(board.HitTest(board.ToScreen(x + 0.5, y + 0.5)) == (x, y), "Picking mismatch.");
            Require(board.HitTest(board.Origin - Vector2.One) is null, "Outside pointer selected a tile.");
            Require(board.HitTest(board.ToScreen(40.01, 20.01)) is null, "Outside edge selected a tile.");
            Require(board.ToScreen(40, 20).X < board.Card.X + board.Card.Width, "Map exceeds panel width.");
        }
    }),
    ("Interactive host preserves headless generation results", () =>
    {
        string root = Path.Combine(Path.GetTempPath(), "wfc-host-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string batch = Path.Combine(root, "batch.jsonl"), interactive = Path.Combine(root, "interactive.jsonl");
            var config = new RunConfiguration(42, 2, 1400, batch, true, false);
            Require(new GenerationLoop().Run(config) == 0, "Headless host failed.");
            Require(new GenerationLoop().Run(config with { LogPath = interactive }, new AutomaticDriver()) == 0, "Interactive host failed.");
            var a = File.ReadAllLines(batch + ".generations.jsonl");
            var b = File.ReadAllLines(interactive + ".generations.jsonl");
            Require(a.Length == 2 && b.Length == 2, "Missing generation.");
            for (int i = 0; i < a.Length; i++)
            {
                var left = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(a[i])!;
                var right = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(b[i])!;
                left.Remove("runId"); right.Remove("runId");
                Require(JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right), "Execution modes diverged.");
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }),
    ("Stopping the viewer is not logged as a biological death", () =>
    {
        string root = Path.Combine(Path.GetTempPath(), "wfc-stop-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string log = Path.Combine(root, "deaths.jsonl");
            var config = new RunConfiguration(42, 3, 3000, log, false, false);
            new GenerationLoop().Run(config, new StopDriver());
            Require(new FileInfo(log).Length == 0, "Closing the viewer fabricated a death.");
            var rows = File.ReadAllLines(log + ".generations.jsonl");
            Require(rows.Length == 1, "More generations ran after closing.");
            using var json = JsonDocument.Parse(rows[0]);
            Require(json.RootElement.GetProperty("status").GetString() == "UserStopped", "Wrong interruption status.");
            Require(json.RootElement.GetProperty("changes").GetArrayLength() == 0, "Interrupted run mutated.");
            using var input = JsonDocument.Parse(File.ReadAllLines(log + ".inputs.jsonl")[0]);
            Require(input.RootElement.GetProperty("intent").GetString() == "East", "Manual input was not captured.");
        }
        finally { Directory.Delete(root, recursive: true); }
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

static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}

sealed class AutomaticDriver : ISimulationDriver
{
    public bool Run(SimulationWorld world, int maxSteps, int totalGenerations, Action<MovementIntent?> step)
    {
        while (world.Agent.IsAlive && world.Tick < maxSteps) step(null);
        return true;
    }
}

sealed class StopDriver : ISimulationDriver
{
    public bool Run(SimulationWorld world, int maxSteps, int totalGenerations, Action<MovementIntent?> step)
    {
        step(MovementIntent.East);
        return false;
    }
}
