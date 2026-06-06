// ABOUTME: Plans UDB-style surface manager vertex chunking and buffer allocation counts.
// ABOUTME: Keeps surface manager sizing behavior testable before live GL buffer management is wired.

namespace DBuilder.Rendering;

public sealed record SurfaceChunkPlan(int VerticesPerEntry, int EntryCount);

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
