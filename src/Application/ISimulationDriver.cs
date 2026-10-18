using WaveFunctionCollapse.Simulation;

namespace WaveFunctionCollapse.Application;

/// <summary>Optional interactive host. Calls step at fixed intervals; null intent uses the automatic policy.</summary>
public interface ISimulationDriver
{
    bool Run(SimulationWorld world, int maxSteps, int totalGenerations, Action<MovementIntent?> step);
}
