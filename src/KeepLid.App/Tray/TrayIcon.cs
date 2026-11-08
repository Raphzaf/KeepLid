using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using KeepLid.Core.Session;

namespace KeepLid.App.Tray;

/// <summary>Visual state of the notification-area icon.</summary>
internal enum TrayState
{
    Idle,
    Active,
    ActiveLid,
    Guard,
}

/// <summary>
/// Notification-area icon (WinForms NotifyIcon, hosted on the WPF UI thread).
/// Left click = toggle, right click = menu (built by the owner on <see cref="MenuOpening"/>).
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private static readonly TimeSpan ClickDebounce = TimeSpan.FromMilliseconds(400);

    private readonly NotifyIcon _icon;
    private readonly Dictionary<TrayState, Icon> _icons = new();
    private DateTime _lastClick = DateTime.MinValue;

    public TrayIcon()
    {
        Size size = SystemInformation.SmallIconSize;
        _icons[TrayState.Idle] = Load("idle", size);
        _icons[TrayState.Active] = Load("active", size);
        _icons[TrayState.ActiveLid] = Load("active-lid", size);
        _icons[TrayState.Guard] = Load("guard", size);

        Menu = new ContextMenuStrip { ShowCheckMargin = true, ShowImageMargin = false };
        Menu.Items.Add("KeepLid"); // placeholder: an empty strip would not open
        Menu.Opening += (_, e) =>
        {
            MenuOpening?.Invoke(Menu, e);
            e.Cancel = false; // WinForms presets Cancel=true when the strip had no items
        };

        _icon = new NotifyIcon
        {
            Icon = _icons[TrayState.Idle],
            Text = "KeepLid",
            ContextMenuStrip = Menu,
            Visible = true,
        };
        _icon.MouseClick += OnMouseClick;
        _icon.BalloonTipClicked += (_, _) => NotificationClicked?.Invoke();
    }

    /// <summary>Raised before the menu shows: the owner (re)builds its items.</summary>
    public event Action<ContextMenuStrip, System.ComponentModel.CancelEventArgs>? MenuOpening;

    public event Action? LeftClick;

    public event Action? NotificationClicked;

    public ContextMenuStrip Menu { get; }

    public void Update(TrayState state, string tooltip)
    {
        _icon.Icon = _icons[state];
        _icon.Text = StatusText.Truncate(tooltip);
    }

    /// <summary>Windows 10/11 show this as a toast notification.</summary>
    public void Notify(string title, string text, bool warning = false) =>
        _icon.ShowBalloonTip(5000, title, text, warning ? ToolTipIcon.Warning : ToolTipIcon.Info);

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        DateTime now = DateTime.UtcNow;
        if (now - _lastClick < ClickDebounce) return; // a double-click must not toggle twice
        _lastClick = now;
        LeftClick?.Invoke();
    }

    private static Icon Load(string name, Size size)
    {
        using Stream stream = typeof(TrayIcon).Assembly.GetManifestResourceStream($"Tray.{name}.ico")
            ?? throw new InvalidOperationException($"Missing tray icon {name}");
        return new Icon(stream, size);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        Menu.Dispose();
        foreach (Icon icon in _icons.Values) icon.Dispose();
    }
}
