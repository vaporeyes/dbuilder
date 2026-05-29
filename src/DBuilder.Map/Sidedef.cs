// ABOUTME: Skeleton of UDB's Map.Sidedef expanded with binary-record fields needed for map I/O.
// ABOUTME: Still omits the full UDB surface (marks, selection, sector ownership logic).

namespace DBuilder.Map;

using DBuilder.Geometry;

public class Sidedef : IMapElement, ISelectable, IMarkable, IFielded
{
    public Linedef Line { get; set; } = null!;
    public Sector? Sector { get; set; }
    public bool IsFront { get; set; }
    public double Angle => IsFront ? Line.Angle : Angle2D.Normalized(Line.Angle + Angle2D.PI);

    /// <summary>True after this element has been removed from its owning map.</summary>
    public bool IsDisposed { get; set; }

    /// <summary>Transient editor selection state. Not part of the saved map; reset after undo/redo.</summary>
    public bool Selected { get; set; }

    /// <summary>Transient editor mark state for batch algorithms. Not part of the saved map.</summary>
    public bool Marked { get; set; }

    /// <summary>The opposite sidedef on the same linedef, when this is a two-sided line. Populated by MapSet.BuildIndexes().</summary>
    public Sidedef? Other { get; set; }

    // Binary record fields.
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public string HighTexture { get; set; } = "-";
    public string MidTexture { get; set; } = "-";
    public string LowTexture { get; set; } = "-";

    // UDMF-specific named flags collected as a string set so sidedef options survive clone and clipboard round-trips.
    public HashSet<string> UdmfFlags { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Custom UDMF fields (non-standard keys) preserved verbatim. Values are int/double/bool/string.</summary>
    public Dictionary<string, object> Fields { get; } = new(StringComparer.Ordinal);

    public Sidedef() { }
    public Sidedef(Linedef line, bool isFront)
    {
        Line = line;
        IsFront = isFront;
    }

    public void SetSector(Sector? sector)
        => Sector = sector;

    public void SetLinedef(Linedef? linedef, bool front)
    {
        if (Line != null)
            Line.DetachSidedef(this);

        if (linedef == null)
        {
            Line = null!;
            Other = null;
            IsFront = front;
        }
        else if (front)
        {
            linedef.AttachFront(this);
        }
        else
        {
            linedef.AttachBack(this);
        }
    }

    public string GetTexture(SidedefPart part) => part switch
    {
        SidedefPart.Upper => HighTexture,
        SidedefPart.Middle => MidTexture,
        SidedefPart.Lower => LowTexture,
        _ => "-",
    };

    public void SetTexture(SidedefPart part, string? name)
    {
        string texture = string.IsNullOrEmpty(name) ? "-" : name;
        switch (part)
        {
            case SidedefPart.Upper:
                HighTexture = texture;
                break;
            case SidedefPart.Middle:
                MidTexture = texture;
                break;
            case SidedefPart.Lower:
                LowTexture = texture;
                break;
        }
    }

    public bool IsTextureRequired(SidedefPart part) => part switch
    {
        SidedefPart.Upper => HighRequired(),
        SidedefPart.Middle => MiddleRequired(),
        SidedefPart.Lower => LowRequired(),
        _ => false,
    };

    public double GetPartHeight(SidedefPart part) => part switch
    {
        SidedefPart.Upper => GetHighHeight(),
        SidedefPart.Middle => GetMiddleHeight(),
        SidedefPart.Lower => GetLowHeight(),
        _ => 0,
    };

    /// <summary>True when this side exposes an upper wall gap above the opposite sector.</summary>
    public bool HighRequired()
    {
        if (Other?.Sector == null || Sector == null) return false;
        var start = Line.Start.Position;
        var end = Line.End.Position;
        return Sector.GetCeilZ(start) > Other.Sector.GetCeilZ(start)
            || Sector.GetCeilZ(end) > Other.Sector.GetCeilZ(end);
    }

    /// <summary>True when this side is one-sided and therefore needs a middle wall texture.</summary>
    public bool MiddleRequired() => Other == null;

    /// <summary>True when this side exposes a lower wall gap below the opposite sector.</summary>
    public bool LowRequired()
    {
        if (Other?.Sector == null || Sector == null) return false;
        var start = Line.Start.Position;
        var end = Line.End.Position;
        return Sector.GetFloorZ(start) < Other.Sector.GetFloorZ(start)
            || Sector.GetFloorZ(end) < Other.Sector.GetFloorZ(end);
    }

    public double GetHighHeight()
    {
        if (Other?.Sector == null || Sector == null) return 0;
        var start = Line.Start.Position;
        var end = Line.End.Position;
        double h1 = Sector.GetCeilZ(start) - Other.Sector.GetCeilZ(start);
        double h2 = Sector.GetCeilZ(end) - Other.Sector.GetCeilZ(end);
        return System.Math.Max(0, System.Math.Max(h1, h2));
    }

    public double GetMiddleHeight()
    {
        if (Sector == null) return 0;
        var start = Line.Start.Position;
        var end = Line.End.Position;
        if (Other?.Sector != null)
        {
            double h1 = System.Math.Min(Sector.GetCeilZ(start), Other.Sector.GetCeilZ(start))
                - System.Math.Max(Sector.GetFloorZ(start), Other.Sector.GetFloorZ(start));
            double h2 = System.Math.Min(Sector.GetCeilZ(end), Other.Sector.GetCeilZ(end))
                - System.Math.Max(Sector.GetFloorZ(end), Other.Sector.GetFloorZ(end));
            return System.Math.Max(0, System.Math.Max(h1, h2));
        }

        double oneSidedH1 = Sector.GetCeilZ(start) - Sector.GetFloorZ(start);
        double oneSidedH2 = Sector.GetCeilZ(end) - Sector.GetFloorZ(end);
        return System.Math.Max(0, System.Math.Max(oneSidedH1, oneSidedH2));
    }

    public double GetLowHeight()
    {
        if (Other?.Sector == null || Sector == null) return 0;
        var start = Line.Start.Position;
        var end = Line.End.Position;
        double h1 = Other.Sector.GetFloorZ(start) - Sector.GetFloorZ(start);
        double h2 = Other.Sector.GetFloorZ(end) - Sector.GetFloorZ(end);
        return System.Math.Max(0, System.Math.Max(h1, h2));
    }
}
