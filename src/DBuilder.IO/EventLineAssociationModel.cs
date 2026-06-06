// ABOUTME: Plans UDB-style event-line associations between tagged map elements.
// ABOUTME: Handles Doom-format linedef tags that target sectors when configured.

using DBuilder.Map;

namespace DBuilder.IO;

public enum EventLineElementKind
{
    Linedef,
    Sector,
    Thing,
}

public sealed record EventLineAssociation(
    EventLineElementKind SourceKind,
    int SourceIndex,
    EventLineElementKind TargetKind,
    int TargetIndex,
    int Tag);

public static class EventLineAssociationModel
{
    public static IReadOnlyList<EventLineAssociation> ForElement(
        MapSet map,
        object element,
        GameConfiguration? config)
    {
        return element switch
        {
            Linedef line => LinedefAssociations(map, line, config),
            Sector sector => SectorAssociations(map, sector, config),
            Thing thing => config?.LineTagIndicatesSectors == true
                ? Array.Empty<EventLineAssociation>()
                : ThingAssociations(map, thing, config),
            _ => Array.Empty<EventLineAssociation>(),
        };
    }

    private static IReadOnlyList<EventLineAssociation> LinedefAssociations(
        MapSet map,
        Linedef line,
        GameConfiguration? config)
    {
        var associations = new List<EventLineAssociation>();
        associations.AddRange(LinedefToTaggedLinedefs(map, line, config));
        if (config?.LineTagIndicatesSectors == true)
        {
            associations.AddRange(LinedefToTaggedSectors(map, line));
            return associations;
        }

        associations.AddRange(ActionArgAssociations(map, line, config));
        associations.AddRange(ReverseActionArgAssociations(
            map,
            EventLineElementKind.Linedef,
            line.Index,
            UniversalType.LinedefTag,
            PositiveTags(line.Tags),
            config,
            sourceLine: line,
            sourceThing: null));
        associations.AddRange(SectorFieldAssociations(
            map,
            EventLineElementKind.Linedef,
            line.Index,
            line.Fields,
            config,
            sourceSector: null));
        return associations;
    }

    private static IReadOnlyList<EventLineAssociation> SectorAssociations(
        MapSet map,
        Sector sector,
        GameConfiguration? config)
    {
        if (config?.LineTagIndicatesSectors == true)
            return SectorToTaggedLinedefs(map, sector);

        var associations = new List<EventLineAssociation>();
        associations.AddRange(ReverseActionArgAssociations(
            map,
            EventLineElementKind.Sector,
            sector.Index,
            UniversalType.SectorTag,
            PositiveTags(sector.Tags),
            config,
            sourceLine: null,
            sourceThing: null));
        associations.AddRange(SectorFieldAssociations(
            map,
            EventLineElementKind.Sector,
            sector.Index,
            sector.Fields,
            config,
            sourceSector: sector));
        return associations;
    }

    private static IReadOnlyList<EventLineAssociation> ThingAssociations(
        MapSet map,
        Thing thing,
        GameConfiguration? config)
    {
        var associations = new List<EventLineAssociation>();
        associations.AddRange(ThingForwardAssociations(map, thing, config));
        associations.AddRange(ThingDirectLinkAssociations(map, thing, config));

        HashSet<int> tags = thing.Tag > 0 ? new HashSet<int> { thing.Tag } : new HashSet<int>();
        associations.AddRange(ReverseActionArgAssociations(
            map,
            EventLineElementKind.Thing,
            thing.Index,
            UniversalType.ThingTag,
            tags,
            config,
            sourceLine: null,
            sourceThing: thing));
        associations.AddRange(SectorFieldAssociations(
            map,
            EventLineElementKind.Thing,
            thing.Index,
            thing.Fields,
            config,
            sourceSector: null));
        return associations;
    }

    private static IReadOnlyList<EventLineAssociation> ThingDirectLinkAssociations(
        MapSet map,
        Thing source,
        GameConfiguration? config)
    {
        if (source.Tag <= 0) return Array.Empty<EventLineAssociation>();
        int directLinkType = config?.GetThing(source.Type)?.ThingLink ?? 0;
        if (directLinkType <= 0) return Array.Empty<EventLineAssociation>();

        var associations = new List<EventLineAssociation>();
        foreach (Thing target in map.Things)
        {
            if (ReferenceEquals(source, target)) continue;
            if (target.Type != directLinkType || target.Tag != source.Tag) continue;
            if (target.Action <= 0 || config?.GetLinedefAction(target.Action) == null) continue;
            associations.Add(new EventLineAssociation(
                EventLineElementKind.Thing,
                source.Index,
                EventLineElementKind.Thing,
                target.Index,
                source.Tag));
        }

        return associations;
    }

    private static IReadOnlyList<EventLineAssociation> ThingForwardAssociations(
        MapSet map,
        Thing thing,
        GameConfiguration? config)
    {
        IReadOnlyDictionary<UniversalType, HashSet<int>> tagsByType =
            ThingForwardTagsByType(thing, config);
        if (tagsByType.Count == 0) return Array.Empty<EventLineAssociation>();

        var associations = new List<EventLineAssociation>();
        AddTaggedSectors(associations, map, EventLineElementKind.Thing, thing.Index, tagsByType);
        AddTaggedLinedefs(associations, map, EventLineElementKind.Thing, thing.Index, null, tagsByType);
        AddTaggedThings(associations, map, EventLineElementKind.Thing, thing.Index, thing, tagsByType);
        return associations;
    }

    private static IReadOnlyList<EventLineAssociation> LinedefToTaggedLinedefs(
        MapSet map,
        Linedef line,
        GameConfiguration? config)
    {
        if (line.Action <= 0 || config?.GetLinedefAction(line.Action) is not { LineToLineTag: true } action)
            return Array.Empty<EventLineAssociation>();

        HashSet<int> tags = PositiveTags(line.Tags);
        if (tags.Count == 0) return Array.Empty<EventLineAssociation>();

        var associations = new List<EventLineAssociation>();
        foreach (Linedef other in map.Linedefs)
        {
            if (ReferenceEquals(line, other)) continue;
            int tag = other.Tag;
            if (tag <= 0 || !tags.Contains(tag)) continue;
            if (action.LineToLineSameAction && line.Action != other.Action) continue;

            associations.Add(new EventLineAssociation(
                EventLineElementKind.Linedef,
                line.Index,
                EventLineElementKind.Linedef,
                other.Index,
                tag));
        }

        return associations;
    }

    private static IReadOnlyList<EventLineAssociation> LinedefToTaggedSectors(MapSet map, Linedef line)
    {
        HashSet<int> tags = PositiveTags(line.Tags);
        if (tags.Count == 0) return Array.Empty<EventLineAssociation>();

        var associations = new List<EventLineAssociation>();
        foreach (Sector sector in map.Sectors)
        {
            int tag = sector.Tag;
            if (!tags.Contains(tag)) continue;

            associations.Add(new EventLineAssociation(
                EventLineElementKind.Linedef,
                line.Index,
                EventLineElementKind.Sector,
                sector.Index,
                tag));
        }

        return associations;
    }

    private static IReadOnlyList<EventLineAssociation> SectorToTaggedLinedefs(MapSet map, Sector sector)
    {
        HashSet<int> tags = PositiveTags(sector.Tags);
        if (tags.Count == 0) return Array.Empty<EventLineAssociation>();

        var associations = new List<EventLineAssociation>();
        foreach (Linedef line in map.Linedefs)
        {
            int tag = line.Tag;
            if (!tags.Contains(tag)) continue;

            associations.Add(new EventLineAssociation(
                EventLineElementKind.Sector,
                sector.Index,
                EventLineElementKind.Linedef,
                line.Index,
                tag));
        }

        return associations;
    }

    private static IReadOnlyList<EventLineAssociation> ActionArgAssociations(
        MapSet map,
        Linedef line,
        GameConfiguration? config)
    {
        IReadOnlyDictionary<UniversalType, HashSet<int>> tagsByType =
            ActionArgTagsByType(line.Action, line.Args, config);
        if (tagsByType.Count == 0) return Array.Empty<EventLineAssociation>();

        var associations = new List<EventLineAssociation>();
        AddTaggedSectors(associations, map, EventLineElementKind.Linedef, line.Index, tagsByType);
        AddTaggedLinedefs(associations, map, EventLineElementKind.Linedef, line.Index, line, tagsByType);
        AddTaggedThings(associations, map, EventLineElementKind.Linedef, line.Index, null, tagsByType);
        return associations;
    }

    private static IReadOnlyList<EventLineAssociation> ActionArgAssociations(
        MapSet map,
        Thing thing,
        GameConfiguration? config)
    {
        IReadOnlyDictionary<UniversalType, HashSet<int>> tagsByType =
            ActionArgTagsByType(thing.Action, thing.Args, config);
        if (tagsByType.Count == 0) return Array.Empty<EventLineAssociation>();

        var associations = new List<EventLineAssociation>();
        AddTaggedSectors(associations, map, EventLineElementKind.Thing, thing.Index, tagsByType);
        AddTaggedLinedefs(associations, map, EventLineElementKind.Thing, thing.Index, null, tagsByType);
        AddTaggedThings(associations, map, EventLineElementKind.Thing, thing.Index, thing, tagsByType);
        return associations;
    }

    private static IReadOnlyDictionary<UniversalType, HashSet<int>> ThingForwardTagsByType(
        Thing thing,
        GameConfiguration? config)
    {
        if (thing.Action > 0) return ActionArgTagsByType(thing.Action, thing.Args, config);
        ThingTypeInfo? info = config?.GetThing(thing.Type);
        if (info == null || info.ThingLink < 0 || Math.Abs(info.ThingLink) == thing.Type)
            return new Dictionary<UniversalType, HashSet<int>>();

        return ArgsTagsByType(thing.Args, info.Args);
    }

    private static IReadOnlyDictionary<UniversalType, HashSet<int>> ActionArgTagsByType(
        int action,
        int[] actionArgs,
        GameConfiguration? config)
    {
        if (action <= 0 || config?.GetLinedefAction(action)?.Args is not { Length: > 0 } args)
            return new Dictionary<UniversalType, HashSet<int>>();

        return ArgsTagsByType(actionArgs, args);
    }

    private static IReadOnlyDictionary<UniversalType, HashSet<int>> ArgsTagsByType(
        int[] actionArgs,
        IReadOnlyList<ArgInfo> args)
    {
        var tagsByType = new Dictionary<UniversalType, HashSet<int>>();
        int count = Math.Min(Math.Min(actionArgs.Length, args.Count), 5);
        for (int i = 0; i < count; i++)
        {
            if (actionArgs[i] <= 0) continue;
            if ((UniversalType)args[i].Type is not (UniversalType.SectorTag or UniversalType.LinedefTag or UniversalType.ThingTag))
                continue;

            var type = (UniversalType)args[i].Type;
            if (!tagsByType.TryGetValue(type, out HashSet<int>? tags))
            {
                tags = new HashSet<int>();
                tagsByType[type] = tags;
            }

            tags.Add(actionArgs[i]);
        }

        return tagsByType;
    }

    private static IReadOnlyList<EventLineAssociation> ReverseActionArgAssociations(
        MapSet map,
        EventLineElementKind sourceKind,
        int sourceIndex,
        UniversalType sourceTagType,
        HashSet<int> sourceTags,
        GameConfiguration? config,
        Linedef? sourceLine,
        Thing? sourceThing)
    {
        if (sourceTags.Count == 0) return Array.Empty<EventLineAssociation>();

        var associations = new List<EventLineAssociation>();
        foreach (Linedef line in map.Linedefs)
        {
            if (sourceLine != null && ReferenceEquals(sourceLine, line)) continue;
            if (FirstMatchingActionArg(line.Action, line.Args, sourceTagType, sourceTags, config) is not { } tag) continue;
            associations.Add(new EventLineAssociation(sourceKind, sourceIndex, EventLineElementKind.Linedef, line.Index, tag));
        }

        foreach (Thing thing in map.Things)
        {
            if (sourceThing != null && ReferenceEquals(sourceThing, thing)) continue;
            if (ShouldSkipChildLinkThingAssociation(sourceThing, thing, config)) continue;
            if (FirstMatchingThingArg(thing, sourceTagType, sourceTags, config) is not { } tag) continue;
            associations.Add(new EventLineAssociation(sourceKind, sourceIndex, EventLineElementKind.Thing, thing.Index, tag));
        }

        return associations;
    }

    private static bool ShouldSkipChildLinkThingAssociation(
        Thing? sourceThing,
        Thing targetThing,
        GameConfiguration? config)
    {
        if (sourceThing == null || config?.GetThing(targetThing.Type) == null) return false;
        int directLinkType = config.GetThing(sourceThing.Type)?.ThingLink ?? 0;
        return directLinkType < 0 && directLinkType != -targetThing.Type;
    }

    private static IReadOnlyList<EventLineAssociation> SectorFieldAssociations(
        MapSet map,
        EventLineElementKind sourceKind,
        int sourceIndex,
        IReadOnlyDictionary<string, object> sourceFields,
        GameConfiguration? config,
        Sector? sourceSector)
    {
        if (config == null || !config.UniversalFields.TryGetValue("sector", out var fields))
            return Array.Empty<EventLineAssociation>();

        var associations = new List<EventLineAssociation>();
        foreach (UniversalFieldInfo field in fields.Values)
        {
            if (field.Associations.Count == 0) continue;
            if (!sourceFields.TryGetValue(field.Name, out object? sourceValue)) continue;

            foreach (UniversalFieldAssociationInfo association in field.Associations.Values)
            {
                if (association.NeverShowEventLines) continue;

                foreach (Sector other in map.Sectors)
                {
                    if (sourceSector != null && ReferenceEquals(sourceSector, other)) continue;
                    if (!other.Fields.TryGetValue(association.Property, out object? targetValue)) continue;
                    if (!FieldAssociationValuesMatch(targetValue, sourceValue, field, association)) continue;

                    associations.Add(new EventLineAssociation(
                        sourceKind,
                        sourceIndex,
                        EventLineElementKind.Sector,
                        other.Index,
                        FieldAssociationTag(field.Type, targetValue)));
                }
            }
        }

        return associations;
    }

    private static bool FieldAssociationValuesMatch(
        object? targetValue,
        object? sourceValue,
        UniversalFieldInfo field,
        UniversalFieldAssociationInfo association)
    {
        if (targetValue == null || sourceValue == null) return false;

        return (UniversalType)field.Type switch
        {
            UniversalType.Float or UniversalType.AngleDegreesFloat or UniversalType.AngleRadians
                => NumericFieldValuesMatch(
                    Convert.ToDouble(targetValue),
                    Convert.ToDouble(sourceValue),
                    field.DefaultValue,
                    association.Modify),
            UniversalType.Integer or UniversalType.AngleDegrees or UniversalType.AngleByte or UniversalType.Color
                or UniversalType.EnumBits or UniversalType.EnumOption or UniversalType.LinedefTag
                or UniversalType.LinedefType or UniversalType.SectorEffect or UniversalType.SectorTag
                or UniversalType.ThingTag or UniversalType.ThingType
                => IntegerFieldValuesMatch(
                    Convert.ToInt32(targetValue),
                    Convert.ToInt32(sourceValue),
                    field.DefaultValue,
                    association.Modify),
            UniversalType.Boolean => Convert.ToBoolean(targetValue) == Convert.ToBoolean(sourceValue),
            UniversalType.Flat or UniversalType.String or UniversalType.Texture or UniversalType.EnumStrings
                or UniversalType.ThingClass
                => string.Equals(Convert.ToString(targetValue), Convert.ToString(sourceValue), StringComparison.Ordinal),
            _ => false,
        };
    }

    private static bool NumericFieldValuesMatch(double targetValue, double sourceValue, object? defaultValue, string modify)
    {
        if (string.Equals(modify, "abs", StringComparison.OrdinalIgnoreCase))
        {
            targetValue = Math.Abs(targetValue);
            sourceValue = Math.Abs(sourceValue);
        }

        if (targetValue != sourceValue) return false;
        return defaultValue == null || targetValue != Convert.ToDouble(defaultValue);
    }

    private static bool IntegerFieldValuesMatch(int targetValue, int sourceValue, object? defaultValue, string modify)
    {
        if (string.Equals(modify, "abs", StringComparison.OrdinalIgnoreCase))
        {
            targetValue = Math.Abs(targetValue);
            sourceValue = Math.Abs(sourceValue);
        }

        if (targetValue != sourceValue) return false;
        return defaultValue == null || targetValue != Convert.ToInt32(defaultValue);
    }

    private static int FieldAssociationTag(int fieldType, object? value)
    {
        return (UniversalType)fieldType switch
        {
            UniversalType.Float or UniversalType.AngleDegreesFloat or UniversalType.AngleRadians
                => (int)Math.Round(Convert.ToDouble(value)),
            UniversalType.Boolean => Convert.ToBoolean(value) ? 1 : 0,
            UniversalType.Flat or UniversalType.String or UniversalType.Texture or UniversalType.EnumStrings
                or UniversalType.ThingClass => 0,
            _ => Convert.ToInt32(value),
        };
    }

    private static int? FirstMatchingActionArg(
        int action,
        int[] actionArgs,
        UniversalType tagType,
        HashSet<int> sourceTags,
        GameConfiguration? config)
    {
        if (action <= 0 || config?.GetLinedefAction(action)?.Args is not { Length: > 0 } args)
            return null;

        int count = Math.Min(Math.Min(actionArgs.Length, args.Length), 5);
        for (int i = 0; i < count; i++)
        {
            int value = actionArgs[i];
            if (value <= 0) continue;
            if ((UniversalType)args[i].Type != tagType) continue;
            if (sourceTags.Contains(value)) return value;
        }

        return null;
    }

    private static int? FirstMatchingThingArg(
        Thing thing,
        UniversalType tagType,
        HashSet<int> sourceTags,
        GameConfiguration? config)
    {
        if (thing.Action > 0)
            return FirstMatchingActionArg(thing.Action, thing.Args, tagType, sourceTags, config);

        ThingTypeInfo? info = config?.GetThing(thing.Type);
        if (info == null || info.ThingLink < 0 || Math.Abs(info.ThingLink) == thing.Type)
            return null;

        return FirstMatchingArg(thing.Args, info.Args, tagType, sourceTags);
    }

    private static int? FirstMatchingArg(
        int[] actionArgs,
        IReadOnlyList<ArgInfo> args,
        UniversalType tagType,
        HashSet<int> sourceTags)
    {
        int count = Math.Min(Math.Min(actionArgs.Length, args.Count), 5);
        for (int i = 0; i < count; i++)
        {
            int value = actionArgs[i];
            if (value <= 0) continue;
            if ((UniversalType)args[i].Type != tagType) continue;
            if (sourceTags.Contains(value)) return value;
        }

        return null;
    }

    private static void AddTaggedSectors(
        List<EventLineAssociation> associations,
        MapSet map,
        EventLineElementKind sourceKind,
        int sourceIndex,
        IReadOnlyDictionary<UniversalType, HashSet<int>> tagsByType)
    {
        if (!tagsByType.TryGetValue(UniversalType.SectorTag, out HashSet<int>? tags)) return;

        foreach (Sector sector in map.Sectors)
        {
            if (!TryFirstMatchingTag(sector.Tags, tags, out int tag)) continue;
            associations.Add(new EventLineAssociation(sourceKind, sourceIndex, EventLineElementKind.Sector, sector.Index, tag));
        }
    }

    private static void AddTaggedLinedefs(
        List<EventLineAssociation> associations,
        MapSet map,
        EventLineElementKind sourceKind,
        int sourceIndex,
        Linedef? sourceLine,
        IReadOnlyDictionary<UniversalType, HashSet<int>> tagsByType)
    {
        if (!tagsByType.TryGetValue(UniversalType.LinedefTag, out HashSet<int>? tags)) return;

        foreach (Linedef line in map.Linedefs)
        {
            if (sourceLine != null && ReferenceEquals(sourceLine, line)) continue;
            if (!TryFirstMatchingTag(line.Tags, tags, out int tag)) continue;
            associations.Add(new EventLineAssociation(sourceKind, sourceIndex, EventLineElementKind.Linedef, line.Index, tag));
        }
    }

    private static void AddTaggedThings(
        List<EventLineAssociation> associations,
        MapSet map,
        EventLineElementKind sourceKind,
        int sourceIndex,
        Thing? sourceThing,
        IReadOnlyDictionary<UniversalType, HashSet<int>> tagsByType)
    {
        if (!tagsByType.TryGetValue(UniversalType.ThingTag, out HashSet<int>? tags)) return;

        foreach (Thing thing in map.Things)
        {
            if (sourceThing != null && ReferenceEquals(sourceThing, thing)) continue;
            int tag = thing.Tag;
            if (!tags.Contains(tag)) continue;
            associations.Add(new EventLineAssociation(sourceKind, sourceIndex, EventLineElementKind.Thing, thing.Index, tag));
        }
    }

    private static HashSet<int> PositiveTags(IReadOnlyList<int> tags)
    {
        var positive = new HashSet<int>();
        foreach (int tag in tags)
            if (tag > 0) positive.Add(tag);
        return positive;
    }

    private static bool TryFirstMatchingTag(IReadOnlyList<int> elementTags, HashSet<int> targetTags, out int tag)
    {
        foreach (int elementTag in elementTags)
        {
            if (elementTag > 0 && targetTags.Contains(elementTag))
            {
                tag = elementTag;
                return true;
            }
        }

        tag = 0;
        return false;
    }
}
