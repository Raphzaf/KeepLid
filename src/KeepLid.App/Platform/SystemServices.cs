using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using KeepLid.App.Native;
using KeepLid.Core.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace KeepLid.App.Platform;

/// <summary>Running process names (cheap enough to call every few seconds).</summary>
internal sealed class WindowsProcessProbe : IProcessProbe
{
    public IReadOnlyCollection<string> GetRunningProcessNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Process p in Process.GetProcesses())
        {
            using (p)
            {
                try { names.Add(p.ProcessName); }
                catch (InvalidOperationException) { /* exited meanwhile */ }
            }
        }
        return names;
    }
}

/// <summary>Package identity helpers.</summary>
internal static class PackageInfo
{
    private static readonly Lazy<string?> FullName = new(() =>
    {
        uint length = 0;
        int rc = NativeMethods.GetCurrentPackageFullName(ref length, null);
        if (rc == NativeMethods.APPMODEL_ERROR_NO_PACKAGE) return null;
        var sb = new StringBuilder((int)length);
        return NativeMethods.GetCurrentPackageFullName(ref length, sb) == 0 ? sb.ToString() : null;
    });

    /// <summary>True when running from the MSIX package (Store or sideloaded).</summary>
    public static bool IsPackaged => FullName.Value is not null;

    public static string Version =>
        typeof(PackageInfo).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
}

/// <summary>Opens files and links with the shell.</summary>
internal static class Shell
{
    public static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception)
        {
            // Nothing sensible to do if the shell cannot open it.
        }
    }

    /// <summary>
    /// Shows a file in Explorer. Under MSIX, %LOCALAPPDATA% is redirected to the package folder and
    /// Explorer (outside the package) would not find the virtual path, so the real path is resolved first.
    /// </summary>
    public static void RevealFile(string path)
    {
        string real = ResolveRealPath(path);
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{real}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (Exception)
        {
            Open(Path.GetDirectoryName(real)!);
        }
    }

    public static string ResolveRealPath(string path)
    {
        try
        {
            using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var sb = new StringBuilder(1024);
            uint len = NativeMethods.GetFinalPathNameByHandle(handle, sb, (uint)sb.Capacity, 0);
            if (len == 0 || len >= sb.Capacity) return path;
            string result = sb.ToString();
            return result.StartsWith(@"\\?\UNC\", StringComparison.Ordinal) ? @"\\" + result[8..]
                 : result.StartsWith(@"\\?\", StringComparison.Ordinal) ? result[4..]
                 : result;
        }
        catch (Exception)
        {
            return path;
        }
    }
}
