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

public enum WadAuthorHighlightRenderSurface
{
    None,
    Plotter,
    Things,
}

public readonly record struct WadAuthorHighlight(WadAuthorHighlightKind Kind, object? Target)
{
    public static WadAuthorHighlight None => new(WadAuthorHighlightKind.None, null);
}

public sealed record WadAuthorLinedefPopupItem(string Title, WadAuthorLinedefPopupAction? Action);

public sealed record WadAuthorLinedefPopupResult(bool Changed, string Status);

public sealed record WadAuthorHighlightTransitionPlan(
    bool Changed,
    WadAuthorHighlightRenderSurface PreviousSurface,
    WadAuthorHighlightRenderSurface CurrentSurface,
    bool HideInfo,
    WadAuthorHighlightKind ShowInfoKind,
    bool Present);

public sealed record WadAuthorToolsMetadata(
    string FormTitle,
    int ClientWidth,
    int ClientHeight,
    bool ControlBox,
    bool MaximizeBox,
    bool MinimizeBox,
    string FormBorderStyle,
    int PopupWidth,
    int PopupHeight);

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
    public const string EditPropertiesStatus = "Edit linedef properties.";

    public static WadAuthorToolsMetadata ToolsMetadata { get; } = new(
        "WAuthorTools",
        ClientWidth: 243,
        ClientHeight: 130,
        ControlBox: false,
        MaximizeBox: false,
        MinimizeBox: false,
        FormBorderStyle: "FixedDialog",
        PopupWidth: 147,
        PopupHeight: 120);

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

    public static bool CanExecuteLinedefPopupAction(WadAuthorLinedefPopupAction action)
        => true;

    public static string EditDescription(WadAuthorLinedefPopupAction action)
        => action switch
        {
            WadAuthorLinedefPopupAction.Delete => "Delete linedef",
            WadAuthorLinedefPopupAction.Split => "Split linedef",
            WadAuthorLinedefPopupAction.Flip => "Flip linedef",
            WadAuthorLinedefPopupAction.Curve => "Curve linedef",
            WadAuthorLinedefPopupAction.Properties => "Edit linedef properties",
            _ => "WadAuthor linedef action",
        };

    public static string ModeToggleStatusText(bool enabled, string currentModeName)
        => enabled
            ? "Mode: WadAuthor. Hover highlights vertices, things, linedefs, and sectors using WadAuthor priority."
            : $"Mode: {currentModeName}";

    public static WadAuthorLinedefPopupResult ExecuteLinedefPopupAction(
        MapSet map,
        Linedef line,
        WadAuthorLinedefPopupAction action,
        Vector2D splitPosition)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (line == null) throw new ArgumentNullException(nameof(line));
        if (!map.Linedefs.Contains(line)) return new WadAuthorLinedefPopupResult(false, "Linedef no longer exists.");

        SelectOnlyLinedef(map, line);

        switch (action)
        {
            case WadAuthorLinedefPopupAction.Properties:
                return new WadAuthorLinedefPopupResult(false, EditPropertiesStatus);
            case WadAuthorLinedefPopupAction.Delete:
                map.RemoveLinedef(line);
                return new WadAuthorLinedefPopupResult(true, "Deleted linedef.");
            case WadAuthorLinedefPopupAction.Split:
                map.SplitLinedef(line, splitPosition);
                return new WadAuthorLinedefPopupResult(true, "Split linedef.");
            case WadAuthorLinedefPopupAction.Flip:
                line.FlipVertices();
                return new WadAuthorLinedefPopupResult(true, "Flipped linedef.");
            case WadAuthorLinedefPopupAction.Curve:
                CurveLinedefsResult curve = CurveLinedefs.ApplyToSelectedLinedefs(map);
                return new WadAuthorLinedefPopupResult(
                    curve.InsertedVertices > 0,
                    curve.CurvedLinedefs == 1 ? "Curved linedef." : $"Curved {curve.CurvedLinedefs} linedefs.");
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    public static void SelectOnlyLinedef(MapSet map, Linedef line)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (line == null) throw new ArgumentNullException(nameof(line));
        map.ClearAllSelected();
        if (map.Linedefs.Contains(line)) line.Selected = true;
    }

    public static string FormatHighlightStatus(MapSet map, WadAuthorHighlight highlight)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));

        return highlight.Kind switch
        {
            WadAuthorHighlightKind.Vertex when highlight.Target is Vertex vertex => $"WadAuthor vertex {map.Vertices.IndexOf(vertex)}",
            WadAuthorHighlightKind.Linedef when highlight.Target is Linedef line => $"WadAuthor linedef {map.Linedefs.IndexOf(line)}",
            WadAuthorHighlightKind.Sector when highlight.Target is Sector sector => $"WadAuthor sector {map.Sectors.IndexOf(sector)}",
            WadAuthorHighlightKind.Thing when highlight.Target is Thing thing => $"WadAuthor thing {map.Things.IndexOf(thing)}",
            _ => "WadAuthor",
        };
    }

    public static WadAuthorHighlightTransitionPlan HighlightTransition(
        WadAuthorHighlight previous,
        WadAuthorHighlight current)
    {
        if (IsSameHighlight(previous, current))
            return new WadAuthorHighlightTransitionPlan(
                Changed: false,
                PreviousSurface: WadAuthorHighlightRenderSurface.None,
                CurrentSurface: WadAuthorHighlightRenderSurface.None,
                HideInfo: false,
                ShowInfoKind: WadAuthorHighlightKind.None,
                Present: false);

        return new WadAuthorHighlightTransitionPlan(
            Changed: true,
            PreviousSurface: RenderSurfaceFor(previous),
            CurrentSurface: RenderSurfaceFor(current),
            HideInfo: true,
            ShowInfoKind: current.Kind,
            Present: true);
    }

    public static WadAuthorHighlightTransitionPlan MouseLeaveTransition(WadAuthorHighlight previous)
        => HighlightTransition(previous, WadAuthorHighlight.None);

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

    private static bool IsSameHighlight(WadAuthorHighlight previous, WadAuthorHighlight current)
        => previous.Kind == current.Kind && ReferenceEquals(previous.Target, current.Target);

    private static WadAuthorHighlightRenderSurface RenderSurfaceFor(WadAuthorHighlight highlight)
        => highlight.Kind switch
        {
            WadAuthorHighlightKind.None => WadAuthorHighlightRenderSurface.None,
            WadAuthorHighlightKind.Thing => WadAuthorHighlightRenderSurface.Things,
            _ => WadAuthorHighlightRenderSurface.Plotter,
        };
}
