// ABOUTME: Plans UDB-style surface manager vertex chunking and buffer allocation counts.
// ABOUTME: Keeps surface manager sizing behavior testable before live GL buffer management is wired.

namespace DBuilder.Rendering;

public sealed record SurfaceChunkPlan(int VerticesPerEntry, int EntryCount);

public sealed class SurfaceBufferSetState
{
    public SurfaceBufferSetState(int verticesPerEntry)
    {
        VerticesPerEntry = verticesPerEntry;
    }

    public int VerticesPerEntry { get; }
    public List<int> BufferSizes { get; } = new();
    public List<SurfaceEntry> Entries { get; } = new();
    public List<SurfaceEntry> Holes { get; } = new();

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
}
