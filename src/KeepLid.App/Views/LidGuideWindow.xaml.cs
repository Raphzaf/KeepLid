using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using KeepLid.App.Platform;
using KeepLid.Core.Resources;
using KeepLid.Core.Session;

namespace KeepLid.App.Views;

/// <summary>
/// Step-by-step guide to change "When I close the lid" by hand. Shows the current value live (reading is
/// always allowed), so the user sees the change as soon as it is saved in Control Panel.
/// </summary>
public partial class LidGuideWindow : Window
{
    private readonly App _app;
    private readonly DispatcherTimer _refresh;

    internal LidGuideWindow(App app)
    {
        _app = app;
        InitializeComponent();
        _refresh = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => ShowCurrent(), Dispatcher);
        ShowCurrent();
        Closed += (_, _) => _refresh.Stop();
    }

    private void ShowCurrent()
    {
        if (_app.Lid.TryReadCurrent() is { } v)
            Current.Text = string.Format(CultureInfo.CurrentCulture, Strings.Guide_Current,
                StatusText.LidActionName(v.Ac), StatusText.LidActionName(v.Dc));
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppLinks.LidControlPanelFile, AppLinks.LidControlPanelArgs) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception)
        {
            Shell.Open(AppLinks.PowerSettingsUri);
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
