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
    Sector? Sector = null,
    bool Selected = false,
    bool Pivot = false,
    bool SmartPivot = false)
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

public sealed record VisualSlopeHandleStateResult(
    IReadOnlyList<VisualSlopeHandle> Handles,
    string? WarningMessage = null);

public sealed record VisualSlopeTargetStateResult(
    IReadOnlyList<VisualSlopeHandle> Handles,
    IReadOnlyList<VisualSlopeHandle> UsedHandles,
    VisualSlopeHandle? PickedHandle = null,
    VisualSlopeHandle? SmartPivotHandle = null);

public enum VisualSlopeChangeResult
{
    Changed,
    MissingPivot,
    SameAsPivot,
    VerticalPlane,
}

public enum VisualSlopeBetweenHandlesResult
{
    Changed,
    MissingSelectedLevels,
    MissingHandlePair,
    UnsupportedHandleKind,
}

public sealed record VisualSlopeBetweenHandlesApplyResult(
    VisualSlopeBetweenHandlesResult Result,
    int ChangedLevels,
    string StatusMessage);

public static class VisualSlopeHandles
{
    public const uint White = 0xffffffff;
    public const uint TransparentWhite = 0x00ffffff;
    public const string MissingSelectedLevelsMessage = "You need to select floors or ceilings to slope between slope handles.";
    public const string MissingArchSelectedLevelsMessage = "You need to select at least two floors and ceilings to slope arch between slope handles.";
    public const string MissingHandlePairMessage = "You need to select exactly two slope handles.";
    public const string UnsupportedHandleKindMessage = "Slope between handles requires sidedef slope handles.";
    public const string CannotSelectPivotMessage = "It is not allowed to mark pivot slope handles as selected.";
    public const string CannotPivotSelectedMessage = "It is not allowed to mark selected slope handles as pivot slope handles.";

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

    public static VisualSlopeHandleStateResult ToggleSelection(
        VisualSlopeHandle target,
        IEnumerable<VisualSlopeHandle> handles)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (handles == null) throw new ArgumentNullException(nameof(handles));

        VisualSlopeHandle[] all = handles.ToArray();
        if (target.Pivot)
            return new VisualSlopeHandleStateResult(all, CannotSelectPivotMessage);

        return new VisualSlopeHandleStateResult(
            all.Select(handle => ReferenceEquals(handle, target) ? handle with { Selected = !handle.Selected } : handle).ToArray());
    }

    public static VisualSlopeHandleStateResult TogglePivot(
        VisualSlopeHandle target,
        IEnumerable<VisualSlopeHandle> handles)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (handles == null) throw new ArgumentNullException(nameof(handles));

        bool warning = target.Selected;
        return new VisualSlopeHandleStateResult(
            handles.Select(handle =>
            {
                if (ReferenceEquals(handle, target))
                    return warning ? handle : handle with { Pivot = !handle.Pivot };

                return handle with { Pivot = false };
            }).ToArray(),
            warning ? CannotPivotSelectedMessage : null);
    }

    public static IReadOnlyList<VisualSlopeHandle> GetUsedHandles(IEnumerable<VisualSlopeHandle> handles)
    {
        if (handles == null) throw new ArgumentNullException(nameof(handles));

        return handles.Where(handle => handle.Selected || handle.Pivot || handle.SmartPivot).ToArray();
    }

    public static VisualSlopeTargetStateResult UpdateTarget(
        VisualSlopeHandle? oldTarget,
        VisualSlopeHandle? newTarget,
        IEnumerable<VisualSlopeHandle> handles,
        bool useOppositeSmartPivotHandle = false,
        IEnumerable<VisualSlopeLevel>? selectedLevels = null)
    {
        if (handles == null) throw new ArgumentNullException(nameof(handles));

        VisualSlopeHandle[] original = handles.ToArray();
        bool clearOldSlopeState = oldTarget != null;
        VisualSlopeHandle[] updated = original
            .Select(handle => clearOldSlopeState && handle.SmartPivot ? handle with { SmartPivot = false } : handle)
            .ToArray();
        VisualSlopeHandle? pickedHandle = FindMappedHandle(original, updated, newTarget);
        VisualSlopeHandle? smartPivotHandle = null;

        if (pickedHandle != null)
        {
            smartPivotHandle = pickedHandle.Kind == VisualSlopeHandleKind.Vertex
                ? GetSmartVertexPivot(pickedHandle, updated, useOppositeSmartPivotHandle, selectedLevels)
                : GetSmartSidedefPivot(pickedHandle, updated, useOppositeSmartPivotHandle, selectedLevels);

            if (smartPivotHandle != null)
            {
                int index = Array.FindIndex(updated, handle => ReferenceEquals(handle, smartPivotHandle));
                if (index >= 0)
                {
                    updated[index] = smartPivotHandle with { SmartPivot = true };
                    smartPivotHandle = updated[index];
                }
            }
        }

        var used = new List<VisualSlopeHandle>();
        foreach (VisualSlopeHandle handle in updated)
        {
            if (handle.Selected || handle.Pivot || handle.SmartPivot || ReferenceEquals(handle, pickedHandle))
                used.Add(handle);
        }

        return new VisualSlopeTargetStateResult(updated, used, pickedHandle, smartPivotHandle);
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

        IReadOnlyList<VisualSlopeLevel> levels = AffectedChangeLevels(handle.Level, affectedLevels);

        foreach (VisualSlopeLevel level in levels)
            ApplySlope(level, plane);

        return VisualSlopeChangeResult.Changed;
    }

    public static VisualSlopeBetweenHandlesApplyResult ApplySlopeBetweenHandles(
        IEnumerable<VisualSlopeLevel> selectedLevels,
        IReadOnlyList<VisualSlopeHandle> handles)
    {
        VisualSlopeLevel[] levels = SelectedLevels(selectedLevels);
        if (levels.Length == 0)
            return new VisualSlopeBetweenHandlesApplyResult(
                VisualSlopeBetweenHandlesResult.MissingSelectedLevels,
                0,
                MissingSelectedLevelsMessage);

        if (handles == null || handles.Count != 2)
            return new VisualSlopeBetweenHandlesApplyResult(
                VisualSlopeBetweenHandlesResult.MissingHandlePair,
                0,
                MissingHandlePairMessage);

        Sidedef? firstSide = handles[0].Sidedef;
        Sidedef? secondSide = handles[1].Sidedef;
        if (handles[0].Kind != VisualSlopeHandleKind.Line || handles[1].Kind != VisualSlopeHandleKind.Line ||
            firstSide == null || secondSide == null)
            return new VisualSlopeBetweenHandlesApplyResult(
                VisualSlopeBetweenHandlesResult.UnsupportedHandleKind,
                0,
                UnsupportedHandleKindMessage);

        Linedef firstLine = firstSide.Line;
        Vector2D start = firstLine.Start.Position;
        Vector2D end = firstLine.End.Position;
        Vector2D secondCenter = secondSide.Line.GetCenterPoint();
        var plane = new Plane(
            new Vector3D(start, handles[0].Level.Plane.GetZ(start)),
            new Vector3D(end, handles[0].Level.Plane.GetZ(end)),
            new Vector3D(secondCenter, handles[1].Level.Plane.GetZ(secondCenter)),
            true);

        foreach (VisualSlopeLevel level in levels)
            ApplySlope(level, plane);

        return new VisualSlopeBetweenHandlesApplyResult(
            VisualSlopeBetweenHandlesResult.Changed,
            levels.Length,
            "Sloped between slope handles.");
    }

    public static VisualSlopeBetweenHandlesApplyResult ApplyArchBetweenHandles(
        IEnumerable<VisualSlopeLevel> selectedLevels,
        IReadOnlyList<VisualSlopeHandle> handles,
        double scale = 1.0,
        double heightOffset = 0.0)
    {
        VisualSlopeLevel[] levels = SelectedLevels(selectedLevels);
        if (levels.Length < 2)
            return new VisualSlopeBetweenHandlesApplyResult(
                VisualSlopeBetweenHandlesResult.MissingSelectedLevels,
                0,
                MissingArchSelectedLevelsMessage);

        if (handles == null || handles.Count != 2)
            return new VisualSlopeBetweenHandlesApplyResult(
                VisualSlopeBetweenHandlesResult.MissingHandlePair,
                0,
                MissingHandlePairMessage);

        Sidedef? firstSide = handles[0].Sidedef;
        Sidedef? secondSide = handles[1].Sidedef;
        if (handles[0].Kind != VisualSlopeHandleKind.Line || handles[1].Kind != VisualSlopeHandleKind.Line ||
            firstSide == null || secondSide == null)
            return new VisualSlopeBetweenHandlesApplyResult(
                VisualSlopeBetweenHandlesResult.UnsupportedHandleKind,
                0,
                UnsupportedHandleKindMessage);

        Vector3D p1 = handles[0].GetPivotPoint();
        Vector3D p2 = handles[1].GetPivotPoint();
        double lineLength = Line2D.GetLength(p2.x - p1.x, p2.y - p1.y);
        if (lineLength <= 0.0)
            return new VisualSlopeBetweenHandlesApplyResult(
                VisualSlopeBetweenHandlesResult.UnsupportedHandleKind,
                0,
                UnsupportedHandleKindMessage);

        double zDiff = Math.Abs(p1.z - p2.z);
        double theta;
        double offsetAngle;
        if (zDiff == 0.0)
        {
            theta = Math.PI;
            offsetAngle = 0.0;
        }
        else
        {
            theta = Math.Atan(zDiff / lineLength) * 2;
            offsetAngle = Math.PI / 2.0;
            if (p2.z < p1.z) offsetAngle -= theta;
        }

        int baseHeight = BaseHeightForArch(handles[0].Level);
        int changed = 0;
        foreach (IGrouping<(VisualSlopeLevelType Type, bool ExtraFloor), VisualSlopeLevel> group in levels.GroupBy(level => (level.Type, level.ExtraFloor)))
        {
            bool ceiling = group.Key.ExtraFloor
                ? group.Key.Type == VisualSlopeLevelType.Floor
                : group.Key.Type == VisualSlopeLevelType.Ceiling;
            var options = new SlopeArchOptions
            {
                Theta = theta,
                OffsetAngle = offsetAngle,
                Scale = scale,
                BaseHeight = baseHeight,
                HeightOffset = heightOffset,
                ApplyToCeiling = ceiling,
            };

            changed += SlopeArchTool.Apply(group.Select(level => level.Sector), new Vector2D(p1.x, p1.y), new Vector2D(p2.x, p2.y), options);
        }

        return new VisualSlopeBetweenHandlesApplyResult(
            VisualSlopeBetweenHandlesResult.Changed,
            changed,
            "Arched between slope handles.");
    }

    public static VisualSlopeHandle? GetSmartVertexPivot(
        VisualSlopeHandle handle,
        IEnumerable<VisualSlopeHandle> handles,
        bool useOppositeLineHandle = false,
        IEnumerable<VisualSlopeLevel>? selectedLevels = null)
    {
        if (handle == null) throw new ArgumentNullException(nameof(handle));
        if (handles == null) throw new ArgumentNullException(nameof(handles));
        if (handle.Kind != VisualSlopeHandleKind.Vertex)
            throw new ArgumentException("Smart vertex pivot requires a vertex slope handle.", nameof(handle));
        if (handle.Vertex == null || handle.Sector == null)
            throw new ArgumentException("Vertex slope handle requires a vertex and sector.", nameof(handle));

        VisualSlopeLevel[] selected = SelectedPivotLevels(selectedLevels);
        VisualSlopeHandle[] candidates = handles.Where(candidate => !ReferenceEquals(candidate, handle)).ToArray();
        if (selected.Length == 0 && useOppositeLineHandle && handle.Sector.Sidedefs.Count == 3)
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
                && (selected.Length == 0
                    ? ReferenceEquals(candidate.Sector, handle.Sector) && SameLevel(candidate.Level, handle.Level)
                    : selected.Any(level => SameLevel(candidate.Level, level))))
            .OrderByDescending(candidate => Vector2D.Distance(candidate.Vertex!.Position, handle.Vertex.Position))
            .FirstOrDefault();
    }

    public static VisualSlopeHandle? GetSmartSidedefPivot(
        VisualSlopeHandle handle,
        IEnumerable<VisualSlopeHandle> handles,
        bool useOppositeVertexHandle = false,
        IEnumerable<VisualSlopeLevel>? selectedLevels = null)
    {
        if (handle == null) throw new ArgumentNullException(nameof(handle));
        if (handles == null) throw new ArgumentNullException(nameof(handles));
        if (handle.Kind != VisualSlopeHandleKind.Line)
            throw new ArgumentException("Smart sidedef pivot requires a sidedef slope handle.", nameof(handle));
        Sidedef side = handle.Sidedef ?? throw new ArgumentException("Line slope handle requires a sidedef.", nameof(handle));

        Sector sector = side.Sector ?? throw new ArgumentException("Line slope handle requires a sector.", nameof(handle));
        VisualSlopeLevel[] selected = SelectedPivotLevels(selectedLevels);
        VisualSlopeHandle[] candidates = handles.Where(candidate => !ReferenceEquals(candidate, handle)).ToArray();
        if (selected.Length == 0 && useOppositeVertexHandle && sector.Sidedefs.Count == 3)
        {
            VisualSlopeHandle? opposite = candidates.FirstOrDefault(candidate =>
                candidate.Kind == VisualSlopeHandleKind.Vertex
                && candidate.Vertex != null
                && ReferenceEquals(candidate.Sector, sector)
                && SameLevel(candidate.Level, handle.Level)
                && !ReferenceEquals(side.Line.Start, candidate.Vertex)
                && !ReferenceEquals(side.Line.End, candidate.Vertex));
            if (opposite != null) return opposite;
        }

        int angle = NormalizedAngleDeg(side.Line);
        var potential = candidates
            .Where(candidate =>
                candidate.Kind == VisualSlopeHandleKind.Line
                && candidate.Sidedef != null
                && (selected.Length == 0
                    ? ReferenceEquals(candidate.Sidedef.Sector, sector) && SameLevel(candidate.Level, handle.Level)
                    : selected.Any(level => SameLevel(candidate.Level, level))))
            .Select(candidate => new
            {
                Handle = candidate,
                Angle = NormalizedAngleDeg(candidate.Sidedef!.Line),
            })
            .OrderBy(candidate => Math.Abs(angle - candidate.Angle))
            .ToArray();

        if (potential.Length == 0) return null;

        int bestAngle = potential[0].Angle;
        return potential
            .Where(candidate => candidate.Angle == bestAngle)
            .OrderByDescending(candidate => Math.Abs(side.Line.Line.GetDistanceToLine(candidate.Handle.Sidedef!.Line.GetCenterPoint(), false)))
            .First()
            .Handle;
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

    private static int NormalizedAngleDeg(Linedef line)
        => line.AngleDeg >= 180 ? line.AngleDeg - 180 : line.AngleDeg;

    private static VisualSlopeHandle? FindMappedHandle(
        VisualSlopeHandle[] original,
        VisualSlopeHandle[] updated,
        VisualSlopeHandle? target)
    {
        if (target == null) return null;

        for (int i = 0; i < original.Length; i++)
        {
            if (ReferenceEquals(original[i], target))
                return updated[i];
        }

        return null;
    }

    private static VisualSlopeLevel[] SelectedPivotLevels(IEnumerable<VisualSlopeLevel>? selectedLevels)
        => selectedLevels == null
            ? []
            : selectedLevels.Where(level => level?.Sector != null && !level.Sector.IsDisposed).ToArray();

    private static IReadOnlyList<VisualSlopeLevel> AffectedChangeLevels(
        VisualSlopeLevel handleLevel,
        IEnumerable<VisualSlopeLevel>? affectedLevels)
    {
        VisualSlopeLevel[] levels = affectedLevels == null
            ? []
            : affectedLevels.Where(level => level?.Sector != null && !level.Sector.IsDisposed).ToArray();
        if (levels.Length == 0) return [handleLevel];
        if (levels.Any(level => SameLevel(level, handleLevel))) return levels;
        return [.. levels, handleLevel];
    }

    private static VisualSlopeLevel[] SelectedLevels(IEnumerable<VisualSlopeLevel> selectedLevels)
    {
        if (selectedLevels == null) throw new ArgumentNullException(nameof(selectedLevels));
        return selectedLevels.Where(level => level?.Sector != null && !level.Sector.IsDisposed).ToArray();
    }

    private static int BaseHeightForArch(VisualSlopeLevel level)
    {
        if (level.Type == VisualSlopeLevelType.Ceiling)
            return level.ExtraFloor ? level.Sector.FloorHeight : level.Sector.CeilHeight;

        return level.ExtraFloor ? level.Sector.CeilHeight : level.Sector.FloorHeight;
    }

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
