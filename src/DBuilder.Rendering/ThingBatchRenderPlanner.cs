// ABOUTME: Plans UDB-style 2D thing batch upload and draw counts.
// ABOUTME: Keeps Renderer2D thing buffer chunking testable outside the live render device.

using System.Numerics;

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

public readonly record struct ThingBoxLineDrawPlan(
    FlatVertex[] Vertices,
    int PointCount,
    int PrimitiveCount,
    Cull CullMode,
    bool DepthEnabled,
    bool AlphaBlendEnabled,
    bool AlphaTestEnabled,
    bool ResetWorldTransformation,
    ShaderName Shader,
    bool BindWhiteTexture,
    PrimitiveType PrimitiveType);

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

public readonly record struct ThingBatchItemDecision(
    bool SkipHighlighted,
    bool CollectModel,
    byte BoundingBoxAlpha);

public readonly record struct ThingModel2DPassPlan(
    bool RenderModels,
    bool AlphaBlendEnabled,
    FillMode FillMode,
    FillMode RestoreFillMode,
    ShaderName Shader,
    Color4 SelectionColor,
    Color4 WireColor);

public readonly record struct ThingModel2DTransformPlan(
    Matrix4x4 World,
    Matrix4x4 ModelScale,
    Matrix4x4 Rotation,
    Matrix4x4 ViewScale,
    Matrix4x4 Position,
    bool UsesRotationCenter);

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

    public static ThingBatchItemDecision BuildItemDecision(
        ThingRenderMode renderMode,
        bool fixedColor,
        bool highlighted,
        bool selected,
        bool thingsMode,
        float alpha)
    {
        if (float.IsNaN(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha));

        bool skipHighlighted = !fixedColor && highlighted;
        bool collectModel = !skipHighlighted && renderMode is ThingRenderMode.MODEL or ThingRenderMode.VOXEL;
        float alphaScale = !fixedColor && !selected && thingsMode ? 128.0f : 255.0f;
        return new ThingBatchItemDecision(
            skipHighlighted,
            collectModel,
            (byte)(alpha * alphaScale));
    }

    public static ThingModel2DPassPlan BuildModel2DPassPlan(
        ModelRenderMode modelRenderMode,
        float alpha,
        PixelColor currentColor,
        PixelColor highlightColor,
        PixelColor selectionColor,
        PixelColor modelWireColor)
    {
        if (float.IsNaN(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha));

        Color4 selected = selectionColor.ToColorValue();
        Color4 wire = currentColor.ToInt() == highlightColor.ToInt()
            ? highlightColor.ToColorValue()
            : modelWireColor.ToColorValue();
        selected.Alpha = alpha < 1.0f ? alpha * 0.25f : 0.6f;
        wire.Alpha = selected.Alpha;

        return new ThingModel2DPassPlan(
            RenderModels: modelRenderMode != ModelRenderMode.NONE,
            AlphaBlendEnabled: false,
            FillMode: FillMode.Wireframe,
            RestoreFillMode: FillMode.Solid,
            Shader: ShaderName.things2d_fill,
            SelectionColor: selected,
            WireColor: wire);
    }

    public static bool ShouldRenderModel2D(ModelRenderMode modelRenderMode, bool selected, float alpha)
    {
        if (float.IsNaN(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha));

        return modelRenderMode switch
        {
            ModelRenderMode.NONE => false,
            ModelRenderMode.SELECTION => selected,
            ModelRenderMode.ACTIVE_THINGS_FILTER => alpha >= 1.0f,
            ModelRenderMode.ALL => true,
            _ => false,
        };
    }

    public static bool IsModel2DVisible(
        double screenX,
        double screenY,
        double modelRadius,
        double viewScale,
        double thingScaleX,
        double actorScaleWidth,
        double windowWidth,
        double windowHeight)
    {
        if (double.IsNaN(screenX)) throw new ArgumentOutOfRangeException(nameof(screenX));
        if (double.IsNaN(screenY)) throw new ArgumentOutOfRangeException(nameof(screenY));
        if (modelRadius < 0 || double.IsNaN(modelRadius)) throw new ArgumentOutOfRangeException(nameof(modelRadius));
        if (double.IsNaN(viewScale)) throw new ArgumentOutOfRangeException(nameof(viewScale));
        if (double.IsNaN(thingScaleX)) throw new ArgumentOutOfRangeException(nameof(thingScaleX));
        if (double.IsNaN(actorScaleWidth)) throw new ArgumentOutOfRangeException(nameof(actorScaleWidth));
        if (windowWidth < 0 || double.IsNaN(windowWidth)) throw new ArgumentOutOfRangeException(nameof(windowWidth));
        if (windowHeight < 0 || double.IsNaN(windowHeight)) throw new ArgumentOutOfRangeException(nameof(windowHeight));

        double modelScale = viewScale * actorScaleWidth * thingScaleX;
        double screenRadius = modelRadius * modelScale;
        return !(((screenX + screenRadius) <= 0.0)
            || ((screenX - screenRadius) >= windowWidth)
            || ((screenY + screenRadius) <= 0.0)
            || ((screenY - screenRadius) >= windowHeight));
    }

    public static ThingModel2DTransformPlan BuildModel2DTransformPlan(
        Matrix4x4 modeldefTransform,
        Vector3 rotationCenter,
        bool useRotationCenter,
        double screenX,
        double screenY,
        double viewScale,
        double scaleX,
        double scaleY,
        double actorScaleWidth,
        double actorScaleHeight,
        double angleRadians,
        double pitchRadians,
        double rollRadians)
    {
        if (double.IsNaN(screenX)) throw new ArgumentOutOfRangeException(nameof(screenX));
        if (double.IsNaN(screenY)) throw new ArgumentOutOfRangeException(nameof(screenY));
        if (double.IsNaN(viewScale)) throw new ArgumentOutOfRangeException(nameof(viewScale));
        if (double.IsNaN(scaleX)) throw new ArgumentOutOfRangeException(nameof(scaleX));
        if (double.IsNaN(scaleY)) throw new ArgumentOutOfRangeException(nameof(scaleY));
        if (double.IsNaN(actorScaleWidth)) throw new ArgumentOutOfRangeException(nameof(actorScaleWidth));
        if (double.IsNaN(actorScaleHeight)) throw new ArgumentOutOfRangeException(nameof(actorScaleHeight));
        if (double.IsNaN(angleRadians)) throw new ArgumentOutOfRangeException(nameof(angleRadians));
        if (double.IsNaN(pitchRadians)) throw new ArgumentOutOfRangeException(nameof(pitchRadians));
        if (double.IsNaN(rollRadians)) throw new ArgumentOutOfRangeException(nameof(rollRadians));

        double sx = scaleX * actorScaleWidth;
        double sy = scaleY * actorScaleHeight;
        Matrix4x4 modelScale = Matrix4x4.CreateScale((float)sx, (float)sx, (float)sy);
        Matrix4x4 rotation = Matrix4x4.CreateRotationY((float)-rollRadians)
            * Matrix4x4.CreateRotationX((float)-pitchRadians)
            * Matrix4x4.CreateRotationZ((float)angleRadians);
        Matrix4x4 scaledView = Matrix4x4.CreateScale((float)viewScale, (float)-viewScale, 0.0f);
        Matrix4x4 position = Matrix4x4.CreateTranslation((float)screenX, (float)screenY, 0.0f);
        Matrix4x4 world = useRotationCenter
            ? modeldefTransform
                * modelScale
                * Matrix4x4.CreateTranslation(-rotationCenter)
                * rotation
                * Matrix4x4.CreateTranslation(rotationCenter)
                * scaledView
                * position
            : modeldefTransform * modelScale * rotation * scaledView * position;

        return new ThingModel2DTransformPlan(
            world,
            modelScale,
            rotation,
            scaledView,
            position,
            useRotationCenter);
    }

    public static ThingBoxLineDrawPlan BuildBoxLineDrawPlan(
        IReadOnlyList<ThingBoxLine> lines,
        double windowWidth,
        double windowHeight)
    {
        if (lines == null) throw new ArgumentNullException(nameof(lines));
        if (windowWidth < 0 || double.IsNaN(windowWidth)) throw new ArgumentOutOfRangeException(nameof(windowWidth));
        if (windowHeight < 0 || double.IsNaN(windowHeight)) throw new ArgumentOutOfRangeException(nameof(windowHeight));

        var vertices = new List<FlatVertex>(lines.Count * 2);
        foreach (ThingBoxLine line in lines)
        {
            if (!IsBoxLineVisible(line, windowWidth, windowHeight)) continue;

            vertices.Add(Vertex(line.StartX, line.StartY, line.Color, 0.0f, 0.0f));
            vertices.Add(Vertex(line.EndX, line.EndY, line.Color, 0.0f, 0.0f));
        }

        int pointCount = vertices.Count;
        return new ThingBoxLineDrawPlan(
            vertices.ToArray(),
            pointCount,
            pointCount / 2,
            CullMode: Cull.None,
            DepthEnabled: false,
            AlphaBlendEnabled: false,
            AlphaTestEnabled: false,
            ResetWorldTransformation: true,
            Shader: ShaderName.display2d_normal,
            BindWhiteTexture: true,
            PrimitiveType: PrimitiveType.LineList);
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

    private static bool IsBoxLineVisible(ThingBoxLine line, double windowWidth, double windowHeight)
    {
        float maxX = Math.Max(line.StartX, line.EndX);
        float minX = Math.Min(line.StartX, line.EndX);
        float maxY = Math.Max(line.StartY, line.EndY);
        float minY = Math.Min(line.StartY, line.EndY);
        double dx = line.EndX - line.StartX;
        double dy = line.EndY - line.StartY;
        double lengthSquared = dx * dx + dy * dy;

        return lengthSquared >= MinimumSpriteRadius
            && maxX > 0.0f
            && minX < windowWidth
            && maxY > 0.0f
            && minY < windowHeight;
    }

    private static int ClampAngle(int angle)
    {
        int result = angle % 360;
        return result < 0 ? result + 360 : result;
    }
}
