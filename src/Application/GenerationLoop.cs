using System.Text.Json;
using System.Text.Json.Serialization;
using WaveFunctionCollapse.Core.Solver;
using WaveFunctionCollapse.Core.Tiles;
using WaveFunctionCollapse.Evolution;
using WaveFunctionCollapse.Evolution.Analytics;
using WaveFunctionCollapse.Simulation;
using WaveFunctionCollapse.Simulation.Agents;
using WaveFunctionCollapse.Simulation.Environment;
using WaveFunctionCollapse.Simulation.Parts;

namespace WaveFunctionCollapse.Application;

public sealed class GenerationLoop
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public int Run(RunConfiguration config, ISimulationDriver? driver = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(config.Generations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(config.Steps);
        var catalog = DemoCatalog.Create();
        var genome = new CellGenome(config.Primitive ? [] : [new Flagellum(), new Mouth(), new ChemicalReceptor()]);
        var initialMap = Generate(catalog, config.Seed);
        PrintMap(initialMap);
        if (!initialMap.IsSuccess) return 1;
        if (config.MapOnly) return 0;

        string runId = Guid.NewGuid().ToString("N");
        string logPath = Path.GetFullPath(config.LogPath ?? Path.Combine("artifacts", runId, "deaths.jsonl"));
        using var logger = new DeathLogger(logPath);
        using var summaries = new StreamWriter(new FileStream(logPath + ".generations.jsonl",
            FileMode.Append, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        using var inputs = driver is null ? null : new StreamWriter(new FileStream(logPath + ".inputs.jsonl",
            FileMode.Append, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        File.WriteAllText(logPath + $".{runId}.manifest.json", JsonSerializer.Serialize(new
        {
            SchemaVersion = 1, RunId = runId, Configuration = config,
            ExecutionMode = driver is null ? "Headless" : "Interactive",
            Width = 40, Height = 20, FixedDeltaSeconds = 0.1, MaxWfcAttempts = 20,
            SpawnX = 20.5, SpawnY = 10.5, Pins = new[] { new TilePin(20, 10, 1) },
            DecisionPolicy = "ForagingPolicy-v1", EvolutionPolicy = "Corrective-v1",
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            InitialGenome = GenomeSnapshot.From(genome),
            InitialCatalog = Enumerable.Range(0, catalog.Count).Select(i => (BiologicalTile)catalog[i]).ToArray()
        }, new JsonSerializerOptions(JsonOptions) { WriteIndented = true }));
        Console.WriteLine($"Run={runId}; death log={logPath}");

        var seeds = new Random(config.Seed);
        var evolver = new GenomeEvolver();
        int mapFailures = 0;
        for (int generation = 0; generation < config.Generations; generation++)
        {
            int mapSeed = generation == 0 ? config.Seed : seeds.Next();
            int simulationSeed = seeds.Next();
            var map = generation == 0 ? initialMap : Generate(catalog, mapSeed);
            string fingerprint = DeathRecord.Fingerprint(catalog);
            if (!map.IsSuccess)
            {
                mapFailures++;
                summaries.WriteLine(JsonSerializer.Serialize(new { RunId = runId, Generation = generation,
                    Status = "WfcContradiction", MapSeed = mapSeed, CatalogFingerprint = fingerprint,
                    map.Attempts, map.ContradictionAt }, JsonOptions));
                Console.WriteLine($"Generation {generation}: WFC contradiction; no biological death or mutation.");
                continue;
            }
            var world = new SimulationWorld(new BiologicalEnvironment(map, catalog), genome,
                20.5, 10.5, generation: generation, simulationSeed: simulationSeed);
            var policy = new ForagingPolicy(simulationSeed);
            DeathRecord? death = null;
            world.CellDied += _ =>
            {
                death = DeathRecord.Capture(world, runId, catalog);
                logger.Append(death);
            };
            bool proceed = true;
            if (driver is null)
            {
                for (int step = 0; step < config.Steps && world.Agent.IsAlive; step++) world.Step(policy.Decide(world));
            }
            else
            {
                bool recorded = false;
                MovementIntent? previous = null;
                proceed = driver.Run(world, config.Steps, config.Generations, intent =>
                {
                    if (!recorded || previous != intent)
                    {
                        inputs!.WriteLine(JsonSerializer.Serialize(new { SchemaVersion = 1, RunId = runId,
                            Generation = generation, Tick = world.Tick, Intent = intent?.ToString() ?? "Auto" }, JsonOptions));
                        previous = intent;
                        recorded = true;
                    }
                    world.Step(intent ?? policy.Decide(world));
                });
            }
            bool interrupted = world.Agent.IsAlive && world.Tick < config.Steps;
            var next = evolver.Evolve(genome, catalog, death is null ? [] : [death]);
            summaries.WriteLine(JsonSerializer.Serialize(new
            {
                SchemaVersion = 1, RunId = runId, Generation = generation,
                Status = interrupted ? "UserStopped" : death is null ? "TimeLimit" : "Death", MapSeed = mapSeed, SimulationSeed = simulationSeed,
                CatalogFingerprint = fingerprint, world.Tick, world.ElapsedSeconds,
                world.Agent.Energy, world.Agent.Health, world.Agent.X, world.Agent.Y,
                Cause = death?.Death.Cause.ToString(), Genome = GenomeSnapshot.From(genome),
                Weights = Enumerable.Range(0, catalog.Count).Select(i => catalog[i].Weight).ToArray(),
                NextGenome = GenomeSnapshot.From(next.Genome),
                NextWeights = Enumerable.Range(0, next.Catalog.Count).Select(i => next.Catalog[i].Weight).ToArray(),
                next.Changes
            }, JsonOptions));
            Console.WriteLine($"Generation {generation}: {(interrupted ? "UserStopped" : death?.Death.Cause.ToString() ?? "TimeLimit")}; " +
                $"time={world.ElapsedSeconds:F1}s; ATP={world.Agent.Energy:F2}; HP={world.Agent.Health:F2}; mutations={next.Changes.Count}");
            (genome, catalog) = (next.Genome, next.Catalog);
            if (!proceed) break;
        }
        return mapFailures == 0 ? 0 : 1;
    }

    private static WfcResult Generate(TileCatalog catalog, int seed) => new WfcSolver(catalog)
        .Solve(40, 20, seed, maxAttempts: 20, pins: [new TilePin(20, 10, 1)]);

    private static void PrintMap(WfcResult map)
    {
        Console.WriteLine($"WFC: {map.Status}; seed={map.Seed}; attempts={map.Attempts}");
        if (!map.IsSuccess) return;
        char[] symbols = ['~', '.', 'X', '^', '>', 'v', '<'];
        for (int y = 0; y < map.Grid.Height; y++)
        {
            for (int x = 0; x < map.Grid.Width; x++)
                Console.Write(symbols[map.Grid.GetCollapsedTileId(x, y)!.Value]);
            Console.WriteLine();
        }
    }
}
