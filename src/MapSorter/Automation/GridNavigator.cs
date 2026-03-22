using System.Drawing;
using MapSorter.Configuration;

namespace MapSorter.Automation;

public sealed class GridNavigator
{
    private readonly List<Point> _slots;

    public GridNavigator(GridConfig config)
    {
        _slots = BuildSlots(config);
    }

    public int SlotCount => _slots.Count;

    public IReadOnlyList<SlotPosition> GetSlots(int startIndex, int count)
    {
        if (_slots.Count == 0 || count <= 0)
        {
            return Array.Empty<SlotPosition>();
        }

        var list = new List<SlotPosition>(count);
        var idx = Mod(startIndex, _slots.Count);
        var processed = 0;
        while (processed < _slots.Count && list.Count < count)
        {
            list.Add(new SlotPosition(idx, _slots[idx]));
            idx = (idx + 1) % _slots.Count;
            processed++;
        }

        return list;
    }

    private static List<Point> BuildSlots(GridConfig config)
    {
        var list = new List<Point>(config.Rows * config.Cols);
        var slotWidth = config.SlotSize.Width;
        var slotHeight = config.SlotSize.Height;
        var spacingX = config.SlotSpacing.X;
        var spacingY = config.SlotSpacing.Y;
        var origin = config.Origin;

        for (var row = 0; row < config.Rows; row++)
        {
            for (var col = 0; col < config.Cols; col++)
            {
                var relX = (int)Math.Round(col * (slotWidth + spacingX) + slotWidth / 2.0);
                var relY = (int)Math.Round(row * (slotHeight + spacingY) + slotHeight / 2.0);
                list.Add(new Point(origin.X + relX, origin.Y + relY));
            }
        }

        return list;
    }

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}

public sealed record SlotPosition(int Index, Point Position);

