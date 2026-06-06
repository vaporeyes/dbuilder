// ABOUTME: Skeleton of UDB's Map.Linedef expanded with binary-record fields and geometry helpers.
// ABOUTME: Keeps selection, marks, args, tags, UDMF fields, sidedef links and UDB-style line measurements.

namespace DBuilder.Map;

using System.Drawing;
using DBuilder.Geometry;

public class Linedef : IMapElement, ISelectable, IMarkable, IGroupable, IFielded, IHasArguments, IMultiTaggedMapElement
{
    public const double SIDE_POINT_DISTANCE = 0.01;
    public const double SidePointDistance = SIDE_POINT_DISTANCE;
    public const int NUM_ARGS = 5;
    public const int BlockingFlagBit = 1;
    public const int TwoSidedFlagBit = 4;

    public int Index { get; set; }
    public Vertex Start { get; set; } = null!;
    public Vertex End { get; set; } = null!;
    public double Angle { get; set; }

    /// <summary>True after this element has been removed from its owning map.</summary>
    public bool IsDisposed { get; set; }

    /// <summary>Map error results ignored for this element, matching UDB's per-element error suppression.</summary>
    public HashSet<MapIssueKind> IgnoredErrorChecks { get; } = new();

    /// <summary>Transient editor selection state. Not part of the saved map; reset after undo/redo.</summary>
    public bool Selected { get; set; }

    /// <summary>Transient editor mark state for batch algorithms. Not part of the saved map.</summary>
    public bool Marked { get; set; }

    /// <summary>Transient editor selection group membership bitmask.</summary>
    public int Groups { get; set; }
    public Sidedef? Front { get; set; }
    public Sidedef? Back { get; set; }
    public Line2D Line => new(Start.Position, End.Position);
    public double LengthSq => (End.Position - Start.Position).GetLengthSq();
    public double Length => Math.Sqrt(LengthSq);
    public double LengthInv => Length > 0.0 ? 1.0 / Length : 1.0 / 0.0000000001;
    public int AngleDeg => (int)(Angle * Angle2D.PIDEG);
    public RectangleF Rect
    {
        get
        {
            double left = Math.Min(Start.Position.x, End.Position.x);
            double top = Math.Min(Start.Position.y, End.Position.y);
            double right = Math.Max(Start.Position.x, End.Position.x);
            double bottom = Math.Max(Start.Position.y, End.Position.y);
            return new RectangleF((float)left, (float)top, (float)(right - left), (float)(bottom - top));
        }
    }

    // Binary record fields (Doom + UDMF).
    public int Flags { get; set; }
    public ushort RawFlags => unchecked((ushort)Flags);
    public int Action { get; set; }
    public int Activate { get; set; }

    /// <summary>All tags (UDMF id + moreids). Authoritative; <see cref="Tag"/> is a convenience over the first entry.</summary>
    public List<int> Tags { get; } = new();

    /// <summary>The primary tag (first entry of <see cref="Tags"/>). Setting it replaces or seeds the first entry.</summary>
    public int Tag
    {
        get => Tags.Count > 0 ? Tags[0] : 0;
        set
        {
            if (Tags.Count == 0) Tags.Add(value);
            else Tags[0] = value;
        }
    }

    // Hexen/UDMF action parameters (5 bytes in Hexen binary, ints in UDMF).
    public int[] Args { get; } = new int[NUM_ARGS];

    // UDMF-specific named flags collected as a string set so we don't lose data on round-trip.
    // Real UDB resolves these against the game config (blocking, dontdraw, etc.).
    public HashSet<string> UdmfFlags { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Custom UDMF fields (non-standard keys) preserved verbatim. Values are int/double/bool/string.</summary>
    public Dictionary<string, object> Fields { get; } = new(StringComparer.Ordinal);

    public Linedef() { }
    public Linedef(Vertex start, Vertex end)
    {
        Start = start;
        End = end;
        Angle = ComputeAngle(start, end);
    }

    // Match UDB's Linedef.Angle convention via Vector2D.GetAngle on the delta.
    public static double ComputeAngle(Vertex start, Vertex end) => (end.Position - start.Position).GetAngle();

    public bool IsFlagSet(string flagName)
        => UdmfFlags.Contains(flagName);

    public void SetFlag(string flagName, bool value)
    {
        if (value) UdmfFlags.Add(flagName);
        else UdmfFlags.Remove(flagName);
    }

    public Dictionary<string, bool> GetFlags()
    {
        var flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (string flag in UdmfFlags) flags[flag] = true;
        return flags;
    }

    public HashSet<string> GetEnabledFlags()
        => new(UdmfFlags, StringComparer.OrdinalIgnoreCase);

    public void ClearFlags()
        => UdmfFlags.Clear();

    public void ApplySidedFlags()
    {
        bool twoSided = Front != null && Back != null;
        SetFlag("blocking", !twoSided);
        SetFlag("twosided", twoSided);

        if (twoSided)
        {
            Flags &= ~BlockingFlagBit;
            Flags |= TwoSidedFlagBit;
        }
        else
        {
            Flags |= BlockingFlagBit;
            Flags &= ~TwoSidedFlagBit;
        }
    }

    public void CopyPropertiesTo(Linedef linedef)
    {
        linedef.Selected = Selected;
        linedef.Marked = Marked;
        linedef.Groups = Groups;
        linedef.Flags = Flags;
        linedef.Action = Action;
        linedef.Activate = Activate;

        linedef.Tags.Clear();
        linedef.Tags.AddRange(Tags);

        Array.Clear(linedef.Args);
        Array.Copy(Args, linedef.Args, Args.Length);

        linedef.UdmfFlags.Clear();
        foreach (var flag in UdmfFlags) linedef.UdmfFlags.Add(flag);

        linedef.IgnoredErrorChecks.Clear();
        foreach (var check in IgnoredErrorChecks) linedef.IgnoredErrorChecks.Add(check);

        CopyFieldsTo(linedef);
    }

    public void Update(Dictionary<string, bool> flags, ushort rawFlags, int activate, List<int> tags, int action, int[] args)
    {
        UdmfFlags.Clear();
        foreach (var flag in flags)
            if (flag.Value) UdmfFlags.Add(flag.Key);

        Flags = rawFlags;
        Activate = activate;
        Action = action;

        Tags.Clear();
        Tags.AddRange(tags);

        Array.Clear(Args);
        Array.Copy(args, Args, Math.Min(args.Length, Args.Length));
    }

    public void SetStartVertex(Vertex vertex)
    {
        if (ReferenceEquals(Start, vertex)) return;

        var old = Start;
        Start = vertex;
        if (!ReferenceEquals(old, End)) old.Linedefs.Remove(this);
        if (!vertex.Linedefs.Contains(this)) vertex.Linedefs.Add(this);
        if (!End.Linedefs.Contains(this)) End.Linedefs.Add(this);
        Angle = ComputeAngle(Start, End);
    }

    public void SetEndVertex(Vertex vertex)
    {
        if (ReferenceEquals(End, vertex)) return;

        var old = End;
        End = vertex;
        if (!ReferenceEquals(old, Start)) old.Linedefs.Remove(this);
        if (!vertex.Linedefs.Contains(this)) vertex.Linedefs.Add(this);
        if (!Start.Linedefs.Contains(this)) Start.Linedefs.Add(this);
        Angle = ComputeAngle(Start, End);
    }

    public void AttachFront(Sidedef? sidedef)
        => AttachSidedef(sidedef, front: true);

    public void AttachBack(Sidedef? sidedef)
        => AttachSidedef(sidedef, front: false);

    public void DetachSidedef(Sidedef sidedef)
    {
        if (ReferenceEquals(Front, sidedef)) Front = null;
        else if (ReferenceEquals(Back, sidedef)) Back = null;
        else return;

        sidedef.Line = null!;
        sidedef.Other = null;
        if (Front != null) Front.Other = Back;
        if (Back != null) Back.Other = Front;
    }

    private void AttachSidedef(Sidedef? sidedef, bool front)
    {
        var old = front ? Front : Back;
        if (ReferenceEquals(old, sidedef)) return;

        if (old != null)
        {
            old.Line = null!;
            old.Other = null;
        }

        if (sidedef?.Line != null && !ReferenceEquals(sidedef.Line, this))
            sidedef.Line.DetachSidedef(sidedef);

        if (front) Front = sidedef;
        else Back = sidedef;

        if (sidedef != null)
        {
            sidedef.Line = this;
            sidedef.IsFront = front;
        }

        if (Front != null) Front.Other = Back;
        if (Back != null) Back.Other = Front;
    }

    /// <summary>Reverses the line direction by swapping its vertices (sidedefs stay attached, so the
    /// line now faces the other way). Call MapSet.BuildIndexes() afterwards to refresh vertex back-refs.</summary>
    public void FlipVertices()
    {
        (Start, End) = (End, Start);
        Angle = ComputeAngle(Start, End);
    }

    /// <summary>Swaps the front and back sidedefs (and their IsFront flags), changing which side is the
    /// front without moving the line. Call MapSet.BuildIndexes() afterwards to refresh sidedef links.</summary>
    public void FlipSidedefs()
    {
        (Front, Back) = (Back, Front);
        if (Front != null) Front.IsFront = true;
        if (Back != null) Back.IsFront = false;
    }

    public Vector2D GetSidePoint(bool front)
    {
        var delta = End.Position - Start.Position;
        double length = delta.GetLength();
        double lengthInv = length > 0.0 ? 1.0 / length : 1.0 / 0.0000000001;
        double nx = delta.x * lengthInv * SidePointDistance;
        double ny = delta.y * lengthInv * SidePointDistance;

        if (front)
        {
            nx = -nx;
            ny = -ny;
        }

        return new Vector2D(
            Start.Position.x + delta.x * 0.5 - ny,
            Start.Position.y + delta.y * 0.5 + nx);
    }

    public Vector2D GetCenterPoint() => Start.Position + (End.Position - Start.Position) * 0.5;

    public List<Vector2D> GetGridIntersections(
        double gridSize,
        Vector2D gridOffset = default,
        double gridRotation = 0.0,
        double gridOriginX = 0.0,
        double gridOriginY = 0.0)
    {
        var coordinates = new List<Vector2D>();
        if (!double.IsFinite(gridSize) || gridSize <= 0.0) return coordinates;

        Vector2D v1 = Start.Position;
        Vector2D v2 = End.Position;
        bool transformed = Math.Abs(gridRotation) > 0.0001
            || Math.Abs(gridOriginX) > 0.0001
            || Math.Abs(gridOriginY) > 0.0001;

        if (transformed)
        {
            var origin = new Vector2D(gridOriginX, gridOriginY);
            v1 = (v1 - origin).GetRotated(-gridRotation);
            v2 = (v2 - origin).GetRotated(-gridRotation);
        }

        double minX;
        double maxX;
        bool reverseX;
        if (v1.x > v2.x)
        {
            minX = v2.x;
            maxX = v1.x;
            reverseX = true;
        }
        else
        {
            minX = v1.x;
            maxX = v2.x;
            reverseX = false;
        }

        double minY;
        double maxY;
        bool reverseY;
        if (v1.y > v2.y)
        {
            minY = v2.y;
            maxY = v1.y;
            reverseY = true;
        }
        else
        {
            minY = v1.y;
            maxY = v2.y;
            reverseY = false;
        }

        double gx = GetHigherGridCoordinate(minX, gridSize) + gridOffset.x;
        for (; maxX > minX && gx < maxX; gx += gridSize)
        {
            double u = (gx - minX) / (maxX - minX);
            if (reverseX) u = 1.0 - u;
            coordinates.Add(new Vector2D(gx, v1.y + (v2.y - v1.y) * u));
        }

        double gy = GetHigherGridCoordinate(minY, gridSize) + gridOffset.y;
        for (; maxY > minY && gy < maxY; gy += gridSize)
        {
            double u = (gy - minY) / (maxY - minY);
            if (reverseY) u = 1.0 - u;
            coordinates.Add(new Vector2D(v1.x + (v2.x - v1.x) * u, gy));
        }

        if (transformed)
        {
            var origin = new Vector2D(gridOriginX, gridOriginY);
            for (int i = 0; i < coordinates.Count; i++)
                coordinates[i] = coordinates[i].GetRotated(gridRotation) + origin;
        }

        return coordinates;
    }

    private static double GetHigherGridCoordinate(double offset, double gridSize)
        => Math.Round((offset + gridSize * 0.5) / gridSize) * gridSize;

    public Vector2D NearestOnLine(Vector2D pos)
    {
        double u = Line2D.GetNearestOnLine(Start.Position, End.Position, pos);
        if (u < 0.0) u = 0.0;
        else if (u > 1.0) u = 1.0;
        return Line2D.GetCoordinatesAt(Start.Position, End.Position, u);
    }

    public double DistanceToSq(Vector2D pos, bool bounded)
        => Line2D.GetDistanceToLineSq(Start.Position, End.Position, pos, bounded);

    public double DistanceTo(Vector2D pos, bool bounded)
        => Math.Sqrt(DistanceToSq(pos, bounded));

    public double SafeDistanceToSq(Vector2D pos, bool bounded)
    {
        Vector2D start = Start.Position;
        Vector2D end = End.Position;
        double deltaX = end.x - start.x;
        double deltaY = end.y - start.y;
        double lengthSq = deltaX * deltaX + deltaY * deltaY;
        double length = Math.Sqrt(lengthSq);
        double lengthInv = length > 0.0 ? 1.0 / length : 1.0 / 0.0000000001;
        double lengthSqInv = lengthSq > 0.0 ? 1.0 / lengthSq : 1.0 / 0.0000000001;

        double u = ((pos.x - start.x) * deltaX + (pos.y - start.y) * deltaY) * lengthSqInv;
        if (bounded)
        {
            u = lengthInv > 1.0
                ? Math.Max(0.0, Math.Min(1.0, u))
                : Math.Max(lengthInv, Math.Min(1.0 - lengthInv, u));
        }

        double distanceX = pos.x - (start.x + u * deltaX);
        double distanceY = pos.y - (start.y + u * deltaY);
        return distanceX * distanceX + distanceY * distanceY;
    }

    public double SafeDistanceTo(Vector2D pos, bool bounded)
        => Math.Sqrt(SafeDistanceToSq(pos, bounded));

    public double SideOfLine(Vector2D pos)
        => Line2D.GetSideOfLine(Start.Position, End.Position, pos);

    private void CopyFieldsTo(IFielded element)
    {
        element.Fields.Clear();
        foreach (var field in Fields)
            element.Fields[field.Key] = field.Value;
    }
}
