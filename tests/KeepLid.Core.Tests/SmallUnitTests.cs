using System.Globalization;
using System.Resources;
using System.Xml.Linq;
using KeepLid.Core.Abstractions;
using KeepLid.Core.Logging;
using KeepLid.Core.Power;
using KeepLid.Core.Session;
using KeepLid.Core.Settings;

namespace KeepLid.Core.Tests;

public class SessionTimerTests
{
    [Fact]
    public void Counts_down_and_never_goes_negative()
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var timer = new SessionTimer(start, TimeSpan.FromHours(1));

        Assert.Equal(TimeSpan.FromMinutes(42), timer.Remaining(start.AddMinutes(18)));
        Assert.False(timer.IsElapsed(start.AddMinutes(59)));
        Assert.True(timer.IsElapsed(start.AddHours(1)));
        Assert.Equal(TimeSpan.Zero, timer.Remaining(start.AddHours(3)));
    }
}

public class ProcessWatcherTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Claude.exe", "claude")]
    [InlineData("  node ", "node")]
    [InlineData("python.EXE", "python")]
    public void Normalizes_names(string input, string expected) =>
        Assert.Equal(expected, ProcessWatcher.Normalize(input));

    [Fact]
    public void Never_seen_process_ends_after_grace_period()
    {
        var w = new ProcessWatcher(["ollama"], T0, TimeSpan.FromSeconds(15));
        Assert.True(w.Update([], T0.AddSeconds(14)));
        Assert.False(w.Update([], T0.AddSeconds(15)));
    }

    [Fact]
    public void Short_gap_is_absorbed()
    {
        var w = new ProcessWatcher(["node"], T0, TimeSpan.FromSeconds(15));
        Assert.True(w.Update(["node"], T0.AddMinutes(10)));
        Assert.True(w.Update([], T0.AddMinutes(10).AddSeconds(10)));
        Assert.True(w.Update(["node"], T0.AddMinutes(10).AddSeconds(12)));
        Assert.Equal(["node"], w.Running);
    }

    [Fact]
    public void Matching_is_case_insensitive_and_exact()
    {
        var w = new ProcessWatcher(["Code"], T0);
        w.Update(["code", "codex", "vscode"], T0);
        Assert.Equal(["code"], w.Running);
    }
}

public class HotkeyTests
{
    [Fact]
    public void Parses_default_shortcut()
    {
        Assert.True(Hotkey.TryParse("Ctrl+Alt+Shift+A", out var h));
        Assert.Equal(Hotkey.ModControl | Hotkey.ModAlt | Hotkey.ModShift, h.Modifiers);
        Assert.Equal(0x41u, h.VirtualKey);
    }

    [Theory]
    [InlineData("Ctrl+F5", 0x74u)]
    [InlineData("Win+Maj+9", 0x39u)]
    [InlineData("alt + f24", 0x87u)]
    public void Parses_keys(string text, uint vk)
    {
        Assert.True(Hotkey.TryParse(text, out var h));
        Assert.Equal(vk, h.VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("Shift+A")]       // no Ctrl/Alt/Win
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+Space")]
    public void Rejects_invalid(string text) => Assert.False(Hotkey.TryParse(text, out _));

    [Fact]
    public void Format_round_trips() =>
        Assert.True(Hotkey.TryParse(Hotkey.Format(true, true, true, false, "a"), out _));
}

public class SettingsTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Defaults_match_the_spec()
    {
        var s = new AppSettings();
        Assert.Equal(20, s.BatteryThresholdPercent);
        Assert.Equal(120, s.MaxLidClosedOnBatteryMinutes);
        Assert.False(s.LidModeOnBattery);
        Assert.Equal("Ctrl+Alt+Shift+A", s.Hotkey);
        Assert.Equal(["claude", "node", "python", "codex", "cursor", "ollama", "code"], s.FavoriteProcesses);
    }

    [Fact]
    public void Save_and_load_round_trip()
    {
        var store = new SettingsStore(_dir.File("settings.json"), NullLog.Instance);
        var s = new AppSettings { BatteryThresholdPercent = 35, FavoriteProcesses = ["Foo.exe", "foo", "bar"], FirstRunCompleted = true };
        store.Save(s);

        var loaded = store.Load();
        Assert.Equal(35, loaded.BatteryThresholdPercent);
        Assert.Equal(["foo", "bar"], loaded.FavoriteProcesses);
        Assert.True(loaded.FirstRunCompleted);
    }

    [Fact]
    public void Corrupt_or_out_of_range_values_are_fixed()
    {
        var path = _dir.File("settings.json");
        var store = new SettingsStore(path, NullLog.Instance);
        File.WriteAllText(path, "{\"BatteryThresholdPercent\": 1, \"MaxLidClosedOnBatteryMinutes\": 999999, \"Hotkey\": \"nope\"}");
        var s = store.Load();
        Assert.Equal(AppSettings.MinBatteryThreshold, s.BatteryThresholdPercent);
        Assert.Equal(AppSettings.MaxLidMinutes, s.MaxLidClosedOnBatteryMinutes);
        Assert.Equal("Ctrl+Alt+Shift+A", s.Hotkey);

        File.WriteAllText(path, "{{{");
        Assert.Equal(20, store.Load().BatteryThresholdPercent);
    }
}

public class FileLogTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Rotates_at_max_size()
    {
        var log = new FileLog(_dir.File("log.txt"), maxBytes: 1000);
        for (int i = 0; i < 50; i++) log.Info("line " + i + new string('x', 40));

        Assert.True(new FileInfo(log.Path).Length <= 1000);
        Assert.True(File.Exists(log.RotatedPath));
        Assert.Contains("line 49", File.ReadAllText(log.Path));
    }

    [Fact]
    public void Writes_levels()
    {
        var log = new FileLog(_dir.File("log.txt"));
        log.Warn("w");
        log.Error("e", new InvalidOperationException("boom"));
        string text = File.ReadAllText(log.Path);
        Assert.Contains("WARN  [", text);
        Assert.Contains("InvalidOperationException: boom", text);
    }
}

public class StatusTextTests
{
    [Fact]
    public void French_tooltip_matches_the_spec_example()
    {
        using var _ = new CultureScope("fr-FR");
        string text = StatusText.ActiveTooltip(
            new SessionOptions(new EndCondition.Duration(TimeSpan.FromHours(2)), false, true),
            lidApplied: true, LidAccess.Allowed, TimeSpan.FromMinutes(102), []);
        Assert.Equal("Éveillé — capot OK — encore 1 h 42", text);
    }

    [Fact]
    public void English_tooltip_for_processes()
    {
        using var _ = new CultureScope("en-US");
        string text = StatusText.ActiveTooltip(
            new SessionOptions(new EndCondition.WhileProcessesRun(["node"]), true, false),
            lidApplied: false, LidAccess.Allowed, null, ["node"]);
        Assert.Equal("Awake — screen on — while node runs", text);
    }

    [Theory]
    [InlineData(0.2, "1 min")]
    [InlineData(42, "42 min")]
    [InlineData(60, "1 h 00")]
    [InlineData(241, "4 h 01")]
    public void Formats_durations(double minutes, string expected)
    {
        using var _ = new CultureScope("en-US");
        Assert.Equal(expected, StatusText.FormatDuration(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void Tooltip_never_exceeds_notifyicon_limit()
    {
        string longText = StatusText.Truncate(new string('a', 500));
        Assert.Equal(StatusText.MaxTooltipLength, longText.Length);
    }

    [Fact]
    public void Lid_action_names()
    {
        using var _ = new CultureScope("fr-FR");
        Assert.Equal("Ne rien faire", StatusText.LidActionName(0));
        Assert.Equal("Veille", StatusText.LidActionName(1));
        Assert.Equal("inconnu (9)", StatusText.LidActionName(9));
    }
}

public class ResourceParityTests
{
    private static string ResourcesDir([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "KeepLid.Core", "Resources"));

    private static Dictionary<string, string> Read(string file) =>
        XDocument.Load(Path.Combine(ResourcesDir(), file)).Root!.Elements("data")
            .ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")!.Value);

    [Fact]
    public void English_and_French_have_the_same_keys_and_placeholders()
    {
        var en = Read("Strings.resx");
        var fr = Read("Strings.fr.resx");
        Assert.Equal(en.Keys.OrderBy(k => k), fr.Keys.OrderBy(k => k));
        foreach (var (key, enValue) in en)
        {
            var placeholders = System.Text.RegularExpressions.Regex.Matches(enValue, @"\{\d").Select(m => m.Value).Distinct().OrderBy(x => x);
            var frPlaceholders = System.Text.RegularExpressions.Regex.Matches(fr[key], @"\{\d").Select(m => m.Value).Distinct().OrderBy(x => x);
            Assert.True(placeholders.SequenceEqual(frPlaceholders), $"Placeholders differ for {key}");
            Assert.False(string.IsNullOrWhiteSpace(fr[key]), key);
        }
    }

    [Fact]
    public void Satellite_assembly_is_used_for_french()
    {
        var rm = new ResourceManager("KeepLid.Core.Resources.Strings", typeof(AwakeController).Assembly);
        Assert.Equal("Quitter (restaure tout)", rm.GetString("Menu_Quit", new CultureInfo("fr-FR")));
        Assert.Equal("Quit (restores everything)", rm.GetString("Menu_Quit", new CultureInfo("de-DE")));
    }
}

internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _old = CultureInfo.CurrentUICulture;
    private readonly CultureInfo _oldCulture = CultureInfo.CurrentCulture;

    public CultureScope(string name)
    {
        CultureInfo.CurrentUICulture = new CultureInfo(name);
        CultureInfo.CurrentCulture = new CultureInfo(name);
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _old;
        CultureInfo.CurrentCulture = _oldCulture;
    }
}
