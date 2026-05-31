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

    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static WavefrontSurfaceVertex Vertex(float x, float y, float z, float u, float v)
        => new(x, y, z, u, v, 0, -1, 0);
}
