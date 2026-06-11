// ABOUTME: Plans UDB-style 2D thing batch upload and draw counts.
// ABOUTME: Keeps Renderer2D thing buffer chunking testable outside the live render device.

namespace DBuilder.Rendering;

public readonly record struct ThingBatchDraw(
    int StartIndex,
    int ItemCount,
    int VertexCount,
    int TriangleCount);

public readonly record struct ThingArrowTextureBounds(
    float Left,
    float Right,
    float Top,
    float Bottom);

public static class ThingBatchRenderPlanner
{
    public const int VerticesPerThing = 6;
    public const int TrianglesPerThing = 2;
    public const float FullArrowTextureLeft = 0.501f;
    public const float FullArrowTextureRight = 0.999f;
    public const float FullArrowTextureTop = 0.001f;
    public const float FullArrowTextureBottom = 0.999f;
    public const float LargeArrowTextureLeft = 0.625f;
    public const float LargeArrowTextureRight = 0.874f;
    public const float LargeArrowTextureTop = -0.039f;
    public const float LargeArrowTextureBottom = 0.46f;

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

    public static ThingArrowTextureBounds ArrowTextureBounds(bool spriteSkipped)
        => spriteSkipped
            ? new ThingArrowTextureBounds(
                LargeArrowTextureLeft,
                LargeArrowTextureRight,
                LargeArrowTextureTop,
                LargeArrowTextureBottom)
            : new ThingArrowTextureBounds(
                FullArrowTextureLeft,
                FullArrowTextureRight,
                FullArrowTextureTop,
                FullArrowTextureBottom);

    public static FlatVertex[] BuildArrowVertices(
        double screenX,
        double screenY,
        double angleRadians,
        double arrowSize,
        bool spriteSkipped)
    {
        if (double.IsNaN(screenX)) throw new ArgumentOutOfRangeException(nameof(screenX));
        if (double.IsNaN(screenY)) throw new ArgumentOutOfRangeException(nameof(screenY));
        if (double.IsNaN(angleRadians)) throw new ArgumentOutOfRangeException(nameof(angleRadians));
        if (arrowSize < 0 || double.IsNaN(arrowSize)) throw new ArgumentOutOfRangeException(nameof(arrowSize));

        double sinArrowSize = Math.Sin(angleRadians + Math.PI * 0.25) * arrowSize;
        double cosArrowSize = Math.Cos(angleRadians + Math.PI * 0.25) * arrowSize;
        ThingArrowTextureBounds uv = ArrowTextureBounds(spriteSkipped);

        var vertices = new FlatVertex[VerticesPerThing];
        vertices[0] = Vertex(screenX + sinArrowSize, screenY + cosArrowSize, uv.Left, uv.Top);
        vertices[1] = Vertex(screenX - cosArrowSize, screenY + sinArrowSize, uv.Right, uv.Top);
        vertices[2] = Vertex(screenX + cosArrowSize, screenY - sinArrowSize, uv.Left, uv.Bottom);
        vertices[3] = vertices[1];
        vertices[4] = vertices[2];
        vertices[5] = Vertex(screenX - sinArrowSize, screenY - cosArrowSize, uv.Right, uv.Bottom);
        return vertices;
    }

    private static FlatVertex Vertex(double x, double y, float u, float v)
        => new()
        {
            x = (float)x,
            y = (float)y,
            c = -1,
            u = u,
            v = v,
        };
}
