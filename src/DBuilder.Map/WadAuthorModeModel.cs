// ABOUTME: Models UDB WadAuthorMode hover highlighting priority and range checks.
// ABOUTME: Keeps classic mode hit-testing rules independent from renderer and popup UI code.

using DBuilder.Geometry;

namespace DBuilder.Map;

public enum WadAuthorHighlightKind
{
    None,
    Vertex,
    Linedef,
    Sector,
    Thing,
}

public enum WadAuthorLinedefPopupAction
{
    Properties,
    Delete,
    Split,
    Flip,
    Curve,
}

public readonly record struct WadAuthorHighlight(WadAuthorHighlightKind Kind, object? Target)
{
    public static WadAuthorHighlight None => new(WadAuthorHighlightKind.None, null);
}

public sealed record WadAuthorLinedefPopupItem(string Title, WadAuthorLinedefPopupAction? Action);

public sealed record WadAuthorModeDescriptor(
    string DisplayName,
    string SwitchAction,
    string ButtonImage,
    int ButtonOrder,
    string ButtonGroup,
    bool UseByDefault,
    bool SafeStartMode);

public static class WadAuthorModeModel
{
    public const double LinedefHighlightRange = 10.0;
    public const double VertexHighlightRange = 8.0;
    public const double ThingHighlightRange = 2.0;

    public static WadAuthorModeDescriptor ModeDescriptor { get; } = new(
        "WadAuthor Mode",
        "wadauthormode",
        "WAuthor.png",
        int.MinValue + 400,
        "000_editing",
        UseByDefault: true,
        SafeStartMode: true);

    public static IReadOnlyList<WadAuthorLinedefPopupItem> LinedefPopupItems { get; } =
    [
        new("Properties...", WadAuthorLinedefPopupAction.Properties),
        new("", null),
        new("Delete", WadAuthorLinedefPopupAction.Delete),
        new("Split", WadAuthorLinedefPopupAction.Split),
        new("Flip", WadAuthorLinedefPopupAction.Flip),
        new("Curve...", WadAuthorLinedefPopupAction.Curve),
    ];

    public static void EnterMode(MapSet map)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        map.ConvertSelection(SelectionType.Sectors, SelectionType.Linedefs);
    }

    public static void LeaveMode(MapSet map)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        map.ClearAllSelected();
    }

    public static WadAuthorHighlight PickHighlight(MapSet map, Vector2D mouseMapPosition, double rendererScale = 1.0)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));

        double scale = rendererScale <= 0.0 ? 1.0 : rendererScale;
        Vertex? vertex = map.NearestVertexSquareRange(mouseMapPosition, VertexHighlightRange / scale);
        Thing? thing = map.NearestThingSquareRange(mouseMapPosition, ThingHighlightRange / scale);

        if (vertex != null && thing != null)
            return vertex.DistanceToSq(mouseMapPosition) < thing.DistanceToSq(mouseMapPosition)
                ? new WadAuthorHighlight(WadAuthorHighlightKind.Vertex, vertex)
                : new WadAuthorHighlight(WadAuthorHighlightKind.Thing, thing);

        if (vertex != null) return new WadAuthorHighlight(WadAuthorHighlightKind.Vertex, vertex);
        if (thing != null) return new WadAuthorHighlight(WadAuthorHighlightKind.Thing, thing);

        Linedef? line = map.NearestLinedef(mouseMapPosition);
        if (line == null) return WadAuthorHighlight.None;

        double lineDistance = line.DistanceTo(mouseMapPosition, bounded: true);
        if (lineDistance < LinedefHighlightRange / scale)
            return new WadAuthorHighlight(WadAuthorHighlightKind.Linedef, line);

        Sector? sector = SectorFromNearestLineSide(line, mouseMapPosition);
        return sector != null
            ? new WadAuthorHighlight(WadAuthorHighlightKind.Sector, sector)
            : WadAuthorHighlight.None;
    }

    public static Sector? SectorFromNearestLineSide(Linedef line, Vector2D mouseMapPosition)
    {
        if (line == null) throw new ArgumentNullException(nameof(line));

        double side = line.SideOfLine(mouseMapPosition);
        return side > 0.0 ? line.Back?.Sector : line.Front?.Sector;
    }
}
