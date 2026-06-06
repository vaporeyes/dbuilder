// ABOUTME: Skeleton of UDB's Map.Sidedef expanded with binary-record fields needed for map I/O.
// ABOUTME: Still omits the full UDB surface (marks, selection, sector ownership logic).

namespace DBuilder.Map;

using DBuilder.Geometry;

public class Sidedef : IMapElement, ISelectable, IMarkable, IFielded
{
    public int Index { get; set; }
    public Linedef Line { get; set; } = null!;
    public Sector? Sector { get; set; }
    public bool IsFront { get; set; }
    public double Angle => IsFront ? Line.Angle : Angle2D.Normalized(Line.Angle + Angle2D.PI);

    /// <summary>True after this element has been removed from its owning map.</summary>
    public bool IsDisposed { get; set; }

    /// <summary>Map error results ignored for this element, matching UDB's per-element error suppression.</summary>
    public HashSet<MapIssueKind> IgnoredErrorChecks { get; } = new();

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
    public string MiddleTexture { get => MidTexture; set => MidTexture = value; }
    public string LowTexture { get; set; } = "-";
    public long LongHighTexture { get; set; } = MapSet.EmptyLongName;
    public long LongMiddleTexture { get; set; } = MapSet.EmptyLongName;
    public long LongLowTexture { get; set; } = MapSet.EmptyLongName;

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

    public bool IsFlagSet(string flagName)
        => UdmfFlags.Contains(flagName);

    public void SetFlag(string flagName, bool value)
    {
        if (value) UdmfFlags.Add(flagName);
        else UdmfFlags.Remove(flagName);
    }

    public Dictionary<string, bool> GetFlags()
    {
        var flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (string flag in UdmfFlags) flags[flag] = true;
        return flags;
    }

    public HashSet<string> GetEnabledFlags()
        => new(UdmfFlags, StringComparer.OrdinalIgnoreCase);

    public void ClearFlags()
        => UdmfFlags.Clear();

    public void CopyPropertiesTo(Sidedef sidedef)
    {
        sidedef.Selected = Selected;
        sidedef.Marked = Marked;
        sidedef.OffsetX = OffsetX;
        sidedef.OffsetY = OffsetY;
        sidedef.HighTexture = HighTexture;
        sidedef.MidTexture = MidTexture;
        sidedef.LowTexture = LowTexture;
        sidedef.LongHighTexture = LongHighTexture;
        sidedef.LongMiddleTexture = LongMiddleTexture;
        sidedef.LongLowTexture = LongLowTexture;

        sidedef.UdmfFlags.Clear();
        foreach (var flag in UdmfFlags) sidedef.UdmfFlags.Add(flag);

        sidedef.IgnoredErrorChecks.Clear();
        foreach (var check in IgnoredErrorChecks) sidedef.IgnoredErrorChecks.Add(check);

        CopyFieldsTo(sidedef);
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

    public void Update(int offsetX, int offsetY, string? highTexture, string? midTexture, string? lowTexture)
        => Update(offsetX, offsetY, highTexture, midTexture, lowTexture, new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase));

    public void Update(
        int offsetX,
        int offsetY,
        string? highTexture,
        string? midTexture,
        string? lowTexture,
        Dictionary<string, bool> flags)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
        SetTextureHigh(highTexture);
        SetTextureMid(midTexture);
        SetTextureLow(lowTexture);

        UdmfFlags.Clear();
        foreach (var flag in flags)
            if (flag.Value) UdmfFlags.Add(flag.Key);
    }

    public void SetTextureHigh(string? name)
    {
        HighTexture = NormalizeTextureName(name);
        if (HighTexture == "-") LongHighTexture = MapSet.EmptyLongName;
    }

    public void SetTextureMid(string? name)
    {
        MidTexture = NormalizeTextureName(name);
        if (MidTexture == "-") LongMiddleTexture = MapSet.EmptyLongName;
    }

    public void SetTextureLow(string? name)
    {
        LowTexture = NormalizeTextureName(name);
        if (LowTexture == "-") LongLowTexture = MapSet.EmptyLongName;
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
        switch (part)
        {
            case SidedefPart.Upper:
                SetTextureHigh(name);
                break;
            case SidedefPart.Middle:
                SetTextureMid(name);
                break;
            case SidedefPart.Lower:
                SetTextureLow(name);
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

    public bool RemoveUnneededTextures(bool removeMiddle, bool autoClearSidedefTextures = true)
        => RemoveUnneededTextures(removeMiddle, force: false, shiftMiddle: false, autoClearSidedefTextures);

    public bool RemoveUnneededTextures(bool removeMiddle, bool force, bool shiftMiddle, bool autoClearSidedefTextures = true)
    {
        bool changed = false;
        bool mayClearUpperLower = force
            || (Line.Tag == 0
                && Line.Action == 0
                && (Sector?.Tag ?? 0) == 0
                && (Other?.Sector?.Tag ?? 0) == 0);

        if (mayClearUpperLower)
        {
            if (autoClearSidedefTextures && !HighRequired())
            {
                changed |= SetTextureHighIfDifferent("-", MapSet.EmptyLongName);
            }
            else if (shiftMiddle && HighTexture == "-" && HighRequired())
            {
                changed |= SetTextureHighIfDifferent(MidTexture, LongMiddleTexture);
            }

            if (autoClearSidedefTextures && !LowRequired())
            {
                changed |= SetTextureLowIfDifferent("-", MapSet.EmptyLongName);
            }
            else if (shiftMiddle && LowTexture == "-" && LowRequired())
            {
                changed |= SetTextureLowIfDifferent(MidTexture, LongMiddleTexture);
            }
        }

        if (removeMiddle && !MiddleRequired())
        {
            changed |= SetTextureMidIfDifferent("-", MapSet.EmptyLongName);
        }

        return changed;
    }

    private bool SetTextureHighIfDifferent(string? name, long longName)
    {
        string texture = NormalizeTextureName(name);
        if (HighTexture == texture && LongHighTexture == longName) return false;
        HighTexture = texture;
        LongHighTexture = longName;
        return true;
    }

    private bool SetTextureMidIfDifferent(string? name, long longName)
    {
        string texture = NormalizeTextureName(name);
        if (MidTexture == texture && LongMiddleTexture == longName) return false;
        MidTexture = texture;
        LongMiddleTexture = longName;
        return true;
    }

    private bool SetTextureLowIfDifferent(string? name, long longName)
    {
        string texture = NormalizeTextureName(name);
        if (LowTexture == texture && LongLowTexture == longName) return false;
        LowTexture = texture;
        LongLowTexture = longName;
        return true;
    }

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

    private static string NormalizeTextureName(string? name)
        => string.IsNullOrEmpty(name) ? "-" : name;

    private void CopyFieldsTo(IFielded element)
    {
        element.Fields.Clear();
        foreach (var field in Fields)
            element.Fields[field.Key] = field.Value;
    }
}
