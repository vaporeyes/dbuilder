// ABOUTME: Plans UDB-style 2D thing batch upload and draw counts.
// ABOUTME: Keeps Renderer2D thing buffer chunking testable outside the live render device.

namespace DBuilder.Rendering;

public readonly record struct ThingBatchDraw(
    int StartIndex,
    int ItemCount,
    int VertexCount,
    int TriangleCount);

public static class ThingBatchRenderPlanner
{
    public const int VerticesPerThing = 6;
    public const int TrianglesPerThing = 2;

    public static IReadOnlyList<ThingBatchDraw> BuildDraws(
        int itemCount,
        int bufferItemCapacity = PresentationRenderTargetPlan.ThingBufferSize)
    {
        if (itemCount < 0) throw new ArgumentOutOfRangeException(nameof(itemCount));
        if (bufferItemCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(bufferItemCapacity));
        if (itemCount == 0) return Array.Empty<ThingBatchDraw>();

        var draws = new List<ThingBatchDraw>((itemCount + bufferItemCapacity - 1) / bufferItemCapacity);
        int remaining = itemCount;
        int startIndex = 0;

        while (remaining > 0)
        {
            int batchCount = Math.Min(bufferItemCapacity, remaining);
            draws.Add(new ThingBatchDraw(
                startIndex,
                batchCount,
                batchCount * VerticesPerThing,
                batchCount * TrianglesPerThing));
            startIndex += batchCount;
            remaining -= batchCount;
        }

        return draws;
    }
}
