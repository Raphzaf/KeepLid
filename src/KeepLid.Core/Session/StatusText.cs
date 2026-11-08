using System.Globalization;
using KeepLid.Core.Power;
using KeepLid.Core.Resources;

namespace KeepLid.Core.Session;

/// <summary>Human-readable texts derived from the controller state (tray tooltip, notifications).</summary>
public static class StatusText
{
    /// <summary>NotifyIcon.Text limit in .NET 8 (longer text throws).</summary>
    public const int MaxTooltipLength = 127;

    /// <summary>e.g. "Éveillé — capot OK — encore 1 h 42".</summary>
    public static string Tooltip(AwakeController c)
    {
        string text = c.State switch
        {
            AwakeState.Active when c.Options is { } o => ActiveTooltip(o, c.LidOverrideApplied, c.LidAccess, c.Remaining, c.WatchedNames),
            AwakeState.GuardTripped => string.Format(CultureInfo.CurrentCulture, Strings.Tray_GuardTripped, GuardLabel(c.LastGuardReason)),
            _ => Strings.Tray_Inactive,
        };
        return Truncate(text);
    }

    internal static string ActiveTooltip(SessionOptions options, bool lidApplied, LidAccess lidAccess,
        TimeSpan? remaining, IReadOnlyCollection<string> watched)
    {
        var parts = new List<string> { Strings.Tray_Awake };
        if (options.LidMode && lidApplied) parts.Add(Strings.Tray_LidOk);
        else if (lidAccess == LidAccess.Denied && options.LidMode) parts.Add(Strings.Tray_LidGuide);
        if (options.KeepDisplayOn) parts.Add(Strings.Tray_DisplayOn);

        parts.Add(options.End switch
        {
            EndCondition.Duration when remaining is { } r => string.Format(CultureInfo.CurrentCulture, Strings.Tray_Remaining, FormatDuration(r)),
            EndCondition.WhileProcessesRun => string.Format(CultureInfo.CurrentCulture, Strings.Tray_WhileProcesses, string.Join(", ", watched)),
            _ => Strings.Tray_UntilOff,
        });
        return string.Join(" — ", parts);
    }

    /// <summary>"1 h 42" / "42 min". Rounded up to the minute so "0 min" never shows while time is left.</summary>
    public static string FormatDuration(TimeSpan span)
    {
        int totalMinutes = (int)Math.Ceiling(Math.Max(0, span.TotalMinutes));
        if (totalMinutes < 60)
            return string.Format(CultureInfo.CurrentCulture, Strings.Duration_Minutes, totalMinutes);
        return string.Format(CultureInfo.CurrentCulture, Strings.Duration_HoursMinutes, totalMinutes / 60, totalMinutes % 60);
    }

    public static string GuardLabel(StopReason? reason) => reason switch
    {
        StopReason.LowBattery => Strings.Guard_LowBattery,
        StopReason.LidClosedTooLongOnBattery => Strings.Guard_LidTooLong,
        _ => string.Empty,
    };

    /// <summary>Name of a lid close action index (winnt.h POWERBUTTON_ACTION_INDEX_*).</summary>
    public static string LidActionName(uint value) => value switch
    {
        0 => Strings.LidAction_0,
        1 => Strings.LidAction_1,
        2 => Strings.LidAction_2,
        3 => Strings.LidAction_3,
        _ => string.Format(CultureInfo.CurrentCulture, Strings.LidAction_Unknown, value),
    };

    public static string Truncate(string text) =>
        text.Length <= MaxTooltipLength ? text : text[..(MaxTooltipLength - 1)] + "…";
}
