using System.Security.Cryptography;
using System.Text.Json;
using WaveFunctionCollapse.Core.Tiles;
using WaveFunctionCollapse.Simulation;
using WaveFunctionCollapse.Simulation.Events;

namespace WaveFunctionCollapse.Evolution.Analytics;

public sealed record DeathRecord(int SchemaVersion, string RunId, string CatalogFingerprint,
    double FixedDeltaSeconds, CellDeathEvent Death)
{
    public const int CurrentSchemaVersion = 1;

    public static DeathRecord Capture(SimulationWorld world, string runId, TileCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return new(CurrentSchemaVersion, runId, Fingerprint(catalog), world.FixedDeltaSeconds,
            world.Death ?? throw new InvalidOperationException("Only a dead world can produce a death record."));
    }

    /// <summary>Ordered catalog identity including weights and all supported biological properties.</summary>
    public static string Fingerprint(TileCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog[i] is not BiologicalTile tile)
                    throw new ArgumentException("Evolution requires biological tiles.", nameof(catalog));
                JsonSerializer.Serialize(writer, tile);
            }
            writer.WriteEndArray();
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
