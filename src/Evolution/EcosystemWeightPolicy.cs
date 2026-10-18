using WaveFunctionCollapse.Core.Tiles;
using WaveFunctionCollapse.Evolution.Analytics;
using WaveFunctionCollapse.Simulation.Events;

namespace WaveFunctionCollapse.Evolution;

public static class EcosystemWeightPolicy
{
    internal static TileCatalog Adapt(TileCatalog current, IReadOnlyList<DeathRecord> records)
    {
        double starvation = records.Count(r => r.Death.Cause == DeathCause.Starvation) / (double)records.Count;
        double maxWeight = Enumerable.Range(0, current.Count).Max(i => current[i].Weight);
        var tiles = new BiologicalTile[current.Count];
        for (int i = 0; i < current.Count; i++)
        {
            var t = (BiologicalTile)current[i];
            double toxinDeaths = records.Count(r => r.Death.Cause == DeathCause.Toxin
                && r.Death.FatalToxinType == t.ToxinType) / (double)records.Count;
            double thermalDeaths = records.Count(r => r.Death.Cause == DeathCause.Temperature
                && r.Death.TileId == i) / (double)records.Count;
            double nutrientBoost = t.Glucose + t.Lipids > 0 && t.Toxin == 0 ? 0.2 * starvation : 0;
            double factor = 1 + nutrientBoost - (t.Toxin > 0 ? 0.2 * toxinDeaths : 0) - 0.2 * thermalDeaths;
            // Normalize and retain a probability floor; no tile or socket is removed.
            double weight = Math.Clamp(t.Weight / maxWeight * factor, 1e-6, 2);
            tiles[i] = new(t.Name, weight, t.North, t.East, t.South, t.West, t.Glucose, t.Lipids,
                t.Toxin, t.ToxinType, t.Density, t.Temperature);
        }
        return new TileCatalog(tiles);
    }
}
