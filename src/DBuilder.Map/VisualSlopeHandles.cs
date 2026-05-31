// ABOUTME: Models UDB visual slope handles without depending on renderer resources.
// ABOUTME: Preserves local handle meshes, placement basis, and pivot points for visual slope editing.

using DBuilder.Geometry;

namespace DBuilder.Map;

public enum VisualSlopeHandleKind
{
    Line,
    Vertex,
}

public enum VisualSlopeLevelType
{
    Floor,
    Ceiling,
}

public readonly record struct VisualSlopeHandleVertex(Vector3D Position, uint Color);

public sealed record VisualSlopeHandleMesh(IReadOnlyList<VisualSlopeHandleVertex> Vertices);

public sealed record VisualSlopeLevel(Sector Sector, VisualSlopeLevelType Type, Plane Plane, bool ExtraFloor = false)
{
    public static VisualSlopeLevel Floor(Sector sector)
    {
        if (sector == null) throw new ArgumentNullException(nameof(sector));
        Plane plane = sector.HasFloorSlope
            ? new Plane(sector.FloorSlope.GetNormal(), double.IsNaN(sector.FloorSlopeOffset) ? 0.0 : sector.FloorSlopeOffset)
            : new Plane(new Vector3D(0, 0, 1), -sector.FloorHeight);
        return new VisualSlopeLevel(sector, VisualSlopeLevelType.Floor, plane);
    }

    public static VisualSlopeLevel Ceiling(Sector sector)
    {
        if (sector == null) throw new ArgumentNullException(nameof(sector));
        Plane plane = sector.HasCeilSlope
            ? new Plane(sector.CeilSlope.GetNormal(), double.IsNaN(sector.CeilSlopeOffset) ? 0.0 : sector.CeilSlopeOffset)
            : new Plane(new Vector3D(0, 0, 1), -sector.CeilHeight);
        return new VisualSlopeLevel(sector, VisualSlopeLevelType.Ceiling, plane);
    }
}

public sealed record VisualSlopeHandlePlacement(
    Vector3D Origin,
    Vector3D LineVector,
    Vector3D PerpendicularVector,
    Vector3D Normal,
    double Length);

public sealed record VisualSlopeHandle(
    VisualSlopeHandleKind Kind,
    VisualSlopeLevel Level,
    VisualSlopeHandlePlacement Placement,
    Sidedef? Sidedef = null,
    Vertex? Vertex = null,
    Sector? Sector = null)
{
    public Vector3D GetPivotPoint()
        => Kind == VisualSlopeHandleKind.Line && Sidedef != null
            ? new Vector3D(Sidedef.Line.GetCenterPoint(), Level.Plane.GetZ(Sidedef.Line.GetCenterPoint()))
            : new Vector3D(Vertex!.Position, Level.Plane.GetZ(Vertex.Position));

    public IReadOnlyList<Vector3D> GetPivotPoints()
    {
        if (Kind != VisualSlopeHandleKind.Line || Sidedef == null) return [GetPivotPoint()];

        return
        [
            new Vector3D(Sidedef.Line.Start.Position, Level.Plane.GetZ(Sidedef.Line.Start.Position)),
            new Vector3D(Sidedef.Line.End.Position, Level.Plane.GetZ(Sidedef.Line.End.Position)),
        ];
    }
}

public static class VisualSlopeHandles
{
    public const uint White = 0xffffffff;
    public const uint TransparentWhite = 0x00ffffff;

    public static VisualSlopeHandleMesh LineMesh { get; } = new(
    [
        new(new Vector3D(0.0, -8.0, 0.1), TransparentWhite),
        new(new Vector3D(0.0, 0.0, 0.1), White),
        new(new Vector3D(1.0, 0.0, 0.1), White),
        new(new Vector3D(0.0, -8.0, 0.1), TransparentWhite),
        new(new Vector3D(1.0, 0.0, 0.1), White),
        new(new Vector3D(1.0, -8.0, 0.1), TransparentWhite),
    ]);

    public static VisualSlopeHandleMesh VertexMesh { get; } = new(
    [
        new(new Vector3D(0.0, 0.0, 0.1), White),
        new(new Vector3D(4.0, -8.0, 0.1), TransparentWhite),
        new(new Vector3D(-4.0, -8.0, 0.1), TransparentWhite),
    ]);

    public static VisualSlopeHandle CreateSidedef(Sidedef sidedef, VisualSlopeLevel level, bool up)
    {
        if (sidedef == null) throw new ArgumentNullException(nameof(sidedef));
        Line2D line = GetSidedefBaseLine(sidedef, level, up);
        return new VisualSlopeHandle(
            VisualSlopeHandleKind.Line,
            level,
            CreatePlacement(line, level.Plane),
            Sidedef: sidedef);
    }

    public static VisualSlopeHandle CreateVertex(Vertex vertex, Sector sector, VisualSlopeLevel level)
    {
        if (vertex == null) throw new ArgumentNullException(nameof(vertex));
        if (sector == null) throw new ArgumentNullException(nameof(sector));

        double angle = ComputeVertexAngle(vertex, sector, level.Type);
        Vector2D direction = Vector2D.FromAngle(angle);
        return new VisualSlopeHandle(
            VisualSlopeHandleKind.Vertex,
            level,
            CreatePlacement(new Line2D(vertex.Position, vertex.Position + direction), level.Plane),
            Vertex: vertex,
            Sector: sector);
    }

    public static Line2D GetSidedefBaseLine(Sidedef sidedef, VisualSlopeLevel level, bool up)
    {
        if (sidedef == null) throw new ArgumentNullException(nameof(sidedef));

        bool invertLine = false;
        if (up)
        {
            if (level.ExtraFloor && level.Type == VisualSlopeLevelType.Ceiling)
            {
                if (sidedef.IsFront) invertLine = true;
            }
            else if (!sidedef.IsFront)
            {
                invertLine = true;
            }
        }
        else
        {
            if (level.ExtraFloor && level.Type == VisualSlopeLevelType.Floor)
            {
                if (!sidedef.IsFront) invertLine = true;
            }
            else if (sidedef.IsFront)
            {
                invertLine = true;
            }
        }

        return invertLine
            ? new Line2D(sidedef.Line.End.Position, sidedef.Line.Start.Position)
            : sidedef.Line.Line;
    }

    public static VisualSlopeHandlePlacement CreatePlacement(Line2D line, Plane plane)
    {
        var line3d = new Line3D(new Vector3D(line.v1, plane.GetZ(line.v1)), new Vector3D(line.v2, plane.GetZ(line.v2)));
        Vector3D lineDelta = line3d.GetDelta();
        double length = lineDelta.GetLength();
        Vector3D perpendicularVector = Vector3D.CrossProduct(lineDelta.GetNormal(), plane.Normal) * -1;
        Vector3D lineVector = Vector3D.CrossProduct(plane.Normal, perpendicularVector) * -1;
        Vector3D origin = new(line.v1, plane.GetZ(line.v1));
        return new VisualSlopeHandlePlacement(origin, lineVector, perpendicularVector, plane.Normal, length);
    }

    public static double ComputeVertexAngle(Vertex vertex, Sector sector, VisualSlopeLevelType levelType)
    {
        if (vertex == null) throw new ArgumentNullException(nameof(vertex));
        if (sector == null) throw new ArgumentNullException(nameof(sector));

        var lines = new List<LineAngleInfo>();
        foreach (Linedef line in vertex.Linedefs)
        {
            if (line.IsDisposed) continue;

            bool frontSame = line.Front?.Sector == sector;
            bool backSame = line.Back?.Sector == sector;
            if (frontSame == backSame) continue;

            lines.Add(new LineAngleInfo(line, vertex, sector));
        }

        if (lines.Count < 2)
        {
            if (vertex.Linedefs.Count == 1)
                return Angle2D.Normalized(vertex.Linedefs[0].Angle + Angle2D.PIHALF);

            return 0.0;
        }

        lines.Sort((a, b) => a.Angle.CompareTo(b.Angle));

        int other = lines[0].Clockwise ? 1 : lines.Count - 1;
        Vector2D v1 = Vector2D.FromAngle(lines[0].Angle);
        Vector2D v2 = Vector2D.FromAngle(lines[other].Angle);

        double angle = lines[0].Angle + (Math.Atan2(v2.y, v2.x) - Math.Atan2(v1.y, v1.x)) / 2.0;
        if (lines[0].Clockwise) angle += Angle2D.PI;
        angle += Angle2D.PIHALF;
        if (levelType == VisualSlopeLevelType.Ceiling) angle += Angle2D.PI;
        return Angle2D.Normalized(angle);
    }

    private readonly record struct LineAngleInfo(double Angle, bool Clockwise)
    {
        public LineAngleInfo(Linedef line, Vertex vertex, Sector sector)
            : this(GetAngle(line, vertex), GetClockwise(line, vertex, sector))
        {
        }

        private static double GetAngle(Linedef line, Vertex vertex)
            => ReferenceEquals(line.Start, vertex)
                ? line.Line.GetAngle()
                : new Line2D(line.End.Position, line.Start.Position).GetAngle();

        private static bool GetClockwise(Linedef line, Vertex vertex, Sector sector)
        {
            bool clockwise = ReferenceEquals(line.Start, vertex);
            if (line.Front?.Sector != sector) clockwise = !clockwise;
            return clockwise;
        }
    }
}
