// ABOUTME: UDB-style select-similar property matching for map elements.
// ABOUTME: Compares selected elements against unselected candidates using configurable property flags.

namespace DBuilder.Map;

public sealed class VertexSimilarityOptions
{
    public bool ZFloor { get; init; } = true;
    public bool ZCeiling { get; init; } = true;
    public bool Fields { get; init; } = true;
}

public sealed class SectorSimilarityOptions
{
    public bool FloorHeight { get; init; } = true;
    public bool CeilingHeight { get; init; } = true;
    public bool FloorTexture { get; init; } = true;
    public bool CeilingTexture { get; init; } = true;
    public bool Brightness { get; init; } = true;
    public bool Special { get; init; } = true;
    public bool Tags { get; init; } = true;
    public bool Slopes { get; init; } = true;
    public bool Flags { get; init; } = true;
    public bool Fields { get; init; } = true;
}

public sealed class SidedefSimilarityOptions
{
    public bool UpperTexture { get; init; } = true;
    public bool MiddleTexture { get; init; } = true;
    public bool LowerTexture { get; init; } = true;
    public bool OffsetX { get; init; } = true;
    public bool OffsetY { get; init; } = true;
    public bool Flags { get; init; } = true;
    public bool Fields { get; init; } = true;
}

public sealed class LinedefSimilarityOptions
{
    public bool Action { get; init; } = true;
    public bool Arguments { get; init; } = true;
    public bool Activation { get; init; } = true;
    public bool Tags { get; init; } = true;
    public bool Flags { get; init; } = true;
    public bool Fields { get; init; } = true;
}

public sealed class ThingSimilarityOptions
{
    public bool Type { get; init; } = true;
    public bool Angle { get; init; } = true;
    public bool Height { get; init; } = true;
    public bool Pitch { get; init; } = true;
    public bool Roll { get; init; } = true;
    public bool Scale { get; init; } = true;
    public bool Action { get; init; } = true;
    public bool Arguments { get; init; } = true;
    public bool Tag { get; init; } = true;
    public bool Flags { get; init; } = true;
    public bool Conversation { get; init; } = true;
    public bool Fields { get; init; } = true;
}

public static class SelectSimilar
{
    public static int SelectVertices(MapSet map, VertexSimilarityOptions? options = null)
    {
        options ??= new VertexSimilarityOptions();
        return SelectSimilarElements(
            map.GetSelectedVertices(),
            map.GetSelectedVertices(selected: false),
            (source, target) => PropertiesMatch(options, source, target));
    }

    public static int SelectSectors(MapSet map, SectorSimilarityOptions? options = null)
    {
        options ??= new SectorSimilarityOptions();
        return SelectSimilarElements(
            map.GetSelectedSectors(),
            map.GetSelectedSectors(selected: false),
            (source, target) => PropertiesMatch(options, source, target));
    }

    public static int SelectLinedefs(
        MapSet map,
        LinedefSimilarityOptions? linedefOptions = null,
        SidedefSimilarityOptions? sidedefOptions = null)
    {
        linedefOptions ??= new LinedefSimilarityOptions();
        sidedefOptions ??= new SidedefSimilarityOptions();
        return SelectSimilarElements(
            map.GetSelectedLinedefs(),
            map.GetSelectedLinedefs(selected: false),
            (source, target) => PropertiesMatch(linedefOptions, sidedefOptions, source, target));
    }

    public static int SelectThings(MapSet map, ThingSimilarityOptions? options = null)
    {
        options ??= new ThingSimilarityOptions();
        return SelectSimilarElements(
            map.GetSelectedThings(),
            map.GetSelectedThings(selected: false),
            (source, target) => PropertiesMatch(options, source, target));
    }

    public static bool PropertiesMatch(VertexSimilarityOptions options, Vertex source, Vertex target)
    {
        if (options.ZCeiling && !NumbersMatch(source.ZCeiling, target.ZCeiling)) return false;
        if (options.ZFloor && !NumbersMatch(source.ZFloor, target.ZFloor)) return false;
        return !options.Fields || FieldsMatch(source.Fields, target.Fields);
    }

    public static bool PropertiesMatch(SectorSimilarityOptions options, Sector source, Sector target)
    {
        if (options.FloorHeight && source.FloorHeight != target.FloorHeight) return false;
        if (options.CeilingHeight && source.CeilHeight != target.CeilHeight) return false;
        if (options.FloorTexture && !TextMatches(source.FloorTexture, target.FloorTexture)) return false;
        if (options.CeilingTexture && !TextMatches(source.CeilTexture, target.CeilTexture)) return false;
        if (options.Brightness && source.Brightness != target.Brightness) return false;
        if (options.Special && source.Special != target.Special) return false;
        if (options.Tags && !TagsMatch(source.Tags, target.Tags)) return false;
        if (options.Slopes && !SlopesMatch(source, target)) return false;
        if (options.Flags && !SetMatches(source.UdmfFlags, target.UdmfFlags)) return false;
        return !options.Fields || FieldsMatch(source.Fields, target.Fields);
    }

    public static bool PropertiesMatch(
        LinedefSimilarityOptions linedefOptions,
        SidedefSimilarityOptions sidedefOptions,
        Linedef source,
        Linedef target)
    {
        if (linedefOptions.Action && source.Action != target.Action) return false;
        if (linedefOptions.Activation && source.Activate != target.Activate) return false;
        if (linedefOptions.Tags && !TagsMatch(source.Tags, target.Tags)) return false;
        if (linedefOptions.Arguments && !ArgsMatch(source.Args, target.Args, source.Fields, target.Fields)) return false;
        if (linedefOptions.Flags && (source.Flags != target.Flags || !SetMatches(source.UdmfFlags, target.UdmfFlags))) return false;
        if (linedefOptions.Fields && !FieldsMatch(source.Fields, target.Fields)) return false;

        return SidedefsMatch(sidedefOptions, source.Front, target.Front) ||
            SidedefsMatch(sidedefOptions, source.Front, target.Back) ||
            SidedefsMatch(sidedefOptions, source.Back, target.Front) ||
            SidedefsMatch(sidedefOptions, source.Back, target.Back);
    }

    public static bool PropertiesMatch(SidedefSimilarityOptions options, Sidedef source, Sidedef target)
    {
        if (options.OffsetX && source.OffsetX != target.OffsetX) return false;
        if (options.OffsetY && source.OffsetY != target.OffsetY) return false;
        if (options.UpperTexture && !TextMatches(source.HighTexture, target.HighTexture)) return false;
        if (options.MiddleTexture && !TextMatches(source.MidTexture, target.MidTexture)) return false;
        if (options.LowerTexture && !TextMatches(source.LowTexture, target.LowTexture)) return false;
        if (options.Flags && !SetMatches(source.UdmfFlags, target.UdmfFlags)) return false;
        return !options.Fields || FieldsMatch(source.Fields, target.Fields);
    }

    public static bool PropertiesMatch(ThingSimilarityOptions options, Thing source, Thing target)
    {
        if (options.Type && source.Type != target.Type) return false;
        if (options.Angle && source.Angle != target.Angle) return false;
        if (options.Height && !NumbersMatch(source.Height, target.Height)) return false;
        if (options.Pitch && source.Pitch != target.Pitch) return false;
        if (options.Roll && source.Roll != target.Roll) return false;
        if (options.Scale && (!NumbersMatch(source.ScaleX, target.ScaleX) || !NumbersMatch(source.ScaleY, target.ScaleY))) return false;
        if (options.Action && source.Action != target.Action) return false;
        if (options.Arguments && !ArgsMatch(source.Args, target.Args, source.Fields, target.Fields)) return false;
        if (options.Tag && source.Tag != target.Tag) return false;
        if (options.Flags && (source.Flags != target.Flags || !SetMatches(source.UdmfFlags, target.UdmfFlags))) return false;
        if (options.Conversation && !FieldValueMatches(source.Fields, target.Fields, "conversation")) return false;
        return !options.Fields || FieldsMatch(source.Fields, target.Fields);
    }

    private static int SelectSimilarElements<T>(
        IReadOnlyList<T> selected,
        IReadOnlyList<T> unselected,
        Func<T, T, bool> matches)
        where T : ISelectable
    {
        if (selected.Count == 0) return 0;

        int changed = 0;
        foreach (var target in unselected)
        {
            foreach (var source in selected)
            {
                if (!matches(source, target)) continue;

                target.Selected = true;
                changed++;
                break;
            }
        }
        return changed;
    }

    private static bool SidedefsMatch(SidedefSimilarityOptions options, Sidedef? source, Sidedef? target)
        => source != null && target != null && PropertiesMatch(options, source, target);

    private static bool SlopesMatch(Sector source, Sector target)
        => source.FloorSlope == target.FloorSlope &&
            NumbersMatch(source.FloorSlopeOffset, target.FloorSlopeOffset) &&
            source.CeilSlope == target.CeilSlope &&
            NumbersMatch(source.CeilSlopeOffset, target.CeilSlopeOffset);

    private static bool ArgsMatch(
        IReadOnlyList<int> sourceArgs,
        IReadOnlyList<int> targetArgs,
        IReadOnlyDictionary<string, object> sourceFields,
        IReadOnlyDictionary<string, object> targetFields)
    {
        if (sourceArgs.Count != targetArgs.Count) return false;
        for (int i = 0; i < sourceArgs.Count; i++)
            if (sourceArgs[i] != targetArgs[i]) return false;

        for (int i = 0; i < 5; i++)
            if (!FieldValueMatches(sourceFields, targetFields, "arg" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "str")) return false;

        return true;
    }

    private static bool TagsMatch(IReadOnlyList<int> source, IReadOnlyList<int> target)
    {
        if (source.Count != target.Count) return false;

        var counts = new Dictionary<int, int>();
        foreach (int tag in source)
            counts[tag] = counts.TryGetValue(tag, out int count) ? count + 1 : 1;

        foreach (int tag in target)
        {
            if (!counts.TryGetValue(tag, out int count)) return false;
            if (count == 1) counts.Remove(tag);
            else counts[tag] = count - 1;
        }

        return counts.Count == 0;
    }

    private static bool SetMatches(HashSet<string> source, HashSet<string> target)
        => source.SetEquals(target);

    private static bool FieldsMatch(IReadOnlyDictionary<string, object> source, IReadOnlyDictionary<string, object> target)
    {
        if (source.Count != target.Count) return false;

        foreach (var field in source)
        {
            if (!target.TryGetValue(field.Key, out var targetValue)) return false;
            if (!ValuesMatch(field.Value, targetValue)) return false;
        }

        return true;
    }

    private static bool FieldValueMatches(
        IReadOnlyDictionary<string, object> source,
        IReadOnlyDictionary<string, object> target,
        string key)
    {
        bool sourceHas = source.TryGetValue(key, out var sourceValue);
        bool targetHas = target.TryGetValue(key, out var targetValue);
        if (sourceHas != targetHas) return false;
        return !sourceHas || ValuesMatch(sourceValue!, targetValue!);
    }

    private static bool TextMatches(string source, string target)
        => string.Equals(source, target, StringComparison.OrdinalIgnoreCase);

    private static bool ValuesMatch(object source, object target)
    {
        if (source is string sourceText && target is string targetText)
            return TextMatches(sourceText, targetText);

        return source.Equals(target);
    }

    private static bool NumbersMatch(double source, double target)
        => source.Equals(target) || (double.IsNaN(source) && double.IsNaN(target));
}
