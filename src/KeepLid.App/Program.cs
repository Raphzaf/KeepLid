using System;
using System.Threading;
using KeepLid.App.Platform;

namespace KeepLid.App;

/// <summary>
/// Entry point. Two modes:
///  * normal: the tray application (single instance per user session);
///  * "--watchdog &lt;pid&gt; &lt;restore.json&gt; &lt;log.txt&gt;": a tiny helper started while the lid override is applied, which restores the
///    lid setting if the main process disappears without doing it (killed from Task Manager, crash).
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 4 && args[0] == Watchdog.Argument && int.TryParse(args[1], out int parentPid))
            return Watchdog.Run(parentPid, restoreFile: args[2], logFile: args[3]);

        using var mutex = new Mutex(initiallyOwned: true, @"Local\KeepLid.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance) return 0; // already running: the icon is in the notification area

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
