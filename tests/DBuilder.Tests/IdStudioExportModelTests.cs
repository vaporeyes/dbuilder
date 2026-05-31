// ABOUTME: Tests UDB-style idStudio export settings and map writer behavior.
// ABOUTME: Verifies map-name validation, refmap hierarchy, file planning, and func/static text.

using DBuilder.Map;

namespace DBuilder.Tests;

public class IdStudioExportModelTests
{
    [Fact]
    public void FromOptionsCopiesUdbSettings()
    {
        IdStudioExportSettings settings = IdStudioExportSettings.FromOptions(new IdStudioExportOptions
        {
            ModPath = "/tmp/mod",
            MapName = "map01",
            Downscale = 32,
            XShift = 1,
            YShift = 2,
            ZShift = 3,
            ExportTextures = true,
            ExportAllTextures = true
        });

        Assert.Equal("/tmp/mod", settings.ModPath);
        Assert.Equal("map01", settings.MapName);
        Assert.Equal(32, settings.Downscale);
        Assert.Equal(1, settings.XShift);
        Assert.Equal(2, settings.YShift);
        Assert.Equal(3, settings.ZShift);
        Assert.True(settings.ExportTextures);
        Assert.True(settings.ExportAllTextures);
    }

    [Theory]
    [InlineData("map01", true)]
    [InlineData("e1m1_ref", true)]
    [InlineData("", false)]
    [InlineData("1map", false)]
    [InlineData("Map01", false)]
    [InlineData("map-01", false)]
    [InlineData("map 01", false)]
    public void IsValidMapNameMatchesUdbRules(string mapName, bool expected)
    {
        Assert.Equal(expected, IdStudioExportValidation.IsValidMapName(mapName));
    }

    [Fact]
    public void CreateFilePlanIncludesRootAndNestedRefmaps()
    {
        IdStudioExportSettings settings = IdStudioExportSettings.FromOptions(new IdStudioExportOptions
        {
            ModPath = "/tmp/mod",
            MapName = "map01"
        });
        var root = new IdStudioMapWriter(settings);
        IdStudioMapWriter wadToBrush = root.AddRefmap("wadtobrush");
        IdStudioMapWriter geo = wadToBrush.AddRefmap("wadgeo");

        IReadOnlyList<IdStudioExportFile> files = root.CreateFilePlan();

        Assert.Equal(3, files.Count);
        Assert.Equal("/tmp/mod/base/maps/map01.map", files[0].Path);
        Assert.Equal("/tmp/mod/base/maps/map01_wadtobrush.refmap", files[1].Path);
        Assert.Equal("/tmp/mod/base/maps/map01_wadtobrush_wadgeo.refmap", files[2].Path);
        Assert.Contains("Version 7\nHierarchyVersion 1\nentity {", NormalizeLineEndings(files[0].Content));
        Assert.Contains("entityPrefix = \"\";", files[0].Content);
        Assert.Contains("entityDef func_reference_1", files[0].Content);
        Assert.Contains("mapname = \"maps/map01_wadtobrush.refmap\";", files[0].Content);
        Assert.Contains("entityPrefix = \"wadtobrush\";", files[1].Content);
        Assert.Contains("entityDef wadtobrush_func_reference_1", files[1].Content);
        Assert.Contains("mapname = \"maps/map01_wadtobrush_wadgeo.refmap\";", files[1].Content);
        Assert.Contains("entityPrefix = \"wadgeo\";", files[2].Content);
        Assert.DoesNotContain("func_reference", files[2].Content);
        Assert.NotNull(geo);
    }

    [Fact]
    public void BeginFuncStaticWritesUdbEntityHeaderAndReturnsEntityName()
    {
        IdStudioExportSettings settings = IdStudioExportSettings.FromOptions(new IdStudioExportOptions
        {
            ModPath = "/tmp/mod",
            MapName = "map01"
        });
        var root = new IdStudioMapWriter(settings);
        IdStudioMapWriter geo = root.AddRefmap("wadgeo");

        string entityName = geo.BeginFuncStatic("floor", 12);
        geo.EndFuncStatic();
        IdStudioExportFile refmap = Assert.Single(
            root.CreateFilePlan(),
            file => file.Path.EndsWith(".refmap", StringComparison.Ordinal));

        Assert.Equal("wadgeo_map01_func_static_1", entityName);
        Assert.Contains("\"floor/12\"", refmap.Content);
        Assert.Contains("entityDef wadgeo_map01_func_static_1", refmap.Content);
        Assert.Contains("inherit = \"func/static\";", refmap.Content);
        Assert.Contains("model = \"maps/map01/wadgeo_map01_func_static_1\";", refmap.Content);
        Assert.Contains("clipModelName = \"maps/map01/wadgeo_map01_func_static_1\";", refmap.Content);
    }

    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
