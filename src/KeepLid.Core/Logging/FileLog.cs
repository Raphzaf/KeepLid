using System.Text;
using KeepLid.Core.Abstractions;

namespace KeepLid.Core.Logging;

/// <summary>
/// Minimal thread-safe text log (log.txt) for support. When the file reaches <see cref="MaxBytes"/> it is
/// renamed to log.1.txt (replacing the previous one), so at most ~2 MB are kept. Never throws.
/// </summary>
public sealed class FileLog(string path, long maxBytes = FileLog.DefaultMaxBytes) : ILog
{
    public const long DefaultMaxBytes = 1024 * 1024;

    private readonly object _gate = new();

    public string Path { get; } = path;

    public long MaxBytes { get; } = maxBytes;

    public string RotatedPath => System.IO.Path.ChangeExtension(Path, ".1.txt");

    public void Info(string message) => Write("INFO ", message);

    public void Warn(string message) => Write("WARN ", message);

    public void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");

    private void Write(string level, string message)
    {
        string line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {level} [{Environment.ProcessId}] {message}{Environment.NewLine}";
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                var info = new FileInfo(Path);
                if (info.Exists && info.Length + line.Length > MaxBytes)
                    File.Move(Path, RotatedPath, overwrite: true);
                File.AppendAllText(Path, line, Encoding.UTF8);
            }
            catch (Exception)
            {
                // Logging must never break the app (disk full, file locked by an editor…).
            }
        }
    }
}

/// <summary>Discards everything (tests, or before the data folder is known).</summary>
public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();

    public void Info(string message) { }

    public void Warn(string message) { }

    public void Error(string message, Exception? ex = null) { }
}
