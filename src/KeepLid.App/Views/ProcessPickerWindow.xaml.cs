using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using KeepLid.App.Platform;
using KeepLid.Core.Resources;
using KeepLid.Core.Session;

namespace KeepLid.App.Views;

/// <summary>"Until a process ends…": favourites first, then every running process, filterable by name.</summary>
public partial class ProcessPickerWindow : Window
{
    private readonly App _app;
    private readonly List<ProcessItem> _items;
    private readonly ICollectionView _view;

    internal ProcessPickerWindow(App app)
    {
        _app = app;
        InitializeComponent();

        var running = new HashSet<string>(
            new WindowsProcessProbe().GetRunningProcessNames().Select(ProcessWatcher.Normalize),
            StringComparer.OrdinalIgnoreCase);
        var favorites = app.Settings.FavoriteProcesses;

        _items = favorites
            .Select(f => new ProcessItem(f, running.Contains(f), isChecked: running.Contains(f)))
            .Concat(running.Except(favorites, StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n => new ProcessItem(n, true, isChecked: false)))
            .ToList();

        _view = CollectionViewSource.GetDefaultView(_items);
        _view.Filter = o => o is ProcessItem p &&
            (string.IsNullOrWhiteSpace(Search.Text) || p.Name.Contains(Search.Text.Trim(), StringComparison.OrdinalIgnoreCase));
        List.ItemsSource = _view;
        Loaded += (_, _) => Search.Focus();
    }

    private void OnSearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => _view.Refresh();

    private void OnStart(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(i => i.IsChecked).Select(i => i.Name).ToList();
        if (selected.Count == 0 && !string.IsNullOrWhiteSpace(Search.Text))
            selected.Add(ProcessWatcher.Normalize(Search.Text)); // typed a name that is not running yet
        if (selected.Count == 0) return;
        _app.Start(new EndCondition.WhileProcessesRun(selected));
        Close();
    }

    /// <summary>One row of the list.</summary>
    public sealed class ProcessItem(string name, bool isRunning, bool isChecked)
    {
        public string Name { get; } = name;

        public bool IsChecked { get; set; } = isChecked;

        public string StatusText { get; } = "  " + (isRunning ? Strings.Process_Running : Strings.Process_NotRunning);
    }
}
