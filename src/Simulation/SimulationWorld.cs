using WaveFunctionCollapse.Simulation.Agents;
using WaveFunctionCollapse.Simulation.Environment;
using WaveFunctionCollapse.Simulation.Events;

namespace WaveFunctionCollapse.Simulation;

public enum MovementIntent { Stay, North, East, South, West }

/// <summary>One agent, fixed-step clock, no graphics, I/O or automatic decision policy.</summary>
public sealed class SimulationWorld
{
    public BiologicalEnvironment Environment { get; }
    public CellAgent Agent { get; }
    public double FixedDeltaSeconds { get; }
    public long Tick { get; private set; }
    public double ElapsedSeconds => Tick * FixedDeltaSeconds;
    public int Generation { get; }
    public int SimulationSeed { get; }
    public CellDeathEvent? Death { get; private set; }
    public event Action<CellDeathEvent>? CellDied;

    public SimulationWorld(BiologicalEnvironment environment, CellGenome genome, double spawnX, double spawnY,
        double fixedDeltaSeconds = 0.1, int generation = 0, int simulationSeed = 0)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(genome);
        FixedDeltaSeconds = Validation.Positive(fixedDeltaSeconds, nameof(fixedDeltaSeconds));
        // Bound displacement to at most one tile per step, so a move cannot skip a hazard tile.
        if (fixedDeltaSeconds > 1) throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds));
        if (!double.IsFinite(spawnX) || spawnX < 0 || spawnX >= environment.Width)
            throw new ArgumentOutOfRangeException(nameof(spawnX));
        if (!double.IsFinite(spawnY) || spawnY < 0 || spawnY >= environment.Height)
            throw new ArgumentOutOfRangeException(nameof(spawnY));
        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        Environment = environment;
        Agent = new(genome, spawnX, spawnY);
        Generation = generation;
        SimulationSeed = simulationSeed;
    }

    /// <summary>Perception uses a Manhattan radius; no receptor means current tile only.</summary>
    public IReadOnlyList<EnvironmentSample> Observe()
    {
        int x = (int)Agent.X, y = (int)Agent.Y;
        long radius = Math.Min(Agent.Genome.PerceptionRange, (long)Environment.Width + Environment.Height - 2);
        var samples = new List<EnvironmentSample>();
        for (int sy = (int)Math.Max(0, y - radius); sy <= Math.Min(Environment.Height - 1, y + radius); sy++)
        for (int sx = (int)Math.Max(0, x - radius); sx <= Math.Min(Environment.Width - 1, x + radius); sx++)
            if (Math.Abs((long)sx - x) + Math.Abs((long)sy - y) <= radius) samples.Add(Environment.Sample(sx, sy));
        return samples.AsReadOnly();
    }

    /// <returns>A death snapshot on the fatal step; null otherwise. Dead worlds stop advancing.</returns>
    public CellDeathEvent? Step(MovementIntent intent = MovementIntent.Stay)
    {
        if (!Enum.IsDefined(intent)) throw new ArgumentOutOfRangeException(nameof(intent));
        if (!Agent.IsAlive) return null;
        Tick++;
        var genome = Agent.Genome;
        double dt = FixedDeltaSeconds;
        Agent.Energy = Math.Min(genome.MaxEnergy, Agent.Energy + Environment.Absorb((int)Agent.X, (int)Agent.Y,
            genome.GlucoseRate * dt, genome.LipidRate * dt, genome.MaxEnergy - Agent.Energy));

        double required = (genome.BasalCost + genome.Maintenance) * dt;
        double deficit = Math.Max(0, required - Agent.Energy);
        Agent.Energy = Math.Max(0, Agent.Energy - required);
        Agent.TotalEnergyDeficit += deficit;
        if (Damage(deficit * genome.StarvationDamage)) return Die(DeathCause.Starvation, deficit);

        Move(intent);
        var floor = Environment.Sample((int)Agent.X, (int)Agent.Y);
        if (Damage(floor.Toxin * (1 - genome.ToxinResistance) * dt)) return Die(DeathCause.Toxin, deficit);
        double excess = Math.Max(0, Math.Abs(floor.Temperature - genome.IdealTemperature) - genome.TemperatureTolerance);
        double thermalDamage = genome.ThermalDamage == 0 ? 0 : excess * genome.ThermalDamage * dt;
        if (Damage(thermalDamage)) return Die(DeathCause.Temperature, deficit);
        return null;
    }

    private void Move(MovementIntent intent)
    {
        if (intent == MovementIntent.Stay || Agent.Genome.Speed == 0) return;
        var floor = Environment.Sample((int)Agent.X, (int)Agent.Y);
        // Density is a dimensionless drag; zero density is allowed and means no extra drag.
        double drag = 1 + floor.Density;
        double cost = Agent.Genome.MovementCost * drag;
        if (!double.IsFinite(cost)) return; // Unrepresentable drag cost means movement is unaffordable.
        double distance = Math.Min(1, Agent.Genome.Speed / drag * FixedDeltaSeconds);
        if (cost > 0) distance = Math.Min(distance, Agent.Energy / cost);
        (int dx, int dy) = intent switch
        {
            MovementIntent.North => (0, -1), MovementIntent.East => (1, 0),
            MovementIntent.South => (0, 1), MovementIntent.West => (-1, 0), _ => (0, 0)
        };
        double x = Math.Clamp(Agent.X + dx * distance, 0, Math.BitDecrement((double)Environment.Width));
        double y = Math.Clamp(Agent.Y + dy * distance, 0, Math.BitDecrement((double)Environment.Height));
        double traveled = Math.Abs(x - Agent.X) + Math.Abs(y - Agent.Y);
        Agent.Energy = Math.Max(0, Agent.Energy - traveled * cost);
        (Agent.X, Agent.Y) = (x, y);
    }

    private bool Damage(double amount)
    {
        Agent.Health = Math.Max(0, Agent.Health - amount);
        return !Agent.IsAlive;
    }

    private CellDeathEvent Die(DeathCause cause, double deficit)
    {
        var floor = Environment.Sample((int)Agent.X, (int)Agent.Y);
        var snapshot = new CellDeathEvent(Generation, Environment.MapSeed, SimulationSeed, Tick, ElapsedSeconds,
            cause, Agent.X, Agent.Y, floor.X, floor.Y, floor.TileId, Agent.Energy, Agent.Health,
            deficit, Agent.TotalEnergyDeficit, cause == DeathCause.Toxin ? floor.ToxinType : null,
            floor.Toxin, floor.Temperature, Agent.Genome.BasalCost + Agent.Genome.Maintenance, Agent.Genome);
        Death = snapshot;
        CellDied?.Invoke(snapshot);
        return snapshot;
    }
}
