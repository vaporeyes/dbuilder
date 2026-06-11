// ABOUTME: Ports UDB BuilderModes CurveLinedefsMode point generation and application defaults.
// ABOUTME: Provides UI-independent selected-linedef curve materialization for editor commands.

using DBuilder.Geometry;

namespace DBuilder.Map;

public sealed record CurveLinedefsOptions(
    int Vertices = CurveLinedefsOptions.DefaultVertices,
    int Distance = CurveLinedefsOptions.DefaultDistance,
    int Angle = CurveLinedefsOptions.DefaultAngle,
    bool FixedCurve = false,
    bool FixedCurveOutwards = true)
{
    public const int DefaultVertices = 8;
    public const int DefaultDistance = 128;
    public const int DefaultAngle = 180;
    public const int MinVertices = 1;
    public const int MaxVertices = 200;
    public const int MinDistance = -10000;
    public const int MaxDistance = 10000;
    public const int MinAngle = 0;
    public const int MaxAngle = 350;
    public const string VerticesKey = "curvelinedefsmode.vertices";
    public const string DistanceKey = "curvelinedefsmode.distance";
    public const string AngleKey = "curvelinedefsmode.angle";
    public const string FixedCurveKey = "curvelinedefsmode.fixedcurve";
    public const string FixedCurveOutwardsKey = "curvelinedefsmode.fixedcurveoutwards";

    public static CurveLinedefsOptions FromDictionary(IReadOnlyDictionary<string, object?> settings)
        => new CurveLinedefsOptions(
            DrawLineModeSettings.ReadInt(settings, VerticesKey, DefaultVertices),
            DrawLineModeSettings.ReadInt(settings, DistanceKey, DefaultDistance),
            DrawLineModeSettings.ReadInt(settings, AngleKey, DefaultAngle),
            DrawLineModeSettings.ReadBool(settings, FixedCurveKey, false),
            DrawLineModeSettings.ReadBool(settings, FixedCurveOutwardsKey, true))
            .Normalized();

    public void WriteTo(IDictionary<string, object?> settings)
    {
        CurveLinedefsOptions options = Normalized();
        settings[VerticesKey] = options.Vertices;
        settings[DistanceKey] = options.Distance;
        settings[AngleKey] = options.Angle;
        settings[FixedCurveKey] = options.FixedCurve;
        settings[FixedCurveOutwardsKey] = options.FixedCurveOutwards;
    }

    public static CurveLinedefsOptions Defaults()
        => new();

    public CurveLinedefsOptions Reset()
        => Defaults();

    public CurveLinedefsOptions Flipped()
    {
        CurveLinedefsOptions options = Normalized();
        return options.FixedCurve
            ? options with { FixedCurveOutwards = !options.FixedCurveOutwards }
            : options with { Distance = -options.Distance };
    }

    public CurveLinedefsOptions Normalized()
        => this with
        {
            Vertices = Math.Clamp(Vertices, MinVertices, MaxVertices),
            Distance = Math.Clamp(Distance, MinDistance, MaxDistance),
            Angle = Math.Clamp(Angle, MinAngle, MaxAngle),
        };

    public CurveLinedefsOptions NormalizedFor(Linedef line)
    {
        if (line == null) throw new ArgumentNullException(nameof(line));
        CurveLinedefsOptions options = Normalized();
        int maxVertices = Math.Max(0, (int)Math.Ceiling(line.Length / 4.0));
        return options with
        {
            Vertices = Math.Clamp(options.Vertices, 0, maxVertices),
        };
    }
}

public sealed record CurveLinedefsResult(int CurvedLinedefs, int InsertedVertices);

public static class CurveLinedefs
{
    public static IReadOnlyList<Vector2D> GenerateCurvePoints(Linedef line, CurveLinedefsOptions? options = null)
    {
        if (line == null) throw new ArgumentNullException(nameof(line));
        CurveLinedefsOptions normalized = (options ?? new CurveLinedefsOptions()).NormalizedFor(line);
        int vertices = normalized.Vertices;
        if (vertices <= 0) return Array.Empty<Vector2D>();

        bool fixedCurve = normalized.FixedCurve;
        int distance = normalized.Distance;
        int angle = !fixedCurve && distance == 0 ? Math.Max(5, normalized.Angle) : normalized.Angle;
        double theta = Angle2D.DegToRad(angle);
        if ((!fixedCurve && distance < 0) || (fixedCurve && normalized.FixedCurveOutwards)) theta = -theta;

        var points = new List<Vector2D>(vertices);
        double segDelta = 1.0 / (vertices + 1);
        Vector2D lineCenter = line.GetCenterPoint();

        if (angle == 0)
        {
            for (int v = 1; v <= vertices; v++)
            {
                double x = line.Length * segDelta * (vertices - v + 1) - line.Length * 0.5;
                Vector2D vertex = new Vector2D(x, 0).GetRotated(line.Angle + Angle2D.PIHALF) + lineCenter;
                points.Add(vertex);
            }
            return points;
        }

        double c = line.Length;
        double d = (c / Math.Tan(theta / 2.0)) / 2.0;
        double r = d / Math.Cos(theta / 2.0);
        double h = r - d;
        double yDeform = fixedCurve ? 1.0 : distance / h;
        double xDelta = Math.Min(1.0, yDeform);

        for (int v = 1; v <= vertices; v++)
        {
            double a = (Angle2D.PI - theta) / 2.0 + v * (theta / (vertices + 1));
            double xr = Math.Cos(a) * r;
            double xl = line.Length * segDelta * (vertices - v + 1) - line.Length * 0.5;
            double x = InterpolationTools.Linear(xl, xr, xDelta);
            double y = (Math.Sin(a) * r - d) * yDeform;
            Vector2D vertex = new Vector2D(x, y).GetRotated(line.Angle + Angle2D.PIHALF) + lineCenter;
            points.Add(vertex);
        }

        return points;
    }

    public static CurveLinedefsResult ApplyToSelectedLinedefs(
        MapSet map,
        CurveLinedefsOptions? options = null,
        MergeGeometryMode? mergeMode = null,
        bool snapToAccuracy = false)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));

        var selected = map.GetSelectedLinedefs();
        int curved = 0;
        int inserted = 0;
        map.ClearAllMarks(false);
        foreach (Linedef line in selected)
        {
            if (!map.Linedefs.Contains(line)) continue;
            IReadOnlyList<Vector2D> points = GenerateCurvePoints(line, options);
            if (points.Count == 0) continue;

            Linedef splitLine = line;
            MarkSplitLine(splitLine);
            foreach (Vector2D point in points)
            {
                Vertex vertex = map.AddVertex(point);
                splitLine = map.SplitLinedefAt(splitLine, vertex);
                MarkSplitLine(splitLine);
                inserted++;
            }
            curved++;
        }

        if (curved > 0 && (mergeMode.HasValue || snapToAccuracy))
        {
            map.BuildIndexes();
            if (mergeMode.HasValue) map.StitchGeometry(mergeMode.Value);
            if (snapToAccuracy) map.SnapAllToAccuracy();
            map.BuildIndexes();
        }

        return new CurveLinedefsResult(curved, inserted);
    }

    private static void MarkSplitLine(Linedef line)
    {
        line.Marked = true;
        line.Start.Marked = true;
        line.End.Marked = true;
    }
}
