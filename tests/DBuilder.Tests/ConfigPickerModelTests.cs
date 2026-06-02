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

    private static string NewTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "dbuilder-config-picker-" + Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }
}
