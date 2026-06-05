// ABOUTME: Skeleton of UDB's Map.Sector with binary-record fields + Sidedefs back-ref used by triangulation.
// ABOUTME: Still omits the full UDB surface (marks, selection, slopes, 3D floors, BSP linkage).

namespace DBuilder.Map;

using System.Drawing;
using DBuilder.Geometry;

public class Sector : IMapElement, ISelectable, IMarkable, IGroupable, IFielded, IMultiTaggedMapElement
{
    private RectangleF bbox;

    public int Index { get; set; }
    public RectangleF BBox => bbox;

    /// <summary>True after this element has been removed from its owning map.</summary>
    public bool IsDisposed { get; set; }

    /// <summary>Map error results ignored for this element, matching UDB's per-element error suppression.</summary>
    public HashSet<MapIssueKind> IgnoredErrorChecks { get; } = new();

    /// <summary>Transient editor selection state. Not part of the saved map; reset after undo/redo.</summary>
    public bool Selected { get; set; }

    /// <summary>Transient editor mark state for batch algorithms. Not part of the saved map.</summary>
    public bool Marked { get; set; }

    /// <summary>Transient editor selection group membership bitmask.</summary>
    public int Groups { get; set; }

    public int FloorHeight { get; set; }
    public int CeilHeight { get; set; } = 128;
    public string FloorTexture { get; set; } = "-";
    public string CeilTexture { get; set; } = "-";
    public int Brightness { get; set; } = 192;
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

    /// <summary>Creates the sector floor plane from explicit UDMF slope data, triangular vertex heights, or flat height.</summary>
    public static Plane GetFloorPlane(Sector sector, bool isUdmf = true)
    {
        if (isUdmf && sector.FloorSlope.GetLengthSq() > 0 && sector.FloorSlope.z != 0 && !double.IsNaN(sector.FloorSlopeOffset / sector.FloorSlope.z))
            return new Plane(sector.FloorSlope, sector.FloorSlopeOffset);

        var flat = new Plane(new Vector3D(0, 0, 1), -sector.FloorHeight);
        if (!isUdmf || sector.Sidedefs.Count != 3) return flat;

        var vertices = new Vector3D[3];
        bool hasSlopedVertex = false;
        for (int i = 0; i < sector.Sidedefs.Count; i++)
        {
            Vertex vertex = sector.Sidedefs[i].IsFront ? sector.Sidedefs[i].Line.End : sector.Sidedefs[i].Line.Start;
            double z = double.IsNaN(vertex.ZFloor) ? sector.FloorHeight : vertex.ZFloor;
            hasSlopedVertex |= !double.IsNaN(vertex.ZFloor);
            vertices[i] = new Vector3D(vertex.Position, z);
        }

        return hasSlopedVertex ? new Plane(vertices[0], vertices[1], vertices[2], up: true) : flat;
    }

    /// <summary>Creates the sector ceiling plane from explicit UDMF slope data, triangular vertex heights, or flat height.</summary>
    public static Plane GetCeilingPlane(Sector sector, bool isUdmf = true)
    {
        if (isUdmf && sector.CeilSlope.GetLengthSq() > 0 && sector.CeilSlope.z != 0 && !double.IsNaN(sector.CeilSlopeOffset / sector.CeilSlope.z))
            return new Plane(sector.CeilSlope, sector.CeilSlopeOffset);

        var flat = new Plane(new Vector3D(0, 0, -1), sector.CeilHeight);
        if (!isUdmf || sector.Sidedefs.Count != 3) return flat;

        var vertices = new Vector3D[3];
        bool hasSlopedVertex = false;
        for (int i = 0; i < sector.Sidedefs.Count; i++)
        {
            Vertex vertex = sector.Sidedefs[i].IsFront ? sector.Sidedefs[i].Line.End : sector.Sidedefs[i].Line.Start;
            double z = double.IsNaN(vertex.ZCeiling) ? sector.CeilHeight : vertex.ZCeiling;
            hasSlopedVertex |= !double.IsNaN(vertex.ZCeiling);
            vertices[i] = new Vector3D(vertex.Position, z);
        }

        return hasSlopedVertex ? new Plane(vertices[0], vertices[2], vertices[1], up: false) : flat;
    }

    public bool IsFlagSet(string flagName)
        => UdmfFlags.Contains(flagName);

    public void SetFlag(string flagName, bool value)
    {
        if (value) UdmfFlags.Add(flagName);
        else UdmfFlags.Remove(flagName);
    }

    public void CopyPropertiesTo(Sector sector)
    {
        sector.Selected = Selected;
        sector.Marked = Marked;
        sector.Groups = Groups;
        sector.FloorHeight = FloorHeight;
        sector.CeilHeight = CeilHeight;
        sector.FloorTexture = FloorTexture;
        sector.CeilTexture = CeilTexture;
        sector.Brightness = Brightness;
        sector.Special = Special;
        sector.FloorSlope = FloorSlope;
        sector.FloorSlopeOffset = FloorSlopeOffset;
        sector.CeilSlope = CeilSlope;
        sector.CeilSlopeOffset = CeilSlopeOffset;

        sector.Tags.Clear();
        sector.Tags.AddRange(Tags);

        sector.UdmfFlags.Clear();
        foreach (var flag in UdmfFlags) sector.UdmfFlags.Add(flag);

        sector.IgnoredErrorChecks.Clear();
        foreach (var check in IgnoredErrorChecks) sector.IgnoredErrorChecks.Add(check);

        CopyFieldsTo(sector);
    }

    public void Update(int floorHeight, int ceilHeight, string? floorTexture, string? ceilTexture, int special, int tag, int brightness)
        => Update(
            floorHeight,
            ceilHeight,
            floorTexture,
            ceilTexture,
            special,
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
            new List<int> { tag },
            brightness,
            double.NaN,
            new Vector3D(),
            double.NaN,
            new Vector3D());

    public void Update(
        int floorHeight,
        int ceilHeight,
        string? floorTexture,
        string? ceilTexture,
        int special,
        Dictionary<string, bool> flags,
        List<int> tags,
        int brightness,
        double floorSlopeOffset,
        Vector3D floorSlope,
        double ceilSlopeOffset,
        Vector3D ceilSlope)
    {
        FloorHeight = floorHeight;
        CeilHeight = ceilHeight;
        FloorTexture = NormalizeTextureName(floorTexture);
        CeilTexture = NormalizeTextureName(ceilTexture);
        Special = special;
        Brightness = brightness;

        Tags.Clear();
        Tags.AddRange(tags);

        UdmfFlags.Clear();
        foreach (var flag in flags)
            if (flag.Value) UdmfFlags.Add(flag.Key);

        FloorSlopeOffset = floorSlopeOffset;
        FloorSlope = floorSlope;
        CeilSlopeOffset = ceilSlopeOffset;
        CeilSlope = ceilSlope;
    }

    public void SetFloorTexture(string? name)
        => FloorTexture = NormalizeTextureName(name);

    public void SetCeilTexture(string? name)
        => CeilTexture = NormalizeTextureName(name);

    public bool Intersect(Vector2D point)
        => Intersect(point, countOnTopAsTrue: true);

    public bool Intersect(Vector2D point, bool countOnTopAsTrue)
    {
        if (Sidedefs.Count == 0) return false;
        if (point.x < bbox.Left || point.x > bbox.Right || point.y < bbox.Top || point.y > bbox.Bottom) return false;

        uint crossings = 0;
        bool selfReferencing = true;

        foreach (Sidedef side in Sidedefs)
        {
            Vector2D v1 = side.Line.Start.Position;
            Vector2D v2 = side.Line.End.Position;

            if (point == v1 || point == v2) return countOnTopAsTrue;

            if (side.Other == null || side.Other.Sector != this)
                selfReferencing = false;

            if (v1.y != v2.y
                && point.y > (v1.y < v2.y ? v1.y : v2.y)
                && point.y <= (v1.y > v2.y ? v1.y : v2.y)
                && (point.x < (v1.x < v2.x ? v1.x : v2.x)
                    || (point.x <= (v1.x > v2.x ? v1.x : v2.x)
                        && (v1.x == v2.x || point.x <= (point.y - v1.y) * (v2.x - v1.x) / (v2.y - v1.y) + v1.x))))
            {
                crossings++;
            }
        }

        return selfReferencing
            ? crossings % 2 == 0
            : crossings % 2 != 0;
    }

    public void UpdateBBox()
        => bbox = CreateBBox();

    private RectangleF CreateBBox()
    {
        if (Sidedefs.Count == 0) return new RectangleF();

        double left = double.MaxValue;
        double top = double.MaxValue;
        double right = double.MinValue;
        double bottom = double.MinValue;
        var processed = new HashSet<Vertex>();

        foreach (Sidedef side in Sidedefs)
        {
            AddVertex(side.Line.Start);
            AddVertex(side.Line.End);
        }

        return new RectangleF((float)left, (float)top, (float)(right - left), (float)(bottom - top));

        void AddVertex(Vertex vertex)
        {
            if (!processed.Add(vertex)) return;
            Vector2D position = vertex.Position;
            if (position.x < left) left = position.x;
            if (position.y < top) top = position.y;
            if (position.x > right) right = position.x;
            if (position.y > bottom) bottom = position.y;
        }
    }

    /// <summary>All sidedefs belonging to this sector. Populated by MapSet.BuildIndexes().</summary>
    public List<Sidedef> Sidedefs { get; } = new();

    // UDMF-specific named flags collected as a string set so sector options survive clone and clipboard round-trips.
    public HashSet<string> UdmfFlags { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Custom UDMF fields (non-standard keys) preserved verbatim. Values are int/double/bool/string.</summary>
    public Dictionary<string, object> Fields { get; } = new(StringComparer.Ordinal);

    private static string NormalizeTextureName(string? name)
        => string.IsNullOrEmpty(name) ? "-" : name;

    private void CopyFieldsTo(IFielded element)
    {
        element.Fields.Clear();
        foreach (var field in Fields)
            element.Fields[field.Key] = field.Value;
    }
}
