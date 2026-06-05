// ABOUTME: Tests the legacy UDB object export settings model, defaults, and path validation.
// ABOUTME: Covers OBJ naming, fix-scale, texture export, and missing-directory behavior without UI.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class ObjectExportModelTests
{
    [Fact]
    public void DefaultFilePathMatchesUdbMapTitleAndLevelName()
    {
        string path = ObjectExportSettings.DefaultFilePath("/maps", "doom2", "MAP01");

        Assert.Equal(Path.Combine("/maps", "doom2_MAP01.obj"), path);
        Assert.Equal("doom2_MAP01", ObjectExportSettings.DefaultFileName("doom2", "MAP01"));
        Assert.Equal(".obj", ObjectExportSettings.DefaultExtension);
        Assert.Equal("Wavefront obj files|*.obj", ObjectExportSettings.DialogFilter);
        Assert.Equal("Wavefront obj files", ObjectExportSettings.DialogFilterName());
        Assert.Equal("*.obj", ObjectExportSettings.DialogFilterPattern());
        Assert.Equal("Select save location:", ObjectExportSettings.DialogTitle);
    }

    [Fact]
    public void FormStateExposesUdbLegacyObjectExportLabels()
    {
        ObjectExportFormState state = ObjectExportFormState.FromPath(
            "/maps/doom2_MAP01.obj",
            fixScale: true,
            exportTextures: true);

        Assert.Equal(new ObjectExportOptions("/maps/doom2_MAP01.obj", FixScale: true, ExportTextures: true), state.DefaultOptions);
        Assert.Equal("Export to Wavefront .obj", state.Title);
        Assert.Equal("Exports selected sectors, or the whole map, using the legacy object exporter.", state.Description);
        Assert.Equal("Export path:", state.PathLabel);
        Assert.Equal("Browse...", state.BrowseButtonText);
        Assert.Equal("Export for GZDoom (Fix Vertical Scale)", state.FixScaleText);
        Assert.Equal("Export textures", state.ExportTexturesText);
        Assert.Equal("Export", state.ExportButtonText);
        Assert.Equal("Cancel", state.CancelButtonText);
        Assert.Equal("Select save location:", state.SaveDialogTitle);
        Assert.Equal("Wavefront obj files|*.obj", state.SaveDialogFilter);
        Assert.Equal("obj", state.SaveDialogExtension);
    }

    [Fact]
    public void EditorObjectExportDialogUsesSharedUdbMetadata()
    {
        string dialog = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/ObjectExportDialog.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));

        Assert.Contains("ObjectExportSettings.FormTitle", dialog, StringComparison.Ordinal);
        Assert.Contains("ObjectExportSettings.FormDescription", dialog, StringComparison.Ordinal);
        Assert.Contains("ObjectExportSettings.PathLabel", dialog, StringComparison.Ordinal);
        Assert.Contains("ObjectExportSettings.FixScaleText", dialog, StringComparison.Ordinal);
        Assert.Contains("ObjectExportSettings.ExportTexturesText", dialog, StringComparison.Ordinal);
        Assert.Contains("ObjectExportSettings.DialogFilterName()", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ObjectExportSettings.DialogFilterPattern()", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void FromOptionsTrimsPathAndKeepsCheckboxValues()
    {
        ObjectExportSettings settings = ObjectExportSettings.FromOptions(new ObjectExportOptions(
            "  /tmp/map.obj  ",
            FixScale: true,
            ExportTextures: true));

        Assert.Equal("/tmp/map.obj", settings.FilePath);
        Assert.True(settings.FixScale);
        Assert.True(settings.ExportTextures);
    }

    [Fact]
    public void ValidateRequiresExistingDirectoryLikeUdbForm()
    {
        IReadOnlyList<string> errors = ObjectExportSettings.Validate(
            new ObjectExportOptions("/missing/map.obj"),
            directory => directory == "/present");

        Assert.Equal(new[] { ObjectExportSettings.InvalidPathMessage }, errors);

        Assert.Empty(ObjectExportSettings.Validate(
            new ObjectExportOptions("/present/map.obj"),
            directory => directory == "/present"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("map.obj")]
    [InlineData("   ")]
    public void ValidateRejectsBlankOrDirectorylessPaths(string path)
    {
        IReadOnlyList<string> errors = ObjectExportSettings.Validate(
            new ObjectExportOptions(path),
            _ => true);

        Assert.Equal(new[] { ObjectExportSettings.InvalidPathMessage }, errors);
    }

    [Fact]
    public void CreateWavefrontSettingsMapsLegacyFixScaleAndTextureOptions()
    {
        ObjectExportSettings settings = ObjectExportSettings.FromOptions(new ObjectExportOptions(
            "/tmp/export/map.obj",
            FixScale: true,
            ExportTextures: true));

        WavefrontExportSettings wavefront = ObjectExportWriter.CreateWavefrontSettings(settings);

        Assert.Equal("map", wavefront.ObjName);
        Assert.Equal("/tmp/export", wavefront.ObjPath);
        Assert.True(wavefront.ExportForGZDoom);
        Assert.True(wavefront.ExportTextures);
        Assert.Equal(1.0, wavefront.Scale);
    }

    [Fact]
    public void CreateWavefrontExportBuildsObjGeometryAndMaterialLists()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map);
        sector.FloorHeight = 0;
        sector.CeilHeight = 128;
        sector.FloorTexture = "FLOOR1";
        sector.CeilTexture = "CEIL1";
        foreach (Sidedef side in sector.Sidedefs) side.MidTexture = "STARTAN3";
        ObjectExportSettings settings = ObjectExportSettings.FromOptions(new ObjectExportOptions(
            "/tmp/export/map.obj",
            FixScale: false,
            ExportTextures: true));

        WavefrontExportSettings wavefront = ObjectExportWriter.CreateWavefrontExport(
            map,
            [sector],
            settings,
            "doom2.wad",
            "MAP01",
            "1.0");

        Assert.True(wavefront.Valid);
        Assert.Contains("# doom2.wad, map MAP01", wavefront.Obj);
        Assert.Contains("mtllib map.mtl", wavefront.Obj);
        Assert.Contains("usemtl STARTAN3", wavefront.Obj);
        Assert.Contains("STARTAN3", wavefront.Textures!);
        Assert.Contains("FLOOR1", wavefront.Flats!);
        Assert.Contains("CEIL1", wavefront.Flats!);
    }

    [Fact]
    public void CreateFilePlanUsesLegacyOutputPathAndClassicMtl()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map);
        sector.FloorHeight = 0;
        sector.CeilHeight = 128;
        sector.FloorTexture = "FLOOR1";
        sector.CeilTexture = "CEIL1";
        foreach (Sidedef side in sector.Sidedefs) side.MidTexture = "STARTAN3";
        ObjectExportSettings settings = ObjectExportSettings.FromOptions(new ObjectExportOptions(
            "/tmp/export/map.obj",
            FixScale: false,
            ExportTextures: true));

        IReadOnlyList<WavefrontExportFile> files = ObjectExportWriter.CreateFilePlan(
            map,
            [sector],
            settings,
            "doom2.wad",
            "MAP01",
            "1.0");

        Assert.Collection(
            files,
            obj =>
            {
                Assert.Equal("/tmp/export/map.obj", obj.Path);
                Assert.Contains("usemtl STARTAN3", obj.Content);
            },
            mtl =>
            {
                Assert.Equal("/tmp/export/map.mtl", mtl.Path);
                Assert.Contains("# MTL for doom2.wad, map MAP01", mtl.Content);
                Assert.Contains("newmtl STARTAN3", mtl.Content);
            });
    }

    private static Sector AddSquareSector(MapSet map)
    {
        var sector = new Sector { Index = map.Sectors.Count };
        var vertices = new[]
        {
            new Vertex(new Vector2D(64, 0)),
            new Vertex(new Vector2D(0, 0)),
            new Vertex(new Vector2D(0, 64)),
            new Vertex(new Vector2D(64, 64)),
        };

        foreach (Vertex vertex in vertices) map.Vertices.Add(vertex);
        map.Sectors.Add(sector);

        for (int i = 0; i < vertices.Length; i++)
        {
            var line = new Linedef(vertices[i], vertices[(i + 1) % vertices.Length]);
            var side = new Sidedef(line, true) { Sector = sector };
            line.Front = side;
            line.Start.Linedefs.Add(line);
            line.End.Linedefs.Add(line);
            sector.Sidedefs.Add(side);
            map.Linedefs.Add(line);
            map.Sidedefs.Add(side);
        }

        return sector;
    }
}
