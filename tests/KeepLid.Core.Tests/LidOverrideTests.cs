using KeepLid.Core.Abstractions;
using KeepLid.Core.Power;

namespace KeepLid.Core.Tests;

public sealed class LidOverrideTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakePowerSettings _power = new();
    private readonly ListLog _log = new();
    private readonly FileRestoreStore _store;

    public LidOverrideTests() => _store = new FileRestoreStore(_dir.File("restore.json"));

    public void Dispose() => _dir.Dispose();

    private LidOverride Create() => new(_power, _store, _log);

    [Fact]
    public void Apply_on_mains_only_changes_AC_and_saves_backup_first()
    {
        var lid = Create();

        lid.Apply(includeBattery: false);

        Assert.Equal(new LidActions(0, 1), _power.Schemes[FakePowerSettings.Balanced]);
        RestoreRecord record = _store.Load()!;
        Assert.Equal(FakePowerSettings.Balanced, record.SchemeGuid);
        Assert.Equal(1u, record.OriginalAc);
        Assert.Null(record.OriginalDc);
        Assert.True(_power.ApplyCount > 0);
    }

    [Fact]
    public void Apply_with_battery_changes_AC_and_DC()
    {
        Create().Apply(includeBattery: true);

        Assert.Equal(new LidActions(0, 0), _power.Schemes[FakePowerSettings.Balanced]);
        Assert.Equal(1u, _store.Load()!.OriginalDc);
    }

    [Fact]
    public void Restore_puts_original_values_back_and_deletes_file()
    {
        var lid = Create();
        lid.Apply(includeBattery: true);

        Assert.Equal(RestoreOutcome.Restored, lid.Restore());

        Assert.Equal(new LidActions(1, 1), _power.Schemes[FakePowerSettings.Balanced]);
        Assert.False(File.Exists(_store.Path));
        Assert.False(lid.IsApplied);
    }

    [Fact]
    public void Restore_is_idempotent()
    {
        var lid = Create();
        lid.Apply(includeBattery: false);
        lid.Restore();

        Assert.Equal(RestoreOutcome.NothingToRestore, lid.Restore());
        Assert.Equal(new LidActions(1, 1), _power.Schemes[FakePowerSettings.Balanced]);
    }

    [Fact]
    public void Apply_twice_keeps_the_true_original()
    {
        var lid = Create();
        lid.Apply(includeBattery: false);
        lid.Apply(includeBattery: false); // must not back up the "0" we wrote

        lid.Restore();
        Assert.Equal(new LidActions(1, 1), _power.Schemes[FakePowerSettings.Balanced]);
    }

    [Fact]
    public void Crash_then_next_launch_restores_from_disk()
    {
        // Process 1 applies the override, then "crashes" (object dropped, file stays on disk).
        Create().Apply(includeBattery: true);
        Assert.True(File.Exists(_store.Path));

        // Process 2 starts: a brand-new instance reads restore.json and restores.
        var afterCrash = new LidOverride(_power, new FileRestoreStore(_store.Path), _log);
        Assert.True(afterCrash.IsApplied);
        Assert.Equal(RestoreOutcome.Restored, afterCrash.Restore());
        Assert.Equal(new LidActions(1, 1), _power.Schemes[FakePowerSettings.Balanced]);
        Assert.False(File.Exists(_store.Path));
    }

    [Fact]
    public void Manual_change_by_user_is_not_overwritten()
    {
        var lid = Create();
        lid.Apply(includeBattery: false);
        _power.Schemes[FakePowerSettings.Balanced] = new LidActions(2, 1); // user picked "Hibernate" meanwhile

        lid.Restore();

        Assert.Equal(new LidActions(2, 1), _power.Schemes[FakePowerSettings.Balanced]);
        Assert.Contains(_log.Lines, l => l.Contains("changed by the user"));
    }

    [Fact]
    public void Write_refused_rolls_back_and_throws()
    {
        _power.DenyWrites = true;
        var lid = Create();

        var ex = Assert.Throws<PowerSettingsException>(() => lid.Apply(includeBattery: false));

        Assert.Equal(5u, ex.ErrorCode);
        Assert.Equal(new LidActions(1, 1), _power.Schemes[FakePowerSettings.Balanced]);
        // The rollback sees that nothing was changed (current value != value KeepLid would have written)
        // and removes the backup without writing anything.
        Assert.False(File.Exists(_store.Path));
        Assert.Equal(RestoreOutcome.NothingToRestore, lid.Restore());
    }

    [Fact]
    public void Scheme_change_moves_the_override_to_the_new_scheme()
    {
        var lid = Create();
        lid.Apply(includeBattery: false);
        _power.Active = FakePowerSettings.HighPerformance;

        lid.FollowActiveScheme();

        Assert.Equal(new LidActions(1, 1), _power.Schemes[FakePowerSettings.Balanced]);        // old restored
        Assert.Equal(new LidActions(0, 1), _power.Schemes[FakePowerSettings.HighPerformance]); // new applied
        Assert.Equal(FakePowerSettings.HighPerformance, _store.Load()!.SchemeGuid);
        Assert.Equal(2u, _store.Load()!.OriginalAc);

        lid.Restore();
        Assert.Equal(new LidActions(2, 1), _power.Schemes[FakePowerSettings.HighPerformance]);
    }

    [Fact]
    public void Deleted_scheme_is_treated_as_restored()
    {
        var lid = Create();
        lid.Apply(includeBattery: false);
        _power.Schemes.Remove(FakePowerSettings.Balanced);

        Assert.Equal(RestoreOutcome.Restored, lid.Restore());
        Assert.False(File.Exists(_store.Path));
    }

    [Fact]
    public void Corrupt_restore_file_is_quarantined()
    {
        File.WriteAllText(_store.Path, "{ not json");
        var lid = Create();

        Assert.Equal(RestoreOutcome.Corrupt, lid.Restore());
        Assert.False(File.Exists(_store.Path));
        Assert.True(File.Exists(Path.ChangeExtension(_store.Path, ".corrupt.json")));
    }

    [Fact]
    public void Probe_writes_back_the_same_value_only()
    {
        var lid = Create();
        Assert.Equal(LidAccess.Allowed, lid.ProbeAccess());
        Assert.Equal(new LidActions(1, 1), _power.Schemes[FakePowerSettings.Balanced]);
        Assert.False(lid.IsApplied);

        _power.DenyWrites = true;
        Assert.Equal(LidAccess.Denied, lid.ProbeAccess());

        _power.IsLidPresent = false;
        Assert.Equal(LidAccess.NoLid, lid.ProbeAccess());
    }
}
