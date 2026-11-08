using KeepLid.Core.Power;

namespace KeepLid.Core.Tests;

public sealed class RestoreStoreTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Round_trip_keeps_every_field()
    {
        var store = new FileRestoreStore(_dir.File("sub/restore.json"));
        var record = new RestoreRecord
        {
            SchemeGuid = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"),
            OriginalAc = 1,
            OriginalDc = null,
            AppliedValue = 0,
            CreatedAt = new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.FromHours(1)),
            ProcessId = 1234,
        };

        store.Save(record);

        Assert.True(store.Exists);
        Assert.Equal(record, store.Load());
        Assert.False(File.Exists(store.Path + ".tmp"));
    }

    [Fact]
    public void Json_is_human_readable_for_support()
    {
        var store = new FileRestoreStore(_dir.File("restore.json"));
        store.Save(new RestoreRecord { SchemeGuid = Guid.NewGuid(), OriginalAc = 1, OriginalDc = 1 });

        string json = File.ReadAllText(store.Path);
        Assert.Contains("\"SchemeGuid\"", json);
        Assert.Contains("\"OriginalAc\": 1", json);
    }

    [Fact]
    public void Missing_file_loads_as_null()
    {
        var store = new FileRestoreStore(_dir.File("restore.json"));
        Assert.Null(store.Load());
        store.Delete(); // no throw
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{\"SchemeGuid\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("garbage")]
    public void Invalid_content_throws_InvalidDataException(string content)
    {
        var store = new FileRestoreStore(_dir.File("restore.json"));
        File.WriteAllText(store.Path, content);
        Assert.Throws<InvalidDataException>(() => store.Load());
    }

    [Fact]
    public void Leftover_temp_file_from_a_crash_during_save_is_ignored()
    {
        // Crash between writing restore.json.tmp and the atomic move: the real file is untouched.
        var store = new FileRestoreStore(_dir.File("restore.json"));
        File.WriteAllText(store.Path + ".tmp", "{ half written");
        Assert.Null(store.Load());

        store.Save(new RestoreRecord { SchemeGuid = Guid.NewGuid(), OriginalAc = 1 });
        Assert.NotNull(store.Load());
    }
}
