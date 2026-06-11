// ABOUTME: Verifies UDB Renderer2D thing batch upload and draw count planning.
// ABOUTME: Pins 2D thing buffer chunk sizes against upstream Renderer2D expressions.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class ThingBatchRenderPlannerTests
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
    public void EmptyThingBatchProducesNoDraws()
    {
        Assert.Empty(ThingBatchRenderPlanner.BuildDraws(0));
    }

    [Fact]
    public void ThingBatchAtCapacityUsesOneDraw()
    {
        ThingBatchDraw draw = Assert.Single(ThingBatchRenderPlanner.BuildDraws(
            PresentationRenderTargetPlan.ThingBufferSize));

        Assert.Equal(0, draw.StartIndex);
        Assert.Equal(100, draw.ItemCount);
        Assert.Equal(600, draw.VertexCount);
        Assert.Equal(200, draw.TriangleCount);
    }

    [Fact]
    public void ThingBatchOverCapacityUsesUdbChunks()
    {
        IReadOnlyList<ThingBatchDraw> draws = ThingBatchRenderPlanner.BuildDraws(250);

        Assert.Equal(new[]
        {
            new ThingBatchDraw(0, 100, 600, 200),
            new ThingBatchDraw(100, 100, 600, 200),
            new ThingBatchDraw(200, 50, 300, 100),
        }, draws);
    }

    [Fact]
    public void ThingBatchDrawsRejectInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildDraws(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildDraws(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildDraws(1, -1));
    }

    [Fact]
    public void ArrowTextureBoundsMatchUdbSpriteState()
    {
        Assert.Equal(new ThingArrowTextureBounds(0.501f, 0.999f, 0.001f, 0.999f),
            ThingBatchRenderPlanner.ArrowTextureBounds(spriteSkipped: false));
        Assert.Equal(new ThingArrowTextureBounds(0.625f, 0.874f, -0.039f, 0.46f),
            ThingBatchRenderPlanner.ArrowTextureBounds(spriteSkipped: true));
    }

    [Fact]
    public void ArrowVerticesMatchUdbRotatedQuadOrder()
    {
        FlatVertex[] vertices = ThingBatchRenderPlanner.BuildArrowVertices(
            screenX: 10,
            screenY: 20,
            angleRadians: 0,
            arrowSize: Math.Sqrt(2),
            spriteSkipped: false);

        Assert.Equal(6, vertices.Length);
        AssertVertex(vertices[0], 11, 21, -1, 0.501f, 0.001f);
        AssertVertex(vertices[1], 9, 21, -1, 0.999f, 0.001f);
        AssertVertex(vertices[2], 11, 19, -1, 0.501f, 0.999f);
        Assert.Equal(vertices[1], vertices[3]);
        Assert.Equal(vertices[2], vertices[4]);
        AssertVertex(vertices[5], 9, 19, -1, 0.999f, 0.999f);
    }

    [Fact]
    public void ArrowVerticesUseLargeArrowTextureWhenSpriteWasSkipped()
    {
        FlatVertex[] vertices = ThingBatchRenderPlanner.BuildArrowVertices(
            screenX: 0,
            screenY: 0,
            angleRadians: 0,
            arrowSize: 1,
            spriteSkipped: true);

        Assert.Equal(0.625f, vertices[0].u);
        Assert.Equal(-0.039f, vertices[0].v);
        Assert.Equal(0.874f, vertices[1].u);
        Assert.Equal(0.46f, vertices[2].v);
        Assert.Equal(0.874f, vertices[5].u);
        Assert.Equal(0.46f, vertices[5].v);
    }

    [Fact]
    public void ArrowVerticesRejectInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildArrowVertices(double.NaN, 0, 0, 1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildArrowVertices(0, double.NaN, 0, 1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildArrowVertices(0, 0, double.NaN, 1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildArrowVertices(0, 0, 0, -1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildArrowVertices(0, 0, 0, double.NaN, false));
    }

    [Fact]
    public void SpriteVerticesMatchUdbQuadOrder()
    {
        FlatVertex[] vertices = ThingBatchRenderPlanner.BuildSpriteVertices(
            screenX: 10,
            screenY: 20,
            width: 3,
            height: 4,
            color: 0x123456,
            mirror: false);

        Assert.Equal(6, vertices.Length);
        AssertVertex(vertices[0], 7, 16, 0x123456, 0.0f, 0.0f);
        AssertVertex(vertices[1], 13, 16, 0x123456, 1.0f, 0.0f);
        AssertVertex(vertices[2], 7, 24, 0x123456, 0.0f, 1.0f);
        Assert.Equal(vertices[1], vertices[3]);
        Assert.Equal(vertices[2], vertices[4]);
        AssertVertex(vertices[5], 13, 24, 0x123456, 1.0f, 1.0f);
    }

    [Fact]
    public void SpriteVerticesMirrorHorizontalTextureCoordinatesLikeUdb()
    {
        FlatVertex[] vertices = ThingBatchRenderPlanner.BuildSpriteVertices(
            screenX: 0,
            screenY: 0,
            width: 1,
            height: 1,
            color: -1,
            mirror: true);

        Assert.Equal(1.0f, vertices[0].u);
        Assert.Equal(0.0f, vertices[1].u);
        Assert.Equal(1.0f, vertices[2].u);
        Assert.Equal(0.0f, vertices[5].u);
    }

    [Fact]
    public void SpriteVerticesRejectInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteVertices(double.NaN, 0, 1, 1, -1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteVertices(0, double.NaN, 1, 1, -1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteVertices(0, 0, -1, 1, -1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteVertices(0, 0, double.NaN, 1, -1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteVertices(0, 0, 1, -1, -1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThingBatchRenderPlanner.BuildSpriteVertices(0, 0, 1, double.NaN, -1, false));
    }

    [Fact]
    public void ThingBatchExpressionsMatchUdbRenderer2DWhenCloneIsAvailable()
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        string source = File.ReadAllText(Path.Combine(udbRoot, "Source", "Core", "Rendering", "Renderer2D.cs"));

        Assert.Contains("FlatVertex[] verts = new FlatVertex[THING_BUFFER_SIZE * 6];", source, StringComparison.Ordinal);
        Assert.Contains("graphics.SetBufferSubdata(thingsvertices, verts, buffercount * 6);", source, StringComparison.Ordinal);
        Assert.Contains("graphics.Draw(PrimitiveType.TriangleList, 0, buffercount * 2);", source, StringComparison.Ordinal);
        Assert.Contains("locksize = ((things.Count - totalcount) > THING_BUFFER_SIZE) ? THING_BUFFER_SIZE : (things.Count - totalcount);", source, StringComparison.Ordinal);
        Assert.Contains("locksize = ((framegroup.Value.Count - totalcount) > THING_BUFFER_SIZE) ? THING_BUFFER_SIZE : (framegroup.Value.Count - totalcount);", source, StringComparison.Ordinal);
        Assert.Contains("locksize = ((thingsByPosition.Count - totalcount) > THING_BUFFER_SIZE) ? THING_BUFFER_SIZE : (thingsByPosition.Count - totalcount);", source, StringComparison.Ordinal);
        Assert.Contains("float sinarrowsize = (float)Math.Sin(t.Angle + Angle2D.PI * 0.25f) * arrowsize;", source, StringComparison.Ordinal);
        Assert.Contains("float cosarrowsize = (float)Math.Cos(t.Angle + Angle2D.PI * 0.25f) * arrowsize;", source, StringComparison.Ordinal);
        Assert.Contains("ul = 0.625f;", source, StringComparison.Ordinal);
        Assert.Contains("ur = 0.874f;", source, StringComparison.Ordinal);
        Assert.Contains("ul = 0.501f;", source, StringComparison.Ordinal);
        Assert.Contains("ur = 0.999f;", source, StringComparison.Ordinal);
        Assert.Contains("float ul = (mirror ? 1f : 0f);", source, StringComparison.Ordinal);
        Assert.Contains("float ur = (mirror ? 0f : 1f);", source, StringComparison.Ordinal);
        Assert.Contains("verts[offset].x = (float)screenpos.x - width;", source, StringComparison.Ordinal);
        Assert.Contains("verts[offset].x = (float)screenpos.x + width;", source, StringComparison.Ordinal);
        Assert.Contains("verts[offset].y = (float)screenpos.y + height;", source, StringComparison.Ordinal);
    }

    private static void AssertVertex(FlatVertex vertex, float x, float y, int color, float u, float v)
    {
        Assert.Equal(x, vertex.x, precision: 5);
        Assert.Equal(y, vertex.y, precision: 5);
        Assert.Equal(color, vertex.c);
        Assert.Equal(u, vertex.u);
        Assert.Equal(v, vertex.v);
    }
}
