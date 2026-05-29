// ABOUTME: Skeleton of UDB's Map.Thing - positioned actor with type, angle, flags and transient sector link.
// ABOUTME: Keeps editor-only selection, group, args, UDMF fields and blockmap sector determination behavior.

namespace DBuilder.Map;

using DBuilder.Geometry;

public class Thing : IMapElement, ISelectable, IMarkable, IGroupable, IFielded, IHasArguments, ITaggedMapElement
{
    public Vector2D Position { get; set; }

    /// <summary>True after this element has been removed from its owning map.</summary>
    public bool IsDisposed { get; set; }

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
}
