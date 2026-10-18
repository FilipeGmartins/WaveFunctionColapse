using WaveFunctionCollapse.Simulation.Parts;

namespace WaveFunctionCollapse.Simulation.Agents;

/// <summary>Immutable phenotype configuration, suitable for sharing in death snapshots.</summary>
public sealed class CellGenome
{
    public IReadOnlyList<CellPart> Parts { get; }
    public double MaxEnergy { get; }
    public double MaxHealth { get; }
    public double BasalCost { get; }
    public double StarvationDamage { get; }
    public double ToxinResistance { get; }
    public double IdealTemperature { get; }
    public double TemperatureTolerance { get; }
    public double ThermalDamage { get; }
    public double Maintenance { get; }
    public double Speed { get; }
    public double MovementCost { get; }
    public double GlucoseRate { get; }
    public double LipidRate { get; }
    public int PerceptionRange { get; }

    public CellGenome(IEnumerable<CellPart> parts, double maxEnergy = 50, double maxHealth = 30,
        double basalCost = 0.5, double starvationDamage = 2, double toxinResistance = 0,
        double idealTemperature = 25, double temperatureTolerance = 10, double thermalDamage = 0.1)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var copy = parts.ToArray();
        if (copy.Any(p => p is null)) throw new ArgumentException("Parts cannot be null.", nameof(parts));
        Parts = Array.AsReadOnly(copy);
        MaxEnergy = Validation.Positive(maxEnergy, nameof(maxEnergy));
        MaxHealth = Validation.Positive(maxHealth, nameof(maxHealth));
        BasalCost = Validation.Nonnegative(basalCost, nameof(basalCost));
        StarvationDamage = Validation.Nonnegative(starvationDamage, nameof(starvationDamage));
        ToxinResistance = Validation.Nonnegative(toxinResistance, nameof(toxinResistance));
        if (toxinResistance > 1) throw new ArgumentOutOfRangeException(nameof(toxinResistance));
        if (!double.IsFinite(idealTemperature) || idealTemperature < -273.15)
            throw new ArgumentOutOfRangeException(nameof(idealTemperature));
        IdealTemperature = idealTemperature;
        TemperatureTolerance = Validation.Nonnegative(temperatureTolerance, nameof(temperatureTolerance));
        ThermalDamage = Validation.Nonnegative(thermalDamage, nameof(thermalDamage));
        Maintenance = Validation.Nonnegative(copy.Sum(p => p.Maintenance), nameof(parts));
        Validation.Nonnegative(BasalCost + Maintenance, nameof(basalCost));
        Speed = Validation.Nonnegative(copy.OfType<Flagellum>().Sum(p => p.Speed), nameof(parts));
        MovementCost = Validation.Nonnegative(copy.OfType<Flagellum>().Sum(p => p.MovementCost), nameof(parts));
        GlucoseRate = Validation.Nonnegative(copy.OfType<Mouth>().Sum(p => p.GlucoseRate), nameof(parts));
        LipidRate = Validation.Nonnegative(copy.OfType<Mouth>().Sum(p => p.LipidRate), nameof(parts));
        PerceptionRange = copy.OfType<ChemicalReceptor>().Select(p => p.Range).DefaultIfEmpty(0).Max();
    }
}
