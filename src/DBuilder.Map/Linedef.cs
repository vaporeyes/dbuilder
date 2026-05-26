// ABOUTME: Skeleton of UDB's Map.Linedef expanded with binary-record fields needed for map I/O.
// ABOUTME: Still omits the full UDB surface (marks, selection, blockmap, length cache, args[5], owner sector linkage).

namespace DBuilder.Map;

using DBuilder.Geometry;

public class Linedef : IMapElement, ISelectable, IMarkable, IGroupable, IFielded, IHasArguments
{
    public Vertex Start { get; set; } = null!;
    public Vertex End { get; set; } = null!;
    public double Angle { get; set; }

    /// <summary>True after this element has been removed from its owning map.</summary>
    public bool IsDisposed { get; set; }

    /// <summary>Transient editor selection state. Not part of the saved map; reset after undo/redo.</summary>
    public bool Selected { get; set; }

    /// <summary>Transient editor mark state for batch algorithms. Not part of the saved map.</summary>
    public bool Marked { get; set; }

    /// <summary>Transient editor selection group membership bitmask.</summary>
    public int Groups { get; set; }
    public Sidedef? Front { get; set; }
    public Sidedef? Back { get; set; }

    // Binary record fields (Doom + UDMF).
    public int Flags { get; set; }
    public int Action { get; set; }

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
    public int[] Args { get; } = new int[5];

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
}
