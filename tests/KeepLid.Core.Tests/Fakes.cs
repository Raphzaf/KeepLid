using KeepLid.Core.Abstractions;

namespace KeepLid.Core.Tests;

/// <summary>In-memory power schemes: lets the restore logic be tested without touching the real system.</summary>
internal sealed class FakePowerSettings : IPowerSettings
{
    public static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    public static readonly Guid HighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

    public Dictionary<Guid, LidActions> Schemes { get; } = new()
    {
        [Balanced] = new LidActions(1, 1),         // default: sleep on AC and DC
        [HighPerformance] = new LidActions(2, 1),
    };

    public Guid Active { get; set; } = Balanced;

    public bool IsLidPresent { get; set; } = true;

    /// <summary>Simulates the standard-user ACL observed in the Phase 0 spike.</summary>
    public bool DenyWrites { get; set; }

    public int WriteCount { get; private set; }

    public int ApplyCount { get; private set; }

    public Guid GetActiveScheme() => Active;

    public LidActions ReadLidActions(Guid scheme) =>
        Schemes.TryGetValue(scheme, out var v) ? v : throw new PowerSettingsException("PowerReadACValueIndex", 2);

    public void WriteLidActions(Guid scheme, uint? ac, uint? dc)
    {
        if (DenyWrites) throw new PowerSettingsException("PowerWriteACValueIndex", 5);
        if (!Schemes.TryGetValue(scheme, out var v)) throw new PowerSettingsException("PowerWriteACValueIndex", 2);
        Schemes[scheme] = new LidActions(ac ?? v.Ac, dc ?? v.Dc);
        WriteCount++;
    }

    public void ApplyScheme(Guid scheme)
    {
        if (DenyWrites) throw new PowerSettingsException("PowerSetActiveScheme", 5);
        ApplyCount++;
    }
}

internal sealed class FakeExecutionState : IExecutionState
{
    public bool Awake { get; private set; }

    public bool DisplayOn { get; private set; }

    public void KeepAwake(bool keepDisplayOn)
    {
        Awake = true;
        DisplayOn = keepDisplayOn;
    }

    public void Release()
    {
        Awake = false;
        DisplayOn = false;
    }
}

internal sealed class FakePowerStatus : IPowerStatusProvider
{
    public PowerStatus Status { get; set; } = new(OnBattery: false, BatteryPercent: 100);

    public PowerStatus GetStatus() => Status;
}

internal sealed class FakeProcessProbe : IProcessProbe
{
    public List<string> Running { get; } = new();

    public IReadOnlyCollection<string> GetRunningProcessNames() => Running;
}

internal sealed class FakeClock : IClock
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan span) => Now += span;
}

internal sealed class ListLog : ILog
{
    public List<string> Lines { get; } = new();

    public void Info(string message) => Lines.Add("INFO " + message);

    public void Warn(string message) => Lines.Add("WARN " + message);

    public void Error(string message, Exception? ex = null) => Lines.Add("ERROR " + message);
}

/// <summary>Temporary folder deleted at the end of the test.</summary>
internal sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "keeplid-tests-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}
