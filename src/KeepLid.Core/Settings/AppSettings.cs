using System.Text.Json;
using KeepLid.Core.Abstractions;

namespace KeepLid.Core.Settings;

/// <summary>User preferences, persisted as settings.json in the data folder.</summary>
public sealed class AppSettings
{
    public const int MinBatteryThreshold = 5;
    public const int MaxBatteryThreshold = 90;
    public const int MinLidMinutes = 5;
    public const int MaxLidMinutes = 24 * 60;

    public static readonly string[] DefaultProcesses = ["claude", "node", "python", "codex", "cursor", "ollama", "code"];

    /// <summary>"Keep the screen on" pre-checked when a session starts.</summary>
    public bool KeepDisplayOnByDefault { get; set; }

    /// <summary>"Keep working when the lid is closed" pre-checked when a session starts.</summary>
    public bool LidModeByDefault { get; set; }

    /// <summary>
    /// Also change the battery (DC) lid action. Off by default: the lid mode only acts on mains power.
    /// Turned on only after the user confirmed the battery warning once.
    /// </summary>
    public bool LidModeOnBattery { get; set; }

    /// <summary>Guard rail: stop when on battery below this percentage.</summary>
    public int BatteryThresholdPercent { get; set; } = 20;

    /// <summary>Guard rail: stop after the lid has been closed on battery for this long.</summary>
    public int MaxLidClosedOnBatteryMinutes { get; set; } = 120;

    /// <summary>Process presets offered by "Until a process ends…".</summary>
    public List<string> FavoriteProcesses { get; set; } = [.. DefaultProcesses];

    public bool HotkeyEnabled { get; set; } = true;

    /// <summary>Global toggle shortcut, e.g. "Ctrl+Alt+Shift+A" (see <see cref="Hotkey"/>).</summary>
    public string Hotkey { get; set; } = "Ctrl+Alt+Shift+A";

    /// <summary>Last value used in "Custom duration…".</summary>
    public int CustomDurationMinutes { get; set; } = 90;

    /// <summary>The 3-step welcome screen was completed.</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>Brings out-of-range values (hand-edited file) back to safe values.</summary>
    public void Normalize()
    {
        BatteryThresholdPercent = Math.Clamp(BatteryThresholdPercent, MinBatteryThreshold, MaxBatteryThreshold);
        MaxLidClosedOnBatteryMinutes = Math.Clamp(MaxLidClosedOnBatteryMinutes, MinLidMinutes, MaxLidMinutes);
        CustomDurationMinutes = Math.Clamp(CustomDurationMinutes, 1, MaxLidMinutes);
        FavoriteProcesses = (FavoriteProcesses ?? [])
            .Select(Session.ProcessWatcher.Normalize)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();
        if (!Settings.Hotkey.TryParse(Hotkey, out _)) Hotkey = new AppSettings().Hotkey;
    }

    public AppSettings Clone() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this))!;
}

/// <summary>Loads and saves <see cref="AppSettings"/>. A missing or corrupt file yields the defaults.</summary>
public sealed class SettingsStore(string path, ILog log)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string Path { get; } = path;

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), Options);
                if (settings is not null)
                {
                    settings.Normalize();
                    return settings;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            log.Warn($"settings.json unreadable, using defaults: {ex.Message}");
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        string tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
        File.Move(tmp, Path, overwrite: true);
    }
}
