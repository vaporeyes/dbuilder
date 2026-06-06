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
}
