using WaveFunctionCollapse.Core.Tiles;

namespace WaveFunctionCollapse.Application;

internal static class DemoCatalog
{
    internal static TileCatalog Create()
    {
        var water = new Socket(0);
        var land = new Socket(1);
        return new([
            new BiologicalTile("Water", 5, water, water, water, water, glucose: 2, density: 0.5),
            new BiologicalTile("Nutrients", 3, land, land, land, land, glucose: 15, lipids: 4),
            new BiologicalTile("Toxic ground", 1, land, land, land, land, toxin: 3, toxinType: "Acid"),
            new BiologicalTile("Shore north", 1, water, land, land, land, glucose: 5),
            new BiologicalTile("Shore east", 1, land, water, land, land, glucose: 5),
            new BiologicalTile("Shore south", 1, land, land, water, land, glucose: 5),
            new BiologicalTile("Shore west", 1, land, land, land, water, glucose: 5)
        ]);
    }
}
