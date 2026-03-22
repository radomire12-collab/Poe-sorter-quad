using System.Text.Json;
using System.Text.Json.Serialization;

namespace MapSorter.Configuration;

public static class ConfigWriter
{
    public static void Save(SorterConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.SourcePath))
        {
            return;
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(config, options);
        try
        {
            File.WriteAllText(config.SourcePath!, json);
            Console.WriteLine($"Configuration saved to {config.SourcePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Failed to save configuration ({config.SourcePath}): {ex.Message}");
        }
    }
}


