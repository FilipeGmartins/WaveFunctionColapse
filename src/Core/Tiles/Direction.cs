namespace WaveFunctionCollapse.Core.Tiles;

public enum Direction { North, East, South, West }

public static class Directions
{
    public static Direction Opposite(this Direction direction) => direction switch
    {
        Direction.North => Direction.South,
        Direction.East => Direction.West,
        Direction.South => Direction.North,
        Direction.West => Direction.East,
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };
}
