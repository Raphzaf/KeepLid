using KeepLid.Core.Abstractions;

namespace KeepLid.Core.Power;

public enum RestoreOutcome
{
    /// <summary>No pending change: nothing to restore.</summary>
    NothingToRestore,
    /// <summary>Original values written back and restore.json deleted.</summary>
    Restored,
    /// <summary>The power API refused the write; restore.json is kept for a later attempt.</summary>
    Failed,
    /// <summary>restore.json was unreadable; it was renamed to restore.corrupt.json for support.</summary>
    Corrupt,
}

/// <summary>
/// Temporarily sets the lid close action to "Do nothing" and guarantees it can be put back.
///
/// Invariant: restore.json exists on disk if and only if a system value may differ from the user's
/// original. It is written BEFORE any modification and deleted only AFTER a successful restore, so a
/// crash at any point leaves enough information to restore at the next launch (or by the watchdog).
/// </summary>
public sealed class LidOverride(IPowerSettings power, IRestoreStore store, ILog log)
{
    /// <summary>winnt.h POWERBUTTON_ACTION_INDEX_NOTHING.</summary>
    public const uint ActionDoNothing = 0;

    private readonly object _gate = new();

    /// <summary>True while a modification is pending restoration.</summary>
    public bool IsApplied
    {
        get { lock (_gate) return store.Exists; }
    }

    /// <summary>
    /// Sets the lid action of the active scheme to "Do nothing" on AC, and also on DC (battery) when
    /// <paramref name="includeBattery"/> is true. Idempotent.
    /// </summary>
    /// <exception cref="PowerSettingsException">The system refused the change (already rolled back).</exception>
    public void Apply(bool includeBattery)
    {
        lock (_gate)
        {
            Guid scheme = power.GetActiveScheme();
            RestoreRecord? existing = TryLoad();
            if (existing is not null)
            {
                bool sameTarget = existing.SchemeGuid == scheme && existing.OriginalDc.HasValue == includeBattery;
                if (sameTarget) return;
                RestoreCore(); // different scheme or battery option changed: start from a clean state
            }

            LidActions original = power.ReadLidActions(scheme);
            var record = new RestoreRecord
            {
                SchemeGuid = scheme,
                OriginalAc = original.Ac,
                OriginalDc = includeBattery ? original.Dc : null,
                AppliedValue = ActionDoNothing,
                CreatedAt = DateTimeOffset.Now,
                ProcessId = Environment.ProcessId,
            };

            store.Save(record); // 1. backup first
            try
            {
                power.WriteLidActions(scheme, ActionDoNothing, includeBattery ? ActionDoNothing : null); // 2. modify
                power.ApplyScheme(scheme);
                log.Info($"Lid override applied on scheme {scheme} (AC {original.Ac}->0" +
                         (includeBattery ? $", DC {original.Dc}->0)" : ", DC untouched)"));
            }
            catch (Exception ex)
            {
                log.Error("Lid override failed, rolling back", ex);
                RestoreCore();
                throw;
            }
        }
    }

    /// <summary>Puts the original values back if a change is pending. Safe to call at any time, any number of times.</summary>
    public RestoreOutcome Restore()
    {
        lock (_gate) return RestoreCore();
    }

    /// <summary>
    /// Call when the active power scheme may have changed: the old scheme is restored and the override
    /// is re-applied to the new one.
    /// </summary>
    public void FollowActiveScheme()
    {
        lock (_gate)
        {
            RestoreRecord? record = TryLoad();
            if (record is null) return;
            Guid active = power.GetActiveScheme();
            if (active == record.SchemeGuid) return;

            log.Info($"Active power scheme changed {record.SchemeGuid} -> {active}: moving lid override");
            if (RestoreCore() == RestoreOutcome.Restored)
                Apply(record.OriginalDc.HasValue);
        }
    }

    private RestoreRecord? TryLoad()
    {
        try
        {
            return store.Load();
        }
        catch (InvalidDataException ex)
        {
            QuarantineCorruptFile(ex);
            return null;
        }
    }

    private RestoreOutcome RestoreCore()
    {
        RestoreRecord? record;
        try
        {
            record = store.Load();
        }
        catch (InvalidDataException ex)
        {
            QuarantineCorruptFile(ex);
            return RestoreOutcome.Corrupt;
        }
        if (record is null) return RestoreOutcome.NothingToRestore;

        try
        {
            LidActions current = power.ReadLidActions(record.SchemeGuid);

            // Only put back a value that still holds what KeepLid wrote: if the user changed the
            // lid action by hand meanwhile, their newer choice wins.
            uint? ac = record.OriginalAc.HasValue && current.Ac == record.AppliedValue ? record.OriginalAc : null;
            uint? dc = record.OriginalDc.HasValue && current.Dc == record.AppliedValue ? record.OriginalDc : null;
            if (record.OriginalAc.HasValue && ac is null)
                log.Warn($"AC lid action was changed by the user meanwhile ({current.Ac}); keeping it");
            if (record.OriginalDc.HasValue && dc is null)
                log.Warn($"DC lid action was changed by the user meanwhile ({current.Dc}); keeping it");

            if (ac.HasValue || dc.HasValue)
            {
                power.WriteLidActions(record.SchemeGuid, ac, dc);
                if (power.GetActiveScheme() == record.SchemeGuid)
                    power.ApplyScheme(record.SchemeGuid);
            }

            store.Delete();
            log.Info($"Lid action restored on scheme {record.SchemeGuid} (AC={ac?.ToString() ?? "kept"}, DC={dc?.ToString() ?? "kept"})");
            return RestoreOutcome.Restored;
        }
        catch (PowerSettingsException ex) when (ex.ErrorCode == 2) // ERROR_FILE_NOT_FOUND: scheme was deleted
        {
            log.Warn($"Scheme {record.SchemeGuid} no longer exists; nothing left to restore");
            store.Delete();
            return RestoreOutcome.Restored;
        }
        catch (Exception ex)
        {
            log.Error("Restore failed; restore.json kept for a later attempt", ex);
            return RestoreOutcome.Failed;
        }
    }

    private void QuarantineCorruptFile(Exception ex)
    {
        log.Error("restore.json is corrupt; original values unknown. Check 'powercfg /q' manually.", ex);
        if (store is FileRestoreStore file && File.Exists(file.Path))
            File.Move(file.Path, Path.ChangeExtension(file.Path, ".corrupt.json"), overwrite: true);
        else
            store.Delete();
    }
}
