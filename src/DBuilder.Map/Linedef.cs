// ABOUTME: Skeleton of UDB's Map.Linedef expanded with binary-record fields needed for map I/O.
// ABOUTME: Still omits the full UDB surface (marks, selection, blockmap, length cache, args[5], owner sector linkage).

namespace DBuilder.Map;

using DBuilder.Geometry;

public class Linedef
{
    public Vertex Start { get; set; } = null!;
    public Vertex End { get; set; } = null!;
    public double Angle { get; set; }
    public Sidedef? Front { get; set; }
    public Sidedef? Back { get; set; }

    // Binary record fields (Doom + UDMF).
    public int Flags { get; set; }
    public int Action { get; set; }
    public int Tag { get; set; }

    // UDMF-specific named flags collected as a string set so we don't lose data on round-trip.
    // Real UDB resolves these against the game config (blocking, dontdraw, etc.).
    public HashSet<string> UdmfFlags { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Linedef() { }
    public Linedef(Vertex start, Vertex end)
    {
        Start = start;
        End = end;
        Angle = ComputeAngle(start, end);
    }

    // Match UDB's Linedef.Angle convention via Vector2D.GetAngle on the delta.
    public static double ComputeAngle(Vertex start, Vertex end) => (end.Position - start.Position).GetAngle();
}
