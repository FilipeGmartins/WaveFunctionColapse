namespace WaveFunctionCollapse.Simulation.Parts;

/// <summary>Immutable equipped module. Maintenance is ATP per simulated second.</summary>
public abstract record CellPart
{
    public double Maintenance { get; }
    protected CellPart(double maintenance) => Maintenance = Validation.Nonnegative(maintenance, nameof(maintenance));
}

public sealed record Flagellum : CellPart
{
    public double Speed { get; }
    public double MovementCost { get; }
    public Flagellum(double speed = 1, double movementCost = 0.6, double maintenance = 0.15)
        : base(maintenance)
    {
        Speed = Validation.Positive(speed, nameof(speed));
        MovementCost = Validation.Nonnegative(movementCost, nameof(movementCost));
    }
}

public sealed record Mouth : CellPart
{
    public double GlucoseRate { get; }
    public double LipidRate { get; }
    public Mouth(double glucoseRate = 1, double lipidRate = 0.25, double maintenance = 0.1)
        : base(maintenance)
    {
        GlucoseRate = Validation.Nonnegative(glucoseRate, nameof(glucoseRate));
        LipidRate = Validation.Nonnegative(lipidRate, nameof(lipidRate));
    }
}

public sealed record ChemicalReceptor : CellPart
{
    public int Range { get; }
    public ChemicalReceptor(int range = 2, double maintenance = 0.05) : base(maintenance)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(range);
        Range = range;
    }
}
