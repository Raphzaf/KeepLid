using KeepLid.Core.Abstractions;
using KeepLid.Core.Power;
using KeepLid.Core.Session;
using KeepLid.Core.Settings;

namespace KeepLid.Core.Tests;

public sealed class AwakeControllerTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakePowerSettings _power = new();
    private readonly FakeExecutionState _exec = new();
    private readonly FakePowerStatus _status = new();
    private readonly FakeProcessProbe _processes = new();
    private readonly FakeClock _clock = new();
    private readonly AppSettings _settings = new();
    private readonly LidOverride _lid;
    private readonly AwakeController _c;
    private readonly List<StateChangedEventArgs> _events = new();

    public AwakeControllerTests()
    {
        var log = new ListLog();
        _lid = new LidOverride(_power, new FileRestoreStore(_dir.File("restore.json")), log);
        _c = new AwakeController(_exec, _lid, _power, _status, _processes, _clock, log, _settings)
        {
            LidAccess = LidAccess.Allowed,
        };
        _c.StateChanged += (_, e) => _events.Add(e);
    }

    public void Dispose() => _dir.Dispose();

    private static SessionOptions Opts(EndCondition? end = null, bool display = false, bool lid = false) =>
        new(end ?? new EndCondition.Indefinite(), display, lid);

    [Fact]
    public void Starts_inactive_and_untouched()
    {
        Assert.Equal(AwakeState.Inactive, _c.State);
        Assert.False(_exec.Awake);
        Assert.False(_lid.IsApplied);
    }

    [Fact]
    public void Start_then_stop_releases_everything()
    {
        _c.Start(Opts(display: true, lid: true));
        Assert.Equal(AwakeState.Active, _c.State);
        Assert.True(_exec.Awake);
        Assert.True(_exec.DisplayOn);
        Assert.True(_lid.IsApplied);
        Assert.Equal(0u, _power.Schemes[FakePowerSettings.Balanced].Ac);

        _c.Stop(StopReason.User);
        Assert.Equal(AwakeState.Inactive, _c.State);
        Assert.False(_exec.Awake);
        Assert.False(_lid.IsApplied);
        Assert.Equal(new LidActions(1, 1), _power.Schemes[FakePowerSettings.Balanced]);
        Assert.Equal(StopReason.User, _events[^1].StopReason);
    }

    [Fact]
    public void Toggle_alternates()
    {
        _c.Toggle(Opts());
        Assert.Equal(AwakeState.Active, _c.State);
        _c.Toggle(Opts());
        Assert.Equal(AwakeState.Inactive, _c.State);
    }

    [Fact]
    public void Timer_session_ends_when_elapsed()
    {
        _c.Start(Opts(new EndCondition.Duration(TimeSpan.FromMinutes(30)), lid: true));
        _clock.Advance(TimeSpan.FromMinutes(12));
        _c.Tick();
        Assert.Equal(AwakeState.Active, _c.State);
        Assert.Equal(TimeSpan.FromMinutes(18), _c.Remaining);

        _clock.Advance(TimeSpan.FromMinutes(18));
        _c.Tick();
        Assert.Equal(AwakeState.Inactive, _c.State);
        Assert.Equal(StopReason.TimerElapsed, _events[^1].StopReason);
        Assert.False(_lid.IsApplied);
    }

    [Fact]
    public void Process_session_ends_after_last_process_exits_and_grace_period()
    {
        _processes.Running.AddRange(["explorer", "Claude", "node"]);
        _c.Start(Opts(new EndCondition.WhileProcessesRun(["claude.exe", "node"])));
        _c.Tick();
        Assert.Equal(["claude", "node"], _c.WatchedRunning.OrderBy(x => x));

        _processes.Running.Remove("Claude");
        _clock.Advance(TimeSpan.FromMinutes(5));
        _c.Tick();
        Assert.Equal(AwakeState.Active, _c.State); // node still running

        _processes.Running.Remove("node");
        _c.Tick();
        _clock.Advance(TimeSpan.FromSeconds(10));
        _c.Tick();
        Assert.Equal(AwakeState.Active, _c.State); // within grace period

        _clock.Advance(TimeSpan.FromSeconds(6));
        _c.Tick();
        Assert.Equal(AwakeState.Inactive, _c.State);
        Assert.Equal(StopReason.ProcessesEnded, _events[^1].StopReason);
    }

    [Fact]
    public void Low_battery_trips_the_guard_and_restores()
    {
        _c.Start(Opts(lid: true));
        _status.Status = new PowerStatus(OnBattery: true, BatteryPercent: 19);
        _c.Tick();

        Assert.Equal(AwakeState.GuardTripped, _c.State);
        Assert.Equal(StopReason.LowBattery, _c.LastGuardReason);
        Assert.False(_exec.Awake);
        Assert.False(_lid.IsApplied);
    }

    [Fact]
    public void Low_battery_on_mains_does_not_trip()
    {
        _c.Start(Opts());
        _status.Status = new PowerStatus(OnBattery: false, BatteryPercent: 5);
        _c.Tick();
        Assert.Equal(AwakeState.Active, _c.State);
    }

    [Fact]
    public void Starting_below_threshold_on_battery_stops_immediately()
    {
        _status.Status = new PowerStatus(true, 10);
        _c.Start(Opts());
        Assert.Equal(AwakeState.GuardTripped, _c.State);
    }

    [Fact]
    public void Lid_closed_too_long_on_battery_trips_the_guard()
    {
        _settings.MaxLidClosedOnBatteryMinutes = 120;
        _c.Start(Opts(lid: true));
        _status.Status = new PowerStatus(true, 80);
        _c.OnLidSwitch(closed: true);
        _c.Tick();

        _clock.Advance(TimeSpan.FromMinutes(119));
        _c.Tick();
        Assert.Equal(AwakeState.Active, _c.State);

        _clock.Advance(TimeSpan.FromMinutes(1));
        _c.Tick();
        Assert.Equal(AwakeState.GuardTripped, _c.State);
        Assert.Equal(StopReason.LidClosedTooLongOnBattery, _c.LastGuardReason);
    }

    [Fact]
    public void Lid_timer_restarts_when_plugged_in()
    {
        _c.Start(Opts(lid: true));
        _status.Status = new PowerStatus(true, 80);
        _c.OnLidSwitch(closed: true);
        _c.Tick();
        _clock.Advance(TimeSpan.FromMinutes(100));
        _status.Status = new PowerStatus(false, 80); // plugged in
        _c.Tick();
        _status.Status = new PowerStatus(true, 80);
        _c.Tick();
        _clock.Advance(TimeSpan.FromMinutes(100));
        _c.Tick();
        Assert.Equal(AwakeState.Active, _c.State);
    }

    [Fact]
    public void Guard_state_is_cleared_by_acknowledge_or_new_start()
    {
        _status.Status = new PowerStatus(true, 10);
        _c.Start(Opts());
        _c.AcknowledgeGuard();
        Assert.Equal(AwakeState.Inactive, _c.State);

        _c.Start(Opts());
        _status.Status = new PowerStatus(false, 10);
        _c.Start(Opts());
        Assert.Equal(AwakeState.Active, _c.State);
        Assert.Null(_c.LastGuardReason);
    }

    [Fact]
    public void Suspend_ends_the_session_and_restores()
    {
        _c.Start(Opts(lid: true));
        _c.OnSuspend();
        Assert.Equal(AwakeState.Inactive, _c.State);
        Assert.Equal(StopReason.SystemSuspend, _events[^1].StopReason);
        Assert.False(_lid.IsApplied);
    }

    [Fact]
    public void Lid_mode_toggled_during_session()
    {
        _c.Start(Opts());
        Assert.False(_lid.IsApplied);
        _c.SetLidMode(true);
        Assert.True(_lid.IsApplied);
        _c.SetLidMode(false);
        Assert.False(_lid.IsApplied);
        Assert.Equal(AwakeState.Active, _c.State);
    }

    [Fact]
    public void Battery_option_applies_to_DC()
    {
        _settings.LidModeOnBattery = true;
        _c.Start(Opts(lid: true));
        Assert.Equal(new LidActions(0, 0), _power.Schemes[FakePowerSettings.Balanced]);
    }

    [Fact]
    public void Denied_access_runs_without_lid_mode_and_never_writes()
    {
        _c.LidAccess = LidAccess.Denied;
        _c.Start(Opts(lid: true));

        Assert.Equal(AwakeState.Active, _c.State);
        Assert.False(_c.Options!.LidMode);
        Assert.Equal(0, _power.WriteCount);
    }

    [Fact]
    public void Lid_write_failure_keeps_session_running_and_reports_error()
    {
        _power.DenyWrites = true; // probe said Allowed, but policy changed meanwhile
        _c.Start(Opts(lid: true));

        Assert.Equal(AwakeState.Active, _c.State);
        Assert.False(_c.Options!.LidMode);
        Assert.Equal("Win32 5", _events[^1].LidError);
    }

    [Fact]
    public void Guide_mode_reminds_when_lid_still_set_to_do_nothing()
    {
        _c.LidAccess = LidAccess.Denied;
        _power.Schemes[FakePowerSettings.Balanced] = new LidActions(0, 1); // user followed the guide
        _c.Start(Opts(lid: true));
        _c.Stop(StopReason.User);
        Assert.True(_events[^1].LidReminder);
    }

    [Fact]
    public void Scheme_change_during_session_is_followed()
    {
        _c.Start(Opts(lid: true));
        _power.Active = FakePowerSettings.HighPerformance;
        _c.Tick();
        Assert.Equal(new LidActions(1, 1), _power.Schemes[FakePowerSettings.Balanced]);
        Assert.Equal(0u, _power.Schemes[FakePowerSettings.HighPerformance].Ac);
        _c.Stop(StopReason.AppExit);
        Assert.Equal(new LidActions(2, 1), _power.Schemes[FakePowerSettings.HighPerformance]);
    }

    [Fact]
    public void Stop_when_inactive_raises_nothing()
    {
        _c.Stop(StopReason.AppExit);
        Assert.Empty(_events);
    }
}
