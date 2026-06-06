// ABOUTME: Plans UDB-style event-line associations between tagged map elements.
// ABOUTME: Handles Doom-format linedef tags that target sectors when configured.

using DBuilder.Map;

namespace DBuilder.IO;

public enum EventLineElementKind
{
    Linedef,
    Sector,
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
            associations.AddRange(LinedefToTaggedSectors(map, line));
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

    private static HashSet<int> PositiveTags(IReadOnlyList<int> tags)
    {
        var positive = new HashSet<int>();
        foreach (int tag in tags)
            if (tag > 0) positive.Add(tag);
        return positive;
    }
}
