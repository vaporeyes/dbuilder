// ABOUTME: Verifies UDB-style Renderer2D.RenderGeometry draw planning.
// ABOUTME: Pins triangle-list primitive count, texture binding choice, transform use, and state.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class Renderer2DGeometryDrawPlannerTests
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
    public void BuildDrawPlanMatchesUdbTexturedTriangleListState()
    {
        FlatVertex[] vertices =
        [
            Vertex(1, 2, 0x123456),
            Vertex(3, 4, 0x123456),
            Vertex(5, 6, 0x123456),
            Vertex(7, 8, 0x654321),
            Vertex(9, 10, 0x654321),
            Vertex(11, 12, 0x654321),
        ];

        Renderer2DGeometryDrawPlan plan = Renderer2DGeometryDrawPlanner.BuildDrawPlan(
            vertices,
            hasTexture: true,
            transformCoordinates: true);

        Assert.Same(vertices, plan.Vertices);
        Assert.True(plan.ShouldDraw);
        Assert.Equal(2, plan.PrimitiveCount);
        Assert.Equal(Cull.None, plan.CullMode);
        Assert.False(plan.DepthEnabled);
        Assert.False(plan.AlphaBlendEnabled);
        Assert.False(plan.AlphaTestEnabled);
        Assert.False(plan.ResetWorldTransformation);
        Assert.True(plan.UseWorldTransformation);
        Assert.Equal(ShaderName.display2d_normal, plan.Shader);
        Assert.False(plan.BindWhiteTexture);
        Assert.True(plan.BindProvidedTexture);
        Assert.Equal(PrimitiveType.TriangleList, plan.PrimitiveType);
    }

    [Fact]
    public void BuildDrawPlanBindsWhiteTextureWhenTextureIsMissing()
    {
        Renderer2DGeometryDrawPlan plan = Renderer2DGeometryDrawPlanner.BuildDrawPlan(
            [Vertex(1, 2, 0), Vertex(3, 4, 0), Vertex(5, 6, 0)],
            hasTexture: false,
            transformCoordinates: false);

        Assert.True(plan.ShouldDraw);
        Assert.Equal(1, plan.PrimitiveCount);
        Assert.False(plan.UseWorldTransformation);
        Assert.True(plan.BindWhiteTexture);
        Assert.False(plan.BindProvidedTexture);
    }

    [Fact]
    public void BuildDrawPlanSkipsEmptyInputLikeUdb()
    {
        Renderer2DGeometryDrawPlan plan = Renderer2DGeometryDrawPlanner.BuildDrawPlan(
            [],
            hasTexture: true,
            transformCoordinates: true);

        Assert.Empty(plan.Vertices);
        Assert.False(plan.ShouldDraw);
        Assert.Equal(0, plan.PrimitiveCount);
        Assert.True(plan.BindProvidedTexture);
        Assert.False(plan.BindWhiteTexture);
    }

    [Fact]
    public void BuildDrawPlanRejectsNullVertices()
        => Assert.Throws<ArgumentNullException>(() => Renderer2DGeometryDrawPlanner.BuildDrawPlan(
            null!,
            hasTexture: false,
            transformCoordinates: false));

    [Fact]
    public void Renderer2DGeometryExpressionsMatchUdbWhenCloneIsAvailable()
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        string source = File.ReadAllText(Path.Combine(udbRoot, "Source", "Core", "Rendering", "Renderer2D.cs"));

        Assert.Contains("public void RenderGeometry(FlatVertex[] vertices, ImageData texture, bool transformcoords)", source, StringComparison.Ordinal);
        Assert.Contains("if(vertices.Length > 0)", source, StringComparison.Ordinal);
        Assert.Contains("t = texture.Texture;", source, StringComparison.Ordinal);
        Assert.Contains("t = General.Map.Data.WhiteTexture.Texture;", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetCullMode(Cull.None);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetZEnable(false);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetAlphaBlendEnable(false);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetAlphaTestEnable(false);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetShader(ShaderName.display2d_normal);", source, StringComparison.Ordinal);
        Assert.Contains("SetWorldTransformation(transformcoords);", source, StringComparison.Ordinal);
        Assert.Contains("SetDisplay2DSettings(1f, 1f, 0f, 1f, General.Settings.ClassicBilinear);", source, StringComparison.Ordinal);
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
