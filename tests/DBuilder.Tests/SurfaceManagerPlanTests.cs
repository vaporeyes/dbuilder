// ABOUTME: Verifies UDB-style surface manager allocation planning rules.
// ABOUTME: Covers sector vertex chunking and doubled floor/ceiling buffer entry sizing.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class SurfaceManagerPlanTests
{
    [Fact]
    public void UsesUdbSurfaceManagerVertexLimits()
    {
        Assert.Equal(30000, SurfaceManagerPlan.MaxVerticesPerBuffer);
        Assert.Equal(6000, SurfaceManagerPlan.MaxVerticesPerSector);
    }

    [Fact]
    public void SplitsLargeSectorVertexCountsIntoUdbChunks()
    {
        IReadOnlyList<int> chunks = SurfaceManagerPlan.SplitSectorVertexCount(13500);

        Assert.Equal(new[] { 6000, 6000, 1500 }, chunks);
    }

    [Fact]
    public void SkipsZeroAndNegativeSectorVertexCounts()
    {
        Assert.Empty(SurfaceManagerPlan.SplitSectorVertexCount(0));
        Assert.Empty(SurfaceManagerPlan.SplitSectorVertexCount(-3));
    }

    [Fact]
    public void GroupsChunkCountsByVerticesPerEntry()
    {
        IReadOnlyList<SurfaceChunkPlan> chunks = SurfaceManagerPlan.PlanChunks(new[] { 3, 6001, 6000, 0 });

        Assert.Equal(
            new[]
            {
                new SurfaceChunkPlan(1, 1),
                new SurfaceChunkPlan(3, 1),
                new SurfaceChunkPlan(6000, 2),
            },
            chunks);
    }

    [Fact]
    public void BufferEntryStoresFloorAndCeilingVertices()
    {
        Assert.Equal(12, SurfaceManagerPlan.VerticesPerBufferEntry(6));
        Assert.Equal(2500, SurfaceManagerPlan.MaxEntriesPerBuffer(6));
        Assert.Equal(2, SurfaceManagerPlan.MaxEntriesPerBuffer(6000));
    }

    [Fact]
    public void BufferSetCreatesHolesForAllocatedBufferSlots()
    {
        var set = new SurfaceBufferSetState(verticesPerEntry: 3);

        set.EnsureFreeEntries(4);

        Assert.Equal(new[] { 24 }, set.BufferSizes);
        Assert.Equal(new[] { 0, 6, 12, 18 }, set.Holes.Select(entry => entry.VertexOffset).ToArray());
        Assert.All(set.Holes, entry =>
        {
            Assert.Equal(3, entry.NumVertices);
            Assert.Equal(0, entry.BufferIndex);
        });
    }

    [Fact]
    public void BufferSetResizesLastBufferBeforeAddingAnother()
    {
        var set = new SurfaceBufferSetState(verticesPerEntry: 3);
        SurfaceEntry first = set.AllocateEntry();
        SurfaceEntry second = set.AllocateEntry();

        set.EnsureFreeEntries(1);

        Assert.Equal(new[] { 18 }, set.BufferSizes);
        Assert.Equal(2, set.Entries.Count);
        Assert.Equal(0, first.VertexOffset);
        Assert.Equal(6, second.VertexOffset);
        SurfaceEntry hole = Assert.Single(set.Holes);
        Assert.Equal(0, hole.BufferIndex);
        Assert.Equal(12, hole.VertexOffset);
    }

    [Fact]
    public void BufferSetCreatesNextBufferWhenLastBufferIsFull()
    {
        var set = new SurfaceBufferSetState(verticesPerEntry: 6000);
        set.AllocateEntry();
        set.AllocateEntry();

        set.EnsureFreeEntries(1);

        Assert.Equal(new[] { 24000, 12000 }, set.BufferSizes);
        SurfaceEntry hole = Assert.Single(set.Holes);
        Assert.Equal(1, hole.BufferIndex);
        Assert.Equal(0, hole.VertexOffset);
    }

    [Fact]
    public void BufferSetFreeEntryReturnsCopyToHolesAndInvalidatesOriginal()
    {
        var set = new SurfaceBufferSetState(verticesPerEntry: 3);
        SurfaceEntry entry = set.AllocateEntry();
        entry.FloorVertices = new[] { new FlatVertex { x = 1, y = 2 } };

        set.FreeEntry(entry);

        Assert.Empty(set.Entries);
        SurfaceEntry hole = Assert.Single(set.Holes);
        Assert.NotSame(entry, hole);
        Assert.Equal(3, hole.NumVertices);
        Assert.Equal(0, hole.BufferIndex);
        Assert.Empty(hole.FloorVertices);
        Assert.Equal(-1, entry.NumVertices);
        Assert.Equal(-1, entry.BufferIndex);
    }

    [Fact]
    public void ReloadPlanUploadsFloorAndCeilingVerticesForEachBufferEntry()
    {
        var set = new SurfaceBufferSetState(verticesPerEntry: 3);
        SurfaceEntry entry = set.AllocateEntry();
        entry.FloorVertices = Vertices(3);
        entry.CeilingVertices = Vertices(3);
        entry.VertexOffset = 12;

        SurfaceBufferReloadPlan plan = Assert.Single(set.PlanReload());

        Assert.Equal(0, plan.BufferIndex);
        Assert.Equal(6, plan.BufferSize);
        Assert.Equal(
            new[]
            {
                new SurfaceBufferUpload(12, SurfaceBufferUploadPlane.Floor, 3),
                new SurfaceBufferUpload(15, SurfaceBufferUploadPlane.Ceiling, 3),
            },
            plan.Uploads);
    }

    [Fact]
    public void ReloadPlanKeepsUploadsWithTheirBufferAndIgnoresHoles()
    {
        var set = new SurfaceBufferSetState(verticesPerEntry: 6000);
        SurfaceEntry first = set.AllocateEntry();
        SurfaceEntry second = set.AllocateEntry();
        first.FloorVertices = Vertices(6000);
        first.CeilingVertices = Vertices(6000);
        second.FloorVertices = Vertices(6000);
        second.CeilingVertices = Vertices(6000);
        set.EnsureFreeEntries(1);

        IReadOnlyList<SurfaceBufferReloadPlan> plans = set.PlanReload();

        Assert.Equal(new[] { 0, 1 }, plans.Select(plan => plan.BufferIndex).ToArray());
        Assert.Equal(new[] { 24000, 12000 }, plans.Select(plan => plan.BufferSize).ToArray());
        Assert.Equal(
            new[]
            {
                new SurfaceBufferUpload(0, SurfaceBufferUploadPlane.Floor, 6000),
                new SurfaceBufferUpload(6000, SurfaceBufferUploadPlane.Ceiling, 6000),
                new SurfaceBufferUpload(12000, SurfaceBufferUploadPlane.Floor, 6000),
                new SurfaceBufferUpload(18000, SurfaceBufferUploadPlane.Ceiling, 6000),
            },
            plans[0].Uploads);
        Assert.Empty(plans[1].Uploads);
    }

    private static FlatVertex[] Vertices(int count)
        => Enumerable.Range(0, count)
            .Select(i => new FlatVertex { x = i, y = i })
            .ToArray();
}
