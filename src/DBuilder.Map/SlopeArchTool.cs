// ABOUTME: Applies UDB BuilderModes slope-arch planes to map sectors.
// ABOUTME: Ports SlopeArcher's arc-height calculation without depending on visual-mode UI handles.

namespace DBuilder.Map;

using DBuilder.Geometry;

public sealed class SlopeArchOptions
{
    public double Theta { get; init; }
    public double OffsetAngle { get; init; }
    public double Scale { get; init; } = 1.0;
    public int BaseHeight { get; init; }
    public double HeightOffset { get; init; }
    public bool ApplyToCeiling { get; init; }
}

public static class SlopeArchTool
{
    public static string ApplyStatusText(int sectorCount)
        => sectorCount == 0
            ? "No sectors slope-arched."
            : $"Applied floor slope arch to {CountLabel(sectorCount, "sector")}.";

    public static int Apply(IEnumerable<Sector> sectors, Vector2D handle1, Vector2D handle2, SlopeArchOptions options)
    {
        ArgumentNullException.ThrowIfNull(sectors);
        ArgumentNullException.ThrowIfNull(options);

        var handleLine = new Line2D(handle1, handle2);
        double length = handleLine.GetLength();
        if (length <= 0.0) return 0;

        double left = Math.Cos(options.Theta + options.OffsetAngle);
        double middle = Math.Cos(options.OffsetAngle);
        double denominator = middle - left;
        if (Math.Abs(denominator) < 1e-9) return 0;

        double radius = length / denominator;
        double sectionStart = Math.Cos(options.OffsetAngle + options.Theta) * radius;
        double baseHeightOffset = HeightAt(radius, sectionStart, options.Scale);
        if (double.IsNaN(baseHeightOffset)) baseHeightOffset = 0.0;

        int count = 0;
        foreach (Sector sector in sectors.Where(sector => sector != null && !sector.IsDisposed))
        {
            if (!TryGetProjectionRange(sector, handleLine, out double u1, out double u2)) continue;
            if (Math.Abs(u2 - u1) < 1e-9) continue;

            double x1 = sectionStart + u1 * length;
            double x2 = sectionStart + u2 * length;
            double height1 = HeightAt(radius, x1, options.Scale);
            double height2 = HeightAt(radius, x2, options.Scale);
            if (double.IsNaN(height1)) height1 = 0.0;
            if (double.IsNaN(height2)) height2 = 0.0;

            height1 = height1 - baseHeightOffset + options.BaseHeight + options.HeightOffset;
            height2 = height2 - baseHeightOffset + options.BaseHeight + options.HeightOffset;

            double slopeAngle = Vector2D.GetAngle(new Vector2D(x1, height1), new Vector2D(x2, height2));
            var plane = new Plane(new Vector3D(handleLine.GetCoordinatesAt(u1), height1), handleLine.GetAngle() + Angle2D.PIHALF, slopeAngle, up: true);

            ApplyPlane(sector, plane, options.ApplyToCeiling);
            count++;
        }

        return count;
    }

    private static double HeightAt(double radius, double x, double scale)
        => Math.Sqrt(radius * radius - x * x) * scale;

    private static bool TryGetProjectionRange(Sector sector, Line2D handleLine, out double u1, out double u2)
    {
        u1 = 1.0;
        u2 = 0.0;
        bool found = false;

        foreach (Vertex vertex in Vertices(sector))
        {
            double u = handleLine.GetNearestOnLine(vertex.Position);
            if (u < u1) u1 = u;
            if (u > u2) u2 = u;
            found = true;
        }

        return found;
    }

    private static IEnumerable<Vertex> Vertices(Sector sector)
    {
        var seen = new HashSet<Vertex>(ReferenceEqualityComparer.Instance);
        foreach (Sidedef side in sector.Sidedefs)
        {
            if (side.Line == null) continue;
            if (seen.Add(side.Line.Start)) yield return side.Line.Start;
            if (seen.Add(side.Line.End)) yield return side.Line.End;
        }
    }

    private static void ApplyPlane(Sector sector, Plane plane, bool ceiling)
    {
        double diff = Math.Abs(Math.Round(plane.d) - plane.d);
        if (plane.Normal.z == 1.0 && diff < 0.000000001)
        {
            int height = -Convert.ToInt32(plane.d);
            if (ceiling)
            {
                sector.CeilHeight = height;
                sector.CeilSlope = new Vector3D();
                sector.CeilSlopeOffset = double.NaN;
            }
            else
            {
                sector.FloorHeight = height;
                sector.FloorSlope = new Vector3D();
                sector.FloorSlopeOffset = double.NaN;
            }

            return;
        }

        if (ceiling)
        {
            Plane downPlane = plane.GetInverted();
            sector.CeilSlope = downPlane.Normal;
            sector.CeilSlopeOffset = downPlane.Offset;
        }
        else
        {
            sector.FloorSlope = plane.Normal;
            sector.FloorSlopeOffset = plane.Offset;
        }
    }

    private static string CountLabel(int count, string noun)
        => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
