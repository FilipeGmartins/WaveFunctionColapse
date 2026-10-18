using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WaveFunctionCollapse.Simulation.Agents;

namespace WaveFunctionCollapse.Evolution.Analytics;

/// <summary>Single-writer JSONL log. Each successful Append is flushed. Does not own the simulation.</summary>
public sealed class DeathLogger : IDisposable
{
    private static readonly JsonSerializerOptions Options = CreateOptions();
    private readonly StreamWriter writer;

    public DeathLogger(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        // A partial final line must be repaired explicitly before appending more records.
        if (File.Exists(fullPath))
        {
            using var check = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (check.Length > 0)
            {
                check.Seek(-1, SeekOrigin.End);
                if (check.ReadByte() != '\n') throw new InvalidDataException("Log has an incomplete final line.");
            }
        }
        writer = new StreamWriter(new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(false)) { AutoFlush = true };
    }

    public void Append(DeathRecord record)
    {
        Validate(record);
        // Complete serialization first, so unsupported parts cannot leave a half-written JSON record.
        string json = JsonSerializer.Serialize(record, Options);
        writer.WriteLine(json);
    }

    public static IEnumerable<DeathRecord> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        int lineNumber = 0;
        using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            DeathRecord record;
            try
            {
                record = JsonSerializer.Deserialize<DeathRecord>(line, Options)
                    ?? throw new JsonException("Null record.");
                Validate(record);
            }
            catch (Exception error) when (error is JsonException or ArgumentException or InvalidDataException or NotSupportedException)
            {
                throw new InvalidDataException($"Invalid death log at line {lineNumber}: {error.Message}", error);
            }
            yield return record;
        }
    }

    internal static void Validate(DeathRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.SchemaVersion != DeathRecord.CurrentSchemaVersion) throw new InvalidDataException("Unsupported death schema version.");
        ArgumentException.ThrowIfNullOrWhiteSpace(record.RunId);
        if (record.CatalogFingerprint is null || record.CatalogFingerprint.Length != 64
            || record.CatalogFingerprint.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Invalid catalog fingerprint.");
        if (!double.IsFinite(record.FixedDeltaSeconds) || record.FixedDeltaSeconds <= 0 || record.FixedDeltaSeconds > 1)
            throw new InvalidDataException("Invalid timestep.");
        var d = record.Death ?? throw new InvalidDataException("Missing death snapshot.");
        if (d.Genome is null || !Enum.IsDefined(d.Cause) || d.Generation < 0 || d.Tick < 1
            || d.TileId < 0 || d.TileX < 0 || d.TileY < 0) throw new InvalidDataException("Invalid death metadata.");
        double[] nonnegative = [d.ElapsedSeconds, d.X, d.Y, d.Energy, d.Health, d.EnergyDeficit,
            d.TotalEnergyDeficit, d.ToxinConcentration, d.MaintenancePerSecond];
        if (nonnegative.Any(v => !double.IsFinite(v) || v < 0) || !double.IsFinite(d.Temperature)
            || d.Health != 0 || d.X < d.TileX || d.X >= (double)d.TileX + 1
            || d.Y < d.TileY || d.Y >= (double)d.TileY + 1 || d.TotalEnergyDeficit < d.EnergyDeficit
            || d.Energy > d.Genome.MaxEnergy
            || Math.Abs(d.ElapsedSeconds - d.Tick * record.FixedDeltaSeconds) > 1e-8)
            throw new InvalidDataException("Inconsistent death metrics.");
        if (d.Cause == Simulation.Events.DeathCause.Toxin && string.IsNullOrWhiteSpace(d.FatalToxinType))
            throw new InvalidDataException("Missing fatal toxin type.");
        if (d.Cause != Simulation.Events.DeathCause.Toxin && d.FatalToxinType is not null)
            throw new InvalidDataException("A nontoxic death cannot name a fatal toxin.");
    }

    public void Dispose() => writer.Dispose();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.Converters.Add(new GenomeConverter());
        return options;
    }

    private sealed class GenomeConverter : JsonConverter<CellGenome>
    {
        public override CellGenome Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            (JsonSerializer.Deserialize<GenomeSnapshot>(ref reader, options) ?? throw new JsonException("Missing genome.")).Restore();

        public override void Write(Utf8JsonWriter writer, CellGenome value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, GenomeSnapshot.From(value), options);
    }
}
