namespace WaveFunctionCollapse.Core.Tiles;

/// <summary>Matching socket IDs on facing edges permit adjacency.</summary>
public readonly record struct Socket(int Id);

public record TileDefinition
{
    public string Name { get; }
    public double Weight { get; }
    public Socket North { get; }
    public Socket East { get; }
    public Socket South { get; }
    public Socket West { get; }

    public TileDefinition(string name, double weight, Socket north, Socket east, Socket south, Socket west)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!double.IsFinite(weight) || weight <= 0)
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be finite and positive.");
        (Name, Weight, North, East, South, West) = (name, weight, north, east, south, west);
    }

    public Socket GetSocket(Direction direction) => direction switch
    {
        Direction.North => North,
        Direction.East => East,
        Direction.South => South,
        Direction.West => West,
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };
}
