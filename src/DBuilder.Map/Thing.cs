// ABOUTME: Skeleton of UDB's Map.Thing - positioned actor with type, angle, flags.
// ABOUTME: Subset needed for map I/O and visualization; omits UDB's selection, marks, args, custom fields.

namespace DBuilder.Map;

using DBuilder.Geometry;

public class Thing
{
    public Vector2D Position { get; set; }
    public double Height { get; set; } // UDMF Z; Doom-format things have no height
    public int Type { get; set; }
    public int Angle { get; set; }
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
}
