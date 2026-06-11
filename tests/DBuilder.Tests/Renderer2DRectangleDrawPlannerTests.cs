// ABOUTME: Verifies UDB-style Renderer2D.RenderRectangle border planning.
// ABOUTME: Pins four border quads, coordinate transforms, color assignment, and state.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class Renderer2DRectangleDrawPlannerTests
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
    public void BuildBorderPlanMatchesUdbTransformedRectangleQuads()
    {
        Renderer2DRectangleDrawPlan plan = Renderer2DRectangleDrawPlanner.BuildBorderPlan(
            left: 0,
            top: -10,
            right: 10,
            bottom: -20,
            borderSize: 2,
            color: 0x123456,
            transformRectangle: true,
            translateX: 1,
            translateY: 0,
            scale: 2);

        Assert.Equal(4, plan.Quads.Length);
        Assert.Equal(2, plan.PrimitiveCountPerQuad);
        Assert.Equal(Cull.None, plan.CullMode);
        Assert.False(plan.DepthEnabled);
        Assert.False(plan.AlphaBlendEnabled);
        Assert.False(plan.AlphaTestEnabled);
        Assert.True(plan.ResetWorldTransformation);
        Assert.Equal(ShaderName.display2d_normal, plan.Shader);
        Assert.True(plan.BindWhiteTexture);
        Assert.Equal(PrimitiveType.TriangleStrip, plan.PrimitiveType);
        AssertQuad(plan.Quads[0], 2, 20, 22, 18, 0x123456);
        AssertQuad(plan.Quads[1], 2, 42, 22, 40, 0x123456);
        AssertQuad(plan.Quads[2], 2, 18, 4, 42, 0x123456);
        AssertQuad(plan.Quads[3], 20, 18, 22, 42, 0x123456);
    }

    [Fact]
    public void BuildBorderPlanKeepsPreprojectedRectangleWhenTransformIsDisabled()
    {
        Renderer2DRectangleDrawPlan plan = Renderer2DRectangleDrawPlanner.BuildBorderPlan(
            left: 10,
            top: 20,
            right: 30,
            bottom: 40,
            borderSize: 3,
            color: 0x654321,
            transformRectangle: false,
            translateX: 100,
            translateY: 100,
            scale: 4);

        AssertQuad(plan.Quads[0], 10, 20, 30, 17, 0x654321);
        AssertQuad(plan.Quads[1], 10, 43, 30, 40, 0x654321);
        AssertQuad(plan.Quads[2], 10, 17, 13, 43, 0x654321);
        AssertQuad(plan.Quads[3], 27, 17, 30, 43, 0x654321);
    }

    [Fact]
    public void BuildBorderPlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DRectangleDrawPlanner.BuildBorderPlan(
            double.NaN,
            0,
            1,
            1,
            1,
            0,
            transformRectangle: false,
            translateX: 0,
            translateY: 0,
            scale: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DRectangleDrawPlanner.BuildBorderPlan(
            0,
            0,
            1,
            1,
            double.NaN,
            0,
            transformRectangle: false,
            translateX: 0,
            translateY: 0,
            scale: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DRectangleDrawPlanner.BuildBorderPlan(
            0,
            0,
            1,
            1,
            1,
            0,
            transformRectangle: false,
            translateX: 0,
            translateY: 0,
            scale: 0));
    }

    [Fact]
    public void Renderer2DRectangleExpressionsMatchUdbWhenCloneIsAvailable()
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        string source = File.ReadAllText(Path.Combine(udbRoot, "Source", "Core", "Rendering", "Renderer2D.cs"));

        Assert.Contains("public void RenderRectangle(RectangleF rect, float bordersize, PixelColor c, bool transformrect)", source, StringComparison.Ordinal);
        Assert.Contains("lt = lt.GetTransformed(translatex, translatey, scale, -scale);", source, StringComparison.Ordinal);
        Assert.Contains("rb = rb.GetTransformed(translatex, translatey, scale, -scale);", source, StringComparison.Ordinal);
        Assert.Contains("quads[0] = new FlatQuad(PrimitiveType.TriangleStrip, (float)lt.x, (float)lt.y, (float)rb.x, (float)lt.y - bordersize);", source, StringComparison.Ordinal);
        Assert.Contains("quads[3] = new FlatQuad(PrimitiveType.TriangleStrip, (float)rb.x - bordersize, (float)lt.y - bordersize, (float)rb.x, (float)rb.y + bordersize);", source, StringComparison.Ordinal);
        Assert.Contains("quads[0].SetColors(c.ToInt());", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetShader(ShaderName.display2d_normal);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetTexture(General.Map.Data.WhiteTexture.Texture);", source, StringComparison.Ordinal);
        Assert.Contains("quads[3].Render(graphics);", source, StringComparison.Ordinal);
    }

    private static void AssertQuad(FlatQuad quad, float left, float top, float right, float bottom, int color)
    {
        Assert.Equal(PrimitiveType.TriangleStrip, quad.Type);
        Assert.Equal(4, quad.Vertices.Length);
        AssertVertex(quad.Vertices[0], left, top, color);
        AssertVertex(quad.Vertices[1], right, top, color);
        AssertVertex(quad.Vertices[2], left, bottom, color);
        AssertVertex(quad.Vertices[3], right, bottom, color);
    }

    private static void AssertVertex(FlatVertex vertex, float x, float y, int color)
    {
        Assert.Equal(x, vertex.x, precision: 5);
        Assert.Equal(y, vertex.y, precision: 5);
        Assert.Equal(color, vertex.c);
    }
}
