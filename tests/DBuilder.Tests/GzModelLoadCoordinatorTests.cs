// ABOUTME: Tests UDB-style shared model loading orchestration across parser formats and texture planning.
// ABOUTME: Verifies model dispatch constraints, Skin and SurfaceSkin selection, companion lookup, errors, bounds, and radius.

using System.Text;
using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class GzModelLoadCoordinatorTests
{
    [Fact]
    public void LoadsObjModelAndUsesEmbeddedSkinWithModelPathFallback()
    {
        byte[] obj = Encoding.ASCII.GetBytes("""
            usemtl crate.png
            v 0 0 0
            v 4 0 0
            v 0 0 -5
            f 1 2 3
            """);
        var request = Request(
            path: "models",
            modelNames: new[] { "models/crate.obj" },
            skinNames: new[] { "" });

        GzLoadedModel result = GzModelLoadCoordinator.Load(
            request,
            path => path == "models/crate.obj" ? obj : null,
            textureExists: path => path == "models/crate.png");

        Assert.Empty(result.Errors);
        GzModelMesh mesh = Assert.Single(result.Meshes);
        Assert.Equal(new[] { "models/crate.png" }, result.TexturePaths);
        Assert.Equal(3, mesh.Vertices.Count);
        Assert.Equal(-4.0f, mesh.Vertices[1].y);
        Assert.Equal(5.0f, mesh.Vertices[2].x);
        Assert.Equal(5, result.Radius);
    }

    [Fact]
    public void ModeldefSkinOverridesEmbeddedAndSurfaceSkins()
    {
        byte[] obj = Encoding.ASCII.GetBytes("""
            usemtl embedded.png
            v 0 0 0
            v 1 0 0
            v 0 1 0
            f 1 2 3
            """);
        var request = Request(
            path: "models",
            modelNames: new[] { "models/crate.obj" },
            skinNames: new[] { "skins/override.png" },
            surfaceSkins: new[] { new Dictionary<int, string> { [0] = "surface.png" } });

        GzLoadedModel result = GzModelLoadCoordinator.Load(
            request,
            path => path == "models/crate.obj" ? obj : null,
            textureExists: path => path == "skins/override.png");

        Assert.Empty(result.Errors);
        Assert.Equal(new[] { "skins/override.png" }, result.TexturePaths);
    }

    [Fact]
    public void SurfaceSkinOverridesEmbeddedSkinWhenModeldefSkinIsEmpty()
    {
        byte[] obj = Encoding.ASCII.GetBytes("""
            usemtl embedded.png
            v 0 0 0
            v 1 0 0
            v 0 1 0
            f 1 2 3
            """);
        var request = Request(
            path: "models",
            modelNames: new[] { "models/crate.obj" },
            skinNames: new[] { "" },
            surfaceSkins: new[] { new Dictionary<int, string> { [0] = "surface.png" } });

        GzLoadedModel result = GzModelLoadCoordinator.Load(
            request,
            path => path == "models/crate.obj" ? obj : null,
            textureExists: path => path == "models/surface.png");

        Assert.Empty(result.Errors);
        Assert.Equal(new[] { "models/surface.png" }, result.TexturePaths);
    }

    [Fact]
    public void ReportsUdbStyleDispatchAndTextureErrors()
    {
        byte[] obj = Encoding.ASCII.GetBytes("""
            usemtl missing.png
            v 0 0 0
            v 1 0 0
            v 0 1 0
            f 1 2 3
            """);
        var request = Request(
            path: "models",
            modelNames: new[] { "models/missing.obj", "models/frame.obj", "models/named.md3", "models/named.iqm", "models/unsupported.txt" },
            skinNames: new[] { "", "", "", "", "" },
            frameNames: new[] { "", "", "Idle", "Idle", "" },
            frameIndices: new[] { 0, 1, 0, 0, 0 });

        GzLoadedModel result = GzModelLoadCoordinator.Load(
            request,
            path => path switch
            {
                "models/frame.obj" => obj,
                "models/named.md3" => new byte[] { 1 },
                "models/named.iqm" => new byte[] { 1 },
                "models/unsupported.txt" => new byte[] { 1 },
                _ => null,
            },
            textureExists: _ => false);

        Assert.Equal(new[]
        {
            "Error while loading \"models/missing.obj\": unable to find file.",
            "Trying to load frame 1 of model \"models/frame.obj\", but OBJ doesn't support frames!",
            "Error while loading \"models/named.md3\": frame names are not supported for MD3 models!",
            "Error while loading \"models/named.iqm\": frame names are not supported for IQM models!",
            "Error while loading \"models/unsupported.txt\": model format is not supported",
        }, result.Errors);
    }

    [Fact]
    public void ReportsUnrealCompanionFileErrorsForEitherInputSide()
    {
        var request = Request(
            path: "",
            modelNames: new[] { "models/thing_d.3d", "models/other_a.3d" },
            skinNames: new[] { "", "" });

        GzLoadedModel result = GzModelLoadCoordinator.Load(
            request,
            path => path is "models/thing_d.3d" or "models/other_a.3d" ? new byte[] { 1, 2, 3, 4 } : null);

        Assert.Equal(new[]
        {
            "Error while loading \"models/thing_d.3d\": unable to find corresponding \"thing_a.3d\" file.",
            "Error while loading \"models/other_a.3d\": unable to find corresponding \"other_d.3d\" file.",
        }, result.Errors);
    }

    private static GzModelLoadRequest Request(
        string path,
        IReadOnlyList<string> modelNames,
        IReadOnlyList<string> skinNames,
        IReadOnlyList<IReadOnlyDictionary<int, string>>? surfaceSkins = null,
        IReadOnlyList<string>? frameNames = null,
        IReadOnlyList<int>? frameIndices = null)
        => new(
            path,
            ModelLoadVector.One,
            modelNames,
            skinNames,
            surfaceSkins ?? modelNames.Select(_ => (IReadOnlyDictionary<int, string>)new Dictionary<int, string>()).ToArray(),
            frameNames ?? modelNames.Select(_ => "").ToArray(),
            frameIndices ?? modelNames.Select(_ => 0).ToArray());
}
