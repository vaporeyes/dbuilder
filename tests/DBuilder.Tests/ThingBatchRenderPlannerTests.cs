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
    }
}
