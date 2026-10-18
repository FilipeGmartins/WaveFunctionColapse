using WaveFunctionCollapse.Core.Solver;
using WaveFunctionCollapse.Core.Tiles;
using WaveFunctionCollapse.Simulation;
using WaveFunctionCollapse.Simulation.Agents;
using WaveFunctionCollapse.Simulation.Environment;
using WaveFunctionCollapse.Simulation.Events;
using WaveFunctionCollapse.Simulation.Parts;

var cases = new (string Name, Action Run)[]
{
    ("Resources belong to positions and independent runs", () =>
    {
        var tile = Tile(glucose: 10);
        var catalog = new TileCatalog([tile]);
        var map = new WfcSolver(catalog).Solve(2, 1, 7);
        var env = new BiologicalEnvironment(map, catalog);
        var other = new BiologicalEnvironment(map, catalog);
        var world = new SimulationWorld(env, new CellGenome([new Mouth(1, 0, 0)], basalCost: 4), 0, 0,
            fixedDeltaSeconds: 1);
        world.Step(); // Full reserve: do not waste resources.
        Near(env.Sample(0, 0).Glucose, 10);
        world.Step();
        Near(env.Sample(0, 0).Glucose, 9);
        Near(env.Sample(1, 0).Glucose, 10);
        Near(other.Sample(0, 0).Glucose, 10);
        Near(tile.Glucose, 10);
    }),
    ("Absorption respects capacity and available nutrients", () =>
    {
        var env = Environment(Tile(glucose: 0.1, lipids: 0.2));
        var genome = new CellGenome([new Mouth(100, 100, 0)], maxEnergy: 1, basalCost: 1, starvationDamage: 0);
        var world = new SimulationWorld(env, genome, 0, 0, fixedDeltaSeconds: 1);
        world.Step();
        world.Step();
        Near(env.Sample(0, 0).Glucose, 0);
        Near(env.Sample(0, 0).Lipids, 0.2 - 0.6 / 9);
        Near(world.Agent.Energy, 0);
        for (int i = 0; i < 10; i++) world.Step();
        Near(env.Sample(0, 0).Lipids, 0);
        Require(world.Agent.Energy >= 0 && world.Agent.Energy <= 1, "ATP out of range.");
    }),
    ("Starvation emits exactly one immutable death snapshot", () =>
    {
        var world = new SimulationWorld(Environment(Tile()),
            new CellGenome([], maxEnergy: 1, maxHealth: 2, basalCost: 1, starvationDamage: 2),
            0.5, 0.5, fixedDeltaSeconds: 1, generation: 3, simulationSeed: 99);
        int events = 0;
        world.CellDied += _ => events++;
        Require(world.Step() is null, "Death occurred too soon.");
        var death = world.Step() ?? throw new Exception("Missing death.");
        Require(death.Cause == DeathCause.Starvation, "Wrong cause.");
        Require(death.Generation == 3 && death.SimulationSeed == 99 && death.MapSeed == 7, "Missing run metadata.");
        Near(death.EnergyDeficit, 1);
        Near(death.TotalEnergyDeficit, 1);
        Near(death.ElapsedSeconds, 2);
        Require(death.FatalToxinType is null, "Incorrect fatal toxin.");
        for (int i = 0; i < 10; i++) Require(world.Step() is null, "Repeated death.");
        Require(events == 1 && world.Tick == 2 && ReferenceEquals(death, world.Death), "World advanced after death.");
    }),
    ("Toxin cause carries its type and resistance reduces damage", () =>
    {
        var tile = Tile(toxin: 4, toxinType: "Acid");
        var world = new SimulationWorld(Environment(tile),
            new CellGenome([], maxHealth: 2, basalCost: 0), 0, 0, fixedDeltaSeconds: 1);
        var death = world.Step()!;
        Require(death.Cause == DeathCause.Toxin && death.FatalToxinType == "Acid", "Lost toxin attribution.");
        Near(death.ToxinConcentration, 4);
        var resistant = new SimulationWorld(Environment(tile),
            new CellGenome([], maxHealth: 2, basalCost: 0, toxinResistance: 0.75), 0, 0, fixedDeltaSeconds: 1);
        resistant.Step();
        Near(resistant.Agent.Health, 1);
        var immune = new SimulationWorld(Environment(tile),
            new CellGenome([], basalCost: 0, toxinResistance: 1), 0, 0);
        immune.Step();
        Near(immune.Agent.Health, immune.Agent.Genome.MaxHealth);
    }),
    ("Temperature death is separate from toxin death", () =>
    {
        var world = new SimulationWorld(Environment(Tile(temperature: 50)),
            new CellGenome([], maxHealth: 1, basalCost: 0, idealTemperature: 25,
                temperatureTolerance: 5, thermalDamage: 0.1), 0, 0, fixedDeltaSeconds: 1);
        var death = world.Step()!;
        Require(death.Cause == DeathCause.Temperature && death.FatalToxinType is null, "Wrong thermal attribution.");
        Near(death.Temperature, 50);
    }),
    ("Flagella pay movement ATP and respect boundaries", () =>
    {
        var world = new SimulationWorld(Environment(Tile(density: 0), 3, 1),
            new CellGenome([new Flagellum(1, 2, 0)], maxEnergy: 3, basalCost: 0),
            0, 0, fixedDeltaSeconds: 1);
        world.Step(MovementIntent.West);
        Near(world.Agent.X, 0);
        Near(world.Agent.Energy, 3);
        world.Step(MovementIntent.East);
        Near(world.Agent.X, 1);
        Near(world.Agent.Energy, 1);
        world.Step(MovementIntent.East);
        Near(world.Agent.X, 1.5);
        Near(world.Agent.Energy, 0);
        world.Step(MovementIntent.East);
        Near(world.Agent.X, 1.5);
    }),
    ("Density slows movement and absent flagella prevent it", () =>
    {
        var genome = new CellGenome([new Flagellum(1, 1, 0)], basalCost: 0);
        var world = new SimulationWorld(Environment(Tile(density: 1), 3, 1), genome, 0, 0, fixedDeltaSeconds: 1);
        world.Step(MovementIntent.East);
        Near(world.Agent.X, 0.5);
        Near(world.Agent.Energy, genome.MaxEnergy - 1);
        var stationary = new SimulationWorld(Environment(Tile(), 3, 1), new CellGenome([], basalCost: 0), 0, 0);
        stationary.Step(MovementIntent.East);
        Near(stationary.Agent.X, 0);
    }),
    ("Receptors expose only samples within Manhattan radius", () =>
    {
        var env = Environment(Tile(), 5, 5);
        var blind = new SimulationWorld(env, new CellGenome([]), 2, 2);
        Require(blind.Observe().Count == 1, "Blind cell sees other tiles.");
        var sensing = new SimulationWorld(env, new CellGenome([new ChemicalReceptor(1)]), 2, 2);
        Require(sensing.Observe().Count == 5, "Incorrect perception radius.");
        var wide = new SimulationWorld(env, new CellGenome([new ChemicalReceptor(int.MaxValue)]), 2, 2);
        Require(wide.Observe().Count == 25, "Large radius overflow.");
        var corner = new SimulationWorld(env, wide.Agent.Genome, 0, 0);
        Require(corner.Observe().Count == 25, "Clipped long-range perception at a corner.");
    }),
    ("Movement cannot skip a fatal destination tile", () =>
    {
        var catalog = new TileCatalog([Tile(),
            new BiologicalTile("Acid pool", 1, new(0), new(0), new(0), new(0), toxin: 10, toxinType: "Acid")]);
        var map = new WfcSolver(catalog).Solve(3, 1, 7, pins: [new(0, 0, 0), new(1, 0, 1), new(2, 0, 0)]);
        var world = new SimulationWorld(new BiologicalEnvironment(map, catalog),
            new CellGenome([new Flagellum(100, 0, 0)], maxHealth: 1, basalCost: 0), 0, 0, fixedDeltaSeconds: 1);
        var death = world.Step(MovementIntent.East)!;
        Require(death.Cause == DeathCause.Toxin && death.TileX == 1 && death.TileId == 1,
            "Agent skipped hazard or logged the wrong position.");
    }),
    ("Extreme valid numeric inputs do not corrupt agent state", () =>
    {
        var world = new SimulationWorld(Environment(Tile(density: double.MaxValue, temperature: double.MaxValue), 2, 1),
            new CellGenome([new Flagellum(1, 2, 0)], basalCost: 0, thermalDamage: 0), 0, 0);
        world.Step(MovementIntent.East);
        Near(world.Agent.X, 0);
        Require(double.IsFinite(world.Agent.Energy) && double.IsFinite(world.Agent.Health), "Nonfinite agent state.");
    }),
    ("Module maintenance contributes to deficit and genome is copied", () =>
    {
        var parts = new List<CellPart> { new Mouth(maintenance: 2), new ChemicalReceptor(maintenance: 1) };
        var genome = new CellGenome(parts, maxEnergy: 1, maxHealth: 1, basalCost: 1);
        parts.Clear();
        Require(genome.Parts.Count == 2, "Genome shares caller's mutable list.");
        var world = new SimulationWorld(Environment(Tile()), genome, 0, 0, fixedDeltaSeconds: 1);
        var death = world.Step()!;
        Near(death.MaintenancePerSecond, 4);
        Near(death.EnergyDeficit, 3);
    }),
    ("Fixed steps produce identical trajectories for identical input", () =>
    {
        var genome = new CellGenome([new Flagellum(), new Mouth(), new ChemicalReceptor()]);
        var a = new SimulationWorld(Environment(Tile(glucose: 2), 4, 4), genome, 1, 1);
        var b = new SimulationWorld(Environment(Tile(glucose: 2), 4, 4), genome, 1, 1);
        for (int i = 0; i < 500; i++)
        {
            var intent = (MovementIntent)(i % 5);
            a.Step(intent);
            b.Step(intent);
            Near(a.Agent.X, b.Agent.X);
            Near(a.Agent.Y, b.Agent.Y);
            Near(a.Agent.Energy, b.Agent.Energy);
            Near(a.Agent.Health, b.Agent.Health);
        }
        Require(a.Tick == b.Tick && a.Death == b.Death, "Simulation diverged.");
    }),
    ("Invalid simulation inputs are rejected", () =>
    {
        Throws<ArgumentOutOfRangeException>(() => new Mouth(-1));
        Throws<ArgumentOutOfRangeException>(() => new Flagellum(double.NaN));
        Throws<ArgumentOutOfRangeException>(() => new ChemicalReceptor(0));
        Throws<ArgumentOutOfRangeException>(() => new CellGenome([], toxinResistance: 2));
        Throws<ArgumentOutOfRangeException>(() => new CellGenome([], maxEnergy: 0));
        var env = Environment(Tile());
        var genome = new CellGenome([]);
        Throws<ArgumentOutOfRangeException>(() => new SimulationWorld(env, genome, 0, 0, fixedDeltaSeconds: 0));
        Throws<ArgumentOutOfRangeException>(() => new SimulationWorld(env, genome, -1, 0));
        var world = new SimulationWorld(env, genome, 0, 0);
        Throws<ArgumentOutOfRangeException>(() => world.Step((MovementIntent)99));
        var invalid = new TileCatalog([new TileDefinition("Not biological", 1, new(0), new(0), new(0), new(0))]);
        Throws<ArgumentException>(() => new BiologicalEnvironment(new WfcSolver(invalid).Solve(1, 1, 1), invalid));
        var impossible = new TileCatalog([new BiologicalTile("Impossible", 1, new(0), new(1), new(0), new(2))]);
        Throws<ArgumentException>(() => new BiologicalEnvironment(new WfcSolver(impossible).Solve(2, 1, 1), impossible));
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

static BiologicalTile Tile(double glucose = 0, double lipids = 0, double toxin = 0,
    string? toxinType = null, double density = 0, double temperature = 25) =>
    new("Floor", 1, new(0), new(0), new(0), new(0), glucose, lipids, toxin, toxinType, density, temperature);
static BiologicalEnvironment Environment(BiologicalTile tile, int width = 1, int height = 1)
{
    var catalog = new TileCatalog([tile]);
    return new(new WfcSolver(catalog).Solve(width, height, 7), catalog);
}
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Near(double actual, double expected) => Require(Math.Abs(actual - expected) < 1e-9, $"{actual} != {expected}");
static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}
