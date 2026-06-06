// ABOUTME: Skeleton of UDB's Map.Thing - positioned actor with type, angle, flags and transient sector link.
// ABOUTME: Keeps editor-only selection, group, args, UDMF fields and blockmap sector determination behavior.

namespace DBuilder.Map;

using DBuilder.Geometry;

public class Thing : IMapElement, ISelectable, IMarkable, IGroupable, IFielded, IHasArguments, ITaggedMapElement
{
    public int Index { get; set; }
    public Vector2D Position { get; set; }

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

    /// <summary>Transient containing sector link. Recomputed from geometry and not serialized.</summary>
    public Sector? Sector { get; set; }

    public double Height { get; set; } // UDMF Z; Doom-format things have no height
    public int Type { get; set; }
    public int Angle { get; set; }

    /// <summary>UDMF pitch in degrees (rotation about the actor's lateral axis). Default 0.</summary>
    public int Pitch { get; set; }

    /// <summary>UDMF roll in degrees (rotation about the actor's forward axis). Default 0.</summary>
    public int Roll { get; set; }

    /// <summary>UDMF horizontal scale multiplier. Default 1.0 (unscaled).</summary>
    public double ScaleX { get; set; } = 1.0;

    /// <summary>UDMF vertical scale multiplier. Default 1.0 (unscaled).</summary>
    public double ScaleY { get; set; } = 1.0;

    /// <summary>Editor display radius from thing type metadata. Not serialized in map data.</summary>
    public double Size { get; set; }

    /// <summary>Whether the thing keeps a fixed screen size in 2D rendering. Not serialized in map data.</summary>
    public bool FixedSize { get; set; }

    public int Flags { get; set; }
    public int Tag { get; set; }
    public int Action { get; set; }

    // Hexen/UDMF action parameters (5 bytes in Hexen binary, ints in UDMF).
    public int[] Args { get; } = new int[5];

    // UDMF-specific named flags.
    public HashSet<string> UdmfFlags { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Custom UDMF fields (non-standard keys) preserved verbatim. Values are int/double/bool/string.</summary>
    public Dictionary<string, object> Fields { get; } = new(StringComparer.Ordinal);

    public Thing() { }
    public Thing(Vector2D position, int type, int angle = 0)
    {
        Position = position;
        Type = type;
        Angle = angle;
    }

    /// <summary>Determines the containing sector from a map-wide spatial query.</summary>
    public void DetermineSector(MapSet map)
    {
        Sector = map.GetSectorAt(Position);
    }

    /// <summary>Determines the containing sector from blockmap sector candidates.</summary>
    public void DetermineSector(BlockMap blockMap)
    {
        Sector = blockMap.GetContainingSector(Position);
    }

    public double DistanceToSq(Vector2D pos)
        => Vector2D.DistanceSq(pos, Position);

    public double DistanceTo(Vector2D pos)
        => Vector2D.Distance(pos, Position);

    public bool IsFlagSet(string flagName)
        => UdmfFlags.Contains(flagName);

    public void SetFlag(string flagName, bool value)
    {
        if (value) UdmfFlags.Add(flagName);
        else UdmfFlags.Remove(flagName);
    }

    public void CopyPropertiesTo(Thing thing)
    {
        thing.Position = Position;
        thing.Selected = Selected;
        thing.Marked = Marked;
        thing.Groups = Groups;
        thing.Height = Height;
        thing.Type = Type;
        thing.Angle = Angle;
        thing.Pitch = Pitch;
        thing.Roll = Roll;
        thing.ScaleX = ScaleX;
        thing.ScaleY = ScaleY;
        thing.Size = Size;
        thing.FixedSize = FixedSize;
        thing.Flags = Flags;
        thing.Tag = Tag;
        thing.Action = Action;

        Array.Clear(thing.Args);
        Array.Copy(Args, thing.Args, Args.Length);

        thing.UdmfFlags.Clear();
        foreach (var flag in UdmfFlags) thing.UdmfFlags.Add(flag);

        thing.IgnoredErrorChecks.Clear();
        foreach (var check in IgnoredErrorChecks) thing.IgnoredErrorChecks.Add(check);

        CopyFieldsTo(thing);
    }

    public void Move(Vector2D newPosition)
        => Position = newPosition;

    public void Move(Vector3D newPosition)
    {
        Position = new Vector2D(newPosition.x, newPosition.y);
        Height = newPosition.z;
    }

    public void Move(double x, double y, double zOffset)
        => Move(new Vector3D(x, y, zOffset));

    public void Rotate(double realAngle)
        => Angle = Angle2D.RealToDoom(realAngle);

    public void Rotate(int doomAngle)
        => Angle = doomAngle;

    public void SetPitch(int pitch)
        => Pitch = ClampAngle(pitch);

    public void SetRoll(int roll)
        => Roll = ClampAngle(roll);

    public void SetScale(double scaleX, double scaleY)
    {
        ScaleX = scaleX;
        ScaleY = scaleY;
    }

    public void Update(
        int type,
        double x,
        double y,
        double zOffset,
        int angle,
        int pitch,
        int roll,
        double scaleX,
        double scaleY,
        Dictionary<string, bool> flags,
        ushort rawFlags,
        int tag,
        int action,
        int[] args)
    {
        Type = type;
        Angle = angle;
        Pitch = pitch;
        Roll = roll;
        ScaleX = scaleX == 0.0 ? 1.0 : scaleX;
        ScaleY = scaleY == 0.0 ? 1.0 : scaleY;
        Flags = rawFlags;
        Tag = tag;
        Action = action;

        UdmfFlags.Clear();
        foreach (var flag in flags)
            if (flag.Value) UdmfFlags.Add(flag.Key);

        Array.Clear(Args);
        Array.Copy(args, Args, Math.Min(args.Length, Args.Length));
        Move(x, y, zOffset);
    }

    public void SnapToAccuracy()
        => SnapToAccuracy(usePrecisePosition: true);

    public void SnapToAccuracy(bool usePrecisePosition)
        => SnapToAccuracy(vertexDecimals: 3, usePrecisePosition);

    public void SnapToAccuracy(int vertexDecimals, bool usePrecisePosition = true)
    {
        int decimals = usePrecisePosition ? Math.Max(0, vertexDecimals) : 0;
        Move(new Vector2D(
            Math.Round(Position.x, decimals),
            Math.Round(Position.y, decimals)));
        Height = Math.Round(Height, decimals);
    }

    private static int ClampAngle(int angle)
    {
        int normalized = angle % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private void CopyFieldsTo(IFielded element)
    {
        element.Fields.Clear();
        foreach (var field in Fields)
            element.Fields[field.Key] = field.Value;
    }
}
