// ABOUTME: Verifies UDB-style Renderer3D.StartGeometry collection lifecycle planning.
// ABOUTME: Pins solid, masked, translucent, sky, model, light, and all-things buckets.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class Renderer3DGeometryLifecyclePlanTests
{
    private static string? FindUdbRoot()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "."));
        string sibling = Path.GetFullPath(Path.Combine(repositoryRoot, "..", "UltimateDoomBuilder"));
        if (File.Exists(Path.Combine(sibling, "Source", "Core", "Rendering", "Renderer3D.cs"))) return sibling;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string root = Path.Combine(home, "dev", "repos", "UltimateDoomBuilder");
        return File.Exists(Path.Combine(root, "Source", "Core", "Rendering", "Renderer3D.cs")) ? root : null;
    }

    [Fact]
    public void BuildStartGeometryPlanInitializesUdbBucketsInOrder()
    {
        Renderer3DStartGeometryPlan plan = Renderer3DGeometryLifecyclePlan.BuildStartGeometryPlan();

        Assert.True(plan.InitializesAllBuckets);
        Assert.Equal(
            [
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.SolidGeometry, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.MaskedGeometry, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.TranslucentGeometry, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.SkyGeometry, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.SolidThings, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.MaskedThings, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.TranslucentThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.MaskedModelThings, Renderer3DGeometryCollectionKind.ModelDictionary, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.TranslucentModelThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.LightThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new Renderer3DGeometryBucketPlan(Renderer3DGeometryBucketKind.AllThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
            ],
            plan.Buckets);
    }

    [Fact]
    public void BuildFinishGeometryCleanupPlanMatchesUdbReferenceCleanup()
    {
        Renderer3DFinishGeometryCleanupPlan plan = Renderer3DGeometryLifecyclePlan.BuildFinishGeometryCleanupPlan();

        Assert.True(plan.UnbindTexture);
        Assert.Equal(
            [
                Renderer3DGeometryBucketKind.SolidGeometry,
                Renderer3DGeometryBucketKind.MaskedGeometry,
                Renderer3DGeometryBucketKind.TranslucentGeometry,
                Renderer3DGeometryBucketKind.SkyGeometry,
                Renderer3DGeometryBucketKind.SolidThings,
                Renderer3DGeometryBucketKind.MaskedThings,
                Renderer3DGeometryBucketKind.TranslucentThings,
                Renderer3DGeometryBucketKind.AllThings,
                Renderer3DGeometryBucketKind.LightThings,
                Renderer3DGeometryBucketKind.MaskedModelThings,
                Renderer3DGeometryBucketKind.TranslucentModelThings,
                Renderer3DGeometryBucketKind.VisualVertices,
            ],
            plan.ClearedBuckets);
    }

    [Fact]
    public void Renderer3DStartGeometryExpressionsMatchUdbWhenCloneIsAvailable()
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        string source = File.ReadAllText(Path.Combine(udbRoot, "Source", "Core", "Rendering", "Renderer3D.cs"));

        Assert.Contains("public void StartGeometry()", source, StringComparison.Ordinal);
        Assert.Contains("solidgeo = new Dictionary<ImageData, List<VisualGeometry>>();", source, StringComparison.Ordinal);
        Assert.Contains("maskedgeo = new Dictionary<ImageData, List<VisualGeometry>>();", source, StringComparison.Ordinal);
        Assert.Contains("translucentgeo = new List<VisualGeometry>();", source, StringComparison.Ordinal);
        Assert.Contains("skygeo = new List<VisualGeometry>();", source, StringComparison.Ordinal);
        Assert.Contains("solidthings = new Dictionary<ImageData, List<VisualThing>>();", source, StringComparison.Ordinal);
        Assert.Contains("maskedthings = new Dictionary<ImageData, List<VisualThing>>();", source, StringComparison.Ordinal);
        Assert.Contains("translucentthings = new List<VisualThing>();", source, StringComparison.Ordinal);
        Assert.Contains("maskedmodelthings = new Dictionary<ModelData, List<VisualThing>>();", source, StringComparison.Ordinal);
        Assert.Contains("translucentmodelthings = new List<VisualThing>();", source, StringComparison.Ordinal);
        Assert.Contains("lightthings = new List<VisualThing>();", source, StringComparison.Ordinal);
        Assert.Contains("allthings = new List<VisualThing>();", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetTexture(null);", source, StringComparison.Ordinal);
        Assert.Contains("solidgeo = null;", source, StringComparison.Ordinal);
        Assert.Contains("maskedgeo = null;", source, StringComparison.Ordinal);
        Assert.Contains("translucentgeo = null;", source, StringComparison.Ordinal);
        Assert.Contains("skygeo = null;", source, StringComparison.Ordinal);
        Assert.Contains("solidthings = null;", source, StringComparison.Ordinal);
        Assert.Contains("maskedthings = null;", source, StringComparison.Ordinal);
        Assert.Contains("translucentthings = null;", source, StringComparison.Ordinal);
        Assert.Contains("allthings = null;", source, StringComparison.Ordinal);
        Assert.Contains("lightthings = null;", source, StringComparison.Ordinal);
        Assert.Contains("maskedmodelthings = null;", source, StringComparison.Ordinal);
        Assert.Contains("translucentmodelthings = null;", source, StringComparison.Ordinal);
        Assert.Contains("visualvertices = null;", source, StringComparison.Ordinal);
    }
}
