using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeepLid.Core.Power;

/// <summary>
/// Content of restore.json: everything needed to put the lid close action back exactly as it was.
/// Written to disk BEFORE any system setting is modified.
/// </summary>
public sealed record RestoreRecord
{
    public int FormatVersion { get; init; } = 1;

    /// <summary>Power scheme whose values were modified.</summary>
    public Guid SchemeGuid { get; init; }

    /// <summary>Original AC lid action, or null if the AC value was not modified.</summary>
    public uint? OriginalAc { get; init; }

    /// <summary>Original DC lid action, or null if the DC value was not modified.</summary>
    public uint? OriginalDc { get; init; }

    /// <summary>Value KeepLid wrote (used to detect a manual change made by the user meanwhile).</summary>
    public uint AppliedValue { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Process that made the change (diagnostics only).</summary>
    public int ProcessId { get; init; }
}

/// <summary>Persists <see cref="RestoreRecord"/>. Abstracted for tests.</summary>
public interface IRestoreStore
{
    bool Exists { get; }

    /// <summary>Returns the record, or null if missing. Throws <see cref="InvalidDataException"/> if unreadable.</summary>
    RestoreRecord? Load();

    void Save(RestoreRecord record);

    void Delete();
}

/// <summary>restore.json on disk, written atomically (temp file + move) and flushed.</summary>
public sealed class FileRestoreStore(string path) : IRestoreStore
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string Path { get; } = path;

    public bool Exists => File.Exists(Path);

    public RestoreRecord? Load()
    {
        if (!File.Exists(Path)) return null;
        try
        {
            var record = JsonSerializer.Deserialize<RestoreRecord>(File.ReadAllText(Path), JsonOptions);
            if (record is null || record.SchemeGuid == Guid.Empty)
                throw new InvalidDataException("restore.json is empty or has no scheme GUID");
            return record;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("restore.json is not valid JSON", ex);
        }
    }

    public void Save(RestoreRecord record)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        string tmp = Path + ".tmp";
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, record, JsonOptions);
            stream.Flush(flushToDisk: true); // must survive a crash or power loss right after
        }
        File.Move(tmp, Path, overwrite: true);
    }

    public void Delete()
    {
        if (File.Exists(Path)) File.Delete(Path);
    }
}
