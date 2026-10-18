namespace WaveFunctionCollapse.Simulation.Agents;

public sealed class CellAgent
{
    public CellGenome Genome { get; }
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public double Energy { get; internal set; }
    public double Health { get; internal set; }
    public double TotalEnergyDeficit { get; internal set; }
    public bool IsAlive => Health > 0;

    // Creation belongs to SimulationWorld, which validates the spawn position.
    internal CellAgent(CellGenome genome, double x, double y)
    {
        Genome = genome;
        X = x;
        Y = y;
        Energy = genome.MaxEnergy;
        Health = genome.MaxHealth;
    }
}
