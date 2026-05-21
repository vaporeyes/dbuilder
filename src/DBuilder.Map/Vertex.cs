// ABOUTME: Skeleton of UDB's Map.Vertex sufficient for the geometry port.
// ABOUTME: Full Vertex (selection state, marks, owners list, linedef back-refs) gets filled in when the Map module is ported in earnest.

namespace DBuilder.Map;

using DBuilder.Geometry;

public class Vertex
{
    public Vector2D Position { get; set; }

    public Vertex() { }
    public Vertex(Vector2D position) { Position = position; }
}
