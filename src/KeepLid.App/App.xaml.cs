using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using KeepLid.App.Platform;
using KeepLid.App.Tray;
using KeepLid.App.Views;
using KeepLid.Core;
using KeepLid.Core.Abstractions;
using KeepLid.Core.Logging;
using KeepLid.Core.Power;
using KeepLid.Core.Resources;
using KeepLid.Core.Session;
using KeepLid.Core.Settings;
using Microsoft.Win32;

namespace KeepLid.App;

/// <summary>
/// Composition root and glue between the tray icon, the windows, Windows notifications and the
/// <see cref="AwakeController"/> state machine. Everything runs on the WPF UI thread.
/// </summary>
public partial class App : Application
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    private AppPaths _paths = AppPaths.Default;
    private ILog _log = NullLog.Instance;
    private SettingsStore _settingsStore = null!;
    private AppSettings _settings = new();
    private LidOverride _lid = null!;
    private ExecutionStateThread _execution = null!;
    private AwakeController _controller = null!;
    private MessageWindow _messages = null!;
    private TrayIcon _tray = null!;
    private DispatcherTimer _timer = null!;
    private LicenseService _license = null!;
    private Process? _watchdog;
    private bool _exiting;

    // Choices shown in the menu while inactive; used for the next session.
    private bool _nextKeepDisplayOn;
    private bool _nextLidMode;

    internal AwakeController Controller => _controller;

    internal AppSettings Settings => _settings;

    internal AppPaths Paths => _paths;

    internal ILog Log => _log;

    internal LidOverride Lid => _lid;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _log = new FileLog(_paths.LogFile);
        _log.Info($"KeepLid {PackageInfo.Version} starting (packaged={PackageInfo.IsPackaged}, ui={CultureInfo.CurrentUICulture.Name}, os={Environment.OSVersion.Version})");
        HookUnhandledExceptions();

        _settingsStore = new SettingsStore(_paths.SettingsFile, _log);
        _settings = _settingsStore.Load();
        _nextKeepDisplayOn = _settings.KeepDisplayOnByDefault;
        _nextLidMode = _settings.LidModeByDefault;

        var power = new WindowsPowerSettings();
        _lid = new LidOverride(power, new FileRestoreStore(_paths.RestoreFile), _log);

        // 1. Crash recovery: a leftover restore.json means the previous run did not restore.
        RestoreOutcome recovery = _lid.Restore();

        // 2. Phase 0 (a+): can this PC's lid action be changed without elevation?
        _execution = new ExecutionStateThread(_log);
        _controller = new AwakeController(_execution, _lid, power, new WindowsPowerStatus(),
            new WindowsProcessProbe(), new SystemClock(), _log, _settings)
        {
            LidAccess = _lid.ProbeAccess(),
        };
        _controller.StateChanged += OnStateChanged;

        _messages = new MessageWindow(_log);
        _messages.LidSwitchChanged += closed => _controller.OnLidSwitch(closed);
        _messages.ActiveSchemeChanged += OnActiveSchemeChanged;
        _messages.HotkeyPressed += Toggle;

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SessionEnding += OnSessionEnding;

        _tray = new TrayIcon();
        _tray.LeftClick += Toggle;
        _tray.MenuOpening += (menu, _) => TrayMenu.Build(menu, this);
        _tray.NotificationClicked += () => { if (_controller.State == AwakeState.GuardTripped) _controller.AcknowledgeGuard(); };

        _timer = new DispatcherTimer(TickInterval, DispatcherPriority.Background, (_, _) => OnTick(), Dispatcher);
        _timer.Stop(); // only runs during a session: ~0 % CPU when idle

        ApplyHotkey(notifyOnFailure: false);
        RefreshTray();

        if (recovery == RestoreOutcome.Restored)
            _tray.Notify(Strings.Notify_Restored_Title, Strings.Notify_Restored_Body);
        else if (recovery is RestoreOutcome.Failed or RestoreOutcome.Corrupt)
            _tray.Notify(Strings.AppName, Strings.Notify_RestoreFailed, warning: true);

        _license = new LicenseService(_log);
        _ = _license.RefreshAsync();

        if (!_settings.FirstRunCompleted) ShowWelcome();
        ScheduleMemoryTrim();
    }

    // ---------------------------------------------------------------------------------------------
    // Commands (menu, icon, hotkey, windows)
    // ---------------------------------------------------------------------------------------------

    internal bool NextKeepDisplayOn => _controller.Options?.KeepDisplayOn ?? _nextKeepDisplayOn;

    internal bool NextLidMode => _controller.Options?.LidMode ?? (_nextLidMode && _controller.LidAccess == LidAccess.Allowed);

    internal bool IsLicensed => _license.IsActive;

    internal void Toggle()
    {
        if (_controller.State == AwakeState.Active) _controller.Stop(StopReason.User);
        else Start(new EndCondition.Indefinite());
    }

    internal void Start(EndCondition end)
    {
        if (!_license.IsActive)
        {
            ShowTrialExpired();
            return;
        }
        _controller.Start(new SessionOptions(end, NextKeepDisplayOn, NextLidMode));
    }

    internal void StopSession() => _controller.Stop(StopReason.User);

    internal void SetKeepDisplayOn(bool on)
    {
        _nextKeepDisplayOn = on;
        _controller.SetKeepDisplayOn(on);
        RefreshTray();
    }

    internal void SetLidMode(bool on)
    {
        // On battery, the first activation asks whether the battery (DC) action may be changed too.
        // Whatever the answer, lid mode stays on: a "no" simply keeps it mains-only.
        if (on) ConfirmBatteryOnce();
        _nextLidMode = on;
        _controller.SetLidMode(on);
        RefreshTray();
    }

    /// <summary>
    /// The first time lid mode is enabled while on battery, explain the risk and ask whether the battery (DC)
    /// lid action should be changed too. Asked once; the answer can be changed in the settings.
    /// </summary>
    internal bool ConfirmBatteryOnce()
    {
        if (_settings.LidModeOnBattery || _settings.BatteryQuestionAsked) return true;
        if (new WindowsPowerStatus().GetStatus().OnBattery != true) return true;

        bool yes = AskBatteryConfirmation(null);
        _settings.BatteryQuestionAsked = true;
        _settings.LidModeOnBattery = yes;
        SaveSettings(_settings);
        return yes;
    }

    internal bool AskBatteryConfirmation(Window? owner)
    {
        string body = string.Format(CultureInfo.CurrentCulture, Strings.Battery_Body,
            _settings.BatteryThresholdPercent, StatusText.FormatDuration(TimeSpan.FromMinutes(_settings.MaxLidClosedOnBatteryMinutes)));
        MessageBoxResult r = owner is null
            ? MessageBox.Show(body, Strings.Battery_Title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, body, Strings.Battery_Title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        return r == MessageBoxResult.Yes;
    }

    internal void SaveSettings(AppSettings updated)
    {
        bool batteryChanged = updated.LidModeOnBattery != _settings.LidModeOnBattery;
        _settings = updated;
        _controller.Settings = updated;
        try
        {
            _settingsStore.Save(updated);
        }
        catch (Exception ex)
        {
            _log.Error("Could not save settings", ex);
        }
        if (batteryChanged) _controller.RefreshLidOverride();
        ApplyHotkey(notifyOnFailure: true);
        RefreshTray();
    }

    /// <summary>"Restore the original power settings now" (settings window).</summary>
    internal RestoreOutcome RestoreNow()
    {
        if (_controller.State == AwakeState.Active && _controller.Options?.LidMode == true)
            _controller.SetLidMode(false);
        RestoreOutcome outcome = _lid.Restore();
        RefreshTray();
        return outcome;
    }

    internal void ShowSettings() => ShowSingle<SettingsWindow>(() => new SettingsWindow(this));

    internal void ShowWelcome() => ShowSingle<WelcomeWindow>(() => new WelcomeWindow(this));

    internal void ShowLidGuide() => ShowSingle<LidGuideWindow>(() => new LidGuideWindow(this));

    internal void ShowProcessPicker() => ShowSingle<ProcessPickerWindow>(() => new ProcessPickerWindow(this));

    internal void ShowCustomDuration() => ShowSingle<CustomDurationWindow>(() => new CustomDurationWindow(this));

    internal void MarkFirstRunCompleted()
    {
        if (_settings.FirstRunCompleted) return;
        AppSettings s = _settings.Clone();
        s.FirstRunCompleted = true;
        SaveSettings(s);
    }

    internal void ShowTrialExpired()
    {
        MessageBoxResult r = MessageBox.Show(Strings.Trial_Expired_Body, Strings.Trial_Expired_Title,
            MessageBoxButton.OKCancel, MessageBoxImage.Information);
        if (r == MessageBoxResult.OK) _ = BuyAsync();
    }

    internal async Task BuyAsync() => await _license.BuyAsync(_messages.Handle);

    internal string? TrialText =>
        _license.IsTrial && _license.TrialRemaining is { } left
            ? string.Format(CultureInfo.CurrentCulture, Strings.Trial_DaysLeft, Math.Max(1, (int)Math.Ceiling(left.TotalDays)))
            : null;

    internal void Quit()
    {
        if (_exiting) return;
        _exiting = true;
        _log.Info("Quit requested");
        Cleanup();
        Shutdown();
    }

    // ---------------------------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------------------------

    private void OnTick()
    {
        _controller.Tick();
        RefreshTray();
    }

    private void OnStateChanged(object? sender, StateChangedEventArgs e)
    {
        ScheduleMemoryTrim();
        if (_controller.State == AwakeState.Active) _timer.Start();
        else _timer.Stop();

        EnsureWatchdog();
        RefreshTray();

        if (e.LidError is { } error)
            _tray.Notify(Strings.AppName, string.Format(CultureInfo.CurrentCulture, Strings.Notify_LidError, error), warning: true);

        switch (e.StopReason)
        {
            case StopReason.TimerElapsed or StopReason.ProcessesEnded:
                _tray.Notify(Strings.Notify_TaskDone_Title, Strings.Notify_TaskDone_Body);
                break;
            case StopReason.LowBattery:
                _tray.Notify(Strings.Notify_Guard_Title, string.Format(CultureInfo.CurrentCulture,
                    Strings.Notify_Guard_LowBattery, _settings.BatteryThresholdPercent), warning: true);
                break;
            case StopReason.LidClosedTooLongOnBattery:
                _tray.Notify(Strings.Notify_Guard_Title, string.Format(CultureInfo.CurrentCulture,
                    Strings.Notify_Guard_LidTooLong, StatusText.FormatDuration(TimeSpan.FromMinutes(_settings.MaxLidClosedOnBatteryMinutes))), warning: true);
                break;
            case StopReason.SystemSuspend:
                _tray.Notify(Strings.Notify_Suspended_Title, Strings.Notify_Suspended_Body);
                break;
        }

        if (e.LidReminder)
            _tray.Notify(Strings.Notify_LidReminder_Title, Strings.Notify_LidReminder_Body);
    }

    private void OnActiveSchemeChanged()
    {
        try
        {
            _controller.OnPowerSchemeChanged();
        }
        catch (Exception ex)
        {
            _log.Error("Power scheme change handling failed", ex);
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        // Raised on a SystemEvents thread.
        Dispatcher.Invoke(() =>
        {
            switch (e.Mode)
            {
                case PowerModes.Suspend:
                    _log.Info("System suspending");
                    _controller.OnSuspend();
                    break;
                case PowerModes.Resume:
                    _log.Info("System resumed");
                    _controller.OnResume();
                    break;
            }
        });
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        // Restore and exit. If the user later cancels the sign-out, KeepLid is simply closed (nothing left modified).
        _log.Info($"Session ending ({e.ReasonSessionEnding}): restoring");
        Quit();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cleanup();
        base.OnExit(e);
    }

    private void HookUnhandledExceptions()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            _log.Error("Unhandled UI exception", e.Exception);
            EmergencyRestore();
            e.Handled = true; // keep the tray app alive, in a clean (inactive) state
            try { _controller?.Stop(StopReason.AppExit); } catch (Exception) { }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            _log.Error("Fatal unhandled exception", e.ExceptionObject as Exception);
            EmergencyRestore();
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            _log.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        };
    }

    private void EmergencyRestore()
    {
        try { _execution?.Release(); } catch (Exception) { }
        try { _lid?.Restore(); } catch (Exception) { }
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    private bool _cleanedUp;

    private void Cleanup()
    {
        if (_cleanedUp) return;
        _cleanedUp = true;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        try { _controller?.Stop(StopReason.AppExit); } catch (Exception ex) { _log.Error("Stop failed", ex); }
        try { _lid?.Restore(); } catch (Exception ex) { _log.Error("Restore failed", ex); }
        _timer?.Stop();
        _execution?.Dispose();
        _messages?.Dispose();
        _tray?.Dispose();
        _log.Info("KeepLid stopped; settings restored");
    }

    private void RefreshTray()
    {
        if (_tray is null) return;
        TrayState state = _controller.State switch
        {
            AwakeState.Active when _controller.LidOverrideApplied => TrayState.ActiveLid,
            AwakeState.Active => TrayState.Active,
            AwakeState.GuardTripped => TrayState.Guard,
            _ => TrayState.Idle,
        };
        _tray.Update(state, StatusText.Tooltip(_controller));
    }

    /// <summary>
    /// A tray app idles for hours: once startup or a window is done, give unused pages back to Windows
    /// (they are reloaded on demand). Keeps the footprint shown in Task Manager well under 50 MB.
    /// </summary>
    private void ScheduleMemoryTrim()
    {
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, Dispatcher) { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (Windows.Count > 0) return; // a window is open: trim when it closes
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Native.NativeMethods.SetProcessWorkingSetSize(Native.NativeMethods.GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
        };
        timer.Start();
    }

    private void EnsureWatchdog()
    {
        if (!_lid.IsApplied) return;
        if (_watchdog is { HasExited: false }) return;
        _watchdog?.Dispose();
        _watchdog = Watchdog.Start(_log, _paths);
    }

    private void ApplyHotkey(bool notifyOnFailure)
    {
        Hotkey? hotkey = _settings.HotkeyEnabled && Hotkey.TryParse(_settings.Hotkey, out var h) ? h : null;
        if (!_messages.SetHotkey(hotkey) && notifyOnFailure)
            _tray.Notify(Strings.AppName, string.Format(CultureInfo.CurrentCulture, Strings.Notify_HotkeyFailed, _settings.Hotkey), warning: true);
    }

    private void ShowSingle<T>(Func<T> create) where T : Window
    {
        T? existing = Windows.OfType<T>().FirstOrDefault();
        if (existing is not null)
        {
            existing.Activate();
            return;
        }
        T window = create();
        window.Closed += (_, _) => ScheduleMemoryTrim();
        window.Show();
        window.Activate();
    }
}
