// ABOUTME: Skeleton of UDB's Map.Linedef sufficient for the geometry port.
// ABOUTME: Real Linedef (action+args, flags, tags, blockmap, sector linkage, length cache) follows in the Map port.

namespace DBuilder.Map;

using DBuilder.Geometry;

public class Linedef
{
    public Vertex Start { get; set; } = null!;
    public Vertex End { get; set; } = null!;
    public double Angle { get; set; }
    public Sidedef? Front { get; set; }
    public Sidedef? Back { get; set; }

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
