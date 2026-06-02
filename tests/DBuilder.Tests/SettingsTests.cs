// ABOUTME: Tests Settings persistence (JSON round-trip, resilient load) and the recent-files list semantics.
// ABOUTME: Uses a temp file path so it never touches the real user settings.

using System.IO;
using System.Linq;
using DBuilder.IO;
using DBuilder.Map;

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
        for (int i = 0; i < Settings.DefaultMaxRecentFiles + 5; i++) s.AddRecent($"/wad{i}.wad");
        Assert.Equal(Settings.DefaultMaxRecentFiles, s.RecentFiles.Count);
        Assert.Equal($"/wad{Settings.DefaultMaxRecentFiles + 4}.wad", s.RecentFiles[0]); // most recent first
    }

    [Fact]
    public void AddRecentMapMovesToFrontAndDedupes()
    {
        var s = new Settings();
        s.AddRecentMap("/maps/a.wad", "MAP01");
        s.AddRecentMap("/maps/a.wad", "MAP02");
        s.AddRecentMap("/MAPS/A.WAD", "map01");

        Assert.Equal(2, s.RecentMaps.Count);
        Assert.Equal("/MAPS/A.WAD", s.RecentMaps[0].Path);
        Assert.Equal("map01", s.RecentMaps[0].MapName);
        Assert.Equal("MAP02", s.RecentMaps[1].MapName);
    }

    [Fact]
    public void AddRecentMapSeparatesPk3ArchiveEntries()
    {
        var s = new Settings();
        s.AddRecentMap("/mods/a.pk3", "MAP01", "maps/a.wad");
        s.AddRecentMap("/mods/a.pk3", "MAP01", "maps/b.wad");

        Assert.Equal(2, s.RecentMaps.Count);
        Assert.Equal("maps/b.wad", s.RecentMaps[0].ArchivePath);
        Assert.Equal("maps/a.wad", s.RecentMaps[1].ArchivePath);
    }

    [Fact]
    public void AddRecentMapCapsAtMax()
    {
        var s = new Settings();
        for (int i = 0; i < Settings.DefaultMaxRecentFiles + 5; i++) s.AddRecentMap("/maps/a.wad", $"MAP{i:00}");
        Assert.Equal(Settings.DefaultMaxRecentFiles, s.RecentMaps.Count);
        Assert.Equal($"MAP{Settings.DefaultMaxRecentFiles + 4:00}", s.RecentMaps[0].MapName);
    }

    [Fact]
    public void RecentListsUseConfiguredUdbLimit()
    {
        var s = new Settings { MaxRecentFiles = 12 };

        for (int i = 0; i < 20; i++)
        {
            s.AddRecent($"/wad{i}.wad");
            s.AddRecentMap("/maps/a.wad", $"MAP{i:00}");
        }

        Assert.Equal(12, s.NormalizedMaxRecentFiles);
        Assert.Equal(12, s.RecentFiles.Count);
        Assert.Equal(12, s.RecentMaps.Count);
        Assert.Equal("/wad19.wad", s.RecentFiles[0]);
        Assert.Equal("MAP19", s.RecentMaps[0].MapName);
    }

    [Fact]
    public void MaxRecentFilesClampsToUdbPreferenceRange()
    {
        Assert.Equal(Settings.DefaultMaxRecentFiles, new Settings().NormalizedMaxRecentFiles);
        Assert.Equal(Settings.MinMaxRecentFiles, new Settings { MaxRecentFiles = 1 }.NormalizedMaxRecentFiles);
        Assert.Equal(Settings.MaxMaxRecentFiles, new Settings { MaxRecentFiles = 50 }.NormalizedMaxRecentFiles);
    }

    [Fact]
    public void ExistingRecentFilesSkipsMissingPathsLikeUdbMenu()
    {
        var s = new Settings
        {
            RecentFiles = new() { "/missing.wad", "/present.wad" },
        };

        Assert.Equal(new[] { "/present.wad" }, s.ExistingRecentFiles(path => path.Contains("present")));
    }

    [Fact]
    public void ExistingRecentMapsSkipsMissingArchivePaths()
    {
        var s = new Settings
        {
            RecentMaps = new()
            {
                new RecentMapReference { Path = "/missing.pk3", MapName = "MAP01", ArchivePath = "maps/a.wad" },
                new RecentMapReference { Path = "/present.pk3", MapName = "MAP02", ArchivePath = "maps/b.wad" },
            },
        };

        var map = Assert.Single(s.ExistingRecentMaps(path => path.Contains("present")));
        Assert.Equal("MAP02", map.MapName);
        Assert.Equal("maps/b.wad", map.ArchivePath);
    }

    [Fact]
    public void SaveAndLoadRoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dbuilder_settings_{System.Guid.NewGuid():N}.json");
        try
        {
            var s = new Settings
            {
                ConfigDir = "/cfg",
                LastUsedConfigName = "Hexen_HexenHexen.cfg",
                LastUsedMapFolder = "/maps",
                TestPort = "/gz",
                TestIwad = "/iwad.wad",
                MaxRecentFiles = 12,
                AutoClearSidedefTextures = false,
                DynamicGridSize = false,
                DefaultViewMode = 3,
                StatusHistoryLimit = 250,
                MergeGeometryMode = MergeGeometryMode.Merge,
                PasteOptions = new PasteOptions
                {
                    ChangeTags = PasteTagMode.Renumber,
                    RemoveActions = true,
                },
                DrawLineSettings = new DrawLineModeSettings(ContinuousDrawing: true, AutoCloseDrawing: true),
                DrawRectangleSettings = new DrawRectangleModeSettings(Subdivisions: 6, BevelWidth: 12),
                DrawEllipseSettings = new DrawEllipseModeSettings(Subdivisions: 10, BevelWidth: -8, Angle: 45),
                DrawCurveSettings = new DrawCurveModeSettings(SegmentLength: 96, ContinuousDrawing: true),
                DrawGridSettings = new DrawGridModeSettings(HorizontalSlices: 5, VerticalSlices: 7, Triangulate: true),
                EditSelectionSettings = new EditSelectionModeSettings(
                    UsePrecisePosition: false,
                    HeightAdjustMode: EditSelectionHeightAdjustMode.AdjustBoth),
                AutomapSettings = new AutomapModeSettings(
                    ShowHiddenLines: true,
                    ShowSecretSectors: true,
                    ShowLocks: false,
                    ShowTextures: false,
                    ColorPreset: AutomapColorPreset.Strife),
                MakeDoorSettings = new MakeDoorModeSettings(
                    HasValues: true,
                    DoorTexture: " BIGDOOR2 ",
                    TrackTexture: "DOORTRAK",
                    CeilingTexture: "CEIL5_1",
                    FloorTexture: "STEP1",
                    ResetOffsets: false,
                    ApplyActionSpecials: false,
                    ApplyTag: true),
                WindowX = 120,
                WindowY = 80,
                WindowWidth = 1280,
                WindowHeight = 900,
                ShortcutOverrides = new()
                {
                    new EditorShortcutBinding("window.save", EditorCommandScope.Window, "F5"),
                },
            };
            s.AddRecent("/x.wad");
            s.AddRecentMap("/x.wad", "MAP01");
            Assert.True(s.Save(path));

            var loaded = Settings.Load(path);
            Assert.Equal("/cfg", loaded.ConfigDir);
            Assert.Equal("Hexen_HexenHexen.cfg", loaded.LastUsedConfigName);
            Assert.Equal("/maps", loaded.LastUsedMapFolder);
            Assert.Equal("/gz", loaded.TestPort);
            Assert.Equal("/iwad.wad", loaded.TestIwad);
            Assert.Equal(12, loaded.MaxRecentFiles);
            Assert.Equal(12, loaded.NormalizedMaxRecentFiles);
            Assert.False(loaded.AutoClearSidedefTextures);
            Assert.False(loaded.DynamicGridSize);
            Assert.Equal(3, loaded.DefaultViewMode);
            Assert.Equal(3, loaded.NormalizedDefaultViewMode);
            Assert.Equal(250, loaded.StatusHistoryLimit);
            Assert.Equal(250, loaded.NormalizedStatusHistoryLimit);
            Assert.Equal(MergeGeometryMode.Merge, loaded.MergeGeometryMode);
            Assert.Equal(MergeGeometryMode.Merge, loaded.NormalizedMergeGeometryMode);
            Assert.Equal(PasteTagMode.Renumber, loaded.PasteOptions.ChangeTags);
            Assert.True(loaded.PasteOptions.RemoveActions);
            Assert.Equal(PasteTagMode.Renumber, loaded.NormalizedPasteOptions.ChangeTags);
            Assert.True(loaded.NormalizedPasteOptions.RemoveActions);
            Assert.True(loaded.DrawLineSettings.ContinuousDrawing);
            Assert.True(loaded.DrawLineSettings.AutoCloseDrawing);
            Assert.Equal(6, loaded.DrawRectangleSettings.Subdivisions);
            Assert.Equal(12, loaded.DrawRectangleSettings.BevelWidth);
            Assert.Equal(6, loaded.NormalizedDrawRectangleSettings.Subdivisions);
            Assert.Equal(10, loaded.DrawEllipseSettings.Subdivisions);
            Assert.Equal(-8, loaded.DrawEllipseSettings.BevelWidth);
            Assert.Equal(45, loaded.DrawEllipseSettings.Angle);
            Assert.Equal(10, loaded.NormalizedDrawEllipseSettings.Subdivisions);
            Assert.Equal(96, loaded.DrawCurveSettings.SegmentLength);
            Assert.Equal(96, loaded.NormalizedDrawCurveSettings.SegmentLength);
            Assert.Equal(5, loaded.DrawGridSettings.HorizontalSlices);
            Assert.Equal(7, loaded.DrawGridSettings.VerticalSlices);
            Assert.True(loaded.DrawGridSettings.Triangulate);
            Assert.Equal(5, loaded.NormalizedDrawGridSettings.HorizontalSlices);
            Assert.False(loaded.EditSelectionSettings.UsePrecisePosition);
            Assert.Equal(EditSelectionHeightAdjustMode.AdjustBoth, loaded.EditSelectionSettings.HeightAdjustMode);
            Assert.Equal(EditSelectionHeightAdjustMode.AdjustBoth, loaded.NormalizedEditSelectionSettings.HeightAdjustMode);
            Assert.True(loaded.AutomapSettings.ShowHiddenLines);
            Assert.True(loaded.AutomapSettings.ShowSecretSectors);
            Assert.False(loaded.AutomapSettings.ShowLocks);
            Assert.False(loaded.AutomapSettings.ShowTextures);
            Assert.Equal(AutomapColorPreset.Strife, loaded.AutomapSettings.ColorPreset);
            Assert.Equal(AutomapColorPreset.Strife, loaded.NormalizedAutomapSettings.ColorPreset);
            Assert.True(loaded.MakeDoorSettings.HasValues);
            Assert.Equal("BIGDOOR2", loaded.NormalizedMakeDoorSettings.DoorTexture);
            Assert.Equal("DOORTRAK", loaded.NormalizedMakeDoorSettings.TrackTexture);
            Assert.Equal("CEIL5_1", loaded.NormalizedMakeDoorSettings.CeilingTexture);
            Assert.Equal("STEP1", loaded.NormalizedMakeDoorSettings.FloorTexture);
            Assert.False(loaded.NormalizedMakeDoorSettings.ResetOffsets);
            Assert.False(loaded.NormalizedMakeDoorSettings.ApplyActionSpecials);
            Assert.True(loaded.NormalizedMakeDoorSettings.ApplyTag);
            Assert.Equal(120, loaded.WindowX);
            Assert.Equal(80, loaded.WindowY);
            Assert.Equal(1280, loaded.WindowWidth);
            Assert.Equal(900, loaded.WindowHeight);
            Assert.Contains("/x.wad", loaded.RecentFiles);
            Assert.Contains(loaded.RecentMaps, m => m.Path == "/x.wad" && m.MapName == "MAP01");
            Assert.Contains(loaded.ShortcutOverrides, b => b.CommandId == "window.save" && b.Key == "F5");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void StartupConfigPrefersExistingEnvironmentPath()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            "/env/Hexen.cfg",
            "/configs/Doom_DoomDoom.cfg",
        };

        string? path = Settings.ResolveStartupConfigPath(
            "/env/Hexen.cfg",
            "/configs",
            "Doom_Doom2Doom.cfg",
            existing.Contains);

        Assert.Equal("/env/Hexen.cfg", path);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 3)]
    public void DefaultViewModeUsesUdbRange(int? value, int expected)
    {
        var settings = new Settings { DefaultViewMode = value };

        Assert.Equal(expected, settings.NormalizedDefaultViewMode);
    }

    [Fact]
    public void StartupConfigFallsBackToLastUsedConfigName()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            Path.Combine("/configs", "Doom_Doom2Doom.cfg"),
            Path.Combine("/configs", "Doom_DoomDoom.cfg"),
        };

        string? path = Settings.ResolveStartupConfigPath(
            "/missing/Hexen.cfg",
            "/configs",
            "Doom_Doom2Doom.cfg",
            existing.Contains);

        Assert.Equal(Path.Combine("/configs", "Doom_Doom2Doom.cfg"), path);
    }

    [Fact]
    public void StartupConfigAcceptsRootedLastUsedConfigPath()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            "/external/Custom.cfg",
            Path.Combine("/configs", "Doom_DoomDoom.cfg"),
        };

        string? path = Settings.ResolveStartupConfigPath(
            null,
            "/configs",
            "/external/Custom.cfg",
            existing.Contains);

        Assert.Equal("/external/Custom.cfg", path);
    }

    [Fact]
    public void StartupConfigFallsBackToDoomDefaultWhenLastUsedIsMissing()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            Path.Combine("/configs", "Doom_DoomDoom.cfg"),
        };

        string? path = Settings.ResolveStartupConfigPath(
            null,
            "/configs",
            "Missing.cfg",
            existing.Contains);

        Assert.Equal(Path.Combine("/configs", "Doom_DoomDoom.cfg"), path);
    }

    [Fact]
    public void RememberMapFolderForPathStoresContainingFolder()
    {
        var s = new Settings();
        string path = Path.Combine("/maps", "doom2.wad");

        s.RememberMapFolderForPath(path);

        Assert.Equal("/maps", s.LastUsedMapFolder);
    }

    [Fact]
    public void RememberMapFolderForPathIgnoresMissingFoldersWhenChecked()
    {
        var s = new Settings { LastUsedMapFolder = "/old" };
        string path = Path.Combine("/missing", "doom2.wad");

        s.RememberMapFolderForPath(path, _ => false);

        Assert.Equal("/old", s.LastUsedMapFolder);
    }

    [Fact]
    public void ExistingMapFolderReturnsOnlyExistingFolders()
    {
        Assert.Equal("/maps", Settings.ExistingMapFolder(" /maps ", path => path == "/maps"));
        Assert.Null(Settings.ExistingMapFolder("/missing", _ => false));
        Assert.Null(Settings.ExistingMapFolder("", _ => true));
    }

    [Fact]
    public void LoadMissingFileReturnsDefaults()
    {
        var s = Settings.Load(Path.Combine(Path.GetTempPath(), "definitely_missing_dbuilder_settings.json"));
        Assert.NotNull(s);
        Assert.Empty(s.RecentFiles);
        Assert.Empty(s.ShortcutOverrides);
        Assert.Null(s.ConfigDir);
        Assert.Equal(MergeGeometryMode.Replace, s.NormalizedMergeGeometryMode);
        Assert.Equal(PasteTagMode.Keep, s.NormalizedPasteOptions.ChangeTags);
        Assert.False(s.NormalizedPasteOptions.RemoveActions);
        Assert.Equal(new DrawLineModeSettings(), s.NormalizedDrawLineSettings);
        Assert.Equal(new DrawRectangleModeSettings(), s.NormalizedDrawRectangleSettings);
        Assert.Equal(new DrawEllipseModeSettings(), s.NormalizedDrawEllipseSettings);
        Assert.Equal(new DrawCurveModeSettings(), s.NormalizedDrawCurveSettings);
        Assert.Equal(new DrawGridModeSettings(), s.NormalizedDrawGridSettings);
        Assert.Equal(new EditSelectionModeSettings(), s.NormalizedEditSelectionSettings);
        Assert.Equal(new AutomapModeSettings(), s.NormalizedAutomapSettings);
        Assert.Equal(new MakeDoorModeSettings(), s.NormalizedMakeDoorSettings);
    }

    [Fact]
    public void InvalidMergeGeometryModeFallsBackToReplace()
    {
        var s = new Settings { MergeGeometryMode = (MergeGeometryMode)99 };

        Assert.Equal(MergeGeometryMode.Replace, s.NormalizedMergeGeometryMode);
    }

    [Fact]
    public void InvalidPasteTagModeFallsBackToDefaults()
    {
        var s = new Settings
        {
            PasteOptions = new PasteOptions
            {
                ChangeTags = (PasteTagMode)99,
                RemoveActions = true,
            },
        };

        Assert.Equal(PasteTagMode.Keep, s.NormalizedPasteOptions.ChangeTags);
        Assert.False(s.NormalizedPasteOptions.RemoveActions);
    }

    [Fact]
    public void InvalidAutomapColorPresetFallsBackToDoom()
    {
        var s = new Settings
        {
            AutomapSettings = new AutomapModeSettings(ColorPreset: (AutomapColorPreset)99),
        };

        Assert.Equal(AutomapColorPreset.Doom, s.NormalizedAutomapSettings.ColorPreset);
    }

    [Fact]
    public void LoadCorruptFileReturnsDefaults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dbuilder_corrupt_{System.Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ this is not valid json ");
        try { Assert.Empty(Settings.Load(path).RecentFiles); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void NormalizedStatusHistoryLimitClampsToSupportedRange()
    {
        Assert.Equal(Settings.DefaultStatusHistoryLimit, new Settings().NormalizedStatusHistoryLimit);
        Assert.Equal(Settings.MinStatusHistoryLimit, new Settings { StatusHistoryLimit = 1 }.NormalizedStatusHistoryLimit);
        Assert.Equal(Settings.MaxStatusHistoryLimit, new Settings { StatusHistoryLimit = 5000 }.NormalizedStatusHistoryLimit);
    }
}
