// ABOUTME: Tests Settings persistence (JSON round-trip, resilient load) and the recent-files list semantics.
// ABOUTME: Uses a temp file path so it never touches the real user settings.

using System.IO;
using System.Linq;
using DBuilder.IO;

namespace DBuilder.Tests;

public class SettingsTests
{
    [Fact]
    public void AddRecentMovesToFrontAndDedupes()
    {
        var s = new Settings();
        s.AddRecent("/a.wad");
        s.AddRecent("/b.wad");
        s.AddRecent("/a.wad"); // re-adding moves it to front, no duplicate
        Assert.Equal(new[] { "/a.wad", "/b.wad" }, s.RecentFiles);
    }

    [Fact]
    public void AddRecentIsCaseInsensitiveForDedup()
    {
        var s = new Settings();
        s.AddRecent("/Maps/E1M1.wad");
        s.AddRecent("/maps/e1m1.WAD");
        Assert.Single(s.RecentFiles);
    }

    [Fact]
    public void AddRecentCapsAtMax()
    {
        var s = new Settings();
        for (int i = 0; i < Settings.MaxRecent + 5; i++) s.AddRecent($"/wad{i}.wad");
        Assert.Equal(Settings.MaxRecent, s.RecentFiles.Count);
        Assert.Equal($"/wad{Settings.MaxRecent + 4}.wad", s.RecentFiles[0]); // most recent first
    }

    [Fact]
    public void SaveAndLoadRoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dbuilder_settings_{System.Guid.NewGuid():N}.json");
        try
        {
            var s = new Settings { ConfigDir = "/cfg", TestPort = "/gz", TestIwad = "/iwad.wad" };
            s.AddRecent("/x.wad");
            Assert.True(s.Save(path));

            var loaded = Settings.Load(path);
            Assert.Equal("/cfg", loaded.ConfigDir);
            Assert.Equal("/gz", loaded.TestPort);
            Assert.Equal("/iwad.wad", loaded.TestIwad);
            Assert.Contains("/x.wad", loaded.RecentFiles);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void LoadMissingFileReturnsDefaults()
    {
        var s = Settings.Load(Path.Combine(Path.GetTempPath(), "definitely_missing_dbuilder_settings.json"));
        Assert.NotNull(s);
        Assert.Empty(s.RecentFiles);
        Assert.Null(s.ConfigDir);
    }

    [Fact]
    public void LoadCorruptFileReturnsDefaults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dbuilder_corrupt_{System.Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ this is not valid json ");
        try { Assert.Empty(Settings.Load(path).RecentFiles); }
        finally { File.Delete(path); }
    }
}
