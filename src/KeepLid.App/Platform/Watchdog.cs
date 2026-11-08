using System;
using System.Diagnostics;
using System.Threading;
using KeepLid.Core;
using KeepLid.Core.Abstractions;
using KeepLid.Core.Logging;
using KeepLid.Core.Power;

namespace KeepLid.App.Platform;

/// <summary>
/// Restores the lid setting if the main KeepLid process ends while restore.json still exists.
/// Runs as a separate, windowless process (same exe, "--watchdog &lt;pid&gt;") only while the lid override
/// is applied, and exits by itself within ~2 s once the main process has restored normally.
/// Limits: if both processes are killed at once (Task Manager "End task" on the whole app group, power
/// loss), the restore happens at the next launch of KeepLid instead.
/// </summary>
internal static class Watchdog
{
    public const string Argument = "--watchdog";

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    public static int Run(int parentPid)
    {
        AppPaths paths = AppPaths.Default;
        var log = new FileLog(paths.LogFile);
        var store = new FileRestoreStore(paths.RestoreFile);

        Process? parent = null;
        try
        {
            parent = Process.GetProcessById(parentPid);
            _ = parent.Handle; // open the handle now so that PID reuse cannot fool us later
        }
        catch (Exception)
        {
            parent = null;
        }

        try
        {
            while (store.Exists)
            {
                bool parentGone = parent is null || parent.WaitForExit((int)PollInterval.TotalMilliseconds);
                if (!parentGone) continue;

                Thread.Sleep(500); // a normal exit may be finishing its own restore
                if (store.Exists)
                {
                    log.Warn($"Watchdog: KeepLid (pid {parentPid}) ended without restoring; restoring now");
                    new LidOverride(new WindowsPowerSettings(), store, log).Restore();
                }
                break;
            }
            return 0;
        }
        catch (Exception ex)
        {
            log.Error("Watchdog failed", ex);
            return 1;
        }
        finally
        {
            parent?.Dispose();
        }
    }

    /// <summary>Starts a watchdog for the current process. Returns null if it could not be started.</summary>
    public static Process? Start(ILog log)
    {
        try
        {
            var psi = new ProcessStartInfo(Environment.ProcessPath!, $"{Argument} {Environment.ProcessId}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Process? p = Process.Start(psi);
            log.Info($"Watchdog started (pid {p?.Id})");
            return p;
        }
        catch (Exception ex)
        {
            log.Error("Could not start the watchdog", ex);
            return null;
        }
    }
}
