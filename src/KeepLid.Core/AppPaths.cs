namespace KeepLid.Core;

/// <summary>
/// Files in %LOCALAPPDATA%\KeepLid\. When running from the MSIX package, Windows transparently redirects
/// this folder to %LOCALAPPDATA%\Packages\&lt;PackageFamilyName&gt;\LocalCache\Local\KeepLid\ (the app sees
/// the same path either way). Nothing is ever written outside this folder.
/// </summary>
public sealed class AppPaths(string dataDirectory)
{
    public const string AppName = "KeepLid";

    public static AppPaths Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName));

    public string DataDirectory { get; } = dataDirectory;

    /// <summary>Original power values to put back (exists only while a change is pending).</summary>
    public string RestoreFile => Path.Combine(DataDirectory, "restore.json");

    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public string LogFile => Path.Combine(DataDirectory, "log.txt");
}
