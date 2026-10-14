using System.Diagnostics;
using System.Text.Json;

namespace ClipWatcher.Services;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public string FilePath { get; set; } = Path.Combine(SettingsDir, "urls.txt");
    public bool SortAfterAdd { get; set; } = true;
    public bool MonitoringEnabled { get; set; } = true;

    private static string SettingsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipWatcher");

    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (loaded is not null) return loaded;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"ClipWatcher: settings unreadable, using defaults ({ex.GetType().Name})");
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        var tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Indented));
        File.Move(tmp, SettingsPath, overwrite: true);
    }
}
