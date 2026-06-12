// ABOUTME: Verifies UDB-style Renderer2D.RenderLine thick-line planning.
// ABOUTME: Pins triangle-strip vertex geometry, transforms, and render state.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class Renderer2DLineDrawPlannerTests
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
    public void BuildDrawPlanMatchesUdbTransformedHorizontalLine()
    {
        Renderer2DLineDrawPlan plan = Renderer2DLineDrawPlanner.BuildDrawPlan(
            startX: 0,
            startY: -10,
            endX: 10,
            endY: -10,
            thickness: 2,
            color: 0x123456,
            transformCoordinates: true,
            translateX: 1,
            translateY: 0,
            scale: 2);

        Assert.Equal(4, plan.Vertices.Length);
        Assert.Equal(2, plan.PrimitiveCount);
        Assert.Equal(Cull.None, plan.CullMode);
        Assert.False(plan.DepthEnabled);
        Assert.False(plan.AlphaBlendEnabled);
        Assert.False(plan.AlphaTestEnabled);
        Assert.True(plan.ResetWorldTransformation);
        Assert.Equal(ShaderName.display2d_normal, plan.Shader);
        Assert.True(plan.BindWhiteTexture);
        Assert.True(plan.UseClassicBilinear);
        Assert.Equal(PrimitiveType.TriangleStrip, plan.PrimitiveType);
        AssertVertex(plan.Vertices[0], 0, 18, 0x123456);
        AssertVertex(plan.Vertices[1], 0, 22, 0x123456);
        AssertVertex(plan.Vertices[2], 24, 18, 0x123456);
        AssertVertex(plan.Vertices[3], 24, 22, 0x123456);
    }

    [Fact]
    public void BuildDrawPlanKeepsPreprojectedDiagonalLineWhenTransformIsDisabled()
    {
        Renderer2DLineDrawPlan plan = Renderer2DLineDrawPlanner.BuildDrawPlan(
            startX: 10,
            startY: 20,
            endX: 13,
            endY: 24,
            thickness: 5,
            color: 0x654321,
            transformCoordinates: false,
            translateX: 100,
            translateY: 100,
            scale: 2);

        AssertVertex(plan.Vertices[0], 11, 13, 0x654321);
        AssertVertex(plan.Vertices[1], 3, 19, 0x654321);
        AssertVertex(plan.Vertices[2], 20, 25, 0x654321);
        AssertVertex(plan.Vertices[3], 12, 31, 0x654321);
    }

    [Fact]
    public void BuildDrawPlanHandlesZeroLengthLinesLikeUdbNormal()
    {
        Renderer2DLineDrawPlan plan = Renderer2DLineDrawPlanner.BuildDrawPlan(
            startX: 5,
            startY: 7,
            endX: 5,
            endY: 7,
            thickness: 4,
            color: 0xabcdef,
            transformCoordinates: false,
            translateX: 0,
            translateY: 0,
            scale: 1);

        AssertVertex(plan.Vertices[0], 5, 7, 0xabcdef);
        AssertVertex(plan.Vertices[1], 5, 7, 0xabcdef);
        AssertVertex(plan.Vertices[2], 5, 7, 0xabcdef);
        AssertVertex(plan.Vertices[3], 5, 7, 0xabcdef);
    }

    [Fact]
    public void BuildDrawPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DLineDrawPlanner.BuildDrawPlan(
            double.NaN,
            0,
            1,
            1,
            1,
            0,
            transformCoordinates: false,
            translateX: 0,
            translateY: 0,
            scale: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DLineDrawPlanner.BuildDrawPlan(
            0,
            0,
            1,
            1,
            -1,
            0,
            transformCoordinates: false,
            translateX: 0,
            translateY: 0,
            scale: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DLineDrawPlanner.BuildDrawPlan(
            0,
            0,
            1,
            1,
            1,
            0,
            transformCoordinates: false,
            translateX: 0,
            translateY: 0,
            scale: 0));
    }

    [Fact]
    public void Renderer2DLineExpressionsMatchUdbWhenCloneIsAvailable()
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        string source = File.ReadAllText(Path.Combine(udbRoot, "Source", "Core", "Rendering", "Renderer2D.cs"));

        Assert.Contains("public void RenderLine(Vector2D start, Vector2D end, float thickness, PixelColor c, bool transformcoords)", source, StringComparison.Ordinal);
        Assert.Contains("start = start.GetTransformed(translatex, translatey, scale, -scale);", source, StringComparison.Ordinal);
        Assert.Contains("end = end.GetTransformed(translatex, translatey, scale, -scale);", source, StringComparison.Ordinal);
        Assert.Contains("Vector2D dn = delta.GetNormal() * thickness;", source, StringComparison.Ordinal);
        Assert.Contains("verts[0].x = (float)(start.x - dn.x + dn.y);", source, StringComparison.Ordinal);
        Assert.Contains("verts[3].y = (float)(end.y + dn.y + dn.x);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetShader(ShaderName.display2d_normal);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetTexture(General.Map.Data.WhiteTexture.Texture);", source, StringComparison.Ordinal);
        Assert.Contains("SetDisplay2DSettings(1f, 1f, 0f, 1f, General.Settings.ClassicBilinear);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.Draw(PrimitiveType.TriangleStrip, 0, 2, verts);", source, StringComparison.Ordinal);
    }

    private static void AssertVertex(FlatVertex vertex, float x, float y, int color)
    {
        Assert.Equal(x, vertex.x, precision: 5);
        Assert.Equal(y, vertex.y, precision: 5);
        Assert.Equal(0.0f, vertex.z);
        Assert.Equal(color, vertex.c);
    }
}
