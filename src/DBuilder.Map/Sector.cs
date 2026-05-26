// ABOUTME: Skeleton of UDB's Map.Sector with binary-record fields + Sidedefs back-ref used by triangulation.
// ABOUTME: Still omits the full UDB surface (marks, selection, slopes, 3D floors, BSP linkage).

namespace DBuilder.Map;

using DBuilder.Geometry;

public class Sector : IMapElement, ISelectable, IMarkable, IGroupable, IFielded, IMultiTaggedMapElement
{
    public int Index { get; set; }

    /// <summary>True after this element has been removed from its owning map.</summary>
    public bool IsDisposed { get; set; }

    /// <summary>Transient editor selection state. Not part of the saved map; reset after undo/redo.</summary>
    public bool Selected { get; set; }

    /// <summary>Transient editor mark state for batch algorithms. Not part of the saved map.</summary>
    public bool Marked { get; set; }

    /// <summary>Transient editor selection group membership bitmask.</summary>
    public int Groups { get; set; }

    public int FloorHeight { get; set; }
    public int CeilHeight { get; set; }
    public string FloorTexture { get; set; } = "-";
    public string CeilTexture { get; set; } = "-";
    public int Brightness { get; set; } = 160;
    public int Special { get; set; }

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

    /// <summary>Floor slope plane normal. Zero-length means a flat (unsloped) floor.</summary>
    public Vector3D FloorSlope { get; set; }

    /// <summary>Floor slope plane offset (UDMF floorplane_d). NaN means unset.</summary>
    public double FloorSlopeOffset { get; set; } = double.NaN;

    /// <summary>Ceiling slope plane normal. Zero-length means a flat (unsloped) ceiling.</summary>
    public Vector3D CeilSlope { get; set; }

    /// <summary>Ceiling slope plane offset (UDMF ceilingplane_d). NaN means unset.</summary>
    public double CeilSlopeOffset { get; set; } = double.NaN;

    /// <summary>True when the floor has an active (non-vertical, non-flat) slope plane.</summary>
    public bool HasFloorSlope => FloorSlope.GetLengthSq() > 0 && FloorSlope.z != 0;

    /// <summary>True when the ceiling has an active (non-vertical, non-flat) slope plane.</summary>
    public bool HasCeilSlope => CeilSlope.GetLengthSq() > 0 && CeilSlope.z != 0;

    /// <summary>Floor height at a map position. Uses the slope plane when sloped, else the flat <see cref="FloorHeight"/>.</summary>
    public double GetFloorZ(Vector2D pos)
        => HasFloorSlope
            ? new Plane(FloorSlope, double.IsNaN(FloorSlopeOffset) ? 0.0 : FloorSlopeOffset).GetZ(pos)
            : FloorHeight;

    /// <summary>Ceiling height at a map position. Uses the slope plane when sloped, else the flat <see cref="CeilHeight"/>.</summary>
    public double GetCeilZ(Vector2D pos)
        => HasCeilSlope
            ? new Plane(CeilSlope, double.IsNaN(CeilSlopeOffset) ? 0.0 : CeilSlopeOffset).GetZ(pos)
            : CeilHeight;

    /// <summary>All sidedefs belonging to this sector. Populated by MapSet.BuildIndexes().</summary>
    public List<Sidedef> Sidedefs { get; } = new();

    /// <summary>Custom UDMF fields (non-standard keys) preserved verbatim. Values are int/double/bool/string.</summary>
    public Dictionary<string, object> Fields { get; } = new(StringComparer.Ordinal);
}
