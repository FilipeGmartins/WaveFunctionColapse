using System.Numerics;
using WaveFunctionCollapse.Core.Tiles;

namespace WaveFunctionCollapse.Core.Solver;

public static class ShannonEntropy
{
    /// <summary>Natural-log Shannon entropy; an empty domain has no defined entropy.</summary>
    public static double Calculate(ulong possibilities, TileCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (possibilities == 0 || (possibilities & ~catalog.AllMask) != 0)
            throw new ArgumentOutOfRangeException(nameof(possibilities));
        double sum = 0, weightedLog = 0;
        while (possibilities != 0)
        {
            int id = BitOperations.TrailingZeroCount(possibilities);
            double weight = catalog.Weight(id);
            sum += weight;
            weightedLog += weight * Math.Log(weight);
            possibilities &= possibilities - 1;
        }
        return Math.Max(0, Math.Log(sum) - weightedLog / sum);
    }
}
