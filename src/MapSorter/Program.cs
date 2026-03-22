using MapSorter.Automation;
using MapSorter.Calibration;
using MapSorter.Configuration;
using MapSorter.Input;

namespace MapSorter;

internal static class Program
{
    private static MapSorterEngine? _engine;
    private static SorterRunner? _runner;
    private static HotkeyLoop? _hotkeys;
    private static CalibrationService? _calibration;
    private static Task? _tapTask;
    private static CancellationTokenSource? _tapCts;

    [STAThread]
    private static void Main()
    {
        Console.Title = "Path of Exile – Map Sorter";
        var config = LoadConfig();

        _engine = new MapSorterEngine(config);
        _runner = new SorterRunner(_engine);
        _calibration = new CalibrationService(config, _engine);

        _hotkeys = new HotkeyLoop(
            new[]
            {
                new HotkeyRegistration(config.Hotkeys.Start, () => _runner?.Start()),
                new HotkeyRegistration(config.Hotkeys.Stop, StopSorting),
                new HotkeyRegistration(config.Hotkeys.CalibrateStash, () => RunCalibration(() => _calibration?.CalibrateStash(), "stash 24x24")),
                new HotkeyRegistration(config.Hotkeys.CalibrateStash12x12, () => RunCalibration(() => _calibration?.CalibrateStash12x12(), "stash 12x12")),
                new HotkeyRegistration(config.Hotkeys.CalibrateInventory, () => RunCalibration(() => _calibration?.CalibrateInventory(), "inventory")),
                new HotkeyRegistration(config.Hotkeys.TapInventory, TriggerInventoryTap),
                new HotkeyRegistration(config.Hotkeys.TapStash, TriggerStashTap)
            });

        PrintBanner(config);

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Shutdown();
        };

        Console.WriteLine("Press Ctrl+C to exit the application.");
        Thread.Sleep(Timeout.Infinite);
    }

    private static void PrintBanner(SorterConfig config)
    {
        Console.WriteLine("Path of Exile Map Sorter");
        Console.WriteLine("========================");
        Console.WriteLine($"Start hotkey: {config.Hotkeys.Start.ToUpperInvariant()}");
        Console.WriteLine($"Stop hotkey : {config.Hotkeys.Stop.ToUpperInvariant()}");
        Console.WriteLine($"Calibrate stash 24x24  : {config.Hotkeys.CalibrateStash.ToUpperInvariant()}");
        Console.WriteLine($"Calibrate stash 12x12  : {config.Hotkeys.CalibrateStash12x12.ToUpperInvariant()}");
        Console.WriteLine($"Calibrate inventory    : {config.Hotkeys.CalibrateInventory.ToUpperInvariant()}");
        Console.WriteLine($"Right-click inventory  : {config.Hotkeys.TapInventory.ToUpperInvariant()}");
        Console.WriteLine($"Right-click stash      : {config.Hotkeys.TapStash.ToUpperInvariant()}");
        Console.WriteLine();
    }

    private static SorterConfig LoadConfig()
    {
        var defaultConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        var userConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PoEMapSorter");
        Directory.CreateDirectory(userConfigDir);
        var userConfigPath = Path.Combine(userConfigDir, "config.json");

        var sourcePath = File.Exists(userConfigPath) ? userConfigPath : defaultConfigPath;
        var config = SorterConfig.Load(sourcePath);
        config.SourcePath = userConfigPath;

        if (!File.Exists(userConfigPath))
        {
            ConfigWriter.Save(config);
            Console.WriteLine($"User config created at {userConfigPath}");
        }

        return config;
    }

    private static void Shutdown()
    {
        StopSorting();
        _hotkeys?.Dispose();
        Environment.Exit(0);
    }


    private static void RunCalibration(Action? calibrateAction, string label)
    {
        if (calibrateAction == null)
        {
            return;
        }

        if (_runner is { IsRunning: true })
        {
            Console.WriteLine("Stopping sorter before calibration…");
            _runner.Stop();
        }

        calibrateAction();
        Console.WriteLine($"Calibration for {label} finished. Press Start to run with the new grid.");
    }

    private static void TriggerInventoryTap()
    {
        if (_engine == null)
        {
            return;
        }

        if (_runner is { IsRunning: true })
        {
            Console.WriteLine("Stopping sorter before manual inventory tap…");
            _runner.Stop();
        }

        if (_tapTask is { IsCompleted: false })
        {
            Console.WriteLine("Inventory tap already in progress.");
            return;
        }

        _tapCts = new CancellationTokenSource();
        _tapTask = Task.Run(() =>
        {
            try
            {
                _engine.TapInventory(_tapCts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Inventory tap failed: {ex.Message}");
            }
        }, _tapCts.Token);
    }

    private static void TriggerStashTap()
    {
        if (_engine == null)
        {
            return;
        }

        if (_runner is { IsRunning: true })
        {
            Console.WriteLine("Stopping sorter before manual stash tap…");
            _runner.Stop();
        }

        if (_tapTask is { IsCompleted: false })
        {
            Console.WriteLine("Stash tap already in progress.");
            return;
        }

        _tapCts = new CancellationTokenSource();
        _tapTask = Task.Run(() =>
        {
            try
            {
                _engine.TapStash(_tapCts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Stash tap failed: {ex.Message}");
            }
        }, _tapCts.Token);
    }

    private static void StopSorting()
    {
        Console.WriteLine("Stopping sorter…");
        StopTapTask();
        _runner?.Stop();
    }

    private static void StopTapTask()
    {
        _tapCts?.Cancel();
        try
        {
            _tapTask?.Wait();
        }
        catch
        {
            // ignored
        }
        finally
        {
            _tapTask = null;
            _tapCts?.Dispose();
            _tapCts = null;
        }
    }
}

internal sealed class SorterRunner
{
    private readonly MapSorterEngine _engine;
    private Task? _worker;
    private CancellationTokenSource? _cts;
    private readonly object _gate = new();

    public SorterRunner(MapSorterEngine engine)
    {
        _engine = engine;
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _cts != null;
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_worker is { IsCompleted: false })
            {
                Console.WriteLine("Sorter already running.");
                return;
            }

            Console.WriteLine("Starting sorter loop…");
            _cts = new CancellationTokenSource();
            _worker = Task.Run(() => _engine.RunLoop(_cts.Token), _cts.Token);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_cts == null)
            {
                Console.WriteLine("Sorter is not running.");
                return;
            }

            Console.WriteLine("Stopping sorter…");
            _cts.Cancel();
            try
            {
                _worker?.Wait();
            }
            catch (AggregateException ae) when (ae.InnerExceptions.All(e => e is TaskCanceledException))
            {
                // ignored
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                Console.WriteLine("Stopped by user");
            }
        }
    }
}


