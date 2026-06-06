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
            Sector sector => config?.LineTagIndicatesSectors == true
                ? SectorToTaggedLinedefs(map, sector)
                : Array.Empty<EventLineAssociation>(),
            Thing thing => config?.LineTagIndicatesSectors == true
                ? Array.Empty<EventLineAssociation>()
                : ActionArgAssociations(map, thing, config),
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

    private static IReadOnlyDictionary<UniversalType, HashSet<int>> ActionArgTagsByType(
        int action,
        int[] actionArgs,
        GameConfiguration? config)
    {
        if (action <= 0 || config?.GetLinedefAction(action)?.Args is not { Length: > 0 } args)
            return new Dictionary<UniversalType, HashSet<int>>();

        var tagsByType = new Dictionary<UniversalType, HashSet<int>>();
        int count = Math.Min(Math.Min(actionArgs.Length, args.Length), 5);
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
