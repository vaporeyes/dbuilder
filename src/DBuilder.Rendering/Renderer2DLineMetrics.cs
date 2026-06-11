// ABOUTME: Models UDB Renderer2D scale-derived line plotting metrics.
// ABOUTME: Keeps 2D line normal and minimum line thresholds testable outside the live renderer.

namespace DBuilder.Rendering;

using DBuilder.Geometry;

public readonly record struct Renderer2DLineMetrics(
    double ScaleInverse,
    double LineNormalSize,
    double MinimumLineLength,
    double MinimumLineNormalLength);

public enum Renderer2DLinedefSegmentKind
{
    Main,
    ThreeDFloor,
    NormalIndicator,
}

public readonly record struct Renderer2DLinedefSegment(
    Renderer2DLinedefSegmentKind Kind,
    int StartX,
    int StartY,
    int EndX,
    int EndY);

public readonly record struct Renderer2DPlotLinePlan(
    bool ShouldDraw,
    int StartX,
    int StartY,
    int EndX,
    int EndY,
    PixelColor Color);

public readonly record struct Renderer2DPlotVertexPlan(
    bool ShouldDraw,
    int X,
    int Y,
    int Size,
    int ColorIndex);

public static class Renderer2DLineMetricPlanner
{
    public const double LineNormalScreenSize = 10.0;
    public const double MinimumLineLengthScale = 0.0625;
    public const double MinimumLineNormalLengthScale = 2.0;
    public const double VertexSizeBaseScale = 1.7;
    public const double VertexSizeRoundBias = 0.5;
    public const int MinVertexSize = 0;
    public const int MaxVertexSize = 4;

    public static Renderer2DLineMetrics Build(double scale)
    {
        if (scale <= 0 || double.IsNaN(scale)) throw new ArgumentOutOfRangeException(nameof(scale));

        double scaleInverse = 1.0 / scale;
        double lineNormalSize = LineNormalScreenSize * scaleInverse;
        return new Renderer2DLineMetrics(
            scaleInverse,
            lineNormalSize,
            lineNormalSize * MinimumLineLengthScale,
            lineNormalSize * MinimumLineNormalLengthScale);
    }

    public static bool ShouldPlotLine(double screenLengthSquared, double lineNormalSize, double lengthScaler = MinimumLineLengthScale)
        => screenLengthSquared >= lineNormalSize * lengthScaler;

    public static Renderer2DPlotLinePlan BuildPlotLinePlan(
        Vector2D start,
        Vector2D end,
        PixelColor color,
        double translateX,
        double translateY,
        double scale,
        int viewportHeight,
        double lengthScaler = MinimumLineLengthScale)
    {
        if (viewportHeight < 0) throw new ArgumentOutOfRangeException(nameof(viewportHeight));
        if (double.IsNaN(lengthScaler)) throw new ArgumentOutOfRangeException(nameof(lengthScaler));

        Renderer2DLineMetrics metrics = Build(scale);
        Vector2D v1 = start.GetTransformed(translateX, translateY, scale, -scale);
        Vector2D v2 = end.GetTransformed(translateX, translateY, scale, -scale);
        if (!ShouldPlotLine((v2 - v1).GetLengthSq(), metrics.LineNormalSize, lengthScaler))
        {
            return new Renderer2DPlotLinePlan(
                ShouldDraw: false,
                StartX: 0,
                StartY: 0,
                EndX: 0,
                EndY: 0,
                color);
        }

        return new Renderer2DPlotLinePlan(
            ShouldDraw: true,
            (int)v1.x,
            TransformY((int)v1.y, viewportHeight),
            (int)v2.x,
            TransformY((int)v2.y, viewportHeight),
            color);
    }

    public static int BuildVertexSize(double scale, double vertexScale2D)
    {
        if (scale <= 0 || double.IsNaN(scale)) throw new ArgumentOutOfRangeException(nameof(scale));
        if (double.IsNaN(vertexScale2D)) throw new ArgumentOutOfRangeException(nameof(vertexScale2D));

        int size = (int)(VertexSizeBaseScale * vertexScale2D * scale + VertexSizeRoundBias);
        return Math.Clamp(size, MinVertexSize, MaxVertexSize);
    }

    public static Renderer2DPlotVertexPlan BuildPlotVertexPlan(
        Vector2D position,
        int colorIndex,
        bool checkMode,
        bool shouldRenderVertices,
        double translateX,
        double translateY,
        double scale,
        int viewportHeight,
        double vertexScale2D)
    {
        if (viewportHeight < 0) throw new ArgumentOutOfRangeException(nameof(viewportHeight));

        int vertexSize = BuildVertexSize(scale, vertexScale2D);
        if (checkMode && !shouldRenderVertices)
        {
            return new Renderer2DPlotVertexPlan(
                ShouldDraw: false,
                X: 0,
                Y: 0,
                vertexSize,
                colorIndex);
        }

        Vector2D transformed = position.GetTransformed(translateX, translateY, scale, -scale);
        return new Renderer2DPlotVertexPlan(
            ShouldDraw: true,
            (int)transformed.x,
            TransformY((int)transformed.y, viewportHeight),
            vertexSize,
            colorIndex);
    }

    public static IReadOnlyList<Renderer2DLinedefSegment> BuildLinedefSegments(
        Vector2D start,
        Vector2D end,
        double translateX,
        double translateY,
        double scale,
        int viewportHeight,
        bool extraFloor = false,
        bool markExtraFloors = false)
    {
        if (viewportHeight < 0) throw new ArgumentOutOfRangeException(nameof(viewportHeight));
        Renderer2DLineMetrics metrics = Build(scale);
        Vector2D v1 = start.GetTransformed(translateX, translateY, scale, -scale);
        Vector2D v2 = end.GetTransformed(translateX, translateY, scale, -scale);
        double screenLengthSq = (v2 - v1).GetLengthSq();
        if (screenLengthSq < metrics.MinimumLineLength) return Array.Empty<Renderer2DLinedefSegment>();

        var segments = new List<Renderer2DLinedefSegment>
        {
            new(
                extraFloor && markExtraFloors ? Renderer2DLinedefSegmentKind.ThreeDFloor : Renderer2DLinedefSegmentKind.Main,
                (int)v1.x,
                TransformY((int)v1.y, viewportHeight),
                (int)v2.x,
                TransformY((int)v2.y, viewportHeight)),
        };

        if (screenLengthSq < metrics.MinimumLineNormalLength) return segments;

        double mapLength = (end - start).GetLength();
        if (mapLength <= 0) return segments;

        double lengthInv = 1.0 / mapLength;
        double mx = (v2.x - v1.x) * 0.5;
        double my = (v2.y - v1.y) * 0.5;
        segments.Add(new Renderer2DLinedefSegment(
            Renderer2DLinedefSegmentKind.NormalIndicator,
            (int)(v1.x + mx),
            TransformY((int)(v1.y + my), viewportHeight),
            (int)((v1.x + mx) - (my * lengthInv) * metrics.LineNormalSize),
            TransformY((int)((v1.y + my) + (mx * lengthInv) * metrics.LineNormalSize), viewportHeight)));

        return segments;
    }

    private static int TransformY(int y, int viewportHeight)
        => viewportHeight - y;
}
