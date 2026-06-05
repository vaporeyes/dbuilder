// ABOUTME: Verifies UDB-style recent menu numbering and map-aware entry presentation.
// ABOUTME: Keeps recent file and recent map menu shaping covered outside the editor window.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class RecentMenuModelTests
{
    [Fact]
    public void BuildReturnsDisabledEmptyEntryWhenNoRecentPathsExist()
    {
        var settings = new Settings();

        IReadOnlyList<RecentMenuEntry> entries = RecentMenuModel.Build(settings, _ => false);

        var entry = Assert.Single(entries);
        Assert.True(entry.IsEmpty);
        Assert.Equal("No recently opened files", entry.Header);
        Assert.Equal(RecentMenuModel.EmptyHeader, entry.Header);
    }

    [Fact]
    public void BuildNumbersRecentMapsAndFilesSeparatelyLikeUdbMenuRows()
    {
        var settings = new Settings
        {
            RecentFiles = ["/maps/doom2.wad"],
            RecentMaps =
            [
                new RecentMapReference { Path = "/mods/a.pk3", MapName = "MAP01", ArchivePath = "maps/a.wad" },
                new RecentMapReference { Path = "/maps/doom.wad", MapName = "E1M1" },
            ],
        };

        IReadOnlyList<RecentMenuEntry> entries = RecentMenuModel.Build(settings, _ => true);

        Assert.Equal(4, entries.Count);
        Assert.Equal("&1  a.pk3 (maps/a.wad:MAP01)", entries[0].Header);
        Assert.True(entries[0].IsMap);
        Assert.Equal("&2  doom.wad (E1M1)", entries[1].Header);
        Assert.True(entries[1].IsMap);
        Assert.True(entries[2].IsSeparator);
        Assert.Equal("&1  /maps/doom2.wad", entries[3].Header);
        Assert.True(entries[3].IsFile);
    }

    [Fact]
    public void BuildSkipsMissingRecentPaths()
    {
        var settings = new Settings
        {
            RecentFiles = ["/missing.wad", "/present.wad"],
            RecentMaps =
            [
                new RecentMapReference { Path = "/missing.pk3", MapName = "MAP01", ArchivePath = "maps/a.wad" },
                new RecentMapReference { Path = "/present.pk3", MapName = "MAP02", ArchivePath = "maps/b.wad" },
            ],
        };

        IReadOnlyList<RecentMenuEntry> entries = RecentMenuModel.Build(settings, path => path.Contains("present", StringComparison.Ordinal));

        Assert.Equal(3, entries.Count);
        Assert.Equal("&1  present.pk3 (maps/b.wad:MAP02)", entries[0].Header);
        Assert.True(entries[1].IsSeparator);
        Assert.Equal("&1  /present.wad", entries[2].Header);
    }

    [Fact]
    public void BuildCapsCombinedRecentMapAndFileRowsToConfiguredLimit()
    {
        var settings = new Settings
        {
            MaxRecentFiles = Settings.MinMaxRecentFiles,
            RecentMaps = Enumerable.Range(1, 5)
                .Select(i => new RecentMapReference { Path = $"/maps/map{i}.wad", MapName = $"MAP{i:00}" })
                .ToList(),
            RecentFiles = Enumerable.Range(1, 5)
                .Select(i => $"/files/file{i}.wad")
                .ToList(),
        };

        IReadOnlyList<RecentMenuEntry> entries = RecentMenuModel.Build(settings, _ => true);
        RecentMenuEntry[] recentRows = entries.Where(entry => entry.IsMap || entry.IsFile).ToArray();

        Assert.Equal(Settings.MinMaxRecentFiles, recentRows.Length);
        Assert.Equal(5, recentRows.Count(entry => entry.IsMap));
        Assert.Equal(3, recentRows.Count(entry => entry.IsFile));
        Assert.True(entries[5].IsSeparator);
        Assert.Equal("&1  /files/file1.wad", entries[6].Header);
        Assert.DoesNotContain(entries, entry => entry.Header.Contains("file4.wad", StringComparison.Ordinal));
    }

    [Fact]
    public void DisplayFilenameTrimsLongPathsLikeUdbRecentFiles()
    {
        string path = "/" + new string('a', 90) + "/maps/doom2.wad";

        string display = RecentMenuModel.DisplayFilename(path);

        Assert.Equal(RecentMenuModel.MaxDisplayCharacters, display.Length);
        Assert.StartsWith("/aa...", display);
        Assert.EndsWith("/maps/doom2.wad", display);
    }
}
