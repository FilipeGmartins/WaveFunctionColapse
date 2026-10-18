using WaveFunctionCollapse.Simulation.Agents;

namespace WaveFunctionCollapse.Simulation.Events;

public enum DeathCause { Starvation, Toxin, Temperature }

/// <summary>Immutable snapshot; time and rates use simulated seconds, never wall-clock time.</summary>
public sealed record CellDeathEvent(int Generation, int MapSeed, int SimulationSeed, long Tick,
    double ElapsedSeconds, DeathCause Cause, double X, double Y, int TileX, int TileY,
    int TileId, double Energy, double Health, double EnergyDeficit, double TotalEnergyDeficit,
    string? FatalToxinType, double ToxinConcentration, double Temperature,
    double MaintenancePerSecond, CellGenome Genome);
