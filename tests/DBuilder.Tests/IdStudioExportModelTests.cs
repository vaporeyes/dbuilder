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

    [Fact]
    public void IdStudioVectorMatchesUdbDeltaMagnitudeAndNormalize()
    {
        var vector = new IdStudioVector(new IdStudioVertex(1, 2), new IdStudioVertex(4, 6));

        Assert.Equal(3, vector.X);
        Assert.Equal(4, vector.Y);
        Assert.Equal(0, vector.Z);
        Assert.Equal(5, vector.Magnitude());

        vector.Normalize();

        Assert.Equal(0.6f, vector.X, precision: 6);
        Assert.Equal(0.8f, vector.Y, precision: 6);
        Assert.Equal(0, vector.Z);
    }

    [Fact]
    public void IdStudioPlaneSetFromNormalizesAndCalculatesDistance()
    {
        var plane = new IdStudioPlane();

        plane.SetFrom(new IdStudioVector(10, 0, 0), new IdStudioVertex(3, 4));

        Assert.Equal(1, plane.Normal.X);
        Assert.Equal(0, plane.Normal.Y);
        Assert.Equal(0, plane.Normal.Z);
        Assert.Equal(3, plane.Distance);
    }

    [Fact]
    public void EntityBuilderWritesGroupedClipAndCasterBrushPlanes()
    {
        var builder = new IdStudioEntityBuilder();
        var plane = new IdStudioPlane
        {
            Normal = new IdStudioVector(0, 0, 1),
            Distance = 16
        };

        builder.BeginBrushDef("stepclip", 7);
        builder.WriteClipPlane(plane);
        builder.WriteCasterPlane(plane);
        builder.EndBrushDef();
        string text = builder.Render();

        Assert.Contains("\"stepclip/7\"", text);
        Assert.Contains("brushDef3", text);
        Assert.Contains("( 0 0 1 -16 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/clip/clip\" 0 0 0", text);
        Assert.Contains("( 0 0 1 -16 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/shadow_caster\" 0 0 0", text);
        Assert.EndsWith("\t}\n}\n", NormalizeLineEndings(text), StringComparison.Ordinal);
    }

    [Fact]
    public void EntityBuilderLowercasesScientificNotationMarkers()
    {
        string text = IdStudioEntityBuilder.LowercaseScientificNotation("1E-05 2E+06 NAME");

        Assert.Equal("1e-05 2e+06 NAME", text);
    }

    [Fact]
    public void BuildFloorBrushWritesUdbCasterBoundsAndTexturePlane()
    {
        IdStudioExportSettings settings = IdStudioExportSettings.FromOptions(new IdStudioExportOptions
        {
            MapName = "map01",
            Downscale = 20,
            XShift = 4,
            YShift = 8
        });

        string brush = NormalizeLineEndings(IdStudioBrushFormatter.BuildFloorBrush(
            settings,
            new IdStudioVertex(0, 0),
            new IdStudioVertex(10, 0),
            new IdStudioVertex(0, 10),
            height: 8,
            isCeiling: false,
            texture: "FLOOR0_1",
            sectorNumber: 12,
            textureWidth: 64,
            textureHeight: 128));

        Assert.StartsWith("{\n\tbrushDef3 {\n", brush, StringComparison.Ordinal);
        Assert.Contains("( 0 -1 0 -0 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/shadow_caster\" 0 0 0", brush);
        Assert.Contains("( 0.70710677 0.70710677 0 -7.071068 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/shadow_caster\" 0 0 0", brush);
        Assert.Contains("( -1 -0 0 0 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/shadow_caster\" 0 0 0", brush);
        Assert.Contains("( 0 0 -1 7.9925 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/shadow_caster\" 0 0 0", brush);
        Assert.Contains("( 0 0 1 -8 ) ( ( 0 0.3125 -0.0625 ) ( -0.15625 0 0.0625 ) ) \"art/wadtobrush/flats/floor0_1\" 0 0 0", brush);
        Assert.EndsWith("\t}\n}\n", brush, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCeilingBrushFlipsSurfaceAndHorizontalTextureScale()
    {
        IdStudioExportSettings settings = IdStudioExportSettings.FromOptions(new IdStudioExportOptions
        {
            MapName = "map01",
            Downscale = 20,
            XShift = 4,
            YShift = 8
        });

        string brush = IdStudioBrushFormatter.BuildFloorBrush(
            settings,
            new IdStudioVertex(0, 0),
            new IdStudioVertex(10, 0),
            new IdStudioVertex(0, 10),
            height: 8,
            isCeiling: true,
            texture: "CEIL5_2",
            sectorNumber: 12,
            textureWidth: 64,
            textureHeight: 128);

        Assert.Contains("( 0 0 1 -8.0075 )", brush);
        Assert.Contains("( 0 0 -1 8 ) ( ( 0 -0.3125 -0.0625 ) ( -0.15625 0 0.0625 ) ) \"art/wadtobrush/flats/ceil5_2\" 0 0 0", brush);
    }

    [Fact]
    public void BuildWallBrushWritesUdbBoundsAndTextureProjection()
    {
        IdStudioExportSettings settings = IdStudioExportSettings.FromOptions(new IdStudioExportOptions
        {
            MapName = "map01",
            Downscale = 20
        });

        string brush = NormalizeLineEndings(IdStudioBrushFormatter.BuildWallBrush(
            settings,
            new IdStudioVertex(0, 0),
            new IdStudioVertex(10, 0),
            minHeight: 0,
            maxHeight: 16,
            drawHeight: 32,
            texture: "STARTAN3",
            offsetX: 2,
            textureWidth: 64,
            textureHeight: 128));

        Assert.StartsWith("{\n\tbrushDef3 {\n", brush, StringComparison.Ordinal);
        Assert.Contains("( -0 1 0 -0.0075 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/shadow_caster\" 0 0 0", brush);
        Assert.Contains("( 1 -0 0 -10 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/shadow_caster\" 0 0 0", brush);
        Assert.Contains("( -1 0 0 -0 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/shadow_caster\" 0 0 0", brush);
        Assert.Contains("( 0 0 1 -16 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/shadow_caster\" 0 0 0", brush);
        Assert.Contains("( 0 0 -1 0 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/shadow_caster\" 0 0 0", brush);
        Assert.Contains("( 0 -1 0 -0 ) ( ( 0.3125 0 0.625 ) ( 0 0.15625 5 ) ) \"art/wadtobrush/walls/startan3\" 0 0 0", brush);
        Assert.EndsWith("\t}\n}\n", brush, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildWallBrushExpandsTooThinBrushHeights()
    {
        IdStudioExportSettings settings = IdStudioExportSettings.FromOptions(new IdStudioExportOptions
        {
            MapName = "map01",
            Downscale = 20
        });

        string brush = IdStudioBrushFormatter.BuildWallBrush(
            settings,
            new IdStudioVertex(0, 0),
            new IdStudioVertex(10, 0),
            minHeight: 10,
            maxHeight: 10,
            drawHeight: 16,
            texture: "MID",
            offsetX: 0,
            textureWidth: 64,
            textureHeight: 64);

        Assert.Contains("( 0 0 1 -15 )", brush);
        Assert.Contains("( 0 0 -1 5 )", brush);
    }

    [Fact]
    public void BuildStepBrushWritesUdbStepclipPlanes()
    {
        string brush = NormalizeLineEndings(IdStudioBrushFormatter.BuildStepBrush(
            new IdStudioVertex(0, 0),
            new IdStudioVertex(10, 0),
            minHeight: 0,
            maxHeight: 10,
            sectorNumber: 9));

        Assert.Contains("\"stepclip/9\"", brush);
        Assert.Contains("( -0 1 0 -0 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/clip/clip\" 0 0 0", brush);
        Assert.Contains("( -1 -0 0 0 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/clip/clip\" 0 0 0", brush);
        Assert.Contains("( 1 0 0 -10 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/clip/clip\" 0 0 0", brush);
        Assert.Contains("( 0 0 -1 0 ) ( ( 1 0 0 ) ( 0 1 0 ) ) \"art/tile/common/clip/clip\" 0 0 0", brush);
        Assert.Contains("-0.4472136 0.8944272 -8.94427", brush);
        Assert.EndsWith("\t}\n}\n", brush, StringComparison.Ordinal);
    }

    [Fact]
    public void TextureExporterReportsRequiredDirectories()
    {
        IReadOnlyList<string> directories = IdStudioTextureExporter.RequiredDirectories("/tmp/mod");

        Assert.Equal(
            [
                "/tmp/mod/base/art/wadtobrush/flats/",
                "/tmp/mod/base/declTree/material2/art/wadtobrush/flats/",
                "/tmp/mod/base/art/wadtobrush/walls/",
                "/tmp/mod/base/declTree/material2/art/wadtobrush/walls/"
            ],
            directories);
    }

    [Fact]
    public void TextureExporterPlansMapUsedTextureAndFlatFiles()
    {
        IdStudioExportSettings settings = IdStudioExportSettings.FromOptions(new IdStudioExportOptions
        {
            ModPath = "/tmp/mod",
            MapName = "map01",
            ExportTextures = true
        });
        byte[] wallBytes = [1, 2, 3];
        byte[] flatBytes = [4, 5, 6];

        IdStudioTextureExportPlan plan = IdStudioTextureExporter.CreatePlan(
            settings,
            mapTextureNames: ["STARTAN3", "MISSINGTEX"],
            mapFlatNames: ["FLOOR0_1", "MISSINGFLAT"],
            allTextures: [],
            allFlats: [],
            getTexture: name => name == "STARTAN3" ? new IdStudioTextureImage(name, wallBytes, IsMasked: true) : null,
            getFlat: name => name == "FLOOR0_1" ? new IdStudioTextureImage(name, flatBytes) : null);

        Assert.Equal(2, plan.ArtFiles.Count);
        Assert.Contains(plan.ArtFiles, file => file.Path == "/tmp/mod/base/art/wadtobrush/walls/startan3.tga" && !file.IsFlat && file.Content.SequenceEqual(wallBytes));
        Assert.Contains(plan.ArtFiles, file => file.Path == "/tmp/mod/base/art/wadtobrush/flats/floor0_1.tga" && file.IsFlat && file.Content.SequenceEqual(flatBytes));
        Assert.Equal(2, plan.MaterialFiles.Count);
        Assert.Contains(plan.MaterialFiles, file => file.Path == "/tmp/mod/base/declTree/material2/art/wadtobrush/walls/startan3.decl");
        Assert.Contains(plan.MaterialFiles, file => file.Content.Contains("template/pbr_alphatest", StringComparison.Ordinal));
        Assert.Contains(plan.MaterialFiles, file => file.Content.Contains("filePath = \"art/wadtobrush/walls/startan3.tga\";", StringComparison.Ordinal));
        Assert.Contains(plan.MaterialFiles, file => file.Path == "/tmp/mod/base/declTree/material2/art/wadtobrush/flats/floor0_1.decl");
        Assert.Contains(plan.MaterialFiles, file => file.Content.Contains("template/pbr\";", StringComparison.Ordinal));
        Assert.Contains("idStudio Exporter: texture \"MISSINGTEX\" does not exist!", plan.MissingImages);
        Assert.Contains("idStudio Exporter: flat \"MISSINGFLAT\" does not exist!", plan.MissingImages);
    }

    [Fact]
    public void TextureExporterPlansAllTextureAndFlatFilesWhenEnabled()
    {
        IdStudioExportSettings settings = IdStudioExportSettings.FromOptions(new IdStudioExportOptions
        {
            ModPath = "/tmp/mod",
            MapName = "map01",
            ExportTextures = true,
            ExportAllTextures = true
        });

        IdStudioTextureExportPlan plan = IdStudioTextureExporter.CreatePlan(
            settings,
            mapTextureNames: ["IGNORED"],
            mapFlatNames: ["IGNORED"],
            allTextures: [new IdStudioTextureImage("BRICK", [1])],
            allFlats: [new IdStudioTextureImage("CEIL5_2", [2])],
            getTexture: _ => throw new InvalidOperationException(),
            getFlat: _ => throw new InvalidOperationException());

        Assert.Equal(2, plan.ArtFiles.Count);
        Assert.Contains(plan.ArtFiles, file => file.Path == "/tmp/mod/base/art/wadtobrush/walls/brick.tga");
        Assert.Contains(plan.ArtFiles, file => file.Path == "/tmp/mod/base/art/wadtobrush/flats/ceil5_2.tga");
        Assert.Empty(plan.MissingImages);
    }

    [Fact]
    public void TextureExporterSkipsPlanningWhenTextureExportDisabled()
    {
        IdStudioExportSettings settings = IdStudioExportSettings.FromOptions(new IdStudioExportOptions
        {
            ModPath = "/tmp/mod",
            MapName = "map01",
            ExportTextures = false
        });

        IdStudioTextureExportPlan plan = IdStudioTextureExporter.CreatePlan(
            settings,
            mapTextureNames: ["STARTAN3"],
            mapFlatNames: ["FLOOR0_1"],
            allTextures: [],
            allFlats: [],
            getTexture: _ => throw new InvalidOperationException(),
            getFlat: _ => throw new InvalidOperationException());

        Assert.Empty(plan.ArtFiles);
        Assert.Empty(plan.MaterialFiles);
        Assert.Empty(plan.MissingImages);
    }

    [Fact]
    public void TextureExporterWritesArtAndMaterialFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "dbuilder-idstudio-textures-" + Guid.NewGuid().ToString("N"));
        var plan = new IdStudioTextureExportPlan(
            [new IdStudioTextureExportFile(Path.Combine(root, "base/art/wadtobrush/walls/startan3.tga"), [1, 2], "startan3", IsFlat: false)],
            [new IdStudioExportFile(Path.Combine(root, "base/declTree/material2/art/wadtobrush/walls/startan3.decl"), "material")],
            []);

        try
        {
            IdStudioTextureExporter.WriteTextureFiles(plan);

            Assert.Equal([1, 2], File.ReadAllBytes(Path.Combine(root, "base/art/wadtobrush/walls/startan3.tga")));
            Assert.Equal("material", File.ReadAllText(Path.Combine(root, "base/declTree/material2/art/wadtobrush/walls/startan3.decl")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
