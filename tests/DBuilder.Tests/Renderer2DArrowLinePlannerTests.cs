// ABOUTME: Verifies UDB-style Renderer2D.RenderArrows line-list planning.
// ABOUTME: Pins transformed arrowhead geometry, visibility filtering, and render state.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class Renderer2DArrowLinePlannerTests
{
    private static string? FindUdbRoot()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "."));
        string sibling = Path.GetFullPath(Path.Combine(repositoryRoot, "..", "UltimateDoomBuilder"));
        if (File.Exists(Path.Combine(sibling, "Source", "Core", "Rendering", "Renderer2D.cs"))) return sibling;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string root = Path.Combine(home, "dev", "repos", "UltimateDoomBuilder");
        return File.Exists(Path.Combine(root, "Source", "Core", "Rendering", "Renderer2D.cs")) ? root : null;
    }

    [Fact]
    public void BuildDrawPlanMatchesUdbTransformedArrowheadVertices()
    {
        Renderer2DArrowLineDrawPlan plan = Renderer2DArrowLinePlanner.BuildDrawPlan(
            new[] { new Renderer2DArrowLine(0, -10, 10, -10, 0x123456, RenderArrowhead: true) },
            transformCoordinates: true,
            translateX: 1,
            translateY: 0,
            scale: 2,
            windowWidth: 100,
            windowHeight: 100);

        Assert.Equal(6, plan.PointCount);
        Assert.Equal(3, plan.PrimitiveCount);
        Assert.Equal(Cull.None, plan.CullMode);
        Assert.False(plan.DepthEnabled);
        Assert.False(plan.AlphaBlendEnabled);
        Assert.False(plan.AlphaTestEnabled);
        Assert.True(plan.ResetWorldTransformation);
        Assert.Equal(ShaderName.display2d_normal, plan.Shader);
        Assert.True(plan.BindWhiteTexture);
        Assert.True(plan.UseClassicBilinear);
        Assert.Equal(PrimitiveType.LineList, plan.PrimitiveType);
        AssertVertex(plan.Vertices[0], 2, 20, 0x123456);
        AssertVertex(plan.Vertices[1], 22, 20, 0x123456);
        Assert.Equal(plan.Vertices[1], plan.Vertices[2]);
        AssertVertex(plan.Vertices[3], 7.663f, 12.897f, 0x123456);
        Assert.Equal(plan.Vertices[1], plan.Vertices[4]);
        AssertVertex(plan.Vertices[5], 7.663f, 27.103f, 0x123456);
    }

    [Fact]
    public void BuildDrawPlanKeepsPreprojectedLinesWhenTransformIsDisabled()
    {
        Renderer2DArrowLineDrawPlan plan = Renderer2DArrowLinePlanner.BuildDrawPlan(
            new[] { new Renderer2DArrowLine(10, 20, 30, 20, 0x654321, RenderArrowhead: false) },
            transformCoordinates: false,
            translateX: 100,
            translateY: 100,
            scale: 4,
            windowWidth: 100,
            windowHeight: 100);

        Assert.Equal(2, plan.PointCount);
        Assert.Equal(1, plan.PrimitiveCount);
        AssertVertex(plan.Vertices[0], 10, 20, 0x654321);
        AssertVertex(plan.Vertices[1], 30, 20, 0x654321);
    }

    [Fact]
    public void BuildDrawPlanSkipsTinyAndOffscreenLinesLikeUdb()
    {
        Renderer2DArrowLineDrawPlan plan = Renderer2DArrowLinePlanner.BuildDrawPlan(
            new[]
            {
                new Renderer2DArrowLine(10, 10, 11, 11, 0x111111, RenderArrowhead: true),
                new Renderer2DArrowLine(-20, 10, 0, 10, 0x222222, RenderArrowhead: true),
                new Renderer2DArrowLine(10, 10, 20, 10, 0x333333, RenderArrowhead: false),
            },
            transformCoordinates: false,
            translateX: 0,
            translateY: 0,
            scale: 1,
            windowWidth: 100,
            windowHeight: 100);

        Assert.Equal(2, plan.PointCount);
        Assert.Equal(1, plan.PrimitiveCount);
        Assert.Equal(2, plan.Vertices.Length);
        AssertVertex(plan.Vertices[0], 10, 10, 0x333333);
        AssertVertex(plan.Vertices[1], 20, 10, 0x333333);
    }

    [Fact]
    public void BuildDrawPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => Renderer2DArrowLinePlanner.BuildDrawPlan(
            null!,
            transformCoordinates: true,
            translateX: 0,
            translateY: 0,
            scale: 1,
            windowWidth: 100,
            windowHeight: 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DArrowLinePlanner.BuildDrawPlan(
            Array.Empty<Renderer2DArrowLine>(),
            transformCoordinates: true,
            translateX: 0,
            translateY: 0,
            scale: 0,
            windowWidth: 100,
            windowHeight: 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DArrowLinePlanner.BuildDrawPlan(
            Array.Empty<Renderer2DArrowLine>(),
            transformCoordinates: true,
            translateX: 0,
            translateY: double.NaN,
            scale: 1,
            windowWidth: 100,
            windowHeight: 100));
    }

    [Fact]
    public void Renderer2DArrowExpressionsMatchUdbWhenCloneIsAvailable()
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        string source = File.ReadAllText(Path.Combine(udbRoot, "Source", "Core", "Rendering", "Renderer2D.cs"));

        Assert.Contains("public void RenderArrows(ICollection<Line3D> lines, bool transformcoords)", source, StringComparison.Ordinal);
        Assert.Contains("line.Start2D = ((Vector2D)line.Start).GetTransformed(translatex, translatey, scale, -scale);", source, StringComparison.Ordinal);
        Assert.Contains("line.End2D = ((Vector2D)line.End).GetTransformed(translatex, translatey, scale, -scale);", source, StringComparison.Ordinal);
        Assert.Contains("pointscount += (line.RenderArrowhead ? 6 : 2);", source, StringComparison.Ordinal);
        Assert.Contains("float scaler = 16f / scale;", source, StringComparison.Ordinal);
        Assert.Contains("Vector2D a1 = new Vector2D(line.End.x - scaler * Math.Sin(angle - 0.46f), line.End.y + scaler * Math.Cos(angle - 0.46f)).GetTransformed(translatex, translatey, scale, -scale);", source, StringComparison.Ordinal);
        Assert.Contains("Vector2D a2 = new Vector2D(line.End.x - scaler * Math.Sin(angle + 0.46f), line.End.y + scaler * Math.Cos(angle + 0.46f)).GetTransformed(translatex, translatey, scale, -scale);", source, StringComparison.Ordinal);
        Assert.Contains("SetDisplay2DSettings(1f, 1f, 0f, 1f, General.Settings.ClassicBilinear);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.Draw(PrimitiveType.LineList, 0, pointscount / 2);", source, StringComparison.Ordinal);
    }

    private static void AssertVertex(FlatVertex vertex, float x, float y, int color)
    {
        Assert.Equal(x, vertex.x, precision: 3);
        Assert.Equal(y, vertex.y, precision: 3);
        Assert.Equal(color, vertex.c);
    }
}
