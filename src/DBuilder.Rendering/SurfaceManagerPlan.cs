// ABOUTME: Plans UDB-style surface manager vertex chunking and buffer allocation counts.
// ABOUTME: Keeps surface manager sizing behavior testable before live GL buffer management is wired.

namespace DBuilder.Rendering;

public sealed record SurfaceChunkPlan(int VerticesPerEntry, int EntryCount);

public enum SurfaceBufferUploadPlane
{
    Floor,
    Ceiling,
}

public sealed record SurfaceBufferUpload(int VertexOffset, SurfaceBufferUploadPlane Plane, int VertexCount);

public sealed record SurfaceBufferReloadPlan(int BufferIndex, int BufferSize, IReadOnlyList<SurfaceBufferUpload> Uploads);

public sealed record SurfaceBufferUnloadPlan(IReadOnlyList<int> DisposedBufferIndexes);

public sealed record SurfaceBufferUnlockPlan(
    int LockedBufferCountBefore,
    bool ResourcesUnloaded,
    bool ClearLockedBuffers,
    int LockedBufferCountAfter);

public enum SurfaceManagerLifecycleOperation
{
    Construct,
    Dispose,
    UnloadResource,
    ReloadResource,
    Reset,
}

public sealed record SurfaceManagerLifecyclePlan(
    SurfaceManagerLifecycleOperation Operation,
    bool RegisterWithRenderDevice,
    bool UnregisterFromRenderDevice,
    bool DisposeBuffers,
    bool ClearLockedBuffers,
    bool InvalidateEntries,
    bool RecreateBuffers,
    bool UploadEntries,
    bool ResourcesUnloadedAfter);

public sealed class SurfaceBufferSetState
{
    public SurfaceBufferSetState(int verticesPerEntry)
    {
        VerticesPerEntry = verticesPerEntry;
    }

    public int VerticesPerEntry { get; }
    public List<int> BufferSizes { get; } = new();
    public List<bool> BufferLoaded { get; } = new();
    public List<SurfaceEntry> Entries { get; } = new();
    public List<SurfaceEntry> Holes { get; } = new();
    public bool ResourcesUnloaded { get; private set; }

    public void EnsureFreeEntries(int freeEntries)
    {
        int addEntries = freeEntries - Holes.Count;
        int bufferIndex = BufferSizes.Count - 1;
        int bufferVerticesPerEntry = SurfaceManagerPlan.VerticesPerBufferEntry(VerticesPerEntry);
        int maxEntriesPerBuffer = SurfaceManagerPlan.MaxEntriesPerBuffer(VerticesPerEntry);

        if (bufferIndex > -1 && BufferSizes[bufferIndex] >= maxEntriesPerBuffer * bufferVerticesPerEntry)
            bufferIndex = -1;

        while (addEntries > 0)
        {
            if (bufferIndex == -1 || bufferIndex > BufferSizes.Count - 1)
            {
                int bufferEntries = Math.Min(addEntries, maxEntriesPerBuffer);
                int allocatedBufferIndex = BufferSizes.Count;
                BufferSizes.Add(bufferEntries * bufferVerticesPerEntry);
                BufferLoaded.Add(!ResourcesUnloaded);
                for (int i = 0; i < bufferEntries; i++)
                    Holes.Add(new SurfaceEntry(VerticesPerEntry, allocatedBufferIndex, i * bufferVerticesPerEntry));

                addEntries -= bufferEntries;
            }
            else
            {
                List<SurfaceEntry> bufferEntries = Entries
                    .Where(entry => entry.BufferIndex == bufferIndex)
                    .ToList();
                int entryCount = Math.Min(bufferEntries.Count + addEntries, maxEntriesPerBuffer);
                int freeEntriesAdded = entryCount - bufferEntries.Count;

                int vertexOffset = 0;
                foreach (SurfaceEntry entry in bufferEntries)
                {
                    entry.VertexOffset = vertexOffset;
                    vertexOffset += bufferVerticesPerEntry;
                }

                BufferSizes[bufferIndex] = entryCount * bufferVerticesPerEntry;
                BufferLoaded[bufferIndex] = !ResourcesUnloaded;
                Holes.Clear();
                for (int i = 0; i < freeEntriesAdded; i++)
                    Holes.Add(new SurfaceEntry(VerticesPerEntry, bufferIndex, i * bufferVerticesPerEntry + vertexOffset));

                addEntries -= freeEntriesAdded;
            }

            bufferIndex = BufferSizes.Count;
        }
    }

    public SurfaceEntry AllocateEntry()
    {
        EnsureFreeEntries(1);
        int index = Holes.Count - 1;
        SurfaceEntry entry = Holes[index];
        Holes.RemoveAt(index);
        Entries.Add(entry);
        return entry;
    }

    public void FreeEntry(SurfaceEntry entry)
    {
        if (entry.NumVertices > 0 && entry.BufferIndex > -1)
        {
            Entries.Remove(entry);
            Holes.Add(new SurfaceEntry(entry));
        }

        entry.NumVertices = -1;
        entry.BufferIndex = -1;
    }

    public IReadOnlyList<SurfaceBufferReloadPlan> PlanReload()
    {
        var plans = new List<SurfaceBufferReloadPlan>();
        for (int bufferIndex = 0; bufferIndex < BufferSizes.Count; bufferIndex++)
        {
            var uploads = new List<SurfaceBufferUpload>();
            foreach (SurfaceEntry entry in Entries)
            {
                if (entry.BufferIndex != bufferIndex) continue;

                uploads.Add(new SurfaceBufferUpload(
                    entry.VertexOffset,
                    SurfaceBufferUploadPlane.Floor,
                    entry.FloorVertices.Length));
                uploads.Add(new SurfaceBufferUpload(
                    entry.VertexOffset + entry.FloorVertices.Length,
                    SurfaceBufferUploadPlane.Ceiling,
                    entry.CeilingVertices.Length));
            }

            plans.Add(new SurfaceBufferReloadPlan(bufferIndex, BufferSizes[bufferIndex], uploads));
        }

        return plans;
    }

    public SurfaceBufferUnloadPlan UnloadResources()
    {
        var disposed = new List<int>();
        ResourcesUnloaded = true;
        for (int i = 0; i < BufferLoaded.Count; i++)
        {
            if (!BufferLoaded[i]) continue;

            disposed.Add(i);
            BufferLoaded[i] = false;
        }

        return new SurfaceBufferUnloadPlan(disposed);
    }

    public IReadOnlyList<SurfaceBufferReloadPlan> ReloadResources()
    {
        IReadOnlyList<SurfaceBufferReloadPlan> plans = PlanReload();
        for (int i = 0; i < BufferLoaded.Count; i++)
            BufferLoaded[i] = true;
        ResourcesUnloaded = false;
        return plans;
    }

    public void Reset()
    {
        foreach (SurfaceEntry entry in Entries)
            Invalidate(entry);
        foreach (SurfaceEntry hole in Holes)
            Invalidate(hole);

        BufferSizes.Clear();
        BufferLoaded.Clear();
        Entries.Clear();
        Holes.Clear();
    }

    private static void Invalidate(SurfaceEntry entry)
    {
        entry.NumVertices = -1;
        entry.BufferIndex = -1;
    }
}

public static class SurfaceManagerPlan
{
    public const int MaxVerticesPerBuffer = 30000;
    public const int MaxVerticesPerSector = 6000;

    public static IReadOnlyList<int> SplitSectorVertexCount(int vertexCount)
    {
        if (vertexCount <= 0) return Array.Empty<int>();

        var chunks = new List<int>();
        int remaining = vertexCount;
        while (remaining > 0)
        {
            int chunk = remaining > MaxVerticesPerSector ? MaxVerticesPerSector : remaining;
            chunks.Add(chunk);
            remaining -= chunk;
        }

        return chunks;
    }

    public static IReadOnlyList<SurfaceChunkPlan> PlanChunks(IEnumerable<int> sectorVertexCounts)
    {
        var counts = new Dictionary<int, int>();
        foreach (int sectorVertexCount in sectorVertexCounts)
        {
            foreach (int chunk in SplitSectorVertexCount(sectorVertexCount))
            {
                counts.TryGetValue(chunk, out int count);
                counts[chunk] = count + 1;
            }
        }

        return counts
            .OrderBy(pair => pair.Key)
            .Select(pair => new SurfaceChunkPlan(pair.Key, pair.Value))
            .ToArray();
    }

    public static int VerticesPerBufferEntry(int verticesPerEntry)
        => checked(verticesPerEntry * 2);

    public static int MaxEntriesPerBuffer(int verticesPerEntry)
        => MaxVerticesPerBuffer / VerticesPerBufferEntry(verticesPerEntry);

    public static SurfaceBufferUnlockPlan BuildUnlockBuffersPlan(int lockedBufferCount, bool resourcesUnloaded)
    {
        if (lockedBufferCount < 0) throw new ArgumentOutOfRangeException(nameof(lockedBufferCount));

        bool clearLockedBuffers = !resourcesUnloaded;
        return new SurfaceBufferUnlockPlan(
            lockedBufferCount,
            resourcesUnloaded,
            clearLockedBuffers,
            clearLockedBuffers ? 0 : lockedBufferCount);
    }

    public static SurfaceManagerLifecyclePlan BuildLifecyclePlan(SurfaceManagerLifecycleOperation operation)
        => operation switch
        {
            SurfaceManagerLifecycleOperation.Construct => new SurfaceManagerLifecyclePlan(
                operation,
                RegisterWithRenderDevice: true,
                UnregisterFromRenderDevice: false,
                DisposeBuffers: false,
                ClearLockedBuffers: false,
                InvalidateEntries: false,
                RecreateBuffers: false,
                UploadEntries: false,
                ResourcesUnloadedAfter: false),
            SurfaceManagerLifecycleOperation.Dispose => new SurfaceManagerLifecyclePlan(
                operation,
                RegisterWithRenderDevice: false,
                UnregisterFromRenderDevice: true,
                DisposeBuffers: true,
                ClearLockedBuffers: false,
                InvalidateEntries: false,
                RecreateBuffers: false,
                UploadEntries: false,
                ResourcesUnloadedAfter: false),
            SurfaceManagerLifecycleOperation.UnloadResource => new SurfaceManagerLifecyclePlan(
                operation,
                RegisterWithRenderDevice: false,
                UnregisterFromRenderDevice: false,
                DisposeBuffers: true,
                ClearLockedBuffers: true,
                InvalidateEntries: false,
                RecreateBuffers: false,
                UploadEntries: false,
                ResourcesUnloadedAfter: true),
            SurfaceManagerLifecycleOperation.ReloadResource => new SurfaceManagerLifecyclePlan(
                operation,
                RegisterWithRenderDevice: false,
                UnregisterFromRenderDevice: false,
                DisposeBuffers: false,
                ClearLockedBuffers: false,
                InvalidateEntries: false,
                RecreateBuffers: true,
                UploadEntries: true,
                ResourcesUnloadedAfter: false),
            SurfaceManagerLifecycleOperation.Reset => new SurfaceManagerLifecyclePlan(
                operation,
                RegisterWithRenderDevice: false,
                UnregisterFromRenderDevice: false,
                DisposeBuffers: true,
                ClearLockedBuffers: false,
                InvalidateEntries: true,
                RecreateBuffers: false,
                UploadEntries: false,
                ResourcesUnloadedAfter: false),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };
}
