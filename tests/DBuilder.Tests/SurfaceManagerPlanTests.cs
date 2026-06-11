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
        Assert.Equal(SurfaceManagerPlan.MaxVerticesPerBuffer, SurfaceManagerPlan.MAX_VERTICES_PER_BUFFER);
        Assert.Equal(SurfaceManagerPlan.MaxVerticesPerSector, SurfaceManagerPlan.MAX_VERTICES_PER_SECTOR);
    }

    [Fact]
    public void LifecyclePlanRegistersManagerOnConstruction()
    {
        SurfaceManagerLifecyclePlan plan =
            SurfaceManagerPlan.BuildLifecyclePlan(SurfaceManagerLifecycleOperation.Construct);

        Assert.Equal(SurfaceManagerLifecycleOperation.Construct, plan.Operation);
        Assert.True(plan.RegisterWithRenderDevice);
        Assert.False(plan.UnregisterFromRenderDevice);
        Assert.False(plan.DisposeBuffers);
        Assert.False(plan.ResourcesUnloadedAfter);
    }

    [Fact]
    public void LifecyclePlanUnregistersAndDisposesBuffersOnDispose()
    {
        SurfaceManagerLifecyclePlan plan =
            SurfaceManagerPlan.BuildLifecyclePlan(SurfaceManagerLifecycleOperation.Dispose);

        Assert.True(plan.UnregisterFromRenderDevice);
        Assert.True(plan.DisposeBuffers);
        Assert.False(plan.RegisterWithRenderDevice);
        Assert.False(plan.InvalidateEntries);
    }

    [Fact]
    public void LifecyclePlanUnloadsResourcesByDisposingBuffersAndClearingLocks()
    {
        SurfaceManagerLifecyclePlan plan =
            SurfaceManagerPlan.BuildLifecyclePlan(SurfaceManagerLifecycleOperation.UnloadResource);

        Assert.True(plan.DisposeBuffers);
        Assert.True(plan.ClearLockedBuffers);
        Assert.True(plan.ResourcesUnloadedAfter);
        Assert.False(plan.RecreateBuffers);
        Assert.False(plan.UploadEntries);
    }

    [Fact]
    public void LifecyclePlanReloadsResourcesByRecreatingBuffersAndUploadingEntries()
    {
        SurfaceManagerLifecyclePlan plan =
            SurfaceManagerPlan.BuildLifecyclePlan(SurfaceManagerLifecycleOperation.ReloadResource);

        Assert.True(plan.RecreateBuffers);
        Assert.True(plan.UploadEntries);
        Assert.False(plan.DisposeBuffers);
        Assert.False(plan.ResourcesUnloadedAfter);
    }

    [Fact]
    public void LifecyclePlanResetDisposesBuffersAndInvalidatesEntries()
    {
        SurfaceManagerLifecyclePlan plan =
            SurfaceManagerPlan.BuildLifecyclePlan(SurfaceManagerLifecycleOperation.Reset);

        Assert.True(plan.DisposeBuffers);
        Assert.True(plan.InvalidateEntries);
        Assert.False(plan.ClearLockedBuffers);
        Assert.False(plan.ResourcesUnloadedAfter);
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
    public void SurfaceBufferSetExposesUdbFieldShape()
    {
        var buffers = new List<VertexBuffer>();
        var bufferSizes = new List<int> { 12 };
        var entries = new List<SurfaceEntry> { new(numVertices: 3, bufferIndex: 0, vertexOffset: 0) };
        var holes = new List<SurfaceEntry> { new(numVertices: 3, bufferIndex: 0, vertexOffset: 6) };

        var set = new SurfaceBufferSet
        {
            numvertices = 3,
            buffers = buffers,
            buffersizes = bufferSizes,
            entries = entries,
            holes = holes,
        };

        Assert.Equal(3, set.numvertices);
        Assert.Same(buffers, set.buffers);
        Assert.Same(bufferSizes, set.buffersizes);
        Assert.Same(entries, set.entries);
        Assert.Same(holes, set.holes);
    }

    [Fact]
    public void SurfaceBufferSetDefaultMatchesUdbStructDefaults()
    {
        var set = default(SurfaceBufferSet);

        Assert.Equal(0, set.numvertices);
        Assert.Null(set.buffers);
        Assert.Null(set.buffersizes);
        Assert.Null(set.entries);
        Assert.Null(set.holes);
    }

    [Fact]
    public void UnlockBuffersClearsLockedListOnlyWhenResourcesAreLoaded()
    {
        SurfaceBufferUnlockPlan loaded = SurfaceManagerPlan.BuildUnlockBuffersPlan(
            lockedBufferCount: 3,
            resourcesUnloaded: false);
        SurfaceBufferUnlockPlan unloaded = SurfaceManagerPlan.BuildUnlockBuffersPlan(
            lockedBufferCount: 3,
            resourcesUnloaded: true);

        Assert.Equal(new SurfaceBufferUnlockPlan(
            LockedBufferCountBefore: 3,
            ResourcesUnloaded: false,
            ClearLockedBuffers: true,
            LockedBufferCountAfter: 0), loaded);
        Assert.Equal(new SurfaceBufferUnlockPlan(
            LockedBufferCountBefore: 3,
            ResourcesUnloaded: true,
            ClearLockedBuffers: false,
            LockedBufferCountAfter: 3), unloaded);
    }

    [Fact]
    public void UnlockBuffersRejectsInvalidLockedBufferCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurfaceManagerPlan.BuildUnlockBuffersPlan(
                lockedBufferCount: -1,
                resourcesUnloaded: false));
    }

    [Fact]
    public void BufferSetCreatesHolesForAllocatedBufferSlots()
    {
        var set = new SurfaceBufferSetState(verticesPerEntry: 3);

        set.EnsureFreeEntries(4);

        Assert.Equal(new[] { 24 }, set.BufferSizes);
        Assert.Equal(new[] { true }, set.BufferLoaded);
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
        Assert.Equal(new[] { true, true }, set.BufferLoaded);
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

    [Fact]
    public void ResetInvalidatesEntriesAndHolesThenClearsBufferState()
    {
        var set = new SurfaceBufferSetState(verticesPerEntry: 3);
        SurfaceEntry entry = set.AllocateEntry();
        set.EnsureFreeEntries(1);
        SurfaceEntry hole = Assert.Single(set.Holes);

        set.Reset();

        Assert.Equal(-1, entry.NumVertices);
        Assert.Equal(-1, entry.BufferIndex);
        Assert.Equal(-1, hole.NumVertices);
        Assert.Equal(-1, hole.BufferIndex);
        Assert.Empty(set.BufferSizes);
        Assert.Empty(set.BufferLoaded);
        Assert.Empty(set.Entries);
        Assert.Empty(set.Holes);
    }

    [Fact]
    public void ResetAllowsFreshBufferAllocation()
    {
        var set = new SurfaceBufferSetState(verticesPerEntry: 3);
        set.AllocateEntry();
        set.Reset();

        SurfaceEntry entry = set.AllocateEntry();

        Assert.Equal(new[] { 6 }, set.BufferSizes);
        Assert.Equal(new[] { true }, set.BufferLoaded);
        Assert.Equal(3, entry.NumVertices);
        Assert.Equal(0, entry.BufferIndex);
        Assert.Equal(0, entry.VertexOffset);
    }

    [Fact]
    public void UnloadResourcesDisposesLoadedBuffersAndKeepsBufferSizes()
    {
        var set = new SurfaceBufferSetState(verticesPerEntry: 6000);
        set.AllocateEntry();
        set.AllocateEntry();
        set.EnsureFreeEntries(1);
        set.BufferLoaded[1] = false;

        SurfaceBufferUnloadPlan plan = set.UnloadResources();

        Assert.True(set.ResourcesUnloaded);
        Assert.Equal(new[] { 0 }, plan.DisposedBufferIndexes);
        Assert.Equal(new[] { 24000, 12000 }, set.BufferSizes);
        Assert.Equal(new[] { false, false }, set.BufferLoaded);
    }

    [Fact]
    public void AllocatingWhileResourcesAreUnloadedCreatesUnloadedBufferSlots()
    {
        var set = new SurfaceBufferSetState(verticesPerEntry: 3);
        set.UnloadResources();

        SurfaceEntry entry = set.AllocateEntry();

        Assert.True(set.ResourcesUnloaded);
        Assert.Equal(0, entry.BufferIndex);
        Assert.Equal(new[] { 6 }, set.BufferSizes);
        Assert.Equal(new[] { false }, set.BufferLoaded);
    }

    [Fact]
    public void ReloadResourcesMarksBuffersLoadedAndReturnsUploadPlan()
    {
        var set = new SurfaceBufferSetState(verticesPerEntry: 3);
        SurfaceEntry entry = set.AllocateEntry();
        entry.FloorVertices = Vertices(3);
        entry.CeilingVertices = Vertices(3);
        set.UnloadResources();

        IReadOnlyList<SurfaceBufferReloadPlan> plans = set.ReloadResources();

        Assert.False(set.ResourcesUnloaded);
        Assert.Equal(new[] { true }, set.BufferLoaded);
        SurfaceBufferReloadPlan plan = Assert.Single(plans);
        Assert.Equal(0, plan.BufferIndex);
        Assert.Equal(new[] { 3, 3 }, plan.Uploads.Select(upload => upload.VertexCount).ToArray());
    }

    [Fact]
    public void ManagerStateAllocatesChunkedSurfaceEntriesIntoBufferSets()
    {
        var manager = new SurfaceManagerState();
        var entries = new SurfaceEntryCollection();
        SurfaceUpdate update = Update(
            vertices: 6001,
            floorStart: 10,
            ceilingStart: 1000,
            floorTexture: 11,
            ceilingTexture: 22,
            hidden: true,
            desaturation: 0.5);

        manager.UpdateSurfaces(entries, update);

        Assert.Equal(6001, entries.TotalVertices);
        Assert.Equal(new[] { 6000, 1 }, entries.Select(entry => entry.NumVertices).ToArray());
        Assert.Equal(new[] { 1, 6000 }, manager.Sets.Keys.OrderBy(key => key).ToArray());
        Assert.Equal(0, entries[0].BufferIndex);
        Assert.Equal(0, entries[1].BufferIndex);
        Assert.Equal(10, entries[0].FloorVertices[0].x);
        Assert.Equal(6009, entries[0].FloorVertices[^1].x);
        Assert.Equal(6010, entries[1].FloorVertices[0].x);
        Assert.Equal(1000, entries[0].CeilingVertices[0].x);
        Assert.Equal(11, entries[0].FloorTexture);
        Assert.Equal(22, entries[0].CeilingTexture);
        Assert.True(entries[0].Hidden);
        Assert.Equal(0.5, entries[0].Desaturation);
        Assert.Equal(10, entries[0].Bounds.Left);
        Assert.Equal(10, entries[0].Bounds.Top);
    }

    [Fact]
    public void ManagerStateReusesSurfaceEntriesWhenVertexCountMatches()
    {
        var manager = new SurfaceManagerState();
        var entries = new SurfaceEntryCollection();
        manager.UpdateSurfaces(entries, Update(vertices: 3, floorStart: 0, ceilingStart: 100, floorTexture: 1, ceilingTexture: 2));
        SurfaceEntry existing = entries[0];

        manager.UpdateSurfaces(entries, Update(vertices: 3, floorStart: 10, ceilingStart: 200, floorTexture: 7, ceilingTexture: 8));

        Assert.Same(existing, entries[0]);
        Assert.Equal(0, entries[0].BufferIndex);
        Assert.Equal(0, entries[0].VertexOffset);
        Assert.Equal(new[] { 10f, 11f, 12f }, entries[0].FloorVertices.Select(vertex => vertex.x).ToArray());
        Assert.Equal(new[] { 200f, 201f, 202f }, entries[0].CeilingVertices.Select(vertex => vertex.x).ToArray());
        Assert.Equal(7, entries[0].FloorTexture);
        Assert.Equal(8, entries[0].CeilingTexture);
        Assert.Single(manager.GetSet(3).Entries);
        Assert.Empty(manager.GetSet(3).Holes);
    }

    [Fact]
    public void ManagerStateFreesEntriesIntoOwningSetHoles()
    {
        var manager = new SurfaceManagerState();
        var entries = new SurfaceEntryCollection();
        manager.UpdateSurfaces(entries, Update(vertices: 3, floorStart: 0, ceilingStart: 100, floorTexture: 1, ceilingTexture: 2));
        SurfaceEntry entry = entries[0];

        manager.FreeSurfaces(entries);

        SurfaceBufferSetState set = manager.GetSet(3);
        Assert.Empty(set.Entries);
        SurfaceEntry hole = Assert.Single(set.Holes);
        Assert.NotSame(entry, hole);
        Assert.Equal(3, hole.NumVertices);
        Assert.Equal(0, hole.BufferIndex);
        Assert.Equal(0, hole.VertexOffset);
        Assert.Equal(-1, entry.NumVertices);
        Assert.Equal(-1, entry.BufferIndex);
    }

    [Fact]
    public void ManagerStateReallocatesWhenSurfaceVertexCountChanges()
    {
        var manager = new SurfaceManagerState();
        var entries = new SurfaceEntryCollection();
        manager.UpdateSurfaces(entries, Update(vertices: 3, floorStart: 0, ceilingStart: 100, floorTexture: 1, ceilingTexture: 2));
        SurfaceEntry oldEntry = entries[0];

        manager.UpdateSurfaces(entries, Update(vertices: 4, floorStart: 10, ceilingStart: 200, floorTexture: 3, ceilingTexture: 4));

        Assert.NotSame(oldEntry, entries[0]);
        Assert.Equal(-1, oldEntry.NumVertices);
        Assert.Equal(-1, oldEntry.BufferIndex);
        Assert.Equal(4, entries.TotalVertices);
        Assert.Single(manager.GetSet(3).Holes);
        Assert.Single(manager.GetSet(4).Entries);
        Assert.Equal(new[] { 10f, 11f, 12f, 13f }, entries[0].FloorVertices.Select(vertex => vertex.x).ToArray());
    }

    [Fact]
    public void ManagerStateRequiresBothSurfacesWhenCreatingEntries()
    {
        var manager = new SurfaceManagerState();
        var entries = new SurfaceEntryCollection();
        var update = new SurfaceUpdate(numVertices: 3, updateFloor: true, updateCeiling: false)
        {
            FloorVertices = Vertices(3),
        };

        Assert.Throws<InvalidOperationException>(() => manager.UpdateSurfaces(entries, update));
    }

    private static FlatVertex[] Vertices(int count)
        => Enumerable.Range(0, count)
            .Select(i => new FlatVertex { x = i, y = i })
            .ToArray();

    private static SurfaceUpdate Update(
        int vertices,
        float floorStart,
        float ceilingStart,
        long floorTexture,
        long ceilingTexture,
        bool hidden = false,
        double desaturation = 0)
        => new(vertices, updateFloor: true, updateCeiling: true)
        {
            FloorVertices = Vertices(vertices, floorStart),
            CeilingVertices = Vertices(vertices, ceilingStart),
            FloorTexture = floorTexture,
            CeilingTexture = ceilingTexture,
            Hidden = hidden,
            Desaturation = desaturation,
        };

    private static FlatVertex[] Vertices(int count, float start)
        => Enumerable.Range(0, count)
            .Select(i => new FlatVertex { x = start + i, y = start + i })
            .ToArray();
}
