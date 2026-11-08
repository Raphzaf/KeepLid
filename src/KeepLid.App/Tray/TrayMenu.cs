using System;
using System.Windows.Forms;
using KeepLid.Core.Power;
using KeepLid.Core.Resources;
using KeepLid.Core.Session;

namespace KeepLid.App.Tray;

/// <summary>Builds the right-click menu from the current state (rebuilt every time it opens).</summary>
internal static class TrayMenu
{
    public static void Build(ContextMenuStrip menu, App app)
    {
        AwakeController c = app.Controller;
        menu.Items.Clear();

        // Status line (not clickable).
        menu.Items.Add(new ToolStripMenuItem(StatusText.Tooltip(c)) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        // Keep awake ▸ durations / until off / until a process ends.
        var keepAwake = new ToolStripMenuItem(Strings.Menu_KeepAwake);
        keepAwake.DropDownItems.Add(Item(Strings.Menu_30Min, () => app.Start(new EndCondition.Duration(TimeSpan.FromMinutes(30)))));
        keepAwake.DropDownItems.Add(Item(Strings.Menu_1H, () => app.Start(new EndCondition.Duration(TimeSpan.FromHours(1)))));
        keepAwake.DropDownItems.Add(Item(Strings.Menu_2H, () => app.Start(new EndCondition.Duration(TimeSpan.FromHours(2)))));
        keepAwake.DropDownItems.Add(Item(Strings.Menu_4H, () => app.Start(new EndCondition.Duration(TimeSpan.FromHours(4)))));
        keepAwake.DropDownItems.Add(Item(Strings.Menu_Custom, app.ShowCustomDuration));
        keepAwake.DropDownItems.Add(new ToolStripSeparator());
        keepAwake.DropDownItems.Add(Item(Strings.Menu_Indefinite, () => app.Start(new EndCondition.Indefinite())));
        keepAwake.DropDownItems.Add(Item(Strings.Menu_UntilProcess, app.ShowProcessPicker));
        menu.Items.Add(keepAwake);

        if (c.State == AwakeState.Active)
            menu.Items.Add(Item(Strings.Menu_TurnOff, app.StopSession));

        menu.Items.Add(new ToolStripSeparator());

        var display = new ToolStripMenuItem(Strings.Menu_KeepDisplayOn) { Checked = app.NextKeepDisplayOn };
        display.Click += (_, _) => app.SetKeepDisplayOn(!display.Checked);
        menu.Items.Add(display);

        switch (c.LidAccess)
        {
            case LidAccess.Allowed:
                var lid = new ToolStripMenuItem(Strings.Menu_LidMode) { Checked = app.NextLidMode };
                lid.Click += (_, _) => app.SetLidMode(!lid.Checked);
                menu.Items.Add(lid);
                break;
            case LidAccess.Denied:
                menu.Items.Add(Item(Strings.Menu_LidGuide, app.ShowLidGuide));
                break;
            default: // no lid (desktop PC): shown greyed out
                menu.Items.Add(new ToolStripMenuItem(Strings.Menu_LidModeNoLid) { Enabled = false });
                break;
        }

        menu.Items.Add(new ToolStripSeparator());

        if (!app.IsLicensed)
            menu.Items.Add(Item(Strings.Menu_Buy, () => _ = app.BuyAsync()));
        else if (app.TrialText is { } trial)
            menu.Items.Add(new ToolStripMenuItem(trial) { Enabled = false });

        menu.Items.Add(Item(Strings.Menu_Settings, app.ShowSettings));
        menu.Items.Add(Item(Strings.Menu_Quit, app.Quit));
    }

    private static ToolStripMenuItem Item(string text, Action onClick)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => onClick();
        return item;
    }
}
