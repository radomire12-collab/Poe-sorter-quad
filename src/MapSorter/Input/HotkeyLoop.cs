using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MapSorter.Input;

public sealed class HotkeyLoop : IDisposable
{
    private readonly IReadOnlyList<HotkeyRegistration> _registrations;
    private readonly Thread _thread;
    private readonly AutoResetEvent _ready = new(false);
    private HotkeyApplicationContext? _context;
    private bool _disposed;

    public HotkeyLoop(IEnumerable<HotkeyRegistration> registrations)
    {
        _registrations = registrations.ToList();
        if (_registrations.Count == 0)
        {
            throw new ArgumentException("At least one hotkey must be registered.");
        }

        _thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "HotkeyLoopThread"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.WaitOne();
    }

    private void RunMessageLoop()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var context = new HotkeyApplicationContext(_registrations);
        _context = context;
        _ready.Set();
        Application.Run(context);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_context != null)
        {
            _context.ExitThread();
        }

        if (Thread.CurrentThread != _thread && _thread.IsAlive)
        {
            _thread.Join();
        }
    }
}

public sealed record HotkeyRegistration(string Combination, Action Callback);

internal sealed class HotkeyApplicationContext : ApplicationContext
{
    private readonly HotkeyWindow _window;
    private readonly List<RegisteredHotkey> _hotkeys;

    public HotkeyApplicationContext(IEnumerable<HotkeyRegistration> registrations)
    {
        _window = new HotkeyWindow(OnHotkeyPressed);
        _hotkeys = ParseRegistrations(registrations).ToList();
        RegisterHotkeys();
    }

    private IEnumerable<RegisteredHotkey> ParseRegistrations(IEnumerable<HotkeyRegistration> registrations)
    {
        var id = 1;
        foreach (var registration in registrations)
        {
            var parsed = HotkeyParser.Parse(registration.Combination);
            yield return new RegisteredHotkey(id++, parsed.Modifiers, parsed.Key, registration.Callback);
        }
    }

    private void RegisterHotkeys()
    {
        foreach (var hotkey in _hotkeys)
        {
            if (!NativeMethodsWrapper.RegisterHotKey(_window.Handle, hotkey.Id, hotkey.Modifiers, (uint)hotkey.Key))
            {
                throw new InvalidOperationException($"Failed to register hotkey: {hotkey}");
            }
        }
    }

    private void OnHotkeyPressed(int id)
    {
        var hotkey = _hotkeys.FirstOrDefault(h => h.Id == id);
        if (hotkey == null)
        {
            return;
        }

        Task.Run(hotkey.Callback);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var hotkey in _hotkeys)
            {
                NativeMethodsWrapper.UnregisterHotKey(_window.Handle, hotkey.Id);
            }

            _window.DestroyHandle();
        }

        base.Dispose(disposing);
    }
}

internal sealed class RegisteredHotkey
{
    public int Id { get; }
    public uint Modifiers { get; }
    public Keys Key { get; }
    public Action Callback { get; }

    public RegisteredHotkey(int id, uint modifiers, Keys key, Action callback)
    {
        Id = id;
        Modifiers = modifiers;
        Key = key;
        Callback = callback;
    }

    public override string ToString() => $"ID={Id}, Key={Key}, Modifiers={Modifiers}";
}

internal static class HotkeyParser
{
    private static readonly Dictionary<string, uint> ModifierMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CTRL"] = 0x0002,
        ["CONTROL"] = 0x0002,
        ["ALT"] = 0x0001,
        ["SHIFT"] = 0x0004,
        ["WIN"] = 0x0008,
        ["WINDOWS"] = 0x0008
    };

    public static ParsedHotkey Parse(string combination)
    {
        if (string.IsNullOrWhiteSpace(combination))
        {
            throw new ArgumentException("Hotkey combination cannot be empty.");
        }

        var parts = combination.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            throw new ArgumentException($"Invalid hotkey: {combination}");
        }

        uint modifiers = 0;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (!ModifierMap.TryGetValue(parts[i], out var modifier))
            {
                throw new ArgumentException($"Unknown modifier '{parts[i]}' in hotkey '{combination}'.");
            }

            modifiers |= modifier;
        }

        var key = ParseKey(parts[^1]);
        return new ParsedHotkey(modifiers, key);
    }

    private static Keys ParseKey(string keyPart)
    {
        if (Enum.TryParse<Keys>(keyPart, true, out var parsed))
        {
            return parsed;
        }

        if (keyPart.Length == 1)
        {
            var ch = keyPart[0];
            if (char.IsDigit(ch))
            {
                return (Keys)((int)Keys.D0 + (ch - '0'));
            }

            if (char.IsLetter(ch))
            {
                return (Keys)Enum.Parse(typeof(Keys), ch.ToString().ToUpperInvariant());
            }
        }

        throw new ArgumentException($"Invalid key '{keyPart}'.");
    }
}

internal readonly struct ParsedHotkey
{
    public uint Modifiers { get; }
    public Keys Key { get; }

    public ParsedHotkey(uint modifiers, Keys key)
    {
        Modifiers = modifiers;
        Key = key;
    }
}

internal sealed class HotkeyWindow : NativeWindow
{
    private const int WM_HOTKEY = 0x0312;
    private readonly Action<int> _onHotkey;

    public HotkeyWindow(Action<int> onHotkey)
    {
        _onHotkey = onHotkey;
        CreateHandle(new CreateParams());
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            _onHotkey(m.WParam.ToInt32());
        }

        base.WndProc(ref m);
    }
}

internal static class NativeMethodsWrapper
{
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

