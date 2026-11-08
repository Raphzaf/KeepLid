using KeepLid.Core.Abstractions;
using KeepLid.Core.Power;
using KeepLid.Core.Settings;

namespace KeepLid.Core.Session;

/// <summary>Top-level state shown by the tray icon.</summary>
public enum AwakeState
{
    /// <summary>Windows behaves normally.</summary>
    Inactive,
    /// <summary>Sleep is blocked (and the lid override may be applied).</summary>
    Active,
    /// <summary>A guard rail stopped the session; everything was restored. Same as Inactive, but visible.</summary>
    GuardTripped,
}

public enum StopReason
{
    User,
    TimerElapsed,
    ProcessesEnded,
    LowBattery,
    LidClosedTooLongOnBattery,
    SystemSuspend,
    AppExit,
}

/// <summary>What the user asked for when starting a session.</summary>
public sealed record SessionOptions(EndCondition End, bool KeepDisplayOn, bool LidMode);

public sealed class StateChangedEventArgs(AwakeState state, StopReason? stopReason, string? lidError, bool lidReminder = false) : EventArgs
{
    /// <summary>
    /// Guide mode only: the session ended while the lid action is still "Do nothing" (set by the user through the
    /// guide), so the UI should remind the user to set it back.
    /// </summary>
    public bool LidReminder { get; } = lidReminder;

    public AwakeState State { get; } = state;

    /// <summary>Why the session stopped (null when it started or changed).</summary>
    public StopReason? StopReason { get; } = stopReason;

    /// <summary>Set when the lid override could not be applied (the session continues without it).</summary>
    public string? LidError { get; } = lidError;
}

/// <summary>
/// The state machine:
/// <code>
///   Inactive ──Start──▶ Active ──Stop(user / timer / processes ended / suspend / exit)──▶ Inactive
///                        │
///                        └──guard rail (low battery, lid closed too long on battery)──▶ GuardTripped
///   GuardTripped ──Start──▶ Active        GuardTripped ──Acknowledge──▶ Inactive
/// </code>
/// Leaving Active ALWAYS releases the execution state and restores the lid action.
/// All members must be called from one thread (the UI thread); <see cref="LidOverride"/> is itself thread-safe.
/// </summary>
public sealed class AwakeController
{
    private readonly IExecutionState _execution;
    private readonly LidOverride _lid;
    private readonly IPowerSettings _power;
    private readonly IPowerStatusProvider _status;
    private readonly IProcessProbe _processes;
    private readonly IClock _clock;
    private readonly ILog _log;
    private readonly GuardRails _guards = new();

    private SessionTimer? _timer;
    private ProcessWatcher? _watcher;

    public AwakeController(IExecutionState execution, LidOverride lid, IPowerSettings power,
        IPowerStatusProvider status, IProcessProbe processes, IClock clock, ILog log, AppSettings settings)
    {
        _execution = execution;
        _lid = lid;
        _power = power;
        _status = status;
        _processes = processes;
        _clock = clock;
        _log = log;
        Settings = settings;
    }

    public event EventHandler<StateChangedEventArgs>? StateChanged;

    /// <summary>Guard thresholds and battery option are read from here on every tick.</summary>
    public AppSettings Settings { get; set; }

    public AwakeState State { get; private set; } = AwakeState.Inactive;

    /// <summary>Options of the current session (null when not active).</summary>
    public SessionOptions? Options { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>Reason of the last stop caused by a guard rail.</summary>
    public StopReason? LastGuardReason { get; private set; }

    public bool LidPresent => _power.IsLidPresent;

    /// <summary>
    /// Result of the startup probe (see <see cref="LidOverride.ProbeAccess"/>). The automatic lid mode is only
    /// used when <see cref="LidAccess.Allowed"/>; otherwise the UI offers the step-by-step guide.
    /// </summary>
    public LidAccess LidAccess { get; set; } = LidAccess.Unknown;

    /// <summary>True when the lid override is really in place right now.</summary>
    public bool LidOverrideApplied => _lid.IsApplied;

    /// <summary>Time left for a timed session, null otherwise.</summary>
    public TimeSpan? Remaining => _timer?.Remaining(_clock.Now);

    /// <summary>Watched processes currently running (process sessions only).</summary>
    public IReadOnlyList<string> WatchedRunning => _watcher?.Running ?? Array.Empty<string>();

    public IReadOnlyCollection<string> WatchedNames => _watcher?.Names ?? Array.Empty<string>();

    /// <summary>Starts a session, or replaces the options of the running one.</summary>
    public void Start(SessionOptions options)
    {
        DateTimeOffset now = _clock.Now;
        bool wasActive = State == AwakeState.Active;

        _execution.KeepAwake(options.KeepDisplayOn);
        string? lidError = ApplyLidMode(ref options);

        _timer = options.End is EndCondition.Duration d ? new SessionTimer(now, d.Length) : null;
        _watcher = options.End is EndCondition.WhileProcessesRun p ? new ProcessWatcher(p.ProcessNames, now) : null;
        if (!wasActive)
        {
            _guards.Reset();
            StartedAt = now;
        }

        Options = options;
        State = AwakeState.Active;
        LastGuardReason = null;
        _log.Info($"Session {(wasActive ? "updated" : "started")}: end={Describe(options.End)}, display={options.KeepDisplayOn}, lid={options.LidMode}");
        StateChanged?.Invoke(this, new StateChangedEventArgs(State, null, lidError));

        // A session started below the battery threshold stops right away, with an explanation.
        EvaluateGuards(now);
    }

    /// <summary>Ends the session and restores everything. Safe to call in any state.</summary>
    public void Stop(StopReason reason)
    {
        bool guard = reason is StopReason.LowBattery or StopReason.LidClosedTooLongOnBattery;
        bool wasActive = State == AwakeState.Active;

        _execution.Release();
        _lid.Restore();
        bool reminder = LidAccess == LidAccess.Denied && _lid.TryReadCurrent() is { } current &&
                        (current.Ac == LidOverride.ActionDoNothing || current.Dc == LidOverride.ActionDoNothing);
        _timer = null;
        _watcher = null;
        Options = null;
        StartedAt = null;

        if (!wasActive && !guard) return;
        State = guard ? AwakeState.GuardTripped : AwakeState.Inactive;
        LastGuardReason = guard ? reason : null;
        _log.Info($"Session stopped: {reason}");
        StateChanged?.Invoke(this, new StateChangedEventArgs(State, reason, null, reminder));
    }

    /// <summary>Left click on the tray icon.</summary>
    public void Toggle(SessionOptions startOptions)
    {
        if (State == AwakeState.Active) Stop(StopReason.User);
        else Start(startOptions);
    }

    /// <summary>Clears the "guard tripped" icon once the user has seen it.</summary>
    public void AcknowledgeGuard()
    {
        if (State != AwakeState.GuardTripped) return;
        State = AwakeState.Inactive;
        LastGuardReason = null;
        StateChanged?.Invoke(this, new StateChangedEventArgs(State, null, null));
    }

    public void SetKeepDisplayOn(bool on)
    {
        if (Options is { } o && State == AwakeState.Active) Start(o with { KeepDisplayOn = on });
    }

    public void SetLidMode(bool on)
    {
        if (Options is { } o && State == AwakeState.Active) Start(o with { LidMode = on });
    }

    /// <summary>Re-applies the lid override after the battery option changed in the settings.</summary>
    public void RefreshLidOverride()
    {
        if (Options is { LidMode: true } o && State == AwakeState.Active) Start(o);
    }

    /// <summary>Periodic check (every few seconds): end conditions, guard rails, power scheme changes.</summary>
    public void Tick()
    {
        if (State != AwakeState.Active || Options is null) return;
        DateTimeOffset now = _clock.Now;

        if (Options.LidMode)
        {
            try { _lid.FollowActiveScheme(); }
            catch (Exception ex) { _log.Error("Could not follow power scheme change", ex); }
        }

        if (EvaluateGuards(now)) return;

        if (_timer?.IsElapsed(now) == true)
        {
            Stop(StopReason.TimerElapsed);
            return;
        }

        if (_watcher is not null && !_watcher.Update(_processes.GetRunningProcessNames(), now))
            Stop(StopReason.ProcessesEnded);
    }

    /// <summary>GUID_LIDSWITCH_STATE_CHANGE notification.</summary>
    public void OnLidSwitch(bool closed)
    {
        _guards.SetLidClosed(closed);
        _log.Info(closed ? "Lid closed" : "Lid opened");
    }

    /// <summary>The system is going to sleep anyway (user action, critical battery…): end and restore.</summary>
    public void OnSuspend()
    {
        if (State == AwakeState.Active) Stop(StopReason.SystemSuspend);
        else _lid.Restore();
    }

    /// <summary>Back from sleep: make sure nothing is left modified.</summary>
    public void OnResume() => _lid.Restore();

    /// <summary>GUID_ACTIVE_POWERSCHEME notification.</summary>
    public void OnPowerSchemeChanged()
    {
        if (State == AwakeState.Active && Options?.LidMode == true) _lid.FollowActiveScheme();
    }

    private bool EvaluateGuards(DateTimeOffset now)
    {
        _guards.BatteryThresholdPercent = Settings.BatteryThresholdPercent;
        _guards.MaxLidClosedOnBattery = TimeSpan.FromMinutes(Settings.MaxLidClosedOnBatteryMinutes);

        GuardReason guard = _guards.Evaluate(_status.GetStatus(), now);
        switch (guard)
        {
            case GuardReason.LowBattery:
                Stop(StopReason.LowBattery);
                return true;
            case GuardReason.LidClosedTooLongOnBattery:
                Stop(StopReason.LidClosedTooLongOnBattery);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Applies or removes the lid override; on failure the session continues without it.</summary>
    private string? ApplyLidMode(ref SessionOptions options)
    {
        if (!options.LidMode || LidAccess != LidAccess.Allowed)
        {
            _lid.Restore();
            if (options.LidMode) options = options with { LidMode = false };
            return null;
        }

        try
        {
            _lid.Apply(includeBattery: Settings.LidModeOnBattery);
            return null;
        }
        catch (Exception ex)
        {
            _log.Error("Lid mode unavailable; continuing without it", ex);
            options = options with { LidMode = false };
            return ex is PowerSettingsException p ? $"Win32 {p.ErrorCode}" : ex.Message;
        }
    }

    private static string Describe(EndCondition end) => end switch
    {
        EndCondition.Duration d => $"duration {d.Length}",
        EndCondition.WhileProcessesRun p => $"processes [{string.Join(", ", p.ProcessNames)}]",
        _ => "indefinite",
    };
}
