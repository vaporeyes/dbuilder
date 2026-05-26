// ABOUTME: Skeleton of UDB's Map.Vertex with the Linedefs back-ref needed for triangulation.
// ABOUTME: Full Vertex (selection state, marks, owners list) gets filled in when the Map module is ported in earnest.

namespace DBuilder.Map;

using DBuilder.Geometry;

public class Vertex : ISelectable, IMarkable, IFielded
{
    public Vector2D Position { get; set; }

    /// <summary>Transient editor selection state. Not part of the saved map; reset after undo/redo.</summary>
    public bool Selected { get; set; }

    /// <summary>Transient editor mark state for batch algorithms. Not part of the saved map.</summary>
    public bool Marked { get; set; }

    /// <summary>UDMF per-vertex ceiling height for vertex slopes. NaN means unset.</summary>
    public double ZCeiling { get; set; } = double.NaN;

    /// <summary>UDMF per-vertex floor height for vertex slopes. NaN means unset.</summary>
    public double ZFloor { get; set; } = double.NaN;

    /// <summary>Linedefs touching this vertex. Populated by MapSet.BuildIndexes().</summary>
    public List<Linedef> Linedefs { get; } = new();

    /// <summary>Custom UDMF fields (non-standard keys) preserved verbatim. Values are int/double/bool/string.</summary>
    public Dictionary<string, object> Fields { get; } = new(StringComparer.Ordinal);

    public Vertex() { }
    public Vertex(Vector2D position) { Position = position; }
}
