using MapSorter.Automation;
using MapSorter.Configuration;
using MapSorter.UI;

namespace MapSorter.Calibration;

public sealed class CalibrationService
{
    private readonly SorterConfig _config;
    private readonly MapSorterEngine _engine;

    public CalibrationService(SorterConfig config, MapSorterEngine engine)
    {
        _config = config;
        _engine = engine;
    }

    public void CalibrateStash()
    {
        CalibrateGrid(_config.StashGrid, "stash");
    }

    public void CalibrateStash12x12()
    {
        CalibrateGrid(_config.Stash12x12Grid, "stash12x12");
    }

    public void CalibrateInventory()
    {
        CalibrateGrid(_config.InventoryGrid, "inventory");
    }

    private void CalibrateGrid(GridConfig grid, string name)
    {
        Console.WriteLine($"Calibration started for {name}. Drag the highlighted {_label(name)} grid into place, use arrow keys for fine tuning, press Enter to confirm.");

        var origin = GridAlignmentOverlay.Align(grid, name);
        if (origin is null)
        {
            Console.WriteLine("Calibration canceled.");
            return;
        }

        grid.Origin = new PointConfig(origin.Value.X, origin.Value.Y);
        Console.WriteLine($"{name} grid updated: origin=({grid.Origin.X},{grid.Origin.Y}), slotSize={grid.SlotSize.Width}x{grid.SlotSize.Height}px");

        ConfigWriter.Save(_config);
        _engine.RefreshGrids();
    }

    private static string _label(string name)
    {
        return name.Equals("stash", StringComparison.OrdinalIgnoreCase) ? "24x24" :
               name.Equals("stash12x12", StringComparison.OrdinalIgnoreCase) ? "12x12" :
               "5x12";
    }
}


