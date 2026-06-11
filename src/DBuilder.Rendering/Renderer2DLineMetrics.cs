// ABOUTME: Models UDB Renderer2D scale-derived line plotting metrics.
// ABOUTME: Keeps 2D line normal and minimum line thresholds testable outside the live renderer.

namespace DBuilder.Rendering;

using DBuilder.Geometry;

public readonly record struct Renderer2DLineMetrics(
    double ScaleInverse,
    double LineNormalSize,
    double MinimumLineLength,
    double MinimumLineNormalLength);

public readonly record struct Renderer2DViewport(
    double X,
    double Y,
    double Width,
    double Height);

public readonly record struct Renderer2DTransformPlan(
    double Scale,
    double ScaleInverse,
    double TranslateX,
    double TranslateY,
    double LineNormalSize,
    double MinimumLineLength,
    double MinimumLineNormalLength,
    int VertexSize,
    Renderer2DViewport Viewport,
    Renderer2DViewport YViewport);

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

public readonly record struct Renderer2DExtraFloorSide(int SectorTag, IReadOnlySet<int> SectorTags);

public readonly record struct Renderer2DExtraFloorLine(
    int Index,
    int Action,
    IReadOnlyList<int> Args,
    Renderer2DExtraFloorSide? Front,
    Renderer2DExtraFloorSide? Back);

public enum Renderer2DPlotSectorOperationKind
{
    PlotLinedef,
    PlotVertex,
}

public readonly record struct Renderer2DPlotSectorSide(int LinedefIndex, int StartVertexIndex, int EndVertexIndex);

public readonly record struct Renderer2DPlotSectorOperation(
    Renderer2DPlotSectorOperationKind Kind,
    int ElementIndex);

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

    public static Renderer2DTransformPlan BuildTransformPlan(
        int windowWidth,
        int windowHeight,
        double offsetX,
        double offsetY,
        double scale,
        double vertexScale2D)
    {
        if (windowWidth < 0) throw new ArgumentOutOfRangeException(nameof(windowWidth));
        if (windowHeight < 0) throw new ArgumentOutOfRangeException(nameof(windowHeight));
        if (double.IsNaN(offsetX)) throw new ArgumentOutOfRangeException(nameof(offsetX));
        if (double.IsNaN(offsetY)) throw new ArgumentOutOfRangeException(nameof(offsetY));

        Renderer2DLineMetrics metrics = Build(scale);
        double translateX = -offsetX + (windowWidth * 0.5) * metrics.ScaleInverse;
        double translateY = -offsetY - (windowHeight * 0.5) * metrics.ScaleInverse;
        Vector2D leftTop = DisplayToMap(new Vector2D(0.0, 0.0), translateX, translateY, metrics.ScaleInverse);
        Vector2D rightBottom = DisplayToMap(new Vector2D(windowWidth, windowHeight), translateX, translateY, metrics.ScaleInverse);

        return new Renderer2DTransformPlan(
            scale,
            metrics.ScaleInverse,
            translateX,
            translateY,
            metrics.LineNormalSize,
            metrics.MinimumLineLength,
            metrics.MinimumLineNormalLength,
            BuildVertexSize(scale, vertexScale2D),
            new Renderer2DViewport(leftTop.x, leftTop.y, rightBottom.x - leftTop.x, rightBottom.y - leftTop.y),
            new Renderer2DViewport(leftTop.x, rightBottom.y, rightBottom.x - leftTop.x, leftTop.y - rightBottom.y));
    }

    public static Vector2D DisplayToMap(Vector2D display, double translateX, double translateY, double scaleInverse)
    {
        if (double.IsNaN(translateX)) throw new ArgumentOutOfRangeException(nameof(translateX));
        if (double.IsNaN(translateY)) throw new ArgumentOutOfRangeException(nameof(translateY));
        if (scaleInverse <= 0 || double.IsNaN(scaleInverse)) throw new ArgumentOutOfRangeException(nameof(scaleInverse));

        return display.GetInvTransformed(-translateX, -translateY, scaleInverse, -scaleInverse);
    }

    public static Vector2D MapToDisplay(Vector2D map, double translateX, double translateY, double scale)
    {
        if (double.IsNaN(translateX)) throw new ArgumentOutOfRangeException(nameof(translateX));
        if (double.IsNaN(translateY)) throw new ArgumentOutOfRangeException(nameof(translateY));
        if (scale <= 0 || double.IsNaN(scale)) throw new ArgumentOutOfRangeException(nameof(scale));

        return map.GetTransformed(translateX, translateY, scale, -scale);
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

    public static HashSet<int> BuildExtraFloorFlaggedLineIndexes(
        IEnumerable<Renderer2DExtraFloorLine> lines,
        bool udmf)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var materialized = lines.ToArray();
        var tags = new HashSet<int>();
        foreach (Renderer2DExtraFloorLine line in materialized)
        {
            if (line.Action != 160 || line.Args.Count < 5) continue;

            int sectorTag = udmf || (line.Args[1] & 8) != 0
                ? line.Args[0]
                : line.Args[0] + (line.Args[4] << 8);
            if (sectorTag != 0) tags.Add(sectorTag);
        }

        var flagged = new HashSet<int>();
        foreach (Renderer2DExtraFloorLine line in materialized)
        {
            if (IsExtraFloorSide(line.Front, tags) || IsExtraFloorSide(line.Back, tags))
                flagged.Add(line.Index);
        }

        return flagged;
    }

    public static IReadOnlyList<Renderer2DPlotSectorOperation> BuildPlotSectorOperations(
        IEnumerable<Renderer2DPlotSectorSide> sides)
    {
        ArgumentNullException.ThrowIfNull(sides);

        var operations = new List<Renderer2DPlotSectorOperation>();
        foreach (Renderer2DPlotSectorSide side in sides)
        {
            operations.Add(new Renderer2DPlotSectorOperation(Renderer2DPlotSectorOperationKind.PlotLinedef, side.LinedefIndex));
            operations.Add(new Renderer2DPlotSectorOperation(Renderer2DPlotSectorOperationKind.PlotVertex, side.StartVertexIndex));
            operations.Add(new Renderer2DPlotSectorOperation(Renderer2DPlotSectorOperationKind.PlotVertex, side.EndVertexIndex));
        }

        return operations;
    }

    private static bool IsExtraFloorSide(Renderer2DExtraFloorSide? side, HashSet<int> tags)
    {
        if (side is null || side.Value.SectorTag == 0) return false;
        return tags.Overlaps(side.Value.SectorTags);
    }

    private static int TransformY(int y, int viewportHeight)
        => viewportHeight - y;
}
