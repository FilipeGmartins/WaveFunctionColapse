using System.Text.Json;
using WaveFunctionCollapse.Core.Tiles;
using WaveFunctionCollapse.Evolution.Analytics;
using WaveFunctionCollapse.Simulation.Agents;
using WaveFunctionCollapse.Simulation.Events;
using WaveFunctionCollapse.Simulation.Parts;

namespace WaveFunctionCollapse.Evolution;

public sealed record EvolutionResult(CellGenome Genome, TileCatalog Catalog, int DeathsAnalyzed,
    IReadOnlyList<string> Changes);

/// <summary>Deterministic corrective mutation policy. No population selection or fitness claims.</summary>
public sealed class GenomeEvolver
{
    public EvolutionResult EvolveFromLog(CellGenome parent, TileCatalog current, string path, string runId, int generation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        return Evolve(parent, current, DeathLogger.Read(path)
            .Where(r => r.RunId == runId && r.Death.Generation == generation));
    }

    public EvolutionResult Evolve(CellGenome parent, TileCatalog current, IEnumerable<DeathRecord> deathRecords)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(deathRecords);
        var records = deathRecords.ToArray();
        string fingerprint = DeathRecord.Fingerprint(current);
        string parentSnapshot = JsonSerializer.Serialize(GenomeSnapshot.From(parent));
        foreach (var record in records)
        {
            DeathLogger.Validate(record);
            if (record.CatalogFingerprint != fingerprint || record.Death.TileId >= current.Count)
                throw new ArgumentException("Logs must belong to the current catalog, including its weights.", nameof(deathRecords));
            if (JsonSerializer.Serialize(GenomeSnapshot.From(record.Death.Genome)) != parentSnapshot)
                throw new ArgumentException("A corrective cohort must contain deaths from the supplied parent genome.", nameof(deathRecords));
        }
        if (records.Length == 0) return new(parent, current, 0, Array.Empty<string>());
        double starvation = records.Count(r => r.Death.Cause == DeathCause.Starvation) / (double)records.Length;
        double toxin = records.Count(r => r.Death.Cause == DeathCause.Toxin) / (double)records.Length;
        double thermal = records.Count(r => r.Death.Cause == DeathCause.Temperature) / (double)records.Length;
        var parts = parent.Parts.ToList();
        var changes = new List<string>();
        double basal = parent.BasalCost, maxEnergy = parent.MaxEnergy;
        if (starvation > 0)
        {
            if (!parts.OfType<Mouth>().Any()) parts.Add(new Mouth());
            else for (int i = 0; i < parts.Count; i++)
                if (parts[i] is Mouth mouth)
                    parts[i] = new Mouth(Grow(Math.Max(0.1, mouth.GlucoseRate), 1 + 0.1 * starvation, 8),
                        Grow(Math.Max(0.025, mouth.LipidRate), 1 + 0.1 * starvation, 2),
                        Grow(Math.Max(0.01, mouth.Maintenance), 1 + 0.02 * starvation, 2));
            if (!parts.OfType<Flagellum>().Any()) parts.Add(new Flagellum());
            basal = Math.Min(basal, Math.Max(0.05, basal * (1 - 0.05 * starvation)));
            maxEnergy = Grow(maxEnergy, 1 + 0.1 * starvation, 200);
            changes.Add("Starvation: improve feeding and energy reserve; reduce basal cost. Mouth throughput adds maintenance.");
        }
        if ((starvation > 0 || toxin > 0) && !parts.OfType<ChemicalReceptor>().Any())
        {
            parts.Add(new ChemicalReceptor());
            changes.Add("Add chemical perception; receptor maintenance increases continuous ATP use.");
        }
        double resistance = Math.Max(parent.ToxinResistance, Math.Min(0.9, parent.ToxinResistance + 0.1 * toxin));
        double tolerance = Math.Max(parent.TemperatureTolerance, Math.Min(40, parent.TemperatureTolerance + 2 * thermal));
        // Protective adaptations have a recurring metabolic cost rather than being free upgrades.
        basal += (resistance - parent.ToxinResistance) * 0.5 + (tolerance - parent.TemperatureTolerance) * 0.01;
        if (toxin > 0) changes.Add("Toxin: increase resistance and its basal ATP cost; reduce weights of the fatal toxin type.");
        if (thermal > 0) changes.Add("Temperature: increase tolerance and its basal ATP cost; reduce weights of fatal temperature tiles.");
        var genome = new CellGenome(parts, maxEnergy, parent.MaxHealth, basal, parent.StarvationDamage,
            resistance, parent.IdealTemperature, tolerance, parent.ThermalDamage);
        return new(genome, EcosystemWeightPolicy.Adapt(current, records), records.Length, changes.AsReadOnly());
    }

    private static double Grow(double value, double factor, double ceiling) => Math.Max(value, Math.Min(ceiling, value * factor));
}
