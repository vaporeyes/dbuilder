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

public static class Renderer2DLineMetricPlanner
{
    public const double LineNormalScreenSize = 10.0;
    public const double MinimumLineLengthScale = 0.0625;
    public const double MinimumLineNormalLengthScale = 2.0;

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
