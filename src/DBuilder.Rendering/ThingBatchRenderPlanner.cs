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

public readonly record struct ThingBoxLine(
    float StartX,
    float StartY,
    float EndX,
    float EndY,
    int Color);

public readonly record struct ThingBoxRenderPlan(
    FlatVertex[] Vertices,
    IReadOnlyList<ThingBoxLine> BoundingBoxLines);

public readonly record struct ThingBatchSetupPlan(
    Cull CullMode,
    bool DepthEnabled,
    bool AlphaBlendEnabled,
    Blend SourceBlend,
    Blend DestinationBlend,
    bool AlphaTestEnabled,
    bool BindThingTexture,
    bool ResetWorldTransformation,
    ShaderName Shader,
    float Alpha);

public readonly record struct ThingSpriteRenderDecision(
    bool SkipForModelRender,
    bool RenderSprite,
    bool MarkArrowLarge);

public static class ThingBatchRenderPlanner
{
    public const int VerticesPerThing = 6;
    public const int TrianglesPerThing = 2;
    public const int SpriteAngleFrameCount = 8;
    public const int SpriteAngleStep = 45;
    public const int SpriteAngleOffset = 270;
    public const float MinimumSpriteRadius = 8.0f;
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

    public static ThingBatchSetupPlan BuildSetupPlan(float alpha)
    {
        if (float.IsNaN(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha));

        return new ThingBatchSetupPlan(
            CullMode: Cull.None,
            DepthEnabled: false,
            AlphaBlendEnabled: true,
            SourceBlend: Blend.SourceAlpha,
            DestinationBlend: Blend.InverseSourceAlpha,
            AlphaTestEnabled: false,
            BindThingTexture: true,
            ResetWorldTransformation: true,
            Shader: ShaderName.things2d_thing,
            Alpha: alpha);
    }

    public static int SpriteFrameAngleIndex(int angleDoom, int spriteFrameCount)
    {
        if (spriteFrameCount < 0) throw new ArgumentOutOfRangeException(nameof(spriteFrameCount));
        if (spriteFrameCount != SpriteAngleFrameCount) return 0;

        return ClampAngle(-angleDoom + SpriteAngleOffset) / SpriteAngleStep;
    }

    public static ThingSpriteRenderDecision BuildSpriteRenderDecision(
        ThingRenderMode renderMode,
        ModelRenderMode modelRenderMode,
        bool selected,
        float alpha,
        bool forceSpriteRendering,
        double spriteSize)
    {
        if (float.IsNaN(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha));
        if (spriteSize < 0 || double.IsNaN(spriteSize)) throw new ArgumentOutOfRangeException(nameof(spriteSize));

        bool modelOrVoxel = renderMode is ThingRenderMode.MODEL or ThingRenderMode.VOXEL;
        bool skipForModelRender = modelOrVoxel
            && (modelRenderMode == ModelRenderMode.SELECTION && selected
                || modelRenderMode == ModelRenderMode.ACTIVE_THINGS_FILTER && alpha == 1.0f);

        if (skipForModelRender)
            return new ThingSpriteRenderDecision(
                SkipForModelRender: true,
                RenderSprite: false,
                MarkArrowLarge: false);

        bool spriteTooSmall = !forceSpriteRendering && spriteSize < MinimumSpriteRadius;
        return new ThingSpriteRenderDecision(
            SkipForModelRender: false,
            RenderSprite: !spriteTooSmall,
            MarkArrowLarge: spriteTooSmall);
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

    public static FlatVertex[] BuildSpriteVertices(
        double screenX,
        double screenY,
        double width,
        double height,
        int color,
        bool mirror)
    {
        if (double.IsNaN(screenX)) throw new ArgumentOutOfRangeException(nameof(screenX));
        if (double.IsNaN(screenY)) throw new ArgumentOutOfRangeException(nameof(screenY));
        if (width < 0 || double.IsNaN(width)) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 0 || double.IsNaN(height)) throw new ArgumentOutOfRangeException(nameof(height));

        float left = mirror ? 1.0f : 0.0f;
        float right = mirror ? 0.0f : 1.0f;

        var vertices = new FlatVertex[VerticesPerThing];
        vertices[0] = Vertex(screenX - width, screenY - height, color, left, 0.0f);
        vertices[1] = Vertex(screenX + width, screenY - height, color, right, 0.0f);
        vertices[2] = Vertex(screenX - width, screenY + height, color, left, 1.0f);
        vertices[3] = vertices[1];
        vertices[4] = vertices[2];
        vertices[5] = Vertex(screenX + width, screenY + height, color, right, 1.0f);
        return vertices;
    }

    public static ThingBoxRenderPlan BuildBoxPlan(
        double screenX,
        double screenY,
        double circleSize,
        double boundingBoxSize,
        int color,
        int boundingBoxColor)
    {
        if (double.IsNaN(screenX)) throw new ArgumentOutOfRangeException(nameof(screenX));
        if (double.IsNaN(screenY)) throw new ArgumentOutOfRangeException(nameof(screenY));
        if (circleSize < 0 || double.IsNaN(circleSize)) throw new ArgumentOutOfRangeException(nameof(circleSize));
        if (double.IsNaN(boundingBoxSize)) throw new ArgumentOutOfRangeException(nameof(boundingBoxSize));

        var vertices = new FlatVertex[VerticesPerThing];
        vertices[0] = Vertex(screenX - circleSize, screenY - circleSize, color, 0.0f, 0.0f);
        vertices[1] = Vertex(screenX + circleSize, screenY - circleSize, color, 0.5f, 0.0f);
        vertices[2] = Vertex(screenX - circleSize, screenY + circleSize, color, 0.0f, 1.0f);
        vertices[3] = vertices[1];
        vertices[4] = vertices[2];
        vertices[5] = Vertex(screenX + circleSize, screenY + circleSize, color, 0.5f, 1.0f);

        if (boundingBoxSize <= 0)
            return new ThingBoxRenderPlan(vertices, Array.Empty<ThingBoxLine>());

        float left = (float)(screenX - boundingBoxSize);
        float right = (float)(screenX + boundingBoxSize);
        float top = (float)(screenY - boundingBoxSize);
        float bottom = (float)(screenY + boundingBoxSize);
        return new ThingBoxRenderPlan(
            vertices,
            new[]
            {
                new ThingBoxLine(left, top, right, top, boundingBoxColor),
                new ThingBoxLine(right, top, right, bottom, boundingBoxColor),
                new ThingBoxLine(left, bottom, right, bottom, boundingBoxColor),
                new ThingBoxLine(left, top, left, bottom, boundingBoxColor),
            });
    }

    private static FlatVertex Vertex(double x, double y, float u, float v)
        => Vertex(x, y, -1, u, v);

    private static FlatVertex Vertex(double x, double y, int color, float u, float v)
        => new()
        {
            x = (float)x,
            y = (float)y,
            c = color,
            u = u,
            v = v,
        };

    private static int ClampAngle(int angle)
    {
        int result = angle % 360;
        return result < 0 ? result + 360 : result;
    }
}
