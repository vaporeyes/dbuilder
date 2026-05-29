// ABOUTME: Tests recoverable autosave WAD snapshot storage.
// ABOUTME: Covers deterministic file naming, metadata writing, and cleanup for autosave entries.

using DBuilder.IO;

namespace DBuilder.Tests;

public class AutoSaveStoreTests
{
    [Fact]
    public void PathForIsDeterministicAndMapScoped()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_autosave_" + Guid.NewGuid().ToString("N"));
        var key = new AutoSaveKey("/maps/project.wad", "MAP01");

        string first = AutoSaveStore.PathFor(key, dir);
        string second = AutoSaveStore.PathFor(key, dir);

        Assert.Equal(first, second);
        Assert.StartsWith(Path.Combine(dir, "MAP01-"), first);
        Assert.EndsWith(".wad", first);
    }

    [Fact]
    public void WriteStoresWadBytesAndMetadata()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_autosave_" + Guid.NewGuid().ToString("N"));
        var key = new AutoSaveKey("/maps/project.pk3", "E1M1", "maps/e1m1.wad");
        byte[] bytes = { 1, 2, 3, 4 };

        string? path = AutoSaveStore.Write(key, bytes, dir);

        Assert.NotNull(path);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        string metadata = File.ReadAllText(path + ".txt");
        Assert.Contains("source=/maps/project.pk3", metadata);
        Assert.Contains("map=E1M1", metadata);
        Assert.Contains("archive=maps/e1m1.wad", metadata);
    }

    [Fact]
    public void DeleteRemovesSnapshotAndMetadata()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_autosave_" + Guid.NewGuid().ToString("N"));
        var key = new AutoSaveKey("/maps/project.wad", "MAP01");
        string path = AutoSaveStore.Write(key, new byte[] { 5 }, dir)!;

        Assert.True(AutoSaveStore.Delete(key, dir));

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + ".txt"));
    }

    [Fact]
    public void ListReturnsEntriesFromMetadataNewestFirst()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_autosave_" + Guid.NewGuid().ToString("N"));
        var oldKey = new AutoSaveKey("/maps/old.wad", "MAP01");
        var newKey = new AutoSaveKey("/maps/new.wad", "MAP02");
        string oldPath = AutoSaveStore.Write(oldKey, new byte[] { 1 }, dir)!;
        string newPath = AutoSaveStore.Write(newKey, new byte[] { 2 }, dir)!;
        File.SetLastWriteTimeUtc(oldPath, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newPath, new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var entries = AutoSaveStore.List(dir);

        Assert.Equal(2, entries.Count);
        Assert.Equal(newKey, entries[0].Key);
        Assert.Equal(newPath, entries[0].SnapshotPath);
        Assert.Equal(oldKey, entries[1].Key);
        Assert.Equal("old.wad:MAP01", entries[1].DisplayName);
    }

    [Fact]
    public void PruneKeepsNewestSnapshotsAndDeletesMetadata()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dbuilder_autosave_" + Guid.NewGuid().ToString("N"));
        string oldPath = AutoSaveStore.Write(new AutoSaveKey("/maps/old.wad", "MAP01"), new byte[] { 1 }, dir)!;
        string midPath = AutoSaveStore.Write(new AutoSaveKey("/maps/mid.wad", "MAP02"), new byte[] { 2 }, dir)!;
        string newPath = AutoSaveStore.Write(new AutoSaveKey("/maps/new.wad", "MAP03"), new byte[] { 3 }, dir)!;
        File.SetLastWriteTimeUtc(oldPath, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(midPath, new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newPath, new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        int deleted = AutoSaveStore.Prune(maxSnapshots: 2, dir);

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(oldPath));
        Assert.False(File.Exists(oldPath + ".txt"));
        Assert.True(File.Exists(midPath));
        Assert.True(File.Exists(newPath));
        Assert.Equal(2, AutoSaveStore.List(dir).Count);
    }
}
