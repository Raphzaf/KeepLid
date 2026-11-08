namespace KeepLid.Core.Settings;

/// <summary>User preferences (persisted as settings.json). Draft: persistence comes later.</summary>
public sealed class AppSettings
{
    public bool KeepDisplayOnByDefault { get; set; }

    public bool LidModeByDefault { get; set; }

    /// <summary>Also change the battery (DC) lid action. Off by default; requires an explicit confirmation once.</summary>
    public bool LidModeOnBattery { get; set; }

    public int BatteryThresholdPercent { get; set; } = 20;

    public int MaxLidClosedOnBatteryMinutes { get; set; } = 120;

    public List<string> FavoriteProcesses { get; set; } =
        ["claude", "node", "python", "codex", "cursor", "ollama", "code"];
}
