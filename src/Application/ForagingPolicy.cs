using WaveFunctionCollapse.Simulation;

namespace WaveFunctionCollapse.Application;

internal sealed class ForagingPolicy(int seed)
{
    private readonly Random random = new(seed);
    private MovementIntent intent;

    internal MovementIntent Decide(SimulationWorld world)
    {
        if (world.Tick % 10 != 0) return intent;
        var target = world.Observe().OrderByDescending(sample =>
            (sample.Glucose * 4 + sample.Lipids * 9) / (1 + Math.Abs(sample.X + 0.5 - world.Agent.X)
                + Math.Abs(sample.Y + 0.5 - world.Agent.Y)) - sample.Toxin * 10).First();
        double dx = target.X + 0.5 - world.Agent.X, dy = target.Y + 0.5 - world.Agent.Y;
        if (target.Glucose + target.Lipids <= 0) intent = (MovementIntent)random.Next(1, 5);
        else if (Math.Abs(dx) < 0.1 && Math.Abs(dy) < 0.1) intent = MovementIntent.Stay;
        else if (Math.Abs(dx) >= Math.Abs(dy)) intent = dx > 0 ? MovementIntent.East : MovementIntent.West;
        else intent = dy > 0 ? MovementIntent.South : MovementIntent.North;
        return intent;
    }
}
