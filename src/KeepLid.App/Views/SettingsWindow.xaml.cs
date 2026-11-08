using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KeepLid.App.Platform;
using KeepLid.Core.Power;
using KeepLid.Core.Resources;
using KeepLid.Core.Session;
using KeepLid.Core.Settings;
using Windows.ApplicationModel;

namespace KeepLid.App.Views;

/// <summary>Single-page settings window. Changes apply on "Save".</summary>
public partial class SettingsWindow : Window
{
    private readonly App _app;
    private bool _loading = true;
    private bool _startupInitiallyEnabled;

    internal SettingsWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Load(app.Settings);
        Loaded += async (_, _) => await LoadStartupStateAsync();
    }

    private void Load(AppSettings s)
    {
        VersionText.Text = string.Format(CultureInfo.CurrentCulture, Strings.Settings_Version, PackageInfo.Version);
        KeepDisplayOnDefault.IsChecked = s.KeepDisplayOnByDefault;
        LidModeDefault.IsChecked = s.LidModeByDefault;
        LidOnBattery.IsChecked = s.LidModeOnBattery;
        BatteryThreshold.Text = s.BatteryThresholdPercent.ToString(CultureInfo.CurrentCulture);
        MaxLidMinutes.Text = s.MaxLidClosedOnBatteryMinutes.ToString(CultureInfo.CurrentCulture);
        Processes.Text = string.Join(Environment.NewLine, s.FavoriteProcesses);
        HotkeyEnabled.IsChecked = s.HotkeyEnabled;
        HotkeyBox.Text = s.Hotkey;

        LidAccess access = _app.Controller.LidAccess;
        LidStatus.Text = access switch
        {
            LidAccess.Allowed => Strings.Settings_LidAllowed,
            LidAccess.Denied => Strings.Settings_LidDenied,
            _ => Strings.Settings_LidNoLid,
        };
        bool hasLid = access is LidAccess.Allowed or LidAccess.Denied;
        LidModeDefault.IsEnabled = access == LidAccess.Allowed;
        LidOnBattery.IsEnabled = access == LidAccess.Allowed;
        OpenGuide.Visibility = access == LidAccess.Denied ? Visibility.Visible : Visibility.Collapsed;
        LidCurrent.Visibility = hasLid ? Visibility.Visible : Visibility.Collapsed;
        if (hasLid && _app.Lid.TryReadCurrent() is { } current)
            LidCurrent.Text = string.Format(CultureInfo.CurrentCulture, Strings.Settings_CurrentLid,
                StatusText.LidActionName(current.Ac), StatusText.LidActionName(current.Dc));
        _loading = false;
    }

    private async System.Threading.Tasks.Task LoadStartupStateAsync()
    {
        StartupTaskState? state = await StartupTaskService.GetStateAsync();
        _startupInitiallyEnabled = state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        StartWithWindows.IsChecked = _startupInitiallyEnabled;
        switch (state)
        {
            case null:
                StartWithWindows.IsEnabled = false;
                ShowStartupHint(Strings.Settings_StartupUnavailable);
                break;
            case StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy or StartupTaskState.EnabledByPolicy:
                StartWithWindows.IsEnabled = false;
                ShowStartupHint(Strings.Settings_StartupDisabledByUser);
                break;
        }
    }

    private void ShowStartupHint(string text)
    {
        StartupHint.Text = text;
        StartupHint.Visibility = Visibility.Visible;
    }

    private void OnLidOnBatteryChecked(object sender, RoutedEventArgs e)
    {
        if (_loading || _app.Settings.LidModeOnBattery) return;
        if (!_app.AskBatteryConfirmation(this)) LidOnBattery.IsChecked = false;
    }

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        string? name = key switch
        {
            >= Key.A and <= Key.Z => key.ToString(),
            >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
            >= Key.F1 and <= Key.F24 => key.ToString(),
            _ => null,
        };
        if (name is null) return; // waiting for the non-modifier key
        ModifierKeys m = Keyboard.Modifiers;
        bool win = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);
        HotkeyBox.Text = Hotkey.Format(m.HasFlag(ModifierKeys.Control), m.HasFlag(ModifierKeys.Alt), m.HasFlag(ModifierKeys.Shift), win, name);
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (!TryReadInt(BatteryThreshold.Text, AppSettings.MinBatteryThreshold, AppSettings.MaxBatteryThreshold, out int threshold) ||
            !TryReadInt(MaxLidMinutes.Text, AppSettings.MinLidMinutes, AppSettings.MaxLidMinutes, out int maxLid))
            return;
        if (HotkeyEnabled.IsChecked == true && !Hotkey.TryParse(HotkeyBox.Text, out _))
        {
            MessageBox.Show(this, Strings.Settings_InvalidHotkey, Strings.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AppSettings s = _app.Settings.Clone();
        s.KeepDisplayOnByDefault = KeepDisplayOnDefault.IsChecked == true;
        s.LidModeByDefault = LidModeDefault.IsChecked == true;
        s.LidModeOnBattery = LidOnBattery.IsChecked == true;
        if (s.LidModeOnBattery) s.BatteryQuestionAsked = true;
        s.BatteryThresholdPercent = threshold;
        s.MaxLidClosedOnBatteryMinutes = maxLid;
        s.FavoriteProcesses = Processes.Text.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        s.HotkeyEnabled = HotkeyEnabled.IsChecked == true;
        if (Hotkey.TryParse(HotkeyBox.Text, out _)) s.Hotkey = HotkeyBox.Text;
        _app.SaveSettings(s);

        if (StartWithWindows.IsEnabled && StartWithWindows.IsChecked != _startupInitiallyEnabled)
            await StartupTaskService.SetEnabledAsync(StartWithWindows.IsChecked == true);
        Close();
    }

    private bool TryReadInt(string text, int min, int max, out int value)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value) && value >= min && value <= max)
            return true;
        MessageBox.Show(this, string.Format(CultureInfo.CurrentCulture, Strings.Settings_InvalidNumber, min, max),
            Strings.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private void OnRestore(object sender, RoutedEventArgs e)
    {
        RestoreResult.Text = _app.RestoreNow() switch
        {
            RestoreOutcome.Restored => Strings.Settings_RestoreDone,
            RestoreOutcome.NothingToRestore => Strings.Settings_RestoreNothing,
            _ => Strings.Settings_RestoreFailed,
        };
        Load(_app.Settings);
    }

    private void OnOpenLog(object sender, RoutedEventArgs e)
    {
        _app.Log.Info("Log opened from settings");
        Shell.RevealFile(_app.Paths.LogFile);
    }

    private void OnOpenGuide(object sender, RoutedEventArgs e) => _app.ShowLidGuide();

    private void OnPrivacy(object sender, RoutedEventArgs e) => Shell.Open(AppLinks.Privacy);

    private void OnHelp(object sender, RoutedEventArgs e) => Shell.Open(AppLinks.Help);

    private void OnWelcome(object sender, RoutedEventArgs e) => _app.ShowWelcome();
}
