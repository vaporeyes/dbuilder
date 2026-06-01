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

public enum VisualSlopeChangeResult
{
    Changed,
    MissingPivot,
    SameAsPivot,
    VerticalPlane,
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

    public static VisualSlopeChangeResult ChangeTargetHeight(
        VisualSlopeHandle handle,
        VisualSlopeHandle? pivot,
        int amount,
        IReadOnlyList<VisualSlopeLevel>? affectedLevels = null)
    {
        if (handle == null) throw new ArgumentNullException(nameof(handle));
        if (pivot == null) return VisualSlopeChangeResult.MissingPivot;
        if (ReferenceEquals(handle, pivot)) return VisualSlopeChangeResult.SameAsPivot;

        Plane plane = handle.Kind == VisualSlopeHandleKind.Line
            ? CreateLineChangePlane(handle, pivot, amount)
            : CreateVertexChangePlane(handle, pivot, amount);

        if (Math.Abs(plane.a) == 1.0 || Math.Abs(plane.b) == 1.0)
            return VisualSlopeChangeResult.VerticalPlane;

        IReadOnlyList<VisualSlopeLevel> levels = affectedLevels is { Count: > 0 }
            ? affectedLevels
            : [handle.Level];

        foreach (VisualSlopeLevel level in levels)
            ApplySlope(level, plane);

        return VisualSlopeChangeResult.Changed;
    }

    public static VisualSlopeHandle? GetSmartVertexPivot(
        VisualSlopeHandle handle,
        IEnumerable<VisualSlopeHandle> handles,
        bool useOppositeLineHandle = false)
    {
        if (handle == null) throw new ArgumentNullException(nameof(handle));
        if (handles == null) throw new ArgumentNullException(nameof(handles));
        if (handle.Kind != VisualSlopeHandleKind.Vertex)
            throw new ArgumentException("Smart vertex pivot requires a vertex slope handle.", nameof(handle));
        if (handle.Vertex == null || handle.Sector == null)
            throw new ArgumentException("Vertex slope handle requires a vertex and sector.", nameof(handle));

        VisualSlopeHandle[] candidates = handles.Where(candidate => !ReferenceEquals(candidate, handle)).ToArray();
        if (useOppositeLineHandle && handle.Sector.Sidedefs.Count == 3)
        {
            VisualSlopeHandle? opposite = candidates.FirstOrDefault(candidate =>
                candidate.Kind == VisualSlopeHandleKind.Line
                && candidate.Sidedef != null
                && SameLevel(candidate.Level, handle.Level)
                && !ReferenceEquals(candidate.Sidedef.Line.Start, handle.Vertex)
                && !ReferenceEquals(candidate.Sidedef.Line.End, handle.Vertex));
            if (opposite != null) return opposite;
        }

        return candidates
            .Where(candidate =>
                candidate.Kind == VisualSlopeHandleKind.Vertex
                && candidate.Vertex != null
                && ReferenceEquals(candidate.Sector, handle.Sector)
                && SameLevel(candidate.Level, handle.Level))
            .OrderByDescending(candidate => Vector2D.Distance(candidate.Vertex!.Position, handle.Vertex.Position))
            .FirstOrDefault();
    }

    public static IReadOnlyList<VisualSlopeHandle> GetAdjacentVertexSlopeHandles(
        VisualSlopeHandle handle,
        IEnumerable<VisualSlopeHandle> handles)
    {
        if (handle == null) throw new ArgumentNullException(nameof(handle));
        if (handles == null) throw new ArgumentNullException(nameof(handles));
        if (handle.Kind != VisualSlopeHandleKind.Vertex)
            throw new ArgumentException("Adjacent selection requires a vertex slope handle.", nameof(handle));
        if (handle.Vertex == null)
            throw new ArgumentException("Vertex slope handle requires a vertex.", nameof(handle));

        HashSet<Sector> sectors = AdjacentSectors(handle.Vertex);
        double z = Math.Round(handle.Level.Plane.GetZ(handle.Vertex.Position), 5);

        return handles
            .Where(candidate =>
                !ReferenceEquals(candidate, handle)
                && candidate.Kind == VisualSlopeHandleKind.Vertex
                && ReferenceEquals(candidate.Vertex, handle.Vertex)
                && candidate.Sector != null
                && sectors.Contains(candidate.Sector)
                && Math.Round(candidate.Level.Plane.GetZ(handle.Vertex.Position), 5) == z)
            .ToArray();
    }

    public static void ApplySlope(VisualSlopeLevel level, Plane plane)
    {
        bool applyToCeiling = level.ExtraFloor
            ? level.Type == VisualSlopeLevelType.Floor
            : level.Type == VisualSlopeLevelType.Ceiling;

        bool reset = false;
        int height = 0;
        double diff = Math.Abs(Math.Round(plane.d) - plane.d);
        if (plane.Normal.z == 1.0 && diff < 0.000000001)
        {
            reset = true;
            height = -Convert.ToInt32(plane.d);
        }

        if (applyToCeiling)
        {
            if (reset)
            {
                level.Sector.CeilHeight = height;
                level.Sector.CeilSlope = new Vector3D();
                level.Sector.CeilSlopeOffset = double.NaN;
            }
            else
            {
                Plane downPlane = plane.GetInverted();
                level.Sector.CeilSlope = downPlane.Normal;
                level.Sector.CeilSlopeOffset = downPlane.Offset;
            }
        }
        else if (reset)
        {
            level.Sector.FloorHeight = height;
            level.Sector.FloorSlope = new Vector3D();
            level.Sector.FloorSlopeOffset = double.NaN;
        }
        else
        {
            level.Sector.FloorSlope = plane.Normal;
            level.Sector.FloorSlopeOffset = plane.Offset;
        }
    }

    private static Plane CreateLineChangePlane(VisualSlopeHandle handle, VisualSlopeHandle pivot, int amount)
    {
        if (handle.Sidedef == null) throw new ArgumentException("Line slope handle requires a sidedef.", nameof(handle));

        Vector2D start = handle.Sidedef.Line.Start.Position;
        Vector2D end = handle.Sidedef.Line.End.Position;
        Vector3D p1 = new(start, handle.Level.Plane.GetZ(start) + amount);
        Vector3D p2 = new(end, handle.Level.Plane.GetZ(end) + amount);
        Vector3D p3 = pivot.GetPivotPoint();
        return new Plane(p1, p2, p3, true);
    }

    private static Plane CreateVertexChangePlane(VisualSlopeHandle handle, VisualSlopeHandle pivot, int amount)
    {
        if (handle.Vertex == null) throw new ArgumentException("Vertex slope handle requires a vertex.", nameof(handle));

        Vector2D position = handle.Vertex.Position;
        Vector3D p1 = new(position, handle.Level.Plane.GetZ(position) + amount);
        Vector3D p2;
        Vector3D p3;

        if (pivot.Kind == VisualSlopeHandleKind.Vertex)
        {
            p3 = pivot.GetPivotPoint();
            Vector2D perpendicular = new Line2D(position, p3).GetPerpendicular();
            Vector2D second = position + perpendicular;
            p2 = new Vector3D(second, handle.Level.Plane.GetZ(second) + amount);
        }
        else
        {
            IReadOnlyList<Vector3D> pivotPoints = pivot.GetPivotPoints();
            p2 = pivotPoints[0];
            p3 = pivotPoints[1];
        }

        return new Plane(p1, p2, p3, true);
    }

    private static bool SameLevel(VisualSlopeLevel left, VisualSlopeLevel right)
        => ReferenceEquals(left.Sector, right.Sector)
           && left.Type == right.Type
           && left.ExtraFloor == right.ExtraFloor
           && left.Plane.Normal == right.Plane.Normal
           && left.Plane.Offset == right.Plane.Offset;

    private static HashSet<Sector> AdjacentSectors(Vertex vertex)
    {
        var sectors = new HashSet<Sector>(ReferenceEqualityComparer.Instance);
        foreach (Linedef line in vertex.Linedefs)
        {
            if (line.Front?.Sector != null) sectors.Add(line.Front.Sector);
            if (line.Back?.Sector != null) sectors.Add(line.Back.Sector);
        }

        return sectors;
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
