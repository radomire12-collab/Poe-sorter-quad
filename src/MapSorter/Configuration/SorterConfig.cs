using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MapSorter.Configuration;

public sealed class SorterConfig
{
    [JsonPropertyName("maxItemsPerTrip")]
    public int MaxItemsPerTrip { get; set; } = 60;

    [JsonPropertyName("hotkeys")]
    public HotkeyConfig Hotkeys { get; set; } = new();

    [JsonPropertyName("timings")]
    public TimingConfig Timings { get; set; } = new();

    [JsonPropertyName("stashGrid")]
    public GridConfig StashGrid { get; set; } = new();

    [JsonPropertyName("stash12x12Grid")]
    public GridConfig Stash12x12Grid { get; set; } = new() { Rows = 12, Cols = 12 };

    [JsonPropertyName("inventoryGrid")]
    public GridConfig InventoryGrid { get; set; } = new() { Rows = 5 };

    [JsonIgnore]
    public string? SourcePath { get; set; }

    public static SorterConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing configuration file: {path}");
        }

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var config = JsonSerializer.Deserialize<SorterConfig>(json, options)
                     ?? throw new InvalidOperationException("Unable to parse config.json");

        config.SourcePath = path;
        config.NormalizeLegacyHotkeys();
        return config;
    }

    private void NormalizeLegacyHotkeys()
    {
        Hotkeys.TapStash = NormalizeHotkey(Hotkeys.TapStash, "[");
        Hotkeys.TapQuadStash = NormalizeHotkey(Hotkeys.TapQuadStash, "]");
    }

    private static string NormalizeHotkey(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim();
        if (normalized.Equals("F3", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("NumPad8", StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        if (normalized.Equals("NumPad9", StringComparison.OrdinalIgnoreCase))
        {
            return fallback == "[" ? "[" : fallback;
        }

        return normalized;
    }
}

public sealed class HotkeyConfig
{
    [JsonPropertyName("start")]
    public string Start { get; set; } = "F6";

    [JsonPropertyName("stop")]
    public string Stop { get; set; } = "F7";

    [JsonPropertyName("calibrateStash")]
    public string CalibrateStash { get; set; } = "Ctrl+8";

    [JsonPropertyName("calibrateStash12x12")]
    public string CalibrateStash12x12 { get; set; } = "Alt+NumPad8";

    [JsonPropertyName("calibrateInventory")]
    public string CalibrateInventory { get; set; } = "Ctrl+9";

    [JsonPropertyName("tapInventory")]
    public string TapInventory { get; set; } = "F4";

    [JsonPropertyName("tapStash")]
    public string TapStash { get; set; } = "[";

    [JsonPropertyName("tapQuadStash")]
    public string TapQuadStash { get; set; } = "]";
}

public sealed class TimingConfig
{
    [JsonPropertyName("clickDelayMs")]
    public int ClickDelayMs { get; set; } = 50;

    [JsonPropertyName("scanDelayMs")]
    public int ScanDelayMs { get; set; } = 10;

    [JsonPropertyName("cycleDelayMs")]
    public int CycleDelayMs { get; set; } = 500;
}

public sealed class GridConfig
{
    [JsonPropertyName("origin")]
    public PointConfig Origin { get; set; } = new(0, 0);

    [JsonPropertyName("rows")]
    public int Rows { get; set; } = 12;

    [JsonPropertyName("cols")]
    public int Cols { get; set; } = 12;

    [JsonPropertyName("slotSize")]
    public SizeConfig SlotSize { get; set; } = new(64, 64);

    [JsonPropertyName("slotSpacing")]
    public PointConfig SlotSpacing { get; set; } = new(1, 1);

    [JsonPropertyName("emptySlotColor")]
    public ColorConfig EmptySlotColor { get; set; } = new(24, 24, 24);

    [JsonPropertyName("emptyTolerance")]
    public double EmptyTolerance { get; set; } = 15;

    [JsonPropertyName("filledSlotColor")]
    public ColorConfig? FilledSlotColor { get; set; }

    [JsonPropertyName("filledTolerance")]
    public double? FilledTolerance { get; set; }

    [JsonPropertyName("scanOrder")]
    public string ScanOrder { get; set; } = "row-major";

    public Rectangle GetRegion()
    {
        var slotWidth = SlotSize.Width;
        var slotHeight = SlotSize.Height;
        var width = Cols * slotWidth + Math.Max(0, Cols - 1) * SlotSpacing.X;
        var height = Rows * slotHeight + Math.Max(0, Rows - 1) * SlotSpacing.Y;
        return new Rectangle(
            Origin.X,
            Origin.Y,
            (int)Math.Round(width),
            (int)Math.Round(height));
    }
}

public sealed record PointConfig(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y
);

public sealed record SizeConfig(
    [property: JsonPropertyName("width")] double Width,
    [property: JsonPropertyName("height")] double Height
);

public sealed record ColorConfig(
    [property: JsonPropertyName("r")] int R,
    [property: JsonPropertyName("g")] int G,
    [property: JsonPropertyName("b")] int B
)
{
    public Color ToColor() => Color.FromArgb(R, G, B);
}


