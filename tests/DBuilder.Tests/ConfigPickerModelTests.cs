// ABOUTME: Tests the game-configuration picker model used by the editor config dialog.
// ABOUTME: Verifies row loading and current-selection matching without needing a UI harness.

using System.IO;
using DBuilder.IO;

namespace DBuilder.Tests;

public class ConfigPickerModelTests
{
    [Fact]
    public void LoadRowsSkipsIncludesAndUsesGameTitle()
    {
        string dir = NewTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Includes"));
            File.WriteAllText(Path.Combine(dir, "Includes", "Shared.cfg"), "game = \"Shared\";");
            File.WriteAllText(Path.Combine(dir, "Doom_Doom2Doom.cfg"), """
                game = "Doom 2";
                engine = "Doom";
                """);

            var row = Assert.Single(ConfigPickerModel.LoadRows(dir));

            Assert.Equal("Doom 2", row.Title);
            Assert.Equal("Doom", row.Engine);
            Assert.Equal("Doom 2 [Doom]", row.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadRowsAddsCurrentExternalConfig()
    {
        string dir = NewTempDir();
        string external = Path.Combine(NewTempDir(), "External.cfg");
        try
        {
            File.WriteAllText(Path.Combine(dir, "Doom_Doom2Doom.cfg"), "game = \"Doom 2\";");
            File.WriteAllText(external, """
                game = "External";
                engine = "ZDoom";
                """);

            var rows = ConfigPickerModel.LoadRows(dir, external, File.Exists);

            var row = Assert.Single(rows, row => row.Path == external);
            Assert.Equal("External", row.Title);
            Assert.Equal("ZDoom", row.Engine);
            Assert.Equal(ConfigPickerModel.SelectedIndex(rows, external), rows.IndexOf(row));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            Directory.Delete(Path.GetDirectoryName(external)!, recursive: true);
        }
    }

    [Fact]
    public void LoadRowsDoesNotDuplicateBundledCurrentConfig()
    {
        string dir = NewTempDir();
        string bundled = Path.Combine(dir, "Doom_Doom2Doom.cfg");
        try
        {
            File.WriteAllText(bundled, "game = \"Doom 2\";");

            var rows = ConfigPickerModel.LoadRows(dir, bundled, File.Exists);

            Assert.Single(rows);
            Assert.Equal(bundled, rows[0].Path);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SelectedIndexMatchesFilenameStem()
    {
        var rows = new[]
        {
            new ConfigPickerRow("Doom", "Doom", "/configs/Doom_DoomDoom.cfg"),
            new ConfigPickerRow("Doom 2", "Doom", "/configs/Doom_Doom2Doom.cfg"),
        };

        Assert.Equal(1, ConfigPickerModel.SelectedIndex(rows, "Doom_Doom2Doom"));
    }

    [Fact]
    public void SelectedIndexMatchesFullPath()
    {
        var rows = new[]
        {
            new ConfigPickerRow("Doom", "Doom", "/configs/Doom_DoomDoom.cfg"),
            new ConfigPickerRow("External", "ZDoom", "/other/External.cfg"),
        };

        Assert.Equal(1, ConfigPickerModel.SelectedIndex(rows, "/other/External.cfg"));
    }

    [Fact]
    public void SelectedIndexFallsBackToFirstRow()
    {
        var rows = new[]
        {
            new ConfigPickerRow("Doom", "Doom", "/configs/Doom_DoomDoom.cfg"),
        };

        Assert.Equal(0, ConfigPickerModel.SelectedIndex(rows, "Missing"));
        Assert.Equal(-1, ConfigPickerModel.SelectedIndex(System.Array.Empty<ConfigPickerRow>(), "Missing"));
    }

    [Theory]
    [InlineData(0, "Doom [Doom]")]
    [InlineData(1, "Doom [Doom] (1 configuration resource)")]
    [InlineData(2, "Doom [Doom] (2 configuration resources)")]
    public void DisplayTextShowsConfigurationResourceCounts(int resources, string expected)
    {
        var row = new ConfigPickerRow("Doom", "Doom", "/configs/Doom_DoomDoom.cfg");

        Assert.Equal(expected, ConfigPickerModel.DisplayText(row, resources));
    }

    [Fact]
    public void ConfigDialogRowsUseConfigurationResourceCounts()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/ConfigDialog.cs"));

        Assert.Contains("public override string ToString() => ConfigPickerModel.DisplayText(Row, ResourceCount);", body, StringComparison.Ordinal);
        Assert.Contains("=> new(row, _settings.ResourcesForConfiguration(row.Path).Count);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveConfigPathMatchesSidecarFilenamesAndExternalPaths()
    {
        string dir = NewTempDir();
        string bundled = Path.Combine(dir, "Doom_Doom2Doom.cfg");
        string external = Path.Combine(dir, "External.cfg");
        try
        {
            File.WriteAllText(bundled, "game = \"Doom 2\";");
            File.WriteAllText(external, "game = \"External\";");

            Assert.Equal(bundled, ConfigPickerModel.ResolveConfigPath(dir, "Doom_Doom2Doom.cfg", File.Exists));
            Assert.Equal(bundled, ConfigPickerModel.ResolveConfigPath(dir, "Doom_Doom2Doom", File.Exists));
            Assert.Equal(external, ConfigPickerModel.ResolveConfigPath(dir, external, File.Exists));
            Assert.Null(ConfigPickerModel.ResolveConfigPath(dir, "Missing.cfg", File.Exists));
            Assert.Null(ConfigPickerModel.ResolveConfigPath(dir, " ", File.Exists));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ResolveLongTextureNameSupportUsesResolvedSidecarConfig()
    {
        string dir = NewTempDir();
        string longNames = Path.Combine(dir, "Long.cfg");
        string shortNames = Path.Combine(dir, "Short.cfg");
        try
        {
            File.WriteAllText(longNames, "");
            File.WriteAllText(shortNames, "");

            bool Support(string path) => Path.GetFileName(path).Equals("Long.cfg", StringComparison.OrdinalIgnoreCase);

            Assert.True(ConfigPickerModel.ResolveLongTextureNameSupport(dir, "Long.cfg", fallback: false, File.Exists, Support));
            Assert.False(ConfigPickerModel.ResolveLongTextureNameSupport(dir, "Short", fallback: true, File.Exists, Support));
            Assert.True(ConfigPickerModel.ResolveLongTextureNameSupport(dir, "Missing.cfg", fallback: true, File.Exists, Support));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SameConfigPathMatchesNormalizedPaths()
    {
        string dir = NewTempDir();
        try
        {
            string path = Path.Combine(dir, "Doom.cfg");
            string nested = Path.Combine(dir, "nested");
            Directory.CreateDirectory(nested);
            string relative = Path.Combine(nested, "..", "Doom.cfg");

            Assert.True(ConfigPickerModel.SameConfigPath(path, relative));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SameConfigPathDoesNotMatchDifferentFoldersWithSameFilename()
    {
        string first = Path.Combine("/configs", "Doom.cfg");
        string second = Path.Combine("/external", "Doom.cfg");

        Assert.False(ConfigPickerModel.SameConfigPath(first, second));
    }

    [Fact]
    public void ConfigResourceDefaultsReplaceDetectedRequiredArchives()
    {
        var location = new DataLocation(DataLocationType.Wad, "/maps/existing.wad")
        {
            RequiredArchives = new List<string> { "old.wad" },
        };

        ConfigResourceDefaultsModel.ApplyRequiredArchiveDefaults(
            location,
            new[] { "doom2.wad", "textures.pk3" },
            notForTesting: true);

        Assert.Equal(new[] { "doom2.wad", "textures.pk3" }, location.RequiredArchives);
        Assert.True(location.NotForTesting);
    }

    [Fact]
    public void ConfigResourceDefaultsDoNotClearExistingTestingExclusion()
    {
        var location = new DataLocation(DataLocationType.Pk3, "/maps/textures.pk3", notForTesting: true);

        ConfigResourceDefaultsModel.ApplyRequiredArchiveDefaults(
            location,
            Array.Empty<string>(),
            notForTesting: false);

        Assert.Empty(location.RequiredArchives);
        Assert.True(location.NotForTesting);
    }

    private static string NewTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "dbuilder-config-picker-" + Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }
}
