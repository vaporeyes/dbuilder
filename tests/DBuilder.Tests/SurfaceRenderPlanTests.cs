// ABOUTME: Verifies UDB-style surface render pass planning for buffered entries.
// ABOUTME: Covers texture grouping, viewport culling, hidden filtering, and buffer order.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class SurfaceRenderPlanTests
{
    [Fact]
    public void FloorPassGroupsVisibleEntriesByFloorTextureInBufferOrder()
    {
        SurfaceBufferSetState set = SetWith(
            Entry(3, floorTexture: 11, ceilingTexture: 21, left: 0),
            Entry(3, floorTexture: 12, ceilingTexture: 22, left: 10),
            Entry(3, floorTexture: 11, ceilingTexture: 23, left: 20));

        IReadOnlyList<SurfaceRenderBatch> batches = SurfaceRenderPlan.Build(
            new[] { set },
            SurfaceRenderPass.Floor,
            new SurfaceBounds(-1, -1, 40, 10),
            skipHidden: true);

        Assert.Equal(new[] { 11L, 12L }, batches.Select(batch => batch.Texture).ToArray());
        Assert.Equal(new[] { set.Entries[0], set.Entries[2] }, batches[0].Entries);
        Assert.Equal(new[] { set.Entries[1] }, batches[1].Entries);
    }

    [Fact]
    public void CeilingPassUsesCeilingTexture()
    {
        SurfaceBufferSetState set = SetWith(
            Entry(3, floorTexture: 11, ceilingTexture: 21, left: 0),
            Entry(3, floorTexture: 12, ceilingTexture: 21, left: 10));

        SurfaceRenderBatch batch = Assert.Single(SurfaceRenderPlan.Build(
            new[] { set },
            SurfaceRenderPass.Ceiling,
            new SurfaceBounds(-1, -1, 30, 10),
            skipHidden: true));

        Assert.Equal(21, batch.Texture);
        Assert.Equal(set.Entries, batch.Entries);
    }

    [Fact]
    public void BrightnessPassUsesWhiteTextureToken()
    {
        SurfaceBufferSetState set = SetWith(
            Entry(3, floorTexture: 11, ceilingTexture: 21, left: 0),
            Entry(3, floorTexture: 12, ceilingTexture: 22, left: 10));

        SurfaceRenderBatch batch = Assert.Single(SurfaceRenderPlan.Build(
            new[] { set },
            SurfaceRenderPass.Brightness,
            new SurfaceBounds(-1, -1, 30, 10),
            skipHidden: true));

        Assert.Equal(SurfaceRenderPlan.BrightnessTexture, batch.Texture);
        Assert.Equal(set.Entries, batch.Entries);
    }

    [Fact]
    public void BuildSkipsHiddenEntriesOnlyWhenRequested()
    {
        SurfaceEntry visible = Entry(3, floorTexture: 11, ceilingTexture: 21, left: 0);
        SurfaceEntry hidden = Entry(3, floorTexture: 11, ceilingTexture: 21, left: 10);
        hidden.Hidden = true;
        SurfaceBufferSetState set = SetWith(visible, hidden);
        var viewport = new SurfaceBounds(-1, -1, 30, 10);

        SurfaceRenderBatch filtered = Assert.Single(SurfaceRenderPlan.Build(
            new[] { set },
            SurfaceRenderPass.Floor,
            viewport,
            skipHidden: true));
        SurfaceRenderBatch unfiltered = Assert.Single(SurfaceRenderPlan.Build(
            new[] { set },
            SurfaceRenderPass.Floor,
            viewport,
            skipHidden: false));

        Assert.Equal(new[] { set.Entries[0] }, filtered.Entries);
        Assert.Equal(new[] { set.Entries[0], set.Entries[1] }, unfiltered.Entries);
    }

    [Fact]
    public void BuildSkipsEntriesOutsideViewport()
    {
        SurfaceEntry visible = Entry(3, floorTexture: 11, ceilingTexture: 21, left: 0);
        SurfaceEntry outside = Entry(3, floorTexture: 11, ceilingTexture: 21, left: 50);
        SurfaceBufferSetState set = SetWith(visible, outside);

        SurfaceRenderBatch batch = Assert.Single(SurfaceRenderPlan.Build(
            new[] { set },
            SurfaceRenderPass.Floor,
            new SurfaceBounds(-1, -1, 20, 10),
            skipHidden: true));

        Assert.Equal(new[] { set.Entries[0] }, batch.Entries);
    }

    [Fact]
    public void BuildCommandsUsesFloorOffsetsForFloorAndBrightnessPasses()
    {
        SurfaceBufferSetState set = SetWith(
            Entry(6, floorTexture: 11, ceilingTexture: 21, left: 0));
        set.Entries[0].VertexOffset = 24;
        set.Entries[0].Desaturation = 0.5;
        SurfaceRenderBatch batch = Assert.Single(SurfaceRenderPlan.Build(
            new[] { set },
            SurfaceRenderPass.Floor,
            new SurfaceBounds(-1, -1, 20, 10),
            skipHidden: true));

        SurfaceRenderCommand floor = Assert.Single(SurfaceRenderPlan.BuildCommands(
            new[] { batch },
            SurfaceRenderPass.Floor));
        SurfaceRenderCommand brightness = Assert.Single(SurfaceRenderPlan.BuildCommands(
            new[] { batch with { Texture = SurfaceRenderPlan.BrightnessTexture } },
            SurfaceRenderPass.Brightness));

        Assert.Equal(new SurfaceRenderCommand(11, 0, 24, 2, 0.5), floor);
        Assert.Equal(new SurfaceRenderCommand(SurfaceRenderPlan.BrightnessTexture, 0, 24, 2, 0.5), brightness);
    }

    [Fact]
    public void BuildCommandsOffsetsCeilingsByEntryVertexCount()
    {
        SurfaceBufferSetState set = SetWith(
            Entry(6, floorTexture: 11, ceilingTexture: 21, left: 0));
        set.Entries[0].VertexOffset = 24;
        SurfaceRenderBatch batch = Assert.Single(SurfaceRenderPlan.Build(
            new[] { set },
            SurfaceRenderPass.Ceiling,
            new SurfaceBounds(-1, -1, 20, 10),
            skipHidden: true));

        SurfaceRenderCommand command = Assert.Single(SurfaceRenderPlan.BuildCommands(
            new[] { batch },
            SurfaceRenderPass.Ceiling));

        Assert.Equal(new SurfaceRenderCommand(21, 0, 30, 2, 0), command);
    }

    [Fact]
    public void BufferBindingsCollapseConsecutiveCommandsForSameBuffer()
    {
        var commands = new[]
        {
            new SurfaceRenderCommand(11, BufferIndex: 0, VertexOffset: 0, PrimitiveCount: 1, Desaturation: 0),
            new SurfaceRenderCommand(11, BufferIndex: 0, VertexOffset: 6, PrimitiveCount: 1, Desaturation: 0),
            new SurfaceRenderCommand(11, BufferIndex: 1, VertexOffset: 0, PrimitiveCount: 1, Desaturation: 0),
            new SurfaceRenderCommand(11, BufferIndex: 1, VertexOffset: 6, PrimitiveCount: 1, Desaturation: 0),
            new SurfaceRenderCommand(11, BufferIndex: 0, VertexOffset: 12, PrimitiveCount: 1, Desaturation: 0),
        };

        IReadOnlyList<SurfaceBufferBinding> bindings = SurfaceRenderPlan.BufferBindings(commands);

        Assert.Equal(
            new[]
            {
                new SurfaceBufferBinding(CommandIndex: 0, BufferIndex: 0),
                new SurfaceBufferBinding(CommandIndex: 2, BufferIndex: 1),
                new SurfaceBufferBinding(CommandIndex: 4, BufferIndex: 0),
            },
            bindings);
    }

    private static SurfaceBufferSetState SetWith(params SurfaceEntry[] entries)
    {
        var set = new SurfaceBufferSetState(verticesPerEntry: 3);
        foreach (SurfaceEntry entry in entries)
        {
            SurfaceEntry allocated = set.AllocateEntry();
            allocated.NumVertices = entry.NumVertices;
            allocated.FloorTexture = entry.FloorTexture;
            allocated.CeilingTexture = entry.CeilingTexture;
            allocated.FloorVertices = entry.FloorVertices;
            allocated.Hidden = entry.Hidden;
            allocated.UpdateBounds();
        }

        return set;
    }

    private static SurfaceEntry Entry(int vertices, long floorTexture, long ceilingTexture, float left)
    {
        var entry = new SurfaceEntry(vertices, bufferIndex: -1, vertexOffset: -1)
        {
            FloorTexture = floorTexture,
            CeilingTexture = ceilingTexture,
            FloorVertices = new[]
            {
                Vertex(left, 0),
                Vertex(left + 4, 0),
                Vertex(left + 2, 4),
            },
        };
        entry.UpdateBounds();
        return entry;
    }

    private static FlatVertex Vertex(float x, float y)
        => new() { x = x, y = y };
}
