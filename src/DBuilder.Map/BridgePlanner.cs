// ABOUTME: Plans UDB BuilderModes bridge geometry from two selected linedef chains.
// ABOUTME: Produces Bezier bridge sector loops and interpolated sector properties without editor UI.

namespace DBuilder.Map;

using DBuilder.Geometry;

public enum BridgeInterpolation
{
    Linear,
    Highest,
    Lowest,
    EaseInSine,
    EaseOutSine,
    EaseInOutSine,
}

public readonly record struct BridgeSectorProperties(
    int FloorHeight,
    int CeilingHeight,
    int Brightness,
    string HighTexture,
    string LowTexture);

public sealed class BridgePlanOptions
{
    public int Subdivisions { get; init; } = 1;
    public BridgeInterpolation FloorMode { get; init; } = BridgeInterpolation.Linear;
    public BridgeInterpolation CeilingMode { get; init; } = BridgeInterpolation.Linear;
    public BridgeInterpolation BrightnessMode { get; init; } = BridgeInterpolation.Linear;
}

public sealed record BridgeShape(IReadOnlyList<Vector2D> Loop, BridgeSectorProperties Properties);

public sealed record BridgePlan(IReadOnlyList<IReadOnlyList<Vector2D>> Curves, IReadOnlyList<BridgeShape> Shapes);

public sealed record BridgeInterpolationOption(string Label, BridgeInterpolation Mode);

public static class BridgePlanner
{
    public const int MinSubdivisions = 0;
    public const int MaxSubdivisions = 32;

    public static IReadOnlyList<BridgeInterpolationOption> FloorInterpolationOptions { get; } =
    [
        new("Linear interpolation", BridgeInterpolation.Linear),
        new("Lowest floor", BridgeInterpolation.Lowest),
        new("EaseInSine interpolation", BridgeInterpolation.EaseInSine),
        new("EaseOutSine interpolation", BridgeInterpolation.EaseOutSine),
        new("EaseInOutSine interpolation", BridgeInterpolation.EaseInOutSine),
    ];

    public static IReadOnlyList<BridgeInterpolationOption> CeilingInterpolationOptions { get; } =
    [
        new("Linear interpolation", BridgeInterpolation.Linear),
        new("Highest ceiling", BridgeInterpolation.Highest),
        new("EaseInSine interpolation", BridgeInterpolation.EaseInSine),
        new("EaseOutSine interpolation", BridgeInterpolation.EaseOutSine),
        new("EaseInOutSine interpolation", BridgeInterpolation.EaseInOutSine),
    ];

    public static IReadOnlyList<BridgeInterpolationOption> BrightnessInterpolationOptions { get; } =
    [
        new("Linear interpolation", BridgeInterpolation.Linear),
        new("Use highest", BridgeInterpolation.Highest),
        new("Use lowest", BridgeInterpolation.Lowest),
    ];

    public static string CreatedStatus(int subdivisions)
        => "Created a Bridge with " + Math.Clamp(subdivisions, MinSubdivisions, MaxSubdivisions) + " subdivisions.";

    public static BridgePlan? TryCreate(IEnumerable<Linedef> linedefs, BridgePlanOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(linedefs);
        options ??= new BridgePlanOptions();

        var lines = linedefs
            .Where(line => line != null && !line.IsDisposed)
            .Select(line => new BridgeLine(line))
            .ToList();
        if (!SetupPointGroups(lines, out Vector2D[] group1, out Vector2D[] group2, out BridgeSectorProperties[] props1, out BridgeSectorProperties[] props2))
            return null;

        int subdivisions = Math.Clamp(options.Subdivisions, MinSubdivisions, MaxSubdivisions);
        double[] relLenGroup1 = RelativeLengths(group1);
        double[] relLenGroup2 = RelativeLengths(group2);
        Vector2D center1 = CurveTools.GetPointOnLine(group1[0], group1[^1], 0.5);
        Vector2D center2 = CurveTools.GetPointOnLine(group2[0], group2[^1], 0.5);
        Vector2D handle1 = group1[0] + HandleLocation(group1[0], group1[^1], center2);
        Vector2D handle2 = group2[0] + HandleLocation(group2[0], group2[^1], center1);
        Vector2D handle3 = group1[^1] + HandleLocation(group1[0], group1[^1], center2);
        Vector2D handle4 = group2[^1] + HandleLocation(group2[0], group2[^1], center1);

        var curves = new List<IReadOnlyList<Vector2D>>();
        for (int i = 0; i < group1.Length; i++)
        {
            Vector2D cp1 = CurveTools.GetPointOnLine(handle1, handle3, relLenGroup1[i]);
            Vector2D cp2 = CurveTools.GetPointOnLine(handle2, handle4, relLenGroup2[i]);
            curves.Add(CurveTools.GetCubicCurve(group1[i], group2[i], cp1, cp2, subdivisions) ?? Array.Empty<Vector2D>());
        }

        var shapes = new List<BridgeShape>();
        for (int i = 1; i < group1.Length; i++)
        {
            for (int c = 1; c <= subdivisions; c++)
            {
                var loop = new[]
                {
                    curves[i - 1][c - 1],
                    curves[i - 1][c],
                    curves[i][c],
                    curves[i][c - 1],
                    curves[i - 1][c - 1],
                };
                shapes.Add(new BridgeShape(loop, InterpolateProperties(props1[i - 1], props2[i - 1], c, subdivisions, options)));
            }
        }

        return new BridgePlan(curves, shapes);
    }

    private static bool SetupPointGroups(
        List<BridgeLine> lines,
        out Vector2D[] group1,
        out Vector2D[] group2,
        out BridgeSectorProperties[] props1,
        out BridgeSectorProperties[] props2)
    {
        group1 = Array.Empty<Vector2D>();
        group2 = Array.Empty<Vector2D>();
        props1 = Array.Empty<BridgeSectorProperties>();
        props2 = Array.Empty<BridgeSectorProperties>();

        for (int i = 0; i < lines.Count; i++)
        {
            BridgeLine current = lines[i];
            for (int c = 0; c < lines.Count; c++)
            {
                if (c == i) continue;
                BridgeLine line = lines[c];

                if (current.Start == line.Start)
                {
                    line.Invert();
                    current.Previous = line;
                }
                else if (current.Start == line.End)
                {
                    current.Previous = line;
                }
                else if (current.End == line.End)
                {
                    line.Invert();
                    current.Next = line;
                }
                else if (current.End == line.Start)
                {
                    current.Next = line;
                }
            }
        }

        var pointGroups = new List<List<Vector2D>>();
        var sortedLines = new List<List<BridgeLine>>();
        foreach (BridgeLine current in lines)
        {
            if (current.Previous != null) continue;

            BridgeLine? line = current;
            var points = new List<Vector2D> { line.Start };
            var sorted = new List<BridgeLine>();
            do
            {
                points.Add(line.End);
                sorted.Add(line);
                line = line.Next;
            } while (line != null);

            pointGroups.Add(points);
            sortedLines.Add(sorted);
        }

        if (pointGroups.Count != 2) return false;
        if (pointGroups[0].Count != pointGroups[1].Count) return false;

        foreach (BridgeLine first in sortedLines[0])
            foreach (BridgeLine second in sortedLines[1])
                if (LinesIntersect(first.Start, first.End, second.Start, second.End))
                    return false;

        props1 = sortedLines[0].Select(line => line.Properties).ToArray();
        props2 = sortedLines[1].Select(line => line.Properties).ToArray();
        if (LinesIntersect(pointGroups[0][0], pointGroups[1][0], pointGroups[0][^1], pointGroups[1][^1]))
        {
            pointGroups[0].Reverse();
            Array.Reverse(props1);
        }

        group1 = pointGroups[0].ToArray();
        group2 = pointGroups[1].ToArray();
        return true;
    }

    private static double[] RelativeLengths(Vector2D[] pointGroup)
    {
        var result = new double[pointGroup.Length];
        result[0] = 0.0;

        double length = Vector2D.Distance(pointGroup[0], pointGroup[^1]);
        if (length == 0.0)
        {
            result[^1] = 1.0;
            return result;
        }

        double angle = Math.Atan2(pointGroup[0].y - pointGroup[^1].y, pointGroup[0].x - pointGroup[^1].x);
        for (int i = 1; i < pointGroup.Length - 1; i++)
        {
            Vector2D p0 = pointGroup[i - 1];
            Vector2D p1 = pointGroup[i];
            double currentAngle = Math.Atan2(p0.y - p1.y, p0.x - p1.x);
            double diff = (angle + Angle2D.PI) - (currentAngle + Angle2D.PI);
            double segmentLength = (int)(Vector2D.Distance(p0, p1) * Math.Cos(diff));
            result[i] = result[i - 1] + segmentLength / length;
        }

        result[^1] = 1.0;
        return result;
    }

    private static Vector2D HandleLocation(Vector2D start, Vector2D end, Vector2D direction)
    {
        double angle = -Math.Atan2(start.y - end.y, start.x - end.x);
        double directionAngle = -Math.Atan2(direction.y - start.y, direction.x - start.x);
        double length = Vector2D.Distance(start, end) * 0.3;
        double diff = (angle + Angle2D.PI) - (directionAngle + Angle2D.PI);

        if (diff > Angle2D.PI || (diff < 0 && diff > -Angle2D.PI)) angle += Angle2D.PI;

        return new Vector2D(Math.Sin(angle) * length, Math.Cos(angle) * length);
    }

    private static BridgeSectorProperties InterpolateProperties(
        BridgeSectorProperties first,
        BridgeSectorProperties second,
        int sectorIndex,
        int subdivisions,
        BridgePlanOptions options)
    {
        double delta = sectorIndex / (double)subdivisions;
        delta += (1.0 - delta) / subdivisions;

        return new BridgeSectorProperties(
            InterpolateValue(first.FloorHeight, second.FloorHeight, delta, options.FloorMode),
            Math.Max(
                InterpolateValue(first.CeilingHeight, second.CeilingHeight, delta, options.CeilingMode),
                InterpolateValue(first.FloorHeight, second.FloorHeight, delta, options.FloorMode) + 8),
            InterpolateValue(first.Brightness, second.Brightness, delta, options.BrightnessMode),
            first.HighTexture != "-" ? first.HighTexture : second.HighTexture,
            first.LowTexture != "-" ? first.LowTexture : second.LowTexture);
    }

    private static int InterpolateValue(int first, int second, double delta, BridgeInterpolation mode)
        => mode switch
        {
            BridgeInterpolation.Highest => Math.Max(first, second),
            BridgeInterpolation.Lowest => Math.Min(first, second),
            BridgeInterpolation.Linear => (int)Math.Round(InterpolationTools.Linear(first, second, delta)),
            BridgeInterpolation.EaseInSine => (int)Math.Round(InterpolationTools.EaseInSine(first, second, delta)),
            BridgeInterpolation.EaseOutSine => (int)Math.Round(InterpolationTools.EaseOutSine(first, second, delta)),
            BridgeInterpolation.EaseInOutSine => (int)Math.Round(InterpolationTools.EaseInOutSine(first, second, delta)),
            _ => throw new NotSupportedException("Unsupported bridge interpolation mode: " + mode),
        };

    private static bool LinesIntersect(Vector2D start1, Vector2D end1, Vector2D start2, Vector2D end2)
    {
        double zn = (end2.y - start2.y) * (end1.x - start1.x) - (end2.x - start2.x) * (end1.y - start1.y);
        double ch1 = (end2.x - start2.x) * (start1.y - start2.y) - (end2.y - start2.y) * (start1.x - start2.x);
        double ch2 = (end1.x - start1.x) * (start1.y - start2.y) - (end1.y - start1.y) * (start1.x - start2.x);

        if (zn == 0.0) return false;
        return ch1 / zn <= 1.0 && ch1 / zn >= 0.0 && ch2 / zn <= 1.0 && ch2 / zn >= 0.0;
    }

    private sealed class BridgeLine
    {
        public Vector2D Start { get; private set; }
        public Vector2D End { get; private set; }
        public BridgeSectorProperties Properties { get; }
        public BridgeLine? Previous { get; set; }
        public BridgeLine? Next { get; set; }

        public BridgeLine(Linedef line)
        {
            Start = new Vector2D((int)line.Start.Position.x, (int)line.Start.Position.y);
            End = new Vector2D((int)line.End.Position.x, (int)line.End.Position.y);
            Properties = GetProperties(line);
        }

        public void Invert()
        {
            (Start, End) = (End, Start);
        }

        private static BridgeSectorProperties GetProperties(Linedef line)
        {
            if (line.Back?.Sector != null)
            {
                return new BridgeSectorProperties(
                    line.Back.Sector.FloorHeight,
                    line.Back.Sector.CeilHeight,
                    line.Back.Sector.Brightness,
                    line.Back.HighTexture != "-" ? line.Back.HighTexture : line.Back.MidTexture,
                    line.Back.LowTexture != "-" ? line.Back.LowTexture : line.Back.MidTexture);
            }

            if (line.Front?.Sector != null)
            {
                return new BridgeSectorProperties(
                    line.Front.Sector.FloorHeight,
                    line.Front.Sector.CeilHeight,
                    line.Front.Sector.Brightness,
                    line.Front.HighTexture != "-" ? line.Front.HighTexture : line.Front.MidTexture,
                    line.Front.LowTexture != "-" ? line.Front.LowTexture : line.Front.MidTexture);
            }

            return new BridgeSectorProperties(0, 128, 192, "-", "-");
        }
    }
}
