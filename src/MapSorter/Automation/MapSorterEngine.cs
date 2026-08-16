using System.Linq;
using MapSorter.Configuration;
using MapSorter.Input;
using MapSorter.Utilities;

namespace MapSorter.Automation;

public sealed class MapSorterEngine
{
    private readonly SorterConfig _config;
    private GridNavigator _stashNavigator;
    private GridNavigator _inventoryNavigator;
    private GridNavigator _stash12x12Navigator;
    private readonly MouseController _mouse;
    private readonly object _scannerLock = new();
    private int _stashCursor;
    private int _stash12x12Cursor;
    private int _quadStashCursor;
    private int _lastTransferCount;

    public MapSorterEngine(SorterConfig config)
    {
        _config = config;
        _stashNavigator = new GridNavigator(config.StashGrid);
        _inventoryNavigator = new GridNavigator(config.InventoryGrid);
        _stash12x12Navigator = new GridNavigator(config.Stash12x12Grid);
        _mouse = new MouseController();
        _stashCursor = 0;
        _stash12x12Cursor = 0;
        _quadStashCursor = 0;
        _lastTransferCount = 0;
    }

    public void RefreshGrids()
    {
        lock (_scannerLock)
        {
            _stashNavigator = new GridNavigator(_config.StashGrid);
            _inventoryNavigator = new GridNavigator(_config.InventoryGrid);
            _stash12x12Navigator = new GridNavigator(_config.Stash12x12Grid);
            _stashCursor = 0;
            _stash12x12Cursor = 0;
            _quadStashCursor = 0;
            _lastTransferCount = 0;
        }
    }

    public void RunLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var picked = PickUpFromStash(token);
            if (token.IsCancellationRequested)
            {
                break;
            }

            if (picked == 0)
            {
                Console.WriteLine("Found 0 items in stash – waiting before retrying…");
                DelayHelper.SleepMilliseconds(_config.Timings.CycleDelayMs, token);
                continue;
            }

            Console.WriteLine("Moving items back to stash…");
            ReturnInventory(token);
            Console.WriteLine("Cycle complete\n");
            DelayHelper.SleepMilliseconds(_config.Timings.CycleDelayMs, token);
        }
    }

    private int PickUpFromStash(CancellationToken token)
    {
        var slots = _stashNavigator.GetSlots(_stashCursor, _config.MaxItemsPerTrip);
        if (slots.Count == 0)
        {
            return 0;
        }

        Console.WriteLine($"Processing {slots.Count} stash slots…");
        foreach (var slot in slots)
        {
            if (token.IsCancellationRequested)
            {
                break;
            }

            ClickSlot(slot.Position, token);
        }

        var lastIndex = slots[^1].Index;
        _stashCursor = (lastIndex + 1) % _stashNavigator.SlotCount;
        _lastTransferCount = slots.Count;

        return slots.Count;
    }

    private void ReturnInventory(CancellationToken token)
    {
        if (_lastTransferCount == 0)
        {
            Console.WriteLine("No recorded transfers; skipping inventory return.");
            return;
        }

        var targets = _inventoryNavigator.GetSlots(0, _lastTransferCount);
        Console.WriteLine($"Returning {_lastTransferCount} inventory slots…");

        foreach (var target in targets)
        {
            if (token.IsCancellationRequested)
            {
                break;
            }

            ClickSlot(target.Position, token);
        }
    }

    public void TapInventory(CancellationToken token)
    {
        var slots = BuildColumnMajorSlots(_config.InventoryGrid);
        Console.WriteLine($"Ctrl+right-clicking {slots.Count} inventory slots (column-major order)…");
        foreach (var slot in slots)
        {
            if (token.IsCancellationRequested)
            {
                break;
            }

            ClickSlotRight(slot, token);
        }
        Console.WriteLine("Inventory tap complete.");
    }

    public void TapStash(CancellationToken token)
    {
        lock (_scannerLock)
        {
            const int maxSlotsPerTrip = 60; // Inventory capacity
            const int totalSlots = 144; // 12x12 = 144 slots
            
            // Check if we've completed the entire stash
            if (_stash12x12Cursor >= totalSlots)
            {
                Console.WriteLine("Stash 12x12 already completed. Resetting cursor to start.");
                _stash12x12Cursor = 0;
            }

            // Get all slots in column-major order
            var allSlots = BuildColumnMajorSlots(_config.Stash12x12Grid);
            
            // Calculate how many slots to process this trip
            var remainingSlots = totalSlots - _stash12x12Cursor;
            var slotsToProcess = Math.Min(maxSlotsPerTrip, remainingSlots);

            if (slotsToProcess == 0)
            {
                Console.WriteLine("No slots to process.");
                return;
            }

            // Get the subset of slots to process
            var slots = allSlots.Skip(_stash12x12Cursor).Take(slotsToProcess).ToList();
            Console.WriteLine($"Ctrl+right-clicking {slots.Count} stash slots (12x12, starting from position {_stash12x12Cursor + 1}/{totalSlots})…");

            foreach (var slot in slots)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                ClickSlotRight(slot, token);
            }

            // Update cursor position
            if (slots.Count > 0)
            {
                _stash12x12Cursor += slots.Count;
                
                if (_stash12x12Cursor >= totalSlots)
                {
                    Console.WriteLine($"Stash tap complete. All {totalSlots} slots processed. Cursor reset.");
                    _stash12x12Cursor = 0; // Reset for next full cycle
                }
                else
                {
                    Console.WriteLine($"Stash tap complete. Processed {slots.Count} slots. Next start position: {_stash12x12Cursor + 1}/{totalSlots}");
                }
            }
        }
    }

    public void TapQuadStash(CancellationToken token)
    {
        lock (_scannerLock)
        {
            const int maxSlotsPerTrip = 60;
            var totalSlots = _config.StashGrid.Rows * _config.StashGrid.Cols;

            if (_stashCursor >= totalSlots)
            {
                Console.WriteLine("Quad stash already completed. Resetting cursor to start.");
                _quadStashCursor = 0;
            }

            var allSlots = BuildColumnMajorSlots(_config.StashGrid);
            var remainingSlots = totalSlots - _quadStashCursor;
            var slotsToProcess = Math.Min(maxSlotsPerTrip, remainingSlots);

            if (slotsToProcess == 0)
            {
                Console.WriteLine("No slots to process.");
                return;
            }

            var slots = allSlots.Skip(_quadStashCursor).Take(slotsToProcess).ToList();
            Console.WriteLine($"Ctrl+right-clicking {slots.Count} quad stash slots (24x24, top-to-bottom) starting from {_quadStashCursor + 1}/{totalSlots}â€¦");

            foreach (var slot in slots)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                ClickSlotRight(slot, token);
            }

            if (slots.Count > 0)
            {
                _quadStashCursor += slots.Count;
                if (_quadStashCursor >= totalSlots)
                {
                    Console.WriteLine($"Quad stash tap complete. All {totalSlots} slots processed. Cursor reset.");
                    _quadStashCursor = 0;
                }
                else
                {
                    Console.WriteLine($"Quad stash tap complete. Processed {slots.Count} slots. Next start position: {_quadStashCursor + 1}/{totalSlots}");
                }
            }
        }
    }

    private void ClickSlotRight(Point position, CancellationToken token)
    {
        const int taps = 2;
        var between = Math.Max(10, _config.Timings.ClickDelayMs / 5);

        for (var i = 0; i < taps; i++)
        {
            _mouse.CtrlRightClick(position);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (i < taps - 1)
            {
                DelayHelper.SleepMilliseconds(between, token);
            }
        }

        DelayHelper.SleepMilliseconds(_config.Timings.ClickDelayMs, token);
    }

    private static List<Point> BuildColumnMajorSlots(GridConfig config)
    {
        var list = new List<Point>(config.Rows * config.Cols);
        var slotWidth = config.SlotSize.Width;
        var slotHeight = config.SlotSize.Height;
        var spacingX = config.SlotSpacing.X;
        var spacingY = config.SlotSpacing.Y;
        var origin = config.Origin;

        // Column-major: iterate columns first, then rows
        for (var col = 0; col < config.Cols; col++)
        {
            for (var row = 0; row < config.Rows; row++)
            {
                var relX = (int)Math.Round(col * (slotWidth + spacingX) + slotWidth / 2.0);
                var relY = (int)Math.Round(row * (slotHeight + spacingY) + slotHeight / 2.0);
                list.Add(new Point(origin.X + relX, origin.Y + relY));
            }
        }

        return list;
    }

    private void ClickSlot(Point position, CancellationToken token)
    {
        const int taps = 2;
        var between = Math.Max(10, _config.Timings.ClickDelayMs / 5);

        for (var i = 0; i < taps; i++)
        {
            _mouse.CtrlClick(position);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (i < taps - 1)
            {
                DelayHelper.SleepMilliseconds(between, token);
            }
        }

        DelayHelper.SleepMilliseconds(_config.Timings.ClickDelayMs, token);
    }
}
