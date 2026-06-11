// ABOUTME: Verifies UDB-style Renderer2D.RenderHighlight draw planning.
// ABOUTME: Pins fill shader state, FillColor uniform, triangle-list draw count, and skip behavior.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class Renderer2DHighlightDrawPlannerTests
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
    public void BuildDrawPlanMatchesUdbHighlightTriangleListState()
    {
        FlatVertex[] vertices =
        [
            Vertex(1, 2, 0x111111),
            Vertex(3, 4, 0x222222),
            Vertex(5, 6, 0x333333),
            Vertex(7, 8, 0x444444),
            Vertex(9, 10, 0x555555),
            Vertex(11, 12, 0x666666),
        ];

        Renderer2DHighlightDrawPlan plan = Renderer2DHighlightDrawPlanner.BuildDrawPlan(
            vertices,
            unchecked((int)0xff123456));

        Assert.Same(vertices, plan.Vertices);
        Assert.True(plan.ShouldDraw);
        Assert.Equal(2, plan.PrimitiveCount);
        Assert.Equal(Cull.None, plan.CullMode);
        Assert.False(plan.DepthEnabled);
        Assert.False(plan.AlphaBlendEnabled);
        Assert.False(plan.AlphaTestEnabled);
        Assert.True(plan.UseWorldTransformation);
        Assert.Equal(UniformName.FillColor, plan.FillColorUniform);
        Assert.Equal(unchecked((int)0xff123456), plan.FillColor.ToArgb());
        Assert.Equal(1.0f, plan.ThingScale);
        Assert.Equal(ShaderName.things2d_fill, plan.Shader);
        Assert.Equal(PrimitiveType.TriangleList, plan.PrimitiveType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BuildDrawPlanSkipsInputsShorterThanOneTriangleLikeUdb(int vertexCount)
    {
        FlatVertex[] vertices = Enumerable.Range(0, vertexCount)
            .Select(index => Vertex(index, index + 1, index))
            .ToArray();

        Renderer2DHighlightDrawPlan plan = Renderer2DHighlightDrawPlanner.BuildDrawPlan(vertices, 0x123456);

        Assert.Same(vertices, plan.Vertices);
        Assert.False(plan.ShouldDraw);
        Assert.Equal(0, plan.PrimitiveCount);
    }

    [Fact]
    public void BuildDrawPlanUsesIntegerTriangleCountLikeUdb()
    {
        Renderer2DHighlightDrawPlan plan = Renderer2DHighlightDrawPlanner.BuildDrawPlan(
            [Vertex(1, 2, 0), Vertex(3, 4, 0), Vertex(5, 6, 0), Vertex(7, 8, 0)],
            0x123456);

        Assert.True(plan.ShouldDraw);
        Assert.Equal(1, plan.PrimitiveCount);
    }

    [Fact]
    public void BuildDrawPlanRejectsNullVertices()
        => Assert.Throws<ArgumentNullException>(() => Renderer2DHighlightDrawPlanner.BuildDrawPlan(null!, 0));

    [Fact]
    public void Renderer2DHighlightExpressionsMatchUdbWhenCloneIsAvailable()
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        string source = File.ReadAllText(Path.Combine(udbRoot, "Source", "Core", "Rendering", "Renderer2D.cs"));

        Assert.Contains("public void RenderHighlight(FlatVertex[] vertices, int color)", source, StringComparison.Ordinal);
        Assert.Contains("if(vertices.Length < 3) return;", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetCullMode(Cull.None);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetZEnable(false);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetAlphaBlendEnable(false);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetAlphaTestEnable(false);", source, StringComparison.Ordinal);
        Assert.Contains("SetWorldTransformation(true);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetUniform(UniformName.FillColor, new Color4(color));", source, StringComparison.Ordinal);
        Assert.Contains("SetThings2DSettings(1.0f);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetShader(ShaderName.things2d_fill);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.Draw(PrimitiveType.TriangleList, 0, vertices.Length / 3, vertices);", source, StringComparison.Ordinal);
    }

    private static FlatVertex Vertex(float x, float y, int color)
        => new()
        {
            x = x,
            y = y,
            c = color,
        };
}
