// ABOUTME: Models UDB VisplaneExplorer point results and 64x64 tile storage.
// ABOUTME: Preserves the progressive tile sampling order and packed stat counters used by the plugin.

using System.Drawing;
using System.Globalization;
using DBuilder.Geometry;

namespace DBuilder.Map;

public enum VisplaneExplorerStat
{
    Visplanes,
    Drawsegs,
    Solidsegs,
    Openings,
    Heatmap,
}

public enum VisplanePointResult
{
    Ok = 0,
    BadZ = -1,
    Void = -2,
    Overflow = -3,
}

public readonly record struct VisplaneTilePoint(int X, int Y, byte Granularity);

public readonly record struct VisplanePointData(
    VisplaneTilePoint Point,
    VisplanePointResult Result,
    int Visplanes,
    int Drawsegs,
    int Solidsegs,
    int Openings);

public readonly record struct VisplaneTilePosition(int X, int Y);

public readonly record struct VisplaneOverlayRectangle(
    int X,
    int Y,
    int Width,
    int Height,
    uint Color);

public readonly record struct VisplaneMapRectangle(int X, int Y, int Width, int Height)
{
    public bool Contains(VisplaneTilePosition position)
        => position.X >= X
            && position.Y >= Y
            && position.X < X + Width
            && position.Y < Y + Height;
}

public readonly record struct VisplaneHoverInfo(int Value, int StaticLimit, bool Overflow)
{
    public string FormatLabel()
        => $"{Value}{(Overflow ? "+" : "")} / {StaticLimit}";
}

public sealed record VisplaneExplorerModeDescriptor(
    string DisplayName,
    string SwitchAction,
    string ButtonImage,
    int ButtonOrder,
    string ButtonGroup,
    bool Volatile,
    bool UseByDefault,
    IReadOnlyList<string> SupportedMapFormats,
    bool AllowCopyPaste);

public sealed record VisplaneExplorerInterfaceSettings(
    bool OpenDoors,
    bool ShowHeatmap,
    VisplaneExplorerStat SelectedStat,
    int ViewHeight,
    int ViewHeightCustom);

public sealed record VisplaneExplorerStatMenuItem(
    VisplaneExplorerStat Stat,
    string Text,
    string ImageName,
    string Tag,
    bool Checked);

public sealed record VisplaneExplorerViewHeightState(
    int ViewHeight,
    int ViewHeightCustom,
    string ButtonText,
    bool CustomItemVisible,
    string CustomItemText,
    bool SettingsChanged);

public sealed record VisplaneExplorerViewHeightMenuItem(
    string Name,
    string Text,
    string Tag,
    bool Checked);

public readonly record struct VisplaneExplorerProgress(
    int TileCount,
    int IssuedPointCount,
    int RemainingPointCount,
    int QueuedPointCount)
{
    public string FormatStatus()
        => "Visplane Explorer analyzing: "
            + IssuedPointCount.ToString(CultureInfo.InvariantCulture)
            + " issued, "
            + RemainingPointCount.ToString(CultureInfo.InvariantCulture)
            + " remaining, "
            + QueuedPointCount.ToString(CultureInfo.InvariantCulture)
            + " queued across "
            + VisplaneExplorerInterfaceModel.CountLabel(TileCount, "tile")
            + ".";
}

public static class VisplaneExplorerViewHeight
{
    public const int DefaultCustomHeight = 0;
    public const int MaxCustomHeight = short.MaxValue;

    public static int NormalizeCustomHeightInput(string? input)
    {
        int height = ParseNumericTextboxInput(input, DefaultCustomHeight);
        return height > MaxCustomHeight ? DefaultCustomHeight : height;
    }

    private static int ParseNumericTextboxInput(string? input, int original)
    {
        string text = input?.Trim() ?? "";
        if (text.Length == 0) return original;

        string valueText = StripRelativePrefix(text, out string prefix);
        if (!int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            return original;

        return prefix switch
        {
            "+++" or "++" => original + value,
            "---" or "--" => original - value < 0 ? original : original - value,
            "*" => original * value,
            "/" => value == 0 ? original : original / value,
            _ => value < 0 ? original : value,
        };
    }

    private static string StripRelativePrefix(string text, out string prefix)
    {
        foreach (string candidate in new[] { "+++", "---", "++", "--", "*", "/" })
        {
            if (!text.StartsWith(candidate, StringComparison.Ordinal)) continue;
            prefix = candidate;
            return text[candidate.Length..].Trim();
        }

        prefix = "";
        return text;
    }
}

public static class VisplaneExplorerInterfaceModel
{
    public const string PluginName = "VisplaneExplorer";
    public const int MinimumRevision = 2411;
    public const string OpenDoorsSettingsKey = "opendoors";
    public const string ShowHeatmapSettingsKey = "showheatmap";
    public const string SelectedStatSettingsKey = "selectedstat";
    public const string ViewHeightSettingsKey = "viewheight";
    public const string ViewHeightCustomSettingsKey = "viewheightcustom";
    public const string StatisticsToolTip = "Statistics to view";
    public const string OpenDoorsText = "Open Doors";
    public const string HeatColorsText = "Heat Colors";
    public const string ViewHeightText = "View Height";
    public const string ViewHeightToolTip = "Position above the floor to calculate stats";
    public const string CustomHeightText = "Set custom height";
    public const string CustomHeightTag = "-1";
    public const string CustomHeightImageName = "Add";

    public static VisplaneExplorerModeDescriptor ModeDescriptor { get; } = new(
        "Visplane Explorer",
        "visplaneexplorermode",
        "Gauge.png",
        300,
        "002_tools",
        Volatile: true,
        UseByDefault: true,
        new[] { "DoomMapSetIO", "HexenMapSetIO" },
        AllowCopyPaste: false);

    public static IReadOnlyList<VisplaneExplorerStatMenuItem> StatMenuItems(VisplaneExplorerStat selected)
        =>
        [
            StatMenuItem(VisplaneExplorerStat.Visplanes, "Visplanes", "Visplanes", selected),
            StatMenuItem(VisplaneExplorerStat.Drawsegs, "Drawsegs", "Drawsegs", selected),
            StatMenuItem(VisplaneExplorerStat.Solidsegs, "Solidsegs", "Solidsegs", selected),
            StatMenuItem(VisplaneExplorerStat.Openings, "Openings", "Openings", selected),
        ];

    public static VisplaneExplorerInterfaceSettings CreateSettings(
        IReadOnlyDictionary<string, object?> settings,
        int viewHeightDefault)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new VisplaneExplorerInterfaceSettings(
            ReadBool(settings, OpenDoorsSettingsKey, false),
            ReadBool(settings, ShowHeatmapSettingsKey, false),
            ReadStat(settings, SelectedStatSettingsKey, VisplaneExplorerStat.Visplanes),
            ReadInt(settings, ViewHeightSettingsKey, viewHeightDefault),
            ReadInt(settings, ViewHeightCustomSettingsKey, 0));
    }

    public static IReadOnlyDictionary<string, object> WriteSettings(VisplaneExplorerInterfaceSettings settings)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [OpenDoorsSettingsKey] = settings.OpenDoors,
            [ShowHeatmapSettingsKey] = settings.ShowHeatmap,
            [SelectedStatSettingsKey] = (int)settings.SelectedStat,
            [ViewHeightSettingsKey] = settings.ViewHeight,
            [ViewHeightCustomSettingsKey] = settings.ViewHeightCustom,
        };

    public static VisplaneExplorerViewHeightState ViewHeightState(int viewHeight, int viewHeightCustom)
        => new(
            viewHeight,
            viewHeightCustom,
            FormatViewHeightButtonText(viewHeight),
            viewHeightCustom > 0,
            viewHeightCustom > 0 ? viewHeightCustom.ToString(CultureInfo.InvariantCulture) + " - Custom" : "",
            SettingsChanged: false);

    public static VisplaneExplorerViewHeightState SelectViewHeight(int currentViewHeight, int selectedViewHeight)
        => ViewHeightState(selectedViewHeight, VisplaneExplorerViewHeight.DefaultCustomHeight) with
        {
            SettingsChanged = currentViewHeight != selectedViewHeight,
        };

    public static VisplaneExplorerViewHeightState ApplyCustomViewHeight(
        int currentViewHeight,
        int currentCustomHeight,
        string? customInput,
        int viewHeightDefault,
        IReadOnlyDictionary<string, string> configuredViewHeights)
    {
        ArgumentNullException.ThrowIfNull(configuredViewHeights);

        int customHeight = VisplaneExplorerViewHeight.NormalizeCustomHeightInput(customInput);
        int viewHeight = customHeight > 0 ? customHeight : viewHeightDefault;
        int visibleCustomHeight = configuredViewHeights.ContainsKey(customHeight.ToString(CultureInfo.InvariantCulture))
            ? 0
            : customHeight;

        return ViewHeightState(viewHeight, visibleCustomHeight) with
        {
            SettingsChanged = currentViewHeight != viewHeight || currentCustomHeight != visibleCustomHeight,
        };
    }

    public static string FormatViewHeightButtonText(int viewHeight)
        => "View Height (" + viewHeight.ToString(CultureInfo.InvariantCulture) + ")";

    public static string ReadyStatus(
        int tileCount,
        int queuedPointCount,
        VisplaneExplorerStat stat,
        VisplaneExplorerInterfaceSettings settings)
    {
        string statText = "";
        foreach (VisplaneExplorerStatMenuItem item in StatMenuItems(stat))
        {
            if (item.Stat == stat)
            {
                statText = item.Text;
                break;
            }
        }

        return $"Visplane Explorer ready: {CountLabel(tileCount, "tile")}, " +
            $"{CountLabel(queuedPointCount, "queued point")}, {statText}, " +
            $"{OpenDoorsText}: {(settings.OpenDoors ? "on" : "off")}, " +
            $"{HeatColorsText}: {(settings.ShowHeatmap ? "on" : "off")}, " +
            $"{FormatViewHeightButtonText(settings.ViewHeight)}.";
    }

    public static string CountLabel(int count, string singular, string? plural = null)
        => $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? singular : plural ?? singular + "s")}";

    public static VisplaneExplorerViewHeightMenuItem ViewHeightMenuItem(
        int height,
        string label,
        int defaultHeight,
        int selectedHeight)
    {
        string tag = height.ToString(CultureInfo.InvariantCulture);
        string text = tag + " - " + label + (height == defaultHeight ? " (default)" : "");
        return new VisplaneExplorerViewHeightMenuItem("viewheight" + tag, text, tag, height == selectedHeight);
    }

    private static VisplaneExplorerStatMenuItem StatMenuItem(
        VisplaneExplorerStat stat,
        string text,
        string imageName,
        VisplaneExplorerStat selected)
        => new(stat, text, imageName, ((int)stat).ToString(CultureInfo.InvariantCulture), stat == selected);

    private static bool ReadBool(IReadOnlyDictionary<string, object?> settings, string key, bool fallback)
    {
        if (!settings.TryGetValue(key, out object? value)) return fallback;
        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out bool b) => b,
            int i => i != 0,
            _ => fallback,
        };
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> settings, string key, int fallback)
    {
        if (!settings.TryGetValue(key, out object? value)) return fallback;
        return value switch
        {
            int i => i,
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) => i,
            _ => fallback,
        };
    }

    private static VisplaneExplorerStat ReadStat(
        IReadOnlyDictionary<string, object?> settings,
        string key,
        VisplaneExplorerStat fallback)
    {
        int value = ReadInt(settings, key, (int)fallback);
        return Enum.IsDefined(typeof(VisplaneExplorerStat), value) && value < (int)VisplaneExplorerStat.Heatmap
            ? (VisplaneExplorerStat)value
            : fallback;
    }
}

public sealed class VisplanePalette
{
    private readonly uint[] colors;

    public VisplanePalette(IReadOnlyList<uint> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if (colors.Count <= byte.MaxValue)
            throw new ArgumentException("Visplane palettes must provide colors for all byte values.", nameof(colors));

        this.colors = new uint[colors.Count];
        for (int i = 0; i < colors.Count; i++) this.colors[i] = colors[i];
    }

    public IReadOnlyList<uint> Colors => colors;

    public uint this[int index] => colors[index];

    public void SetColor(int index, uint color) => colors[index] = color;

    public uint ColorForByte(byte value) => colors[value];
}

public sealed class VisplanePaletteSet
{
    private readonly VisplanePalette[] palettes;

    public VisplanePaletteSet(
        VisplanePalette visplanes,
        VisplanePalette drawsegs,
        VisplanePalette solidsegs,
        VisplanePalette openings,
        VisplanePalette heatmap)
    {
        palettes = new[]
        {
            visplanes ?? throw new ArgumentNullException(nameof(visplanes)),
            drawsegs ?? throw new ArgumentNullException(nameof(drawsegs)),
            solidsegs ?? throw new ArgumentNullException(nameof(solidsegs)),
            openings ?? throw new ArgumentNullException(nameof(openings)),
            heatmap ?? throw new ArgumentNullException(nameof(heatmap)),
        };
    }

    public VisplanePalette this[VisplaneExplorerStat stat] => palettes[PaletteIndex(stat)];

    public VisplanePalette PaletteFor(VisplaneExplorerStat viewStat, bool showHeatmap)
        => showHeatmap ? palettes[(int)VisplaneExplorerStat.Heatmap] : palettes[PaletteIndex(viewStat)];

    public void SetVoidColor(uint color)
    {
        foreach (VisplanePalette palette in palettes)
            palette.SetColor(VisplaneTile.PointVoidByte, color);
    }

    private static int PaletteIndex(VisplaneExplorerStat stat)
    {
        int index = (int)stat;
        if (index < 0 || index > (int)VisplaneExplorerStat.Heatmap)
            throw new ArgumentOutOfRangeException(nameof(stat));
        return index;
    }
}

public sealed class VisplaneTileScan
{
    private readonly Dictionary<VisplaneTilePosition, VisplaneTile> tiles = new();

    public IReadOnlyDictionary<VisplaneTilePosition, VisplaneTile> Tiles => tiles;

    public static VisplaneTilePosition TileForPoint(double x, double y)
        => new(
            (int)Math.Floor(x / VisplaneTile.TileSize) * VisplaneTile.TileSize,
            (int)Math.Floor(y / VisplaneTile.TileSize) * VisplaneTile.TileSize);

    public VisplaneTile AddTile(VisplaneTilePosition position)
    {
        var tile = new VisplaneTile(position);
        tiles.Add(position, tile);
        return tile;
    }

    public VisplaneTile GetOrCreateTile(VisplaneTilePosition position)
    {
        if (!tiles.TryGetValue(position, out VisplaneTile? tile))
        {
            tile = new VisplaneTile(position);
            tiles.Add(position, tile);
        }

        return tile;
    }

    public static VisplaneTileScan CreateForMap(MapSet map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var scan = new VisplaneTileScan();
        if (map.Vertices.Count == 0 || map.Linedefs.Count == 0) return scan;

        RectangleF area = MapSet.CreateArea(map.Vertices);
        if (area.Width < 0.0f || area.Height < 0.0f) return scan;

        int left = (int)Math.Round(area.Left);
        int top = (int)Math.Round(area.Top);
        int right = (int)Math.Round(area.Right);
        int bottom = (int)Math.Round(area.Bottom);

        VisplaneTilePosition leftTop = TileForPoint(left - VisplaneTile.TileSize, top - VisplaneTile.TileSize);
        VisplaneTilePosition rightBottom = TileForPoint(right + VisplaneTile.TileSize, bottom + VisplaneTile.TileSize);

        for (int x = leftTop.X; x <= rightBottom.X; x += VisplaneTile.TileSize)
        {
            for (int y = leftTop.Y; y <= rightBottom.Y; y += VisplaneTile.TileSize)
            {
                var center = new Vector2D(x + (VisplaneTile.TileSize >> 1), y + (VisplaneTile.TileSize >> 1));
                Linedef? nearest = map.NearestLinedef(center);
                if (nearest == null) continue;

                double distanceSq = nearest.DistanceToSq(center, bounded: true);
                if (distanceSq > VisplaneTile.TileSize * VisplaneTile.TileSize)
                {
                    double side = nearest.SideOfLine(center);
                    if (side > 0.0 && nearest.Back == null)
                        continue;
                }

                scan.AddTile(new VisplaneTilePosition(x, y));
            }
        }

        return scan;
    }

    public IReadOnlyList<VisplaneTilePoint> CollectNextPointBatch(VisplaneMapRectangle viewRectangle)
    {
        var points = new List<VisplaneTilePoint>(tiles.Count);
        foreach (KeyValuePair<VisplaneTilePosition, VisplaneTile> entry in tiles)
            if (!entry.Value.IsComplete && viewRectangle.Contains(entry.Key))
                points.Add(entry.Value.GetNextPoint());

        if (points.Count > 0) return points;

        foreach (VisplaneTile tile in tiles.Values)
            if (!tile.IsComplete)
                points.Add(tile.GetNextPoint());

        return points;
    }

    public IReadOnlyList<VisplaneTilePoint> QueuePoints(VisplaneMapRectangle viewRectangle, int currentQueuedPoints, int targetQueuedPoints)
    {
        var points = new List<VisplaneTilePoint>();
        int queued = currentQueuedPoints;
        while (queued < targetQueuedPoints)
        {
            IReadOnlyList<VisplaneTilePoint> batch = CollectNextPointBatch(viewRectangle);
            if (batch.Count == 0) break;
            points.AddRange(batch);
            queued += batch.Count;
        }

        return points;
    }

    public VisplaneExplorerProgress Progress(int queuedPointCount)
    {
        int issued = 0;
        int remaining = 0;
        foreach (VisplaneTile tile in tiles.Values)
        {
            issued += tile.IssuedPointCount;
            remaining += tile.RemainingPointCount;
        }

        return new VisplaneExplorerProgress(tiles.Count, issued, remaining, queuedPointCount);
    }

    public VisplaneHoverInfo? GetHoverInfo(double mapX, double mapY, VisplaneExplorerStat stat, int staticLimit)
    {
        VisplaneTilePosition position = TileForPoint(mapX, mapY);
        if (!tiles.TryGetValue(position, out VisplaneTile? tile)) return null;

        int x = (int)Math.Floor(mapX) - position.X;
        int y = (int)Math.Floor(mapY) - position.Y;
        byte point = tile.GetPointByte(x, y, stat);
        if (point == VisplaneTile.PointVoidByte) return null;

        return new VisplaneHoverInfo(
            tile.GetPointValue(x, y, stat),
            staticLimit,
            point == VisplaneTile.PointOverflowByte);
    }

    public IReadOnlyList<VisplaneOverlayRectangle> BuildOverlayRectangles(
        VisplaneExplorerStat stat,
        VisplanePalette palette,
        bool showHeatmap,
        int configuredVisplaneLimit)
    {
        ArgumentNullException.ThrowIfNull(palette);

        var result = new List<VisplaneOverlayRectangle>();
        foreach (VisplaneTile tile in tiles.Values)
            AddOverlayRectangles(result, tile, stat, palette, showHeatmap, configuredVisplaneLimit);
        return result;
    }

    private static void AddOverlayRectangles(
        List<VisplaneOverlayRectangle> result,
        VisplaneTile tile,
        VisplaneExplorerStat stat,
        VisplanePalette palette,
        bool showHeatmap,
        int configuredVisplaneLimit)
    {
        for (int y = 0; y < VisplaneTile.TileSize; y++)
        {
            int runStart = -1;
            uint runColor = 0;
            for (int x = 0; x < VisplaneTile.TileSize; x++)
            {
                byte point = showHeatmap
                    ? tile.GetHeatmapByte(x, y, stat, configuredVisplaneLimit)
                    : tile.GetPointByte(x, y, stat);
                uint color = tile.IsComplete || point != 0 ? palette.ColorForByte(point) : 0;
                if ((color >> 24) == 0) color = 0;

                if (color == runColor && runStart >= 0) continue;

                if (runStart >= 0)
                    result.Add(new VisplaneOverlayRectangle(tile.Position.X + runStart, tile.Position.Y + y, x - runStart, 1, runColor));

                runStart = color == 0 ? -1 : x;
                runColor = color;
            }

            if (runStart >= 0)
                result.Add(new VisplaneOverlayRectangle(tile.Position.X + runStart, tile.Position.Y + y, VisplaneTile.TileSize - runStart, 1, runColor));
        }
    }
}

public sealed class VisplaneTile
{
    public const int TileSize = 64;
    public const uint PointMaxRange = 254;
    public const uint PointOverflow = 0xFEFEFEFE;
    public const uint PointVoid = 0xFFFFFFFF;
    public const byte PointOverflowByte = 0xFE;
    public const byte PointVoidByte = 0xFF;

    public static readonly int[] StatCompressors = { 1, 2, 1, 160 };

    private readonly uint[][] points;
    private int nextIndex;

    public VisplaneTile(VisplaneTilePosition position)
    {
        Position = position;
        points = new uint[TileSize][];
        for (int y = 0; y < TileSize; y++) points[y] = new uint[TileSize];
    }

    public VisplaneTilePosition Position { get; }

    public bool IsComplete => nextIndex == TileSize * TileSize;

    public int IssuedPointCount => nextIndex;

    public int RemainingPointCount => TileSize * TileSize - nextIndex;

    public void StorePointData(VisplanePointData data)
    {
        uint value = data.Result switch
        {
            VisplanePointResult.Ok => MakePointValue(
                Compress(data.Visplanes, StatCompressors[(int)VisplaneExplorerStat.Visplanes]),
                Compress(data.Drawsegs, StatCompressors[(int)VisplaneExplorerStat.Drawsegs]),
                Compress(data.Solidsegs, StatCompressors[(int)VisplaneExplorerStat.Solidsegs]),
                Compress(data.Openings, StatCompressors[(int)VisplaneExplorerStat.Openings])),
            VisplanePointResult.BadZ => MakePointValue(1, 0, 0, 0),
            VisplanePointResult.Void => PointVoid,
            _ => PointOverflow,
        };

        FillPoints(data.Point, value);
    }

    public byte GetPointByte(int x, int y, VisplaneExplorerStat stat)
    {
        int statIndex = PackedStatIndex(stat);
        uint value = points[y][x];
        return (byte)((value >> (statIndex * 8)) & 0xFF);
    }

    public int GetPointValue(int x, int y, VisplaneExplorerStat stat)
    {
        int statIndex = PackedStatIndex(stat);
        return GetPointByte(x, y, stat) * StatCompressors[statIndex];
    }

    public byte GetHeatmapByte(int x, int y, VisplaneExplorerStat stat, int configuredVisplaneLimit = 128)
    {
        byte value = GetPointByte(x, y, stat);
        if (stat == VisplaneExplorerStat.Visplanes && value != 0 && value != PointVoidByte)
            return InterpolateVisplanes(value, configuredVisplaneLimit);

        return value;
    }

    public VisplaneTilePoint GetNextPoint()
    {
        VisplaneTilePoint point = PointByIndex(nextIndex++);
        return point with
        {
            X = point.X + Position.X,
            Y = point.Y + Position.Y,
        };
    }

    public static VisplaneTilePoint PointByIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index >= TileSize * TileSize) throw new ArgumentOutOfRangeException(nameof(index));

        byte granularity;
        if (index == 0) granularity = 64;
        else if (index < 4) granularity = 32;
        else if (index < 16) granularity = 16;
        else if (index < 64) granularity = 8;
        else if (index < 256) granularity = 4;
        else if (index < 1024) granularity = 2;
        else granularity = 1;

        int x = (index & 1) << 5;
        int y = (((index >> 1) ^ index) & 1) << 5;

        index >>= 2;
        x += (index & 1) << 4;
        y += (((index >> 1) ^ index) & 1) << 4;

        index >>= 2;
        x |= (index & 1) << 3;
        y |= (((index >> 1) ^ index) & 1) << 3;

        index >>= 2;
        x |= (index & 1) << 2;
        y |= (((index >> 1) ^ index) & 1) << 2;

        index >>= 2;
        x |= (index & 1) << 1;
        y |= (((index >> 1) ^ index) & 1) << 1;

        index >>= 2;
        x |= index & 1;
        y |= ((index >> 1) ^ index) & 1;

        return new VisplaneTilePoint(x, y, granularity);
    }

    private void FillPoints(VisplaneTilePoint point, uint value)
    {
        int startX = point.X - Position.X;
        int startY = point.Y - Position.Y;
        int endX = startX + point.Granularity;
        int endY = startY + point.Granularity;

        for (int x = startX; x < endX; x++)
            for (int y = startY; y < endY; y++)
                points[y][x] = value;
    }

    private static uint Compress(int value, int compressor)
        => (uint)Math.Min((value + compressor - 1) / compressor, PointMaxRange);

    private static int PackedStatIndex(VisplaneExplorerStat stat)
    {
        int statIndex = (int)stat;
        if (statIndex < 0 || statIndex >= StatCompressors.Length)
            throw new ArgumentOutOfRangeException(nameof(stat));
        return statIndex;
    }

    private static byte InterpolateVisplanes(byte value, int configuredVisplaneLimit)
    {
        const int defaultMaxVisplanes = 128;
        if (configuredVisplaneLimit <= 0 || configuredVisplaneLimit == defaultMaxVisplanes) return value;

        double scaled = defaultMaxVisplanes * value / (double)configuredVisplaneLimit;
        return (byte)Math.Ceiling(scaled);
    }

    private static uint MakePointValue(uint visplanes, uint drawsegs, uint solidsegs, uint openings)
    {
        unchecked
        {
            return visplanes + (drawsegs << 8) + (solidsegs << 16) + (openings << 24);
        }
    }
}
