namespace WaveFunctionCollapse.Core.Solver;

public enum WfcStatus { Success, Contradiction }
public readonly record struct TilePin(int X, int Y, int TileId);
public readonly record struct GridPosition(int X, int Y);

/// <summary>On contradiction Grid contains the last failed attempt, including its empty domain.</summary>
public sealed record WfcResult(WfcStatus Status, WaveGrid Grid, int Seed, int Attempts,
    GridPosition? ContradictionAt)
{
    public bool IsSuccess => Status == WfcStatus.Success;
}
