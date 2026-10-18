namespace WaveFunctionCollapse.Core.Tiles;

/// <summary>Immutable initial conditions. Mutable resources belong to Simulation.</summary>
public sealed record BiologicalTile : TileDefinition
{
    public double Glucose { get; }
    public double Lipids { get; }
    public double Toxin { get; }
    public string? ToxinType { get; }
    public double Density { get; }
    public double Temperature { get; }

    public BiologicalTile(string name, double weight, Socket north, Socket east, Socket south, Socket west,
        double glucose = 0, double lipids = 0, double toxin = 0, string? toxinType = null,
        double density = 1, double temperature = 25)
        : base(name, weight, north, east, south, west)
    {
        ValidateNonnegative(glucose, nameof(glucose));
        ValidateNonnegative(lipids, nameof(lipids));
        ValidateNonnegative(toxin, nameof(toxin));
        ValidateNonnegative(density, nameof(density));
        if (!double.IsFinite(temperature) || temperature < -273.15)
            throw new ArgumentOutOfRangeException(nameof(temperature));
        if (toxin > 0 && string.IsNullOrWhiteSpace(toxinType))
            throw new ArgumentException("A toxin type is required when toxin concentration is positive.", nameof(toxinType));
        (Glucose, Lipids, Toxin, ToxinType, Density, Temperature) =
            (glucose, lipids, toxin, toxinType, density, temperature);
    }

    private static void ValidateNonnegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
    }
}
