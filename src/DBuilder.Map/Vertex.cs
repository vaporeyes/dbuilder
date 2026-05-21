// ABOUTME: Skeleton of UDB's Map.Vertex with the Linedefs back-ref needed for triangulation.
// ABOUTME: Full Vertex (selection state, marks, owners list) gets filled in when the Map module is ported in earnest.

namespace DBuilder.Map;

using DBuilder.Geometry;

public class Vertex
{
    public Vector2D Position { get; set; }

    /// <summary>Linedefs touching this vertex. Populated by MapSet.BuildIndexes().</summary>
    public List<Linedef> Linedefs { get; } = new();

    public Vertex() { }
    public Vertex(Vector2D position) { Position = position; }
}
