using KeepLid.Core.Abstractions;

namespace KeepLid.Core.Session;

public enum GuardReason
{
    None,
    /// <summary>On battery and the charge fell below the threshold.</summary>
    LowBattery,
    /// <summary>The lid stayed closed on battery longer than allowed.</summary>
    LidClosedTooLongOnBattery,
}

/// <summary>
/// Safety limits evaluated on every tick. Pure logic: inputs are the power status, the lid state
/// and the clock. Note: no thermal protection — reading temperatures requires admin rights.
/// </summary>
public sealed class GuardRails
{
    private DateTimeOffset? _lidClosedOnBatterySince;

    public int BatteryThresholdPercent { get; set; } = 20;

    public TimeSpan MaxLidClosedOnBattery { get; set; } = TimeSpan.FromHours(2);

    /// <summary>Lid state, as reported by the GUID_LIDSWITCH_STATE_CHANGE notification.</summary>
    public bool LidClosed { get; private set; }

    public void SetLidClosed(bool closed) => LidClosed = closed;

    /// <summary>Clears the lid-closed timer (new session).</summary>
    public void Reset() => _lidClosedOnBatterySince = null;

    /// <summary>How long the lid has been closed on battery during this session, if it is.</summary>
    public TimeSpan? LidClosedOnBatteryFor(DateTimeOffset now) =>
        _lidClosedOnBatterySince is { } since ? now - since : null;

    /// <remarks>
    /// The lid timer runs whenever the lid is closed on battery during a session, whether the lid action was
    /// changed by KeepLid or by the user through the guide: if the PC did not sleep, it is running lid closed.
    /// </remarks>
    public GuardReason Evaluate(PowerStatus status, DateTimeOffset now)
    {
        if (status.OnBattery && status.BatteryPercent is { } percent && percent < BatteryThresholdPercent)
            return GuardReason.LowBattery;

        if (LidClosed && status.OnBattery)
        {
            _lidClosedOnBatterySince ??= now;
            if (now - _lidClosedOnBatterySince.Value >= MaxLidClosedOnBattery)
                return GuardReason.LidClosedTooLongOnBattery;
        }
        else
        {
            _lidClosedOnBatterySince = null; // lid opened or back on mains: the clock restarts
        }
        return GuardReason.None;
    }
}
