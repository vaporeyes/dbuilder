// ABOUTME: Tests UDB-style Wavefront OBJ export settings and formatting helpers.
// ABOUTME: Verifies validation, material fallback, skip textures, and coordinate output parity.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class WavefrontExportModelTests
{
    [Fact]
    public void FromOptionsDerivesObjNamePathAndDefaults()
    {
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/maps/demo.obj",
            Scale = 2.0,
            SkipTextures = ["SKIP"]
        });

        Assert.Equal("demo", settings.ObjName);
        Assert.Equal("/tmp/maps", settings.ObjPath);
        Assert.Equal(2.0, settings.Scale);
        Assert.False(settings.ExportForGZDoom);
        Assert.False(settings.Valid);
        Assert.Equal(string.Empty, settings.Obj);
        Assert.Equal(20, settings.Radius);
        Assert.Equal(16, settings.Height);
        Assert.Empty(settings.SkipTextures);
        Assert.Null(settings.Textures);
        Assert.Null(settings.Flats);
    }

    [Fact]
    public void FromOptionsAddsDashSkipTextureForGzdoom()
    {
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/maps/demo.obj",
            ExportForGZDoom = true,
            SkipTextures = ["SKIP"]
        });

        Assert.Equal(["SKIP", "-"], settings.SkipTextures);
    }

    [Theory]
    [InlineData(null, WavefrontExportSettings.DefaultMaterial)]
    [InlineData("", WavefrontExportSettings.DefaultMaterial)]
    [InlineData("-", WavefrontExportSettings.DefaultMaterial)]
    [InlineData("STARTAN3", "STARTAN3")]
    public void NormalizeMaterialNameUsesDefaultForEmptyAndDash(string? texture, string expected)
    {
        Assert.Equal(expected, WavefrontExportSettings.NormalizeMaterialName(texture));
    }

    [Fact]
    public void EnsureMaterialNormalizesNameAndAddsMissingBucket()
    {
        var geometry = new Dictionary<string, List<int>>();
        string texture = "-";

        WavefrontExportSettings.EnsureMaterial(geometry, ref texture);

        Assert.Equal(WavefrontExportSettings.DefaultMaterial, texture);
        Assert.True(geometry.ContainsKey(WavefrontExportSettings.DefaultMaterial));
    }

    [Fact]
    public void PluginSettingsUseUdbKeysAndDefaults()
    {
        WavefrontPluginSettings settings = WavefrontPluginSettings.FromDictionary(
            new Dictionary<string, object?>(),
            "/tmp/mapdir");

        Assert.False(settings.ExportTextures);
        Assert.False(settings.ExportForGZDoom);
        Assert.Equal(1.0f, settings.Scale);
        Assert.Equal("/tmp/mapdir", settings.BasePath);
        Assert.Equal("/tmp/mapdir", settings.ActorPath);
        Assert.Equal("/tmp/mapdir", settings.ModelPath);
        Assert.Equal("PLAY", settings.Sprite);
        Assert.True(settings.GenerateCode);
        Assert.True(settings.GenerateModeldef);
        Assert.Empty(settings.NormalizedSkipTextures);
    }

    [Fact]
    public void PluginSettingsReadUdbPersistedValues()
    {
        var source = new Dictionary<string, object?>
        {
            [WavefrontPluginSettings.ExportTexturesKey] = true,
            [WavefrontPluginSettings.ExportForGZDoomKey] = true,
            [WavefrontPluginSettings.ScaleKey] = 2.5f,
            [WavefrontPluginSettings.BasePathKey] = "/base",
            [WavefrontPluginSettings.ActorPathKey] = "/actors",
            [WavefrontPluginSettings.ModelPathKey] = "/models",
            [WavefrontPluginSettings.SpriteKey] = "BOS2",
            [WavefrontPluginSettings.GenerateCodeKey] = false,
            [WavefrontPluginSettings.GenerateModeldefKey] = false,
            [WavefrontPluginSettings.SkipTexturesKey] = new Dictionary<string, string>
            {
                ["texture0"] = "SKY1",
                ["texture1"] = "AASTINKY",
            },
        };

        WavefrontPluginSettings settings = WavefrontPluginSettings.FromDictionary(source, "/fallback");

        Assert.True(settings.ExportTextures);
        Assert.True(settings.ExportForGZDoom);
        Assert.Equal(2.5f, settings.Scale);
        Assert.Equal("/base", settings.BasePath);
        Assert.Equal("/actors", settings.ActorPath);
        Assert.Equal("/models", settings.ModelPath);
        Assert.Equal("BOS2", settings.Sprite);
        Assert.False(settings.GenerateCode);
        Assert.False(settings.GenerateModeldef);
        Assert.Equal(["SKY1", "AASTINKY"], settings.NormalizedSkipTextures);
    }

    [Fact]
    public void PluginSettingsWriteUdbPersistedValues()
    {
        var target = new Dictionary<string, object?>();
        var settings = new WavefrontPluginSettings(
            ExportTextures: true,
            ExportForGZDoom: true,
            Scale: 0.5f,
            BasePath: "/base",
            ActorPath: "/actors",
            ModelPath: "/models",
            Sprite: "play",
            GenerateCode: false,
            GenerateModeldef: false,
            SkipTextures: ["SKY1", "AASTINKY"]);

        settings.WriteTo(target);

        Assert.Equal(true, target[WavefrontPluginSettings.ExportTexturesKey]);
        Assert.Equal(true, target[WavefrontPluginSettings.ExportForGZDoomKey]);
        Assert.Equal(0.5f, target[WavefrontPluginSettings.ScaleKey]);
        Assert.Equal("/base", target[WavefrontPluginSettings.BasePathKey]);
        Assert.Equal("/actors", target[WavefrontPluginSettings.ActorPathKey]);
        Assert.Equal("/models", target[WavefrontPluginSettings.ModelPathKey]);
        Assert.Equal("PLAY", target[WavefrontPluginSettings.SpriteKey]);
        Assert.Equal(false, target[WavefrontPluginSettings.GenerateCodeKey]);
        Assert.Equal(false, target[WavefrontPluginSettings.GenerateModeldefKey]);

        var skip = Assert.IsType<Dictionary<string, string>>(target[WavefrontPluginSettings.SkipTexturesKey]);
        Assert.Equal("SKY1", skip["texture0"]);
        Assert.Equal("AASTINKY", skip["texture1"]);
    }

    [Fact]
    public void UiStateEnablesClassicExportControlsWhenGzdoomExportIsOff()
    {
        WavefrontExportUiState state = WavefrontExportUiState.FromOptions(exportForGZDoom: false, generateCode: true);

        Assert.False(state.GzdoomOptionsEnabled);
        Assert.False(state.ActorSettingsEnabled);
        Assert.False(state.ActorFormatEnabled);
        Assert.False(state.ActorPathEnabled);
        Assert.False(state.ModelPathEnabled);
        Assert.True(state.ClassicExportPathEnabled);
        Assert.True(state.ClassicExportTexturesEnabled);
        Assert.True(state.ScaleEnabled);
    }

    [Fact]
    public void UiStateEnablesGzdoomActorControlsWhenCodeGenerationIsOn()
    {
        WavefrontExportUiState state = WavefrontExportUiState.FromOptions(exportForGZDoom: true, generateCode: true);

        Assert.True(state.GzdoomOptionsEnabled);
        Assert.True(state.ActorSettingsEnabled);
        Assert.True(state.ActorFormatEnabled);
        Assert.True(state.ActorPathEnabled);
        Assert.True(state.ModelPathEnabled);
        Assert.False(state.ClassicExportPathEnabled);
        Assert.False(state.ClassicExportTexturesEnabled);
        Assert.False(state.ScaleEnabled);
    }

    [Fact]
    public void UiStateKeepsModelPathEnabledWhenGzdoomCodeGenerationIsOff()
    {
        WavefrontExportUiState state = WavefrontExportUiState.FromOptions(exportForGZDoom: true, generateCode: false);

        Assert.True(state.GzdoomOptionsEnabled);
        Assert.False(state.ActorSettingsEnabled);
        Assert.False(state.ActorFormatEnabled);
        Assert.False(state.ActorPathEnabled);
        Assert.True(state.ModelPathEnabled);
        Assert.False(state.ClassicExportPathEnabled);
        Assert.False(state.ClassicExportTexturesEnabled);
        Assert.False(state.ScaleEnabled);
    }

    [Fact]
    public void PrepareExportSelectionUsesWholeMapWhenNoSectorsAreSelected()
    {
        var map = new MapSet();
        Sector first = AddSquareSector(map, 0);
        Sector second = AddSquareSector(map, 128);

        WavefrontExportPreflight preflight = WavefrontExportPlanner.PrepareExportSelection(map);

        Assert.True(preflight.CanExport);
        Assert.Null(preflight.Warning);
        Assert.Equal(-1, preflight.DialogSectorCount);
        Assert.Equal([first, second], preflight.Sectors);
    }

    [Fact]
    public void PrepareExportSelectionConvertsSelectedGeometryToSectors()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0);
        foreach (Linedef line in map.Linedefs) line.Selected = true;

        WavefrontExportPreflight preflight = WavefrontExportPlanner.PrepareExportSelection(map);

        Assert.True(preflight.CanExport);
        Assert.Null(preflight.Warning);
        Assert.Equal(1, preflight.DialogSectorCount);
        Assert.Equal([sector], preflight.Sectors);
        Assert.True(sector.Selected);
    }

    [Fact]
    public void PrepareExportSelectionReportsUdbWarningForEmptyMaps()
    {
        WavefrontExportPreflight preflight = WavefrontExportPlanner.PrepareExportSelection(new MapSet());

        Assert.False(preflight.CanExport);
        Assert.Equal(-1, preflight.DialogSectorCount);
        Assert.Equal(WavefrontExportPlanner.NoSectorsWarning, preflight.Warning);
        Assert.Empty(preflight.Sectors);
    }

    [Fact]
    public void ValidateMatchesGzdoomActorAndSpriteRules()
    {
        IReadOnlyList<string> errors = WavefrontExportValidation.Validate(
            new WavefrontExportOptions
            {
                ExportForGZDoom = true,
                ActorName = "1 Bad",
                BasePath = "/base",
                ActorPath = "/actor",
                ModelPath = "/model",
                Sprite = "PLA"
            },
            _ => true);

        Assert.Contains("Actor name must not start with a digit.", errors);
        Assert.Contains("Sprite must be exactly four characters.", errors);
    }

    [Fact]
    public void ValidateChecksGzdoomOutputDirectories()
    {
        IReadOnlyList<string> errors = WavefrontExportValidation.Validate(
            new WavefrontExportOptions
            {
                ExportForGZDoom = true,
                ActorName = "ExportActor",
                BasePath = "/base",
                ActorPath = "/actor",
                ModelPath = "/model",
                Sprite = "PLAY"
            },
            path => path == "/base");

        Assert.DoesNotContain("Base path does not exist.", errors);
        Assert.Contains("Actor path does not exist.", errors);
        Assert.Contains("Model path does not exist.", errors);
    }

    [Fact]
    public void ValidateMatchesClassicScaleAndPathRules()
    {
        IReadOnlyList<string> errors = WavefrontExportValidation.Validate(
            new WavefrontExportOptions
            {
                FilePath = "/missing/demo.obj",
                Scale = 0
            },
            _ => false);

        Assert.Contains("Scale must not be zero.", errors);
        Assert.Contains("Export path does not exist.", errors);
    }

    [Fact]
    public void FormatVertexLineMatchesUdbClassicAxisMapping()
    {
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/maps/demo.obj",
            Scale = 2,
            NormalizeLowestVertex = true
        });

        string line = WavefrontObjFormatter.FormatVertex(new Vector3D(10, 20, 30), settings, new Vector2D(1, 2), 5);

        Assert.Equal("v -18 50 36\n", line);
    }

    [Fact]
    public void FormatVertexLineMatchesUdbGzdoomAxisMapping()
    {
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/maps/demo.obj",
            Scale = 2,
            ExportForGZDoom = true,
            NormalizeLowestVertex = true
        });

        string line = WavefrontObjFormatter.FormatVertex(new Vector3D(10, 20, 30), settings, new Vector2D(1, 2), 5);

        Assert.Equal("v 18 60 -36\n", line);
    }

    [Fact]
    public void FormatUvInvertsVAndMaterialLibrarySkipsGzdoom()
    {
        WavefrontExportSettings classic = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/maps/demo.obj"
        });
        WavefrontExportSettings gzdoom = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/maps/demo.obj",
            ExportForGZDoom = true
        });

        Assert.Equal("vt 0.25 -0.5\n", WavefrontObjFormatter.FormatUv(0.25, 0.5));
        Assert.Equal("mtllib demo.mtl\n", WavefrontObjFormatter.FormatMaterialLibrary(classic));
        Assert.Equal(string.Empty, WavefrontObjFormatter.FormatMaterialLibrary(gzdoom));
    }

    [Fact]
    public void BuildMaterialLibraryMatchesUdbTextureAndFlatEntries()
    {
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj",
            ExportTextures = true
        });
        settings.Textures = [WavefrontExportSettings.DefaultMaterial, "walls/STARTAN3", "NUKAGE1"];
        settings.Flats = ["NUKAGE1", "FLOOR0_1"];

        string mtl = NormalizeLineEndings(WavefrontExportContent.BuildMaterialLibrary(settings, "doom2.wad", "MAP01", "1.0"));

        Assert.Contains("# MTL for doom2.wad, map MAP01\n", mtl);
        Assert.DoesNotContain($"newmtl {WavefrontExportSettings.DefaultMaterial}", mtl);
        Assert.Contains("newmtl walls/STARTAN3\nKd 1.0 1.0 1.0\nmap_Kd /tmp/export/walls/STARTAN3.png\n", mtl);
        Assert.Contains("newmtl NUKAGE1\nKd 1.0 1.0 1.0\nmap_Kd /tmp/export/NUKAGE1.png\n", mtl);
        Assert.Contains("newmtl NUKAGE1\nKd 1.0 1.0 1.0\nmap_Kd /tmp/export/NUKAGE1_FLAT.png\n", mtl);
        Assert.Contains("newmtl FLOOR0_1\nKd 1.0 1.0 1.0\nmap_Kd /tmp/export/FLOOR0_1.png\n", mtl);
    }

    [Fact]
    public void BuildActorCodeAppliesUdbFlagsAndProperties()
    {
        WavefrontExportSettings zscript = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj",
            ActorName = "DemoActor",
            Sprite = "PLAY",
            ZScript = true,
            NoGravity = true,
            SpawnOnCeiling = true,
            Solid = true
        });
        WavefrontExportSettings decorate = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj",
            ActorName = "DemoActor",
            Sprite = "PLAY"
        });

        string zscriptText = WavefrontExportContent.BuildActorCode(zscript);
        string decorateText = WavefrontExportContent.BuildActorCode(decorate);

        Assert.Contains("Class DemoActor : Actor", zscriptText);
        Assert.Contains("Radius 20;", zscriptText);
        Assert.Contains("+NOGRAVITY", zscriptText);
        Assert.Contains("+SPAWNCEILING", zscriptText);
        Assert.Contains("+INVULNERABLE", zscriptText);
        Assert.Contains("PLAY A -1;", zscriptText);
        Assert.Contains("ACTOR DemoActor", decorateText);
        Assert.Contains("Radius 20", decorateText);
        Assert.Contains("PLAY A -1", decorateText);
        Assert.DoesNotContain("+NOGRAVITY", decorateText);
    }

    [Fact]
    public void BuildModeldefUsesRelativeModelPath()
    {
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj",
            ActorName = "DemoActor",
            BasePath = "/tmp/project",
            ModelPath = "/tmp/project/models",
            Sprite = "PLAY"
        });

        string modeldef = WavefrontExportContent.BuildModeldef(settings);

        Assert.Contains("Model DemoActor", modeldef);
        Assert.Contains("Model 0 \"models/DemoActor.obj\"", modeldef);
        Assert.Contains("FrameIndex PLAY A 0 0", modeldef);
    }

    [Fact]
    public void OptimizeGeometryMergesWallRectanglesAndKeepsFloorTriangles()
    {
        WavefrontSurfaceVertex[] vertices =
        [
            Vertex(0, 0, 0, 0, 0),
            Vertex(1, 0, 0, 1, 0),
            Vertex(1, 1, 0, 1, 1),
            Vertex(0, 0, 0, 0, 0),
            Vertex(1, 1, 0, 1, 1),
            Vertex(0, 1, 0, 0, 1)
        ];

        List<WavefrontSurfaceVertex[]> wallGroups = WavefrontGeometryExporter.OptimizeGeometry(vertices, WavefrontSurfaceType.Wall);
        List<WavefrontSurfaceVertex[]> floorGroups = WavefrontGeometryExporter.OptimizeGeometry(vertices, WavefrontSurfaceType.Floor);

        WavefrontSurfaceVertex[] wall = Assert.Single(wallGroups);
        Assert.Equal([vertices[5], vertices[2], vertices[1], vertices[0]], wall);
        Assert.Equal(2, floorGroups.Count);
        Assert.Equal([vertices[2], vertices[1], vertices[0]], floorGroups[0]);
        Assert.Equal([vertices[5], vertices[4], vertices[3]], floorGroups[1]);
    }

    [Fact]
    public void CreateObjGeometryDeduplicatesIndicesAndUpdatesActorSize()
    {
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj",
            CenterModel = true,
            Scale = 1
        });
        var geometry = new List<IDictionary<string, List<WavefrontSurfaceVertex[]>>>
        {
            new Dictionary<string, List<WavefrontSurfaceVertex[]>>
            {
                ["STARTAN3"] =
                [
                    [
                        Vertex(0, 0, 0, 0, 0),
                        Vertex(64, 0, 0, 1, 0),
                        Vertex(64, 32, 16, 1, 1)
                    ]
                ]
            },
            new Dictionary<string, List<WavefrontSurfaceVertex[]>>()
        };

        string obj = WavefrontGeometryExporter.CreateObjGeometry(geometry, settings);

        Assert.Contains("v 32 0 -16\n", obj);
        Assert.Contains("v -32 0 -16\n", obj);
        Assert.Contains("v -32 16 16\n", obj);
        Assert.Contains("vn 0 0 1\n", obj);
        Assert.Contains("vt 1 -1\n", obj);
        Assert.Contains("mtllib demo.mtl\n", obj);
        Assert.Contains("usemtl STARTAN3\nf 1/1/1 2/2/1 3/3/1\n", obj);
        Assert.Equal(16, settings.Radius);
        Assert.Equal(16, settings.Height);
    }

    [Fact]
    public void CollectGroupsSectorFloorsCeilingsAndWallsLikeUdbBuckets()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0);
        sector.FloorTexture = "FLOOR1";
        sector.CeilTexture = "CEIL1";
        sector.FloorHeight = 0;
        sector.CeilHeight = 128;
        foreach (Sidedef side in sector.Sidedefs) side.MidTexture = "STARTAN3";
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj"
        });

        WavefrontGeometryCollection collection = WavefrontGeometryCollector.Collect([sector], settings);

        Assert.True(collection.Valid);
        Assert.Equal([WavefrontExportSettings.DefaultMaterial, "STARTAN3"], collection.Textures);
        Assert.Equal(["CEIL1", WavefrontExportSettings.DefaultMaterial, "FLOOR1"], collection.Flats);
        Assert.True(collection.GeometryByTexture[0]["STARTAN3"].Count > 0);
        Assert.True(collection.GeometryByTexture[1]["FLOOR1"].Count > 0);
        Assert.True(collection.GeometryByTexture[1]["CEIL1"].Count > 0);
    }

    [Fact]
    public void CollectHonorsGzdoomSkipTexturesAndControlSectorFilter()
    {
        var map = new MapSet();
        Sector skippedControl = AddSquareSector(map, 0);
        Sector exported = AddSquareSector(map, 128);
        skippedControl.Sidedefs[0].Line.Action = 160;
        skippedControl.FloorTexture = "CONTROL";
        skippedControl.CeilTexture = "CONTROL";
        exported.FloorTexture = "FLOOR1";
        exported.CeilTexture = "CEIL1";
        foreach (Sidedef side in skippedControl.Sidedefs) side.MidTexture = "CONTROLWALL";
        foreach (Sidedef side in exported.Sidedefs) side.MidTexture = "SKIPWALL";
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj",
            ExportForGZDoom = true,
            IgnoreControlSectors = true,
            SkipTextures = ["SKIPWALL", "FLOOR1"]
        });

        WavefrontGeometryCollection collection = WavefrontGeometryCollector.Collect([skippedControl, exported], settings);

        Assert.True(collection.Valid);
        Assert.Empty(collection.Textures);
        Assert.Equal(["CEIL1"], collection.Flats);
        Assert.False(collection.GeometryByTexture[1].ContainsKey("FLOOR1"));
        Assert.False(collection.GeometryByTexture[0].ContainsKey("CONTROLWALL"));
    }

    [Fact]
    public void CollectAddsTwoSidedUpperAndLowerWallSpans()
    {
        var start = new Vertex(new Vector2D(0, 0));
        var end = new Vertex(new Vector2D(64, 0));
        var line = new Linedef(start, end);
        var frontSector = new Sector { FloorHeight = 0, CeilHeight = 128 };
        var backSector = new Sector { FloorHeight = 32, CeilHeight = 96 };
        var front = new Sidedef(line, true)
        {
            Sector = frontSector,
            HighTexture = "UPPER",
            MidTexture = "MIDDLE",
            LowTexture = "LOWER",
        };
        var back = new Sidedef(line, false) { Sector = backSector };
        line.Front = front;
        line.Back = back;
        front.Other = back;
        back.Other = front;
        frontSector.Sidedefs.Add(front);
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj"
        });

        WavefrontGeometryCollection collection = WavefrontGeometryCollector.Collect([frontSector], settings);

        Assert.True(collection.Valid);
        Assert.Contains("UPPER", collection.Textures);
        Assert.Contains("LOWER", collection.Textures);
        Assert.DoesNotContain("MIDDLE", collection.Textures);
        Assert.Single(collection.GeometryByTexture[0]["UPPER"]);
        Assert.Single(collection.GeometryByTexture[0]["LOWER"]);
    }

    [Fact]
    public void CollectReportsNoVisualSectorsWhenAllSectorsAreFiltered()
    {
        var map = new MapSet();
        Sector control = AddSquareSector(map, 0);
        control.Sidedefs[0].Line.Action = 160;
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj",
            ExportForGZDoom = true,
            IgnoreControlSectors = true
        });

        WavefrontGeometryCollection collection = WavefrontGeometryCollector.Collect([control], settings);

        Assert.False(collection.Valid);
        Assert.Equal(WavefrontGeometryCollector.NoVisualSectorsError, collection.Error);
    }

    [Fact]
    public void CreateObjFromSectorsSetsSettingsContentAndMaterials()
    {
        var map = new MapSet();
        Sector sector = AddSquareSector(map, 0);
        sector.FloorTexture = "FLOOR1";
        sector.CeilTexture = "CEIL1";
        sector.FloorHeight = 0;
        sector.CeilHeight = 128;
        foreach (Sidedef side in sector.Sidedefs) side.MidTexture = "STARTAN3";
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj"
        });

        string obj = WavefrontGeometryCollector.CreateObjFromSectors([sector], settings, "doom2.wad", "MAP01", "1.0");

        Assert.True(settings.Valid);
        Assert.Same(settings.Obj, obj);
        Assert.StartsWith("# doom2.wad, map MAP01\n# Created by Ultimate Doom Builder 1.0\n\no MAP01\n", obj);
        Assert.Contains("usemtl STARTAN3\n", obj);
        Assert.Contains("usemtl FLOOR1\n", obj);
        Assert.Contains("usemtl CEIL1\n", obj);
        Assert.Equal([WavefrontExportSettings.DefaultMaterial, "STARTAN3"], settings.Textures);
        Assert.Equal(["CEIL1", WavefrontExportSettings.DefaultMaterial, "FLOOR1"], settings.Flats);
    }

    [Fact]
    public void PngEncoderWritesDecodableRgbaPng()
    {
        byte[] png = WavefrontPngEncoder.EncodeRgba(
            2,
            1,
            [255, 0, 0, 255, 0, 255, 0, 128]);

        var image = DBuilder.IO.PngDecoder.Decode(png);

        Assert.NotNull(image);
        Assert.Equal(2, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal([255, 0, 0, 255, 0, 255, 0, 128], image.Rgba);
    }

    [Fact]
    public void CreateFilePlanIncludesObjAndMtlForClassicExport()
    {
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj"
        });
        settings.Obj = "obj text";
        settings.Textures = ["STARTAN3"];

        IReadOnlyList<WavefrontExportFile> files = WavefrontExportPlanner.CreateFilePlan(settings, "doom2.wad", "MAP01", "1.0");

        Assert.Equal(2, files.Count);
        Assert.Equal("/tmp/export/demo.obj", files[0].Path);
        Assert.Equal("obj text", files[0].Content);
        Assert.Equal("/tmp/export/demo.mtl", files[1].Path);
        Assert.Contains("# MTL for doom2.wad, map MAP01", files[1].Content);
    }

    [Fact]
    public void CreateFilePlanIncludesGzdoomActorAndModeldefOutputs()
    {
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj",
            ExportForGZDoom = true,
            ActorName = "DemoActor",
            BasePath = "/tmp/project",
            ActorPath = "/tmp/project/actors",
            ModelPath = "/tmp/project/models",
            Sprite = "PLAY",
            ZScript = true
        });
        settings.Obj = "obj text";

        IReadOnlyList<WavefrontExportFile> files = WavefrontExportPlanner.CreateFilePlan(settings, "doom2.wad", "MAP01");

        Assert.Equal(3, files.Count);
        Assert.Equal("/tmp/project/models/DemoActor.obj", files[0].Path);
        Assert.Equal("obj text", files[0].Content);
        Assert.Equal("/tmp/project/actors/DemoActor.zs", files[1].Path);
        Assert.Contains("Class DemoActor : Actor", files[1].Content);
        Assert.Equal("/tmp/project/modeldef.DemoActor.txt", files[2].Path);
        Assert.Contains("Model 0 \"models/DemoActor.obj\"", files[2].Content);
    }

    [Fact]
    public void WriteFilesCreatesDirectoriesAndWritesContents()
    {
        string root = Path.Combine(Path.GetTempPath(), "dbuilder-wavefront-" + Guid.NewGuid().ToString("N"));
        string first = Path.Combine(root, "models", "DemoActor.obj");
        string second = Path.Combine(root, "actors", "DemoActor.zs");

        try
        {
            WavefrontExportPlanner.WriteFiles(
            [
                new WavefrontExportFile(first, "obj text"),
                new WavefrontExportFile(second, "actor text")
            ]);

            Assert.Equal("obj text", File.ReadAllText(first));
            Assert.Equal("actor text", File.ReadAllText(second));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CreateImagePlanExportsClassicTexturesAndFlatsOnly()
    {
        WavefrontExportSettings settings = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj",
            ExportTextures = true
        });
        settings.Textures = [WavefrontExportSettings.DefaultMaterial, "walls/STARTAN3", "NUKAGE1", "MISSINGTEX", "BADTEX"];
        settings.Flats = [WavefrontExportSettings.DefaultMaterial, "NUKAGE1", "MISSINGFLAT", "BADFLAT"];
        var textureBytes = new byte[] { 1, 2, 3 };
        var flatBytes = new byte[] { 4, 5, 6 };

        WavefrontImagePlan plan = WavefrontExportPlanner.CreateImagePlan(
            settings,
            name => name switch
            {
                "walls/STARTAN3" => new WavefrontImageData(64, 128, textureBytes),
                "NUKAGE1" => new WavefrontImageData(64, 64, textureBytes),
                "BADTEX" => new WavefrontImageData(0, 64, []),
                _ => null
            },
            name => name switch
            {
                "NUKAGE1" => new WavefrontImageData(64, 64, flatBytes),
                "BADFLAT" => new WavefrontImageData(64, 0, []),
                _ => null
            });

        Assert.Equal(3, plan.Files.Count);
        Assert.Contains(plan.Files, file => file.Path == "/tmp/export/walls/STARTAN3.png" && !file.IsFlat && file.Content.SequenceEqual(textureBytes));
        Assert.Contains(plan.Files, file => file.Path == "/tmp/export/NUKAGE1.png" && !file.IsFlat);
        Assert.Contains(plan.Files, file => file.Path == "/tmp/export/NUKAGE1_FLAT.PNG" && file.IsFlat && file.Content.SequenceEqual(flatBytes));
        Assert.DoesNotContain(plan.Files, file => file.MaterialName == WavefrontExportSettings.DefaultMaterial);
        Assert.Contains("OBJ Exporter: texture \"MISSINGTEX\" does not exist!", plan.Warnings);
        Assert.Contains("OBJ Exporter: texture \"BADTEX\" has invalid size (0x64)!", plan.Warnings);
        Assert.Contains("OBJ Exporter: flat \"MISSINGFLAT\" does not exist!", plan.Warnings);
        Assert.Contains("OBJ Exporter: flat \"BADFLAT\" has invalid size (64x0)!", plan.Warnings);
    }

    [Fact]
    public void CreateImagePlanSkipsGzdoomAndDisabledTextureExports()
    {
        WavefrontExportSettings gzdoom = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj",
            ExportForGZDoom = true,
            ExportTextures = true
        });
        WavefrontExportSettings disabled = WavefrontExportSettings.FromOptions(new WavefrontExportOptions
        {
            FilePath = "/tmp/export/demo.obj",
            ExportTextures = false
        });
        gzdoom.Textures = ["STARTAN3"];
        disabled.Textures = ["STARTAN3"];

        WavefrontImagePlan gzdoomPlan = WavefrontExportPlanner.CreateImagePlan(gzdoom, _ => throw new InvalidOperationException(), _ => null);
        WavefrontImagePlan disabledPlan = WavefrontExportPlanner.CreateImagePlan(disabled, _ => throw new InvalidOperationException(), _ => null);

        Assert.Empty(gzdoomPlan.Files);
        Assert.Empty(gzdoomPlan.Warnings);
        Assert.Empty(disabledPlan.Files);
        Assert.Empty(disabledPlan.Warnings);
    }

    [Fact]
    public void WriteImageFilesCreatesDirectoriesAndWritesBytes()
    {
        string root = Path.Combine(Path.GetTempPath(), "dbuilder-wavefront-images-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "walls", "STARTAN3.png");
        byte[] bytes = [7, 8, 9];

        try
        {
            WavefrontExportPlanner.WriteImageFiles(
            [
                new WavefrontExportImageFile(path, bytes, "STARTAN3", IsFlat: false)
            ]);

            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static WavefrontSurfaceVertex Vertex(float x, float y, float z, float u, float v)
        => new(x, y, z, u, v, 0, -1, 0);

    private static Sector AddSquareSector(MapSet map, double offsetX)
    {
        var sector = new Sector { Index = map.Sectors.Count };
        var vertices = new[]
        {
            new Vertex(new Vector2D(offsetX + 64, 0)),
            new Vertex(new Vector2D(offsetX, 0)),
            new Vertex(new Vector2D(offsetX, 64)),
            new Vertex(new Vector2D(offsetX + 64, 64)),
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
