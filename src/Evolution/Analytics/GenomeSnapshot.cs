using WaveFunctionCollapse.Simulation.Agents;
using WaveFunctionCollapse.Simulation.Parts;

namespace WaveFunctionCollapse.Evolution.Analytics;

public enum PartKind { Flagellum, Mouth, ChemicalReceptor }

public sealed record PartSnapshot(PartKind Kind, double Maintenance, double Speed = 0,
    double MovementCost = 0, double GlucoseRate = 0, double LipidRate = 0, int Range = 0)
{
    public static PartSnapshot From(CellPart part) => part switch
    {
        Flagellum p => new(PartKind.Flagellum, p.Maintenance, Speed: p.Speed, MovementCost: p.MovementCost),
        Mouth p => new(PartKind.Mouth, p.Maintenance, GlucoseRate: p.GlucoseRate, LipidRate: p.LipidRate),
        ChemicalReceptor p => new(PartKind.ChemicalReceptor, p.Maintenance, Range: p.Range),
        _ => throw new NotSupportedException($"No snapshot codec for part {part.GetType().Name}.")
    };

    public CellPart Restore() => Kind switch
    {
        PartKind.Flagellum => new Flagellum(Speed, MovementCost, Maintenance),
        PartKind.Mouth => new Mouth(GlucoseRate, LipidRate, Maintenance),
        PartKind.ChemicalReceptor => new ChemicalReceptor(Range, Maintenance),
        _ => throw new ArgumentOutOfRangeException(nameof(Kind))
    };
}

public sealed record GenomeSnapshot(PartSnapshot[] Parts, double MaxEnergy, double MaxHealth, double BasalCost,
    double StarvationDamage, double ToxinResistance, double IdealTemperature, double TemperatureTolerance,
    double ThermalDamage)
{
    public static GenomeSnapshot From(CellGenome genome) => new(genome.Parts.Select(PartSnapshot.From).ToArray(),
        genome.MaxEnergy, genome.MaxHealth, genome.BasalCost, genome.StarvationDamage, genome.ToxinResistance,
        genome.IdealTemperature, genome.TemperatureTolerance, genome.ThermalDamage);

    public CellGenome Restore()
    {
        if (Parts is null || Parts.Any(p => p is null)) throw new ArgumentException("Missing genome parts.");
        return new(Parts.Select(p => p.Restore()), MaxEnergy, MaxHealth, BasalCost, StarvationDamage,
            ToxinResistance, IdealTemperature, TemperatureTolerance, ThermalDamage);
    }
}
