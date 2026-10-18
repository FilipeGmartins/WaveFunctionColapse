using System.Text.Json;
using WaveFunctionCollapse.Core.Solver;
using WaveFunctionCollapse.Core.Tiles;
using WaveFunctionCollapse.Evolution;
using WaveFunctionCollapse.Evolution.Analytics;
using WaveFunctionCollapse.Simulation;
using WaveFunctionCollapse.Simulation.Agents;
using WaveFunctionCollapse.Simulation.Environment;
using WaveFunctionCollapse.Simulation.Events;
using WaveFunctionCollapse.Simulation.Parts;

string temp = Path.Combine(Path.GetTempPath(), "wfc-evolution-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
var cases = new (string Name, Action Run)[]
{
    ("JSONL round trip preserves death data and every part parameter", () =>
    {
        var catalog = Catalog();
        var genome = new CellGenome([new Flagellum(1.3, 0.4, 0.2), new Mouth(0.7, 0.2, 0.05),
            new ChemicalReceptor(3, 0.03)], maxEnergy: 2, maxHealth: 1, basalCost: 2,
            toxinResistance: 0.15, temperatureTolerance: 12);
        var record = Kill(catalog, genome, 0);
        string path = Path.Combine(temp, "roundtrip.jsonl");
        using (var logger = new DeathLogger(path))
        {
            logger.Append(record);
            var readWhileOpen = DeathLogger.Read(path).Single();
            Require(readWhileOpen.Death.Cause == record.Death.Cause, "Append was not flushed.");
        }
        using (var logger = new DeathLogger(path)) logger.Append(record with { RunId = "second" });
        var records = DeathLogger.Read(path).ToArray();
        Require(records.Length == 2, "Append replaced existing records.");
        var restored = records[0];
        Require(restored.RunId == record.RunId && restored.CatalogFingerprint == record.CatalogFingerprint, "Identity lost.");
        Require(restored.Death with { Genome = genome } == record.Death, "Death metrics changed.");
        Require(GenomeJson(restored.Death.Genome) == GenomeJson(genome), "Genome or parts changed.");
        Require(File.ReadAllText(path).Contains("\"kind\":\"Flagellum\""), "Missing part discriminator.");
    }),
    ("Logger rejects live worlds, bad metadata and unknown schema", () =>
    {
        var c = Catalog();
        var genome = new CellGenome([]);
        var world = World(c, genome, 0);
        Throws<InvalidOperationException>(() => DeathRecord.Capture(world, "live", c));
        var record = Kill(c, genome, 0);
        using var logger = new DeathLogger(Path.Combine(temp, "invalid-write.jsonl"));
        Throws<InvalidDataException>(() => logger.Append(record with { SchemaVersion = 99 }));
        Throws<InvalidDataException>(() => logger.Append(record with { Death = record.Death with { Health = 1 } }));
        Throws<InvalidDataException>(() => logger.Append(record with { FixedDeltaSeconds = double.NaN }));
        Require(new FileInfo(Path.Combine(temp, "invalid-write.jsonl")).Length == 0, "Invalid record was partially written.");
    }),
    ("Malformed and truncated logs report their line number", () =>
    {
        var record = Kill(Catalog(), new CellGenome([]), 0);
        string path = Path.Combine(temp, "corrupt.jsonl");
        using (var logger = new DeathLogger(path)) logger.Append(record);
        File.AppendAllText(path, "{broken\n");
        try { _ = DeathLogger.Read(path).ToArray(); throw new Exception("Corruption ignored."); }
        catch (InvalidDataException error) { Require(error.Message.Contains("line 2"), "No failing line number."); }
        string partial = Path.Combine(temp, "partial.jsonl");
        File.WriteAllText(partial, "{\"schemaVersion\":1");
        Throws<InvalidDataException>(() => { using var logger = new DeathLogger(partial); });
    }),
    ("Starvation adds corrective parts and keeps the parent immutable", () =>
    {
        var c = Catalog();
        var parent = new CellGenome([], maxEnergy: 2, maxHealth: 1, basalCost: 1);
        var record = Kill(c, parent, 0);
        var next = new GenomeEvolver().Evolve(parent, c, [record]);
        Require(next.Genome.Parts.OfType<Mouth>().Any(), "No mouth mutation.");
        Require(next.Genome.Parts.OfType<Flagellum>().Any(), "No mobility mutation.");
        Require(next.Genome.Parts.OfType<ChemicalReceptor>().Any(), "No perception mutation.");
        Require(next.Genome.MaxEnergy > parent.MaxEnergy && next.Genome.BasalCost < parent.BasalCost, "No energetic adaptation.");
        Require(next.Genome.Maintenance > 0, "Parts became free.");
        Require(parent.Parts.Count == 0 && c[0].Weight == 1, "Parent modified.");
        Require(next.Catalog[1].Weight / next.Catalog[0].Weight > c[1].Weight / c[0].Weight, "Food was not favored.");
    }),
    ("Existing mouths improve throughput with metabolic overhead", () =>
    {
        var c = Catalog();
        var parent = new CellGenome([new Mouth()], maxEnergy: 1, maxHealth: 1);
        var next = new GenomeEvolver().Evolve(parent, c, [Kill(c, parent, 0)]);
        Require(next.Genome.GlucoseRate > parent.GlucoseRate, "Mouth did not improve.");
        Require(next.Genome.Parts.OfType<Mouth>().Single().Maintenance > parent.Parts.OfType<Mouth>().Single().Maintenance,
            "Improved throughput had no ATP cost.");
    }),
    ("Toxin adaptation targets its type and has a basal cost", () =>
    {
        var c = Catalog();
        var parent = new CellGenome([], maxHealth: 1, basalCost: 0);
        var record = Kill(c, parent, 2);
        Require(record.Death.Cause == DeathCause.Toxin, "Bad fixture.");
        var next = new GenomeEvolver().Evolve(parent, c, [record]);
        Require(next.Genome.ToxinResistance > parent.ToxinResistance && next.Genome.BasalCost > parent.BasalCost, "Free or missing resistance.");
        Require(next.Catalog[2].Weight / next.Catalog[3].Weight < c[2].Weight / c[3].Weight, "Wrong toxin target.");
        Near(next.Genome.MaxEnergy, parent.MaxEnergy);
    }),
    ("Thermal adaptation changes tolerance and only penalizes the fatal tile", () =>
    {
        var c = Catalog();
        var parent = new CellGenome([], maxHealth: 1, basalCost: 0);
        var record = Kill(c, parent, 4);
        Require(record.Death.Cause == DeathCause.Temperature, "Bad fixture.");
        var next = new GenomeEvolver().Evolve(parent, c, [record]);
        Require(next.Genome.TemperatureTolerance > parent.TemperatureTolerance, "No thermal protection.");
        Require(next.Genome.BasalCost > parent.BasalCost, "Free protection.");
        Require(next.Catalog[4].Weight / next.Catalog[0].Weight < c[4].Weight / c[0].Weight, "Wrong temperature target.");
    }),
    ("No deaths leaves genome and ecosystem unchanged", () =>
    {
        var c = Catalog();
        var parent = new CellGenome([]);
        var next = new GenomeEvolver().Evolve(parent, c, []);
        Require(ReferenceEquals(parent, next.Genome) && ReferenceEquals(c, next.Catalog)
            && next.DeathsAnalyzed == 0 && next.Changes.Count == 0, "No-data mutation.");
    }),
    ("Incompatible catalog logs are rejected", () =>
    {
        var c = Catalog();
        var parent = new CellGenome([]);
        var record = Kill(c, parent, 0);
        var other = new TileCatalog([Tile("Other", 1)]);
        Throws<ArgumentException>(() => new GenomeEvolver().Evolve(parent, other, [record]));
        Throws<ArgumentException>(() => new GenomeEvolver().Evolve(parent, c,
            [record with { Death = record.Death with { TileId = c.Count } }]));
        Throws<ArgumentException>(() => new GenomeEvolver().Evolve(new CellGenome([new Mouth()]), c, [record]));
    }),
    ("A zero-throughput mouth can recover from starvation", () =>
    {
        var c = Catalog();
        var parent = new CellGenome([new Mouth(0, 0, 0)], maxEnergy: 1, maxHealth: 1);
        var next = new GenomeEvolver().Evolve(parent, c, [Kill(c, parent, 0)]);
        Require(next.Genome.GlucoseRate > 0 && next.Genome.LipidRate > 0, "Degenerate mouth remained nonfunctional.");
        Require(next.Genome.Parts.OfType<Mouth>().Single().Maintenance > 0, "Recovery has no cost.");
    }),
    ("Repeated adaptation retains sockets, IDs, resources and positive weights", () =>
    {
        var original = Catalog();
        var c = original;
        var genome = new CellGenome([], maxHealth: 1, basalCost: 0);
        for (int generation = 0; generation < 80; generation++)
        {
            var record = Kill(c, genome, 2, generation: generation);
            var next = new GenomeEvolver().Evolve(genome, c, [record]);
            (genome, c) = (next.Genome, next.Catalog);
            Require(genome.ToxinResistance <= 0.9 && double.IsFinite(genome.BasalCost), "Unbounded adaptation.");
            for (int i = 0; i < c.Count; i++)
            {
                var a = (BiologicalTile)c[i];
                var b = (BiologicalTile)original[i];
                Require(a.Weight > 0 && double.IsFinite(a.Weight) && a.Name == b.Name, "Invalid weight or changed ID.");
                Require(a.North == b.North && a.East == b.East && a.South == b.South && a.West == b.West, "Changed sockets.");
                Near(a.Glucose, b.Glucose);
                Near(a.Toxin, b.Toxin);
                Near(a.Temperature, b.Temperature);
            }
        }
    }),
    ("Logs can select one run and generation without mixing histories", () =>
    {
        var c = Catalog();
        var parent = new CellGenome([], maxHealth: 1, basalCost: 1);
        string path = Path.Combine(temp, "selection.jsonl");
        using (var logger = new DeathLogger(path))
        {
            logger.Append(Kill(c, parent, 0, "target", 5));
            logger.Append(Kill(c, parent, 2, "other", 5));
            logger.Append(Kill(c, parent, 4, "target", 6));
        }
        var next = new GenomeEvolver().EvolveFromLog(parent, c, path, "target", 5);
        Require(next.DeathsAnalyzed == 1 && next.Genome.ToxinResistance == 0
            && next.Genome.TemperatureTolerance == parent.TemperatureTolerance, "Mixed run histories.");
    }),
    ("Correction is deterministic for an identical death cohort", () =>
    {
        var c = Catalog();
        var parent = new CellGenome([], maxHealth: 1, basalCost: 1);
        var cohort = new[] { Kill(c, parent, 0), Kill(c, parent, 2), Kill(c, parent, 4) };
        var a = new GenomeEvolver().Evolve(parent, c, cohort);
        var b = new GenomeEvolver().Evolve(parent, c, cohort.Reverse());
        Require(GenomeJson(a.Genome) == GenomeJson(b.Genome), "Cohort ordering changed mutations.");
        Require(DeathRecord.Fingerprint(a.Catalog) == DeathRecord.Fingerprint(b.Catalog), "Nondeterministic weights.");
        Near(a.Genome.ToxinResistance, 0.1 / 3);
    })
};

int failures = 0;
try
{
    foreach (var test in cases)
    {
        try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
        catch (Exception error) { failures++; Console.Error.WriteLine($"FAIL {test.Name}: {error.Message}"); }
    }
}
finally
{
    // Only this test's newly-created directory is removed.
    Directory.Delete(temp, recursive: true);
}
Console.WriteLine($"{cases.Length - failures}/{cases.Length} passed");
return failures == 0 ? 0 : 1;

static string GenomeJson(CellGenome genome) => JsonSerializer.Serialize(GenomeSnapshot.From(genome));
static BiologicalTile Tile(string name, double weight, double glucose = 0, double toxin = 0,
    string? type = null, double temperature = 25) =>
    new(name, weight, new(0), new(0), new(0), new(0), glucose: glucose, toxin: toxin, toxinType: type, temperature: temperature);
static TileCatalog Catalog() => new([
    Tile("Empty", 1), Tile("Food", 2, glucose: 10), Tile("Acid", 1, toxin: 20, type: "Acid"),
    Tile("Other toxin", 1, toxin: 20, type: "Alkali"), Tile("Hot", 1, temperature: 100)
]);
static SimulationWorld World(TileCatalog catalog, CellGenome genome, int tileId, int generation = 0)
{
    var map = new WfcSolver(catalog).Solve(1, 1, 42, pins: [new(0, 0, tileId)]);
    return new(new BiologicalEnvironment(map, catalog), genome, 0.5, 0.5, fixedDeltaSeconds: 1,
        generation: generation, simulationSeed: 17);
}
static DeathRecord Kill(TileCatalog catalog, CellGenome genome, int tileId, string runId = "test", int generation = 0)
{
    var world = World(catalog, genome, tileId, generation);
    for (int i = 0; i < 10000 && world.Agent.IsAlive; i++) world.Step();
    return DeathRecord.Capture(world, runId, catalog);
}
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Near(double actual, double expected) => Require(Math.Abs(actual - expected) < 1e-9, $"{actual} != {expected}");
static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}
