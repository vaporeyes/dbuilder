// ABOUTME: Tests UDB-style idStudio export settings and map writer behavior.
// ABOUTME: Verifies map-name validation, refmap hierarchy, file planning, and func/static text.

using DBuilder.Map;
using DBuilder.Geometry;

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

    [Fact]
    public void FormStateMatchesUdbDefaultsAndTextureCountLabels()
    {
        MapSet map = BuildMapWithTextures();

        IdStudioExportFormState state = IdStudioExportFormState.FromMap(
            map,
            Path.Combine("mods", "doom2.wad"),
            "MAP01",
            allTextureCount: 12,
            allFlatCount: 5);

        Assert.Equal("mods", state.DefaultOptions.ModPath);
        Assert.Equal("map01", state.DefaultOptions.MapName);
        Assert.Equal(20, state.DefaultOptions.Downscale);
        Assert.Equal(0, state.DefaultOptions.XShift);
        Assert.Equal(0, state.DefaultOptions.YShift);
        Assert.Equal(0, state.DefaultOptions.ZShift);
        Assert.False(state.DefaultOptions.ExportTextures);
        Assert.False(state.DefaultOptions.ExportAllTextures);
        Assert.Equal(6, state.MapTextureExportCount);
        Assert.Equal(17, state.AllTextureExportCount);
        Assert.Equal("6 TGA images and 6 material2 decls will be created.", state.MapTextureCountText);
        Assert.Equal("17 TGA images and 17 material2 decls will be created.", state.AllTextureCountText);
    }

    [Fact]
    public void FormStateUsesEmptyModPathWhenMapPathHasNoDirectory()
    {
        IdStudioExportFormState state = IdStudioExportFormState.FromMap(
            new MapSet(),
            "doom2.wad",
            "E1M1",
            allTextureCount: 0,
            allFlatCount: 0);

        Assert.Equal(string.Empty, state.DefaultOptions.ModPath);
        Assert.Equal("e1m1", state.DefaultOptions.MapName);
        Assert.Equal("0 TGA images and 0 material2 decls will be created.", state.MapTextureCountText);
        Assert.Equal("0 TGA images and 0 material2 decls will be created.", state.AllTextureCountText);
    }

    [Theory]
    [InlineData(1, 1, 0, "Exported idStudio map map01: 1 geometry file, 1 texture file.")]
    [InlineData(2, 3, 1, "Exported idStudio map map01: 2 geometry files, 3 texture files. 1 missing image.")]
    [InlineData(0, 0, 2, "Exported idStudio map map01: 0 geometry files, 0 texture files. 2 missing images.")]
    public void ExportPlanStatusTextFormatsSingularAndPluralCounts(
        int geometryFileCount,
        int textureFileCount,
        int missingImageCount,
        string expected)
    {
        var geometryFiles = Enumerable.Range(0, geometryFileCount)
            .Select(index => new IdStudioExportFile($"map{index}.refmap", string.Empty))
            .ToArray();
        var textureFiles = Enumerable.Range(0, textureFileCount)
            .Select(index => new IdStudioTextureExportFile($"tex{index}.tga", [], $"tex{index}", IsFlat: false))
            .ToArray();
        var missingImages = Enumerable.Range(0, missingImageCount)
            .Select(index => $"missing{index}")
            .ToArray();
        var plan = new IdStudioExportPlan(
            geometryFiles,
            new IdStudioTextureExportPlan(textureFiles, Array.Empty<IdStudioExportFile>(), missingImages));

        Assert.Equal(expected, plan.StatusText("map01"));
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
    public void GeometryExporterBuildsOneSidedWallBrushGroup()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        sector.FloorHeight = 0;
        sector.CeilHeight = 128;
        Vertex v0 = map.AddVertex(new Vector2D(0, 0));
        Vertex v1 = map.AddVertex(new Vector2D(160, 0));
        Linedef line = map.AddLinedef(v0, v1);
        line.Flags = 0x10;
        Sidedef side = map.AddSidedef(line, isFront: true, sector);
        side.MidTexture = "STARTAN3";
        side.OffsetX = 16;
        side.OffsetY = 32;
        var settings = new IdStudioExportSettings("/tmp/mod", "map01", 16, 0, 0, 0, false, false);

        IReadOnlyList<IdStudioBrushGroup> groups = IdStudioGeometryExporter.BuildWallBrushGroups(map, settings, TextureDimensions);

        IdStudioBrushGroup group = Assert.Single(groups);
        Assert.Equal("wall", group.Group);
        Assert.Equal(0, group.SectorNumber);
        string brush = Assert.Single(group.Brushes);
        Assert.Contains("\"art/wadtobrush/walls/startan3\"", brush);
        Assert.Contains("( 0 -1 0 -0 )", brush);
        Assert.Contains("( ( 0.25 0 0.25 ) ( 0 0.25 0.5 ) )", brush);
    }

    [Fact]
    public void GeometryExporterBuildsTwoSidedWallAndStepBrushGroups()
    {
        var map = new MapSet();
        Sector frontSector = map.AddSector();
        frontSector.FloorHeight = 0;
        frontSector.CeilHeight = 128;
        Sector backSector = map.AddSector();
        backSector.FloorHeight = 16;
        backSector.CeilHeight = 96;
        Vertex v0 = map.AddVertex(new Vector2D(0, 0));
        Vertex v1 = map.AddVertex(new Vector2D(160, 0));
        Linedef line = map.AddLinedef(v0, v1);
        Sidedef front = map.AddSidedef(line, isFront: true, frontSector);
        Sidedef back = map.AddSidedef(line, isFront: false, backSector);
        front.LowTexture = "LOWER";
        front.MidTexture = "MID";
        front.HighTexture = "UPPER";
        back.MidTexture = "BACKMID";

        var settings = new IdStudioExportSettings("/tmp/mod", "map01", 16, 0, 0, 0, false, false);

        IReadOnlyList<IdStudioBrushGroup> groups = IdStudioGeometryExporter.BuildWallBrushGroups(map, settings, TextureDimensions);

        Assert.Equal(2, groups.Count);
        Assert.Equal(1, groups[0].SectorNumber);
        Assert.Equal(4, groups[0].Brushes.Count);
        Assert.Contains(groups[0].Brushes, brush => brush.Contains("\"art/wadtobrush/walls/lower\"", StringComparison.Ordinal));
        Assert.Contains(groups[0].Brushes, brush => brush.Contains("\"stepclip/1\"", StringComparison.Ordinal));
        Assert.Contains(groups[0].Brushes, brush => brush.Contains("\"art/wadtobrush/walls/mid\"", StringComparison.Ordinal));
        Assert.Contains(groups[0].Brushes, brush => brush.Contains("\"art/wadtobrush/walls/upper\"", StringComparison.Ordinal));

        Assert.Equal(0, groups[1].SectorNumber);
        string backBrush = Assert.Single(groups[1].Brushes);
        Assert.Contains("\"art/wadtobrush/walls/backmid\"", backBrush);
        Assert.Contains("( -0 -1 0 -0.0075 )", backBrush);
    }

    [Fact]
    public void GeometryExporterBuildsSectorFloorAndCeilingBrushGroups()
    {
        (MapSet map, Sector sector) = BuildSquareSector(size: 64);
        sector.FloorHeight = 8;
        sector.CeilHeight = 72;
        sector.FloorTexture = "FLOOR0_1";
        sector.CeilTexture = "CEIL5_2";
        var settings = new IdStudioExportSettings("/tmp/mod", "map01", 16, 16, 32, 8, false, false);

        IReadOnlyList<IdStudioBrushGroup> groups = IdStudioGeometryExporter.BuildSectorBrushGroups(map, settings, TextureDimensions);

        Assert.Equal(2, groups.Count);
        Assert.Equal("floor", groups[0].Group);
        Assert.Equal(0, groups[0].SectorNumber);
        Assert.Equal(2, groups[0].Brushes.Count);
        Assert.Contains(groups[0].Brushes, brush => brush.Contains("\"art/wadtobrush/flats/floor0_1\"", StringComparison.Ordinal));
        Assert.Contains(groups[0].Brushes, brush => brush.Contains("( 0 0 1 -1 )", StringComparison.Ordinal));
        Assert.Contains(groups[0].Brushes, brush => brush.Contains("( ( 0 0.25 -0.25 ) ( -0.25 0 0.5 ) )", StringComparison.Ordinal));

        Assert.Equal("ceil", groups[1].Group);
        Assert.Equal(0, groups[1].SectorNumber);
        Assert.Equal(2, groups[1].Brushes.Count);
        Assert.Contains(groups[1].Brushes, brush => brush.Contains("\"art/wadtobrush/flats/ceil5_2\"", StringComparison.Ordinal));
        Assert.Contains(groups[1].Brushes, brush => brush.Contains("( 0 0 -1 5 )", StringComparison.Ordinal));
        Assert.Contains(groups[1].Brushes, brush => brush.Contains("( ( 0 -0.25 -0.25 ) ( -0.25 0 0.5 ) )", StringComparison.Ordinal));
    }

    [Fact]
    public void GeometryExporterSkipsSkySectorSurfaces()
    {
        (MapSet map, Sector sector) = BuildSquareSector(size: 64);
        sector.FloorTexture = "F_SKY1";
        sector.CeilTexture = "F_SKY1";
        var settings = new IdStudioExportSettings("/tmp/mod", "map01", 16, 0, 0, 0, false, false);

        IReadOnlyList<IdStudioBrushGroup> groups = IdStudioGeometryExporter.BuildSectorBrushGroups(
            map,
            settings,
            TextureDimensions,
            hasSkyFloor: s => ReferenceEquals(s, sector),
            hasSkyCeiling: s => ReferenceEquals(s, sector));

        Assert.Empty(groups);
    }

    [Fact]
    public void GeometryExporterCreatesUdbStyleRefmapFilePlan()
    {
        (MapSet map, _) = BuildSquareSector(size: 64);
        map.Sidedefs[0].MidTexture = "STARTAN3";
        var settings = new IdStudioExportSettings("/tmp/mod", "map01", 16, 0, 0, 0, false, false);

        IReadOnlyList<IdStudioExportFile> files = IdStudioGeometryExporter.CreateGeometryFilePlan(
            map,
            settings,
            TextureDimensions,
            TextureDimensions);

        Assert.Equal(3, files.Count);
        IdStudioExportFile geo = files[2];
        Assert.Equal("/tmp/mod/base/maps/map01_wadtobrush_wadgeo.refmap", geo.Path);
        Assert.Contains("entityPrefix = \"wadgeo\";", geo.Content);
        Assert.Contains("\"floor/0\"", geo.Content);
        Assert.Contains("\"ceil/0\"", geo.Content);
        Assert.Contains("\"wall/0\"", geo.Content);
        Assert.Contains("\"art/wadtobrush/flats/-\"", geo.Content);
        Assert.Contains("\"art/wadtobrush/walls/startan3\"", geo.Content);
    }

    [Fact]
    public void GeometryExporterWritesStepclipBrushesToWorldEntity()
    {
        var map = new MapSet();
        Sector frontSector = map.AddSector();
        frontSector.FloorHeight = 0;
        frontSector.CeilHeight = 128;
        Sector backSector = map.AddSector();
        backSector.FloorHeight = 16;
        backSector.CeilHeight = 96;
        Vertex v0 = map.AddVertex(new Vector2D(0, 0));
        Vertex v1 = map.AddVertex(new Vector2D(160, 0));
        Linedef line = map.AddLinedef(v0, v1);
        Sidedef front = map.AddSidedef(line, isFront: true, frontSector);
        map.AddSidedef(line, isFront: false, backSector);
        front.LowTexture = "LOWER";

        var settings = new IdStudioExportSettings("/tmp/mod", "map01", 16, 0, 0, 0, false, false);

        IdStudioExportFile geo = IdStudioGeometryExporter.CreateGeometryFilePlan(
            map,
            settings,
            TextureDimensions,
            TextureDimensions)[2];

        int stepclipIndex = geo.Content.IndexOf("\"stepclip/1\"", StringComparison.Ordinal);
        int staticIndex = geo.Content.IndexOf("entityDef wadgeo_map01_func_static_1", StringComparison.Ordinal);

        Assert.True(stepclipIndex > 0);
        Assert.True(staticIndex > stepclipIndex);
        Assert.Contains("\"art/wadtobrush/walls/lower\"", geo.Content);
    }

    [Fact]
    public void ExportPlannerCombinesGeometryAndMapUsedTextures()
    {
        (MapSet map, Sector sector) = BuildSquareSector(size: 64);
        sector.FloorTexture = "FLOOR0_1";
        sector.CeilTexture = "CEIL5_2";
        map.Sidedefs[0].MidTexture = "STARTAN3";
        var settings = new IdStudioExportSettings("/tmp/mod", "map01", 16, 0, 0, 0, ExportTextures: true, ExportAllTextures: false);

        IdStudioExportPlan plan = IdStudioExportPlanner.CreatePlan(
            map,
            settings,
            allTextures: [],
            allFlats: [],
            getTexture: name => name == "STARTAN3" ? new IdStudioTextureImage(name, [1]) : null,
            getFlat: name => name switch
            {
                "FLOOR0_1" => new IdStudioTextureImage(name, [2]),
                "CEIL5_2" => new IdStudioTextureImage(name, [3]),
                _ => null
            },
            getTextureDimensions: TextureDimensions,
            getFlatDimensions: TextureDimensions);

        Assert.Equal(3, plan.GeometryFiles.Count);
        Assert.Contains("art/wadtobrush/walls/startan3", plan.GeometryFiles[2].Content);
        Assert.Equal(3, plan.TexturePlan.ArtFiles.Count);
        Assert.Contains(plan.TexturePlan.ArtFiles, file => file.Path == "/tmp/mod/base/art/wadtobrush/walls/startan3.tga");
        Assert.Contains(plan.TexturePlan.ArtFiles, file => file.Path == "/tmp/mod/base/art/wadtobrush/flats/floor0_1.tga");
        Assert.Contains(plan.TexturePlan.ArtFiles, file => file.Path == "/tmp/mod/base/art/wadtobrush/flats/ceil5_2.tga");
        Assert.Empty(plan.TexturePlan.MissingImages);
    }

    [Fact]
    public void ExportPlannerWritesGeometryAndTextureFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "dbuilder-idstudio-export-" + Guid.NewGuid().ToString("N"));
        var plan = new IdStudioExportPlan(
            [new IdStudioExportFile(Path.Combine(root, "base/maps/map01.map"), "map")],
            new IdStudioTextureExportPlan(
                [new IdStudioTextureExportFile(Path.Combine(root, "base/art/wadtobrush/walls/startan3.tga"), [1, 2], "startan3", IsFlat: false)],
                [new IdStudioExportFile(Path.Combine(root, "base/declTree/material2/art/wadtobrush/walls/startan3.decl"), "decl")],
                []));

        try
        {
            IdStudioExportPlanner.WriteFiles(plan);

            Assert.Equal("map", File.ReadAllText(Path.Combine(root, "base/maps/map01.map")));
            Assert.Equal([1, 2], File.ReadAllBytes(Path.Combine(root, "base/art/wadtobrush/walls/startan3.tga")));
            Assert.Equal("decl", File.ReadAllText(Path.Combine(root, "base/declTree/material2/art/wadtobrush/walls/startan3.decl")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
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
    public void TextureExporterCollectsMapUsedTexturesAndFlatsLikeUdb()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        sector.FloorTexture = "FLOOR0_1";
        sector.CeilTexture = "-";
        Sector otherSector = map.AddSector();
        otherSector.FloorTexture = "";
        otherSector.CeilTexture = "CEIL5_2";
        Vertex v0 = map.AddVertex(new Vector2D(0, 0));
        Vertex v1 = map.AddVertex(new Vector2D(64, 0));
        Vertex v2 = map.AddVertex(new Vector2D(128, 0));
        Linedef line = map.AddLinedef(v0, v1);
        Sidedef front = map.AddSidedef(line, isFront: true, sector);
        Sidedef back = map.AddSidedef(line, isFront: false, otherSector);
        front.LowTexture = "LOWER";
        front.MidTexture = "-";
        front.HighTexture = "UPPER";
        back.LowTexture = "";
        back.MidTexture = "MID";
        back.HighTexture = "BACKUPPER";

        Linedef backOnlyLine = map.AddLinedef(v1, v2);
        Sidedef backOnly = map.AddSidedef(backOnlyLine, isFront: false, sector);
        backOnly.LowTexture = "IGNOREDLOW";
        backOnly.MidTexture = "IGNOREDMID";
        backOnly.HighTexture = "IGNOREDUPPER";

        IdStudioMapTextureSet used = IdStudioTextureExporter.CollectMapTextures(map);

        Assert.Equal(new HashSet<string>(["LOWER", "UPPER", "MID", "BACKUPPER"], StringComparer.Ordinal), used.Textures);
        Assert.Equal(new HashSet<string>(["FLOOR0_1", "CEIL5_2"], StringComparer.Ordinal), used.Flats);
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

    [Fact]
    public void TextureExporterEncodesUdbStyleUncompressedTga()
    {
        byte[] tga = IdStudioTextureExporter.EncodeTga(
            width: 2,
            height: 1,
            [
                new IdStudioRgba(0x11, 0x22, 0x33, 0x44),
                new IdStudioRgba(0x55, 0x66, 0x77, 0x88)
            ]);

        Assert.Equal(26, tga.Length);
        Assert.Equal(2, tga[2]);
        Assert.Equal(2, tga[12]);
        Assert.Equal(0, tga[13]);
        Assert.Equal(1, tga[14]);
        Assert.Equal(0, tga[15]);
        Assert.Equal(32, tga[16]);
        Assert.Equal(0x20, tga[17]);
        Assert.Equal([0x33, 0x22, 0x11, 0x44, 0x77, 0x66, 0x55, 0x88], tga.Skip(18).ToArray());
    }

    [Fact]
    public void TextureExporterRejectsTgaPixelCountMismatch()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            IdStudioTextureExporter.EncodeTga(2, 2, [new IdStudioRgba(0, 0, 0, 0)]));

        Assert.Equal("pixels", exception.ParamName);
    }

    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static MapSet BuildMapWithTextures()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        sector.FloorTexture = "FLOOR0_1";
        sector.CeilTexture = "-";
        Sector otherSector = map.AddSector();
        otherSector.FloorTexture = "";
        otherSector.CeilTexture = "CEIL5_2";
        Vertex v0 = map.AddVertex(new Vector2D(0, 0));
        Vertex v1 = map.AddVertex(new Vector2D(64, 0));
        Linedef line = map.AddLinedef(v0, v1);
        Sidedef front = map.AddSidedef(line, isFront: true, sector);
        Sidedef back = map.AddSidedef(line, isFront: false, otherSector);
        front.LowTexture = "LOWER";
        front.MidTexture = "-";
        front.HighTexture = "UPPER";
        back.LowTexture = "";
        back.MidTexture = "MID";
        back.HighTexture = "BACKUPPER";
        return map;
    }

    private static IdStudioTextureDimensions TextureDimensions(string texture)
        => texture switch
        {
            "STARTAN3" => new IdStudioTextureDimensions(64, 64),
            "FLOOR0_1" => new IdStudioTextureDimensions(64, 64),
            "CEIL5_2" => new IdStudioTextureDimensions(64, 64),
            "MID" => new IdStudioTextureDimensions(64, 128),
            "BACKMID" => new IdStudioTextureDimensions(64, 128),
            _ => new IdStudioTextureDimensions(64, 64)
        };

    private static (MapSet Map, Sector Sector) BuildSquareSector(double size)
    {
        var map = new MapSet();
        var vertices = new List<Vertex>
        {
            map.AddVertex(new Vector2D(0, 0)),
            map.AddVertex(new Vector2D(size, 0)),
            map.AddVertex(new Vector2D(size, size)),
            map.AddVertex(new Vector2D(0, size)),
        };

        Sector sector = SectorBuilder.CreateSector(map, vertices)!;
        map.BuildIndexes();
        return (map, sector);
    }
}
