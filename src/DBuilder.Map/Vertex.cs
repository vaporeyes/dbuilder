// ABOUTME: Skeleton of UDB's Map.Vertex with selection state, UDMF fields and linedef back-references.
// ABOUTME: Provides UDB-style vertex distance helpers used by hit-testing and nearest-element queries.

namespace DBuilder.Map;

using DBuilder.Geometry;

public class Vertex : IMapElement, ISelectable, IMarkable, IGroupable, IFielded
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

    public double DistanceToSq(Vector2D pos)
        => Vector2D.DistanceSq(pos, Position);

    public double DistanceTo(Vector2D pos)
        => Vector2D.Distance(pos, Position);

    public Linedef? NearestLinedef(Vector2D pos)
        => MapSet.NearestLinedef(Linedefs, pos);
}
