// ABOUTME: Applies selected UDB paste-properties groups to existing map elements.
// ABOUTME: Copies only property data, leaving geometry links and editor selection state intact.

namespace DBuilder.Map;

public static class PastePropertiesKeys
{
    public const string VertexZFloor = "vertex.zfloor";
    public const string VertexZCeiling = "vertex.zceiling";
    public const string VertexFields = "vertex.fields";

    public const string LinedefAction = "linedef.action";
    public const string LinedefArguments = "linedef.arguments";
    public const string LinedefActivation = "linedef.activation";
    public const string LinedefTag = "linedef.tag";
    public const string LinedefFlags = "linedef.flags";
    public const string LinedefFields = "linedef.fields";

    public const string SidedefUpperTexture = "sidedef.uppertexture";
    public const string SidedefMiddleTexture = "sidedef.middletexture";
    public const string SidedefLowerTexture = "sidedef.lowertexture";
    public const string SidedefOffsetX = "sidedef.offsetx";
    public const string SidedefOffsetY = "sidedef.offsety";
    public const string SidedefFlags = "sidedef.flags";
    public const string SidedefFields = "sidedef.fields";

    public const string SectorFloorHeight = "sector.floorheight";
    public const string SectorCeilingHeight = "sector.ceilingheight";
    public const string SectorFloorTexture = "sector.floortexture";
    public const string SectorCeilingTexture = "sector.ceilingtexture";
    public const string SectorBrightness = "sector.brightness";
    public const string SectorTag = "sector.tag";
    public const string SectorSpecial = "sector.special";
    public const string SectorFloorSlope = "sector.floorslope";
    public const string SectorCeilingSlope = "sector.ceilingslope";
    public const string SectorFlags = "sector.flags";
    public const string SectorFields = "sector.fields";

    public const string ThingType = "thing.type";
    public const string ThingAngle = "thing.angle";
    public const string ThingZHeight = "thing.zheight";
    public const string ThingPitch = "thing.pitch";
    public const string ThingRoll = "thing.roll";
    public const string ThingScale = "thing.scale";
    public const string ThingFlags = "thing.flags";
    public const string ThingTag = "thing.tag";
    public const string ThingAction = "thing.action";
    public const string ThingArguments = "thing.arguments";
    public const string ThingFields = "thing.fields";
}

public static class PastePropertiesApplier
{
    public static ISet<string> EnabledKeys(PastePropertiesOptionsResult options)
        => options.Tabs
            .SelectMany(tab => tab.Options)
            .Where(option => option.IsChecked)
            .Select(option => option.Key)
            .ToHashSet(StringComparer.Ordinal);

    public static void Apply(Vertex source, Vertex target, ISet<string> enabledKeys)
    {
        if (enabledKeys.Contains(PastePropertiesKeys.VertexZFloor)) target.ZFloor = source.ZFloor;
        if (enabledKeys.Contains(PastePropertiesKeys.VertexZCeiling)) target.ZCeiling = source.ZCeiling;
        if (enabledKeys.Contains(PastePropertiesKeys.VertexFields)) CopyFields(source.Fields, target.Fields);
    }

    public static void Apply(Linedef source, Linedef target, ISet<string> enabledKeys)
    {
        if (enabledKeys.Contains(PastePropertiesKeys.LinedefFlags)) CopyFlags(source.UdmfFlags, target.UdmfFlags);
        if (enabledKeys.Contains(PastePropertiesKeys.LinedefActivation)) target.Activate = source.Activate;
        if (enabledKeys.Contains(PastePropertiesKeys.LinedefTag)) CopyList(source.Tags, target.Tags);
        if (enabledKeys.Contains(PastePropertiesKeys.LinedefAction)) target.Action = source.Action;
        if (enabledKeys.Contains(PastePropertiesKeys.LinedefArguments))
        {
            CopyArray(source.Args, target.Args);
            CopyField(source.Fields, target.Fields, "arg0str");
        }
        if (enabledKeys.Contains(PastePropertiesKeys.LinedefFields)) CopyFields(source.Fields, target.Fields);

        if (source.Front != null && target.Front != null)
            Apply(source.Front, target.Front, enabledKeys);
        if (source.Back != null && target.Back != null)
            Apply(source.Back, target.Back, enabledKeys);
    }

    public static void Apply(Sidedef source, Sidedef target, ISet<string> enabledKeys)
    {
        if (enabledKeys.Contains(PastePropertiesKeys.SidedefUpperTexture)) target.SetTextureHigh(source.HighTexture);
        if (enabledKeys.Contains(PastePropertiesKeys.SidedefMiddleTexture)) target.SetTextureMid(source.MidTexture);
        if (enabledKeys.Contains(PastePropertiesKeys.SidedefLowerTexture)) target.SetTextureLow(source.LowTexture);
        if (enabledKeys.Contains(PastePropertiesKeys.SidedefOffsetX)) target.OffsetX = source.OffsetX;
        if (enabledKeys.Contains(PastePropertiesKeys.SidedefOffsetY)) target.OffsetY = source.OffsetY;
        if (enabledKeys.Contains(PastePropertiesKeys.SidedefFlags)) CopyFlags(source.UdmfFlags, target.UdmfFlags);
        if (enabledKeys.Contains(PastePropertiesKeys.SidedefFields)) CopyFields(source.Fields, target.Fields);
    }

    public static void Apply(Sector source, Sector target, ISet<string> enabledKeys)
    {
        if (enabledKeys.Contains(PastePropertiesKeys.SectorFloorHeight)) target.FloorHeight = source.FloorHeight;
        if (enabledKeys.Contains(PastePropertiesKeys.SectorCeilingHeight)) target.CeilHeight = source.CeilHeight;
        if (enabledKeys.Contains(PastePropertiesKeys.SectorFloorTexture)) target.SetFloorTexture(source.FloorTexture);
        if (enabledKeys.Contains(PastePropertiesKeys.SectorCeilingTexture)) target.SetCeilTexture(source.CeilTexture);
        if (enabledKeys.Contains(PastePropertiesKeys.SectorBrightness)) target.Brightness = source.Brightness;
        if (enabledKeys.Contains(PastePropertiesKeys.SectorTag)) CopyList(source.Tags, target.Tags);
        if (enabledKeys.Contains(PastePropertiesKeys.SectorSpecial)) target.Special = source.Special;
        if (enabledKeys.Contains(PastePropertiesKeys.SectorFloorSlope))
        {
            target.FloorSlope = source.FloorSlope;
            target.FloorSlopeOffset = source.FloorSlopeOffset;
        }
        if (enabledKeys.Contains(PastePropertiesKeys.SectorCeilingSlope))
        {
            target.CeilSlope = source.CeilSlope;
            target.CeilSlopeOffset = source.CeilSlopeOffset;
        }
        if (enabledKeys.Contains(PastePropertiesKeys.SectorFlags)) CopyFlags(source.UdmfFlags, target.UdmfFlags);
        if (enabledKeys.Contains(PastePropertiesKeys.SectorFields)) CopyFields(source.Fields, target.Fields);
    }

    public static void Apply(Thing source, Thing target, ISet<string> enabledKeys)
    {
        if (enabledKeys.Contains(PastePropertiesKeys.ThingType)) target.Type = source.Type;
        if (enabledKeys.Contains(PastePropertiesKeys.ThingAngle)) target.Angle = source.Angle;
        if (enabledKeys.Contains(PastePropertiesKeys.ThingZHeight)) target.Height = source.Height;
        if (enabledKeys.Contains(PastePropertiesKeys.ThingPitch)) target.Pitch = source.Pitch;
        if (enabledKeys.Contains(PastePropertiesKeys.ThingRoll)) target.Roll = source.Roll;
        if (enabledKeys.Contains(PastePropertiesKeys.ThingScale))
        {
            target.ScaleX = source.ScaleX;
            target.ScaleY = source.ScaleY;
        }
        if (enabledKeys.Contains(PastePropertiesKeys.ThingFlags)) CopyFlags(source.UdmfFlags, target.UdmfFlags);
        if (enabledKeys.Contains(PastePropertiesKeys.ThingTag)) target.Tag = source.Tag;
        if (enabledKeys.Contains(PastePropertiesKeys.ThingAction)) target.Action = source.Action;
        if (enabledKeys.Contains(PastePropertiesKeys.ThingArguments))
        {
            CopyArray(source.Args, target.Args);
            CopyField(source.Fields, target.Fields, "arg0str");
        }
        if (enabledKeys.Contains(PastePropertiesKeys.ThingFields)) CopyFields(source.Fields, target.Fields);
    }

    private static void CopyArray(int[] source, int[] target)
    {
        Array.Clear(target);
        Array.Copy(source, target, Math.Min(source.Length, target.Length));
    }

    private static void CopyList(List<int> source, List<int> target)
    {
        target.Clear();
        target.AddRange(source);
    }

    private static void CopyFlags(HashSet<string> source, HashSet<string> target)
    {
        target.Clear();
        foreach (string flag in source)
            target.Add(flag);
    }

    private static void CopyFields(Dictionary<string, object> source, Dictionary<string, object> target)
    {
        target.Clear();
        foreach (var field in source)
            target[field.Key] = field.Value;
    }

    private static void CopyField(Dictionary<string, object> source, Dictionary<string, object> target, string key)
    {
        if (source.TryGetValue(key, out object? value)) target[key] = value;
        else target.Remove(key);
    }
}
