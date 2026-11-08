namespace KeepLid.Core.Abstractions;

/// <summary>Lid close action indexes of one power scheme (AC = plugged in, DC = on battery).</summary>
/// <remarks>Values follow winnt.h POWERBUTTON_ACTION_INDEX_*: 0 = do nothing, 1 = sleep, 2 = hibernate, 3 = shut down.</remarks>
public readonly record struct LidActions(uint Ac, uint Dc);

/// <summary>Thrown when a power API returns a Win32 error code.</summary>
public sealed class PowerSettingsException(string operation, uint errorCode)
    : Exception($"{operation} failed with Win32 error {errorCode}")
{
    public string Operation { get; } = operation;
    public uint ErrorCode { get; } = errorCode;
}

/// <summary>
/// Access to the lid close action of Windows power schemes.
/// Abstracted so that the restore logic can be tested without touching the real system.
/// </summary>
public interface IPowerSettings
{
    /// <summary>True when the machine reports a lid switch (GetPwrCapabilities.LidPresent).</summary>
    bool IsLidPresent { get; }

    /// <summary>GUID of the currently active power scheme.</summary>
    Guid GetActiveScheme();

    /// <summary>Reads the lid close action (AC and DC) of a scheme.</summary>
    LidActions ReadLidActions(Guid scheme);

    /// <summary>Writes the AC and/or DC lid close action of a scheme (null = leave unchanged).</summary>
    void WriteLidActions(Guid scheme, uint? ac, uint? dc);

    /// <summary>Re-applies a scheme so that freshly written values take effect.</summary>
    void ApplyScheme(Guid scheme);
}

/// <summary>Battery / mains snapshot (GetSystemPowerStatus).</summary>
/// <param name="OnBattery">True when running on battery.</param>
/// <param name="BatteryPercent">0–100, or null when unknown / no battery.</param>
public readonly record struct PowerStatus(bool OnBattery, int? BatteryPercent);

public interface IPowerStatusProvider
{
    PowerStatus GetStatus();
}

/// <summary>Keeps the system (and optionally the display) awake via SetThreadExecutionState.</summary>
public interface IExecutionState
{
    void KeepAwake(bool keepDisplayOn);
    void Release();
}

/// <summary>Lists the names (without ".exe") of running processes.</summary>
public interface IProcessProbe
{
    IReadOnlyCollection<string> GetRunningProcessNames();
}

public interface IClock
{
    DateTimeOffset Now { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}

public interface ILog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}
