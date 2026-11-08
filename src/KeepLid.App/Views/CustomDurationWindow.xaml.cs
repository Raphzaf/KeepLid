using System;
using System.Globalization;
using System.Windows;
using KeepLid.Core.Resources;
using KeepLid.Core.Session;
using KeepLid.Core.Settings;

namespace KeepLid.App.Views;

/// <summary>"Custom duration…": a number of minutes, remembered for next time.</summary>
public partial class CustomDurationWindow : Window
{
    private readonly App _app;

    internal CustomDurationWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Minutes.Text = app.Settings.CustomDurationMinutes.ToString(CultureInfo.CurrentCulture);
        Loaded += (_, _) => { Minutes.Focus(); Minutes.SelectAll(); };
    }

    private void OnStart(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(Minutes.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int minutes) ||
            minutes < 1 || minutes > AppSettings.MaxLidMinutes)
        {
            MessageBox.Show(this, string.Format(CultureInfo.CurrentCulture, Strings.Settings_InvalidNumber, 1, AppSettings.MaxLidMinutes),
                Strings.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        AppSettings s = _app.Settings.Clone();
        s.CustomDurationMinutes = minutes;
        _app.SaveSettings(s);
        _app.Start(new EndCondition.Duration(TimeSpan.FromMinutes(minutes)));
        Close();
    }
}
