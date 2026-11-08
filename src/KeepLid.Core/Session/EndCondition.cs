namespace KeepLid.Core.Session;

/// <summary>When an awake session ends on its own.</summary>
public abstract record EndCondition
{
    /// <summary>Runs until the user turns it off.</summary>
    public sealed record Indefinite : EndCondition;

    /// <summary>Runs for a fixed duration from the start.</summary>
    public sealed record Duration(TimeSpan Length) : EndCondition;

    /// <summary>Runs while at least one of the watched processes is running.</summary>
    public sealed record WhileProcessesRun(IReadOnlyList<string> ProcessNames) : EndCondition;
}

/// <summary>Countdown for <see cref="EndCondition.Duration"/>.</summary>
public sealed class SessionTimer(DateTimeOffset start, TimeSpan length)
{
    public DateTimeOffset EndsAt { get; } = start + length;

    public TimeSpan Remaining(DateTimeOffset now) => now >= EndsAt ? TimeSpan.Zero : EndsAt - now;

    public bool IsElapsed(DateTimeOffset now) => now >= EndsAt;
}

/// <summary>
/// Tracks watched processes. The session ends once none of them has been seen for
/// <see cref="GracePeriod"/>, which absorbs short gaps (e.g. a tool restarting its worker).
/// The start time counts as "last seen", so a session watching processes that never appear ends
/// after the grace period instead of running forever.
/// </summary>
public sealed class ProcessWatcher
{
    public static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromSeconds(15);

    private readonly HashSet<string> _names;

    public ProcessWatcher(IEnumerable<string> processNames, DateTimeOffset start, TimeSpan? gracePeriod = null)
    {
        _names = new HashSet<string>(processNames.Select(Normalize).Where(n => n.Length > 0), StringComparer.OrdinalIgnoreCase);
        LastSeen = start;
        GracePeriod = gracePeriod ?? DefaultGracePeriod;
    }

    public IReadOnlyCollection<string> Names => _names;

    public TimeSpan GracePeriod { get; }

    public DateTimeOffset LastSeen { get; private set; }

    /// <summary>Processes currently matched, refreshed by <see cref="Update"/>.</summary>
    public IReadOnlyList<string> Running { get; private set; } = Array.Empty<string>();

    /// <summary>Feeds a fresh process list; returns true while the session should continue.</summary>
    public bool Update(IEnumerable<string> runningProcessNames, DateTimeOffset now)
    {
        Running = runningProcessNames.Select(Normalize).Where(_names.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (Running.Count > 0) LastSeen = now;
        return now - LastSeen < GracePeriod;
    }

    /// <summary>"Claude.exe", " node " → "claude", "node".</summary>
    public static string Normalize(string name)
    {
        string n = name.Trim();
        if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n = n[..^4];
        return n.ToLowerInvariant();
    }
}
