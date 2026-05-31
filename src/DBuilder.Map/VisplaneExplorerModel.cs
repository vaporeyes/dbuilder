// ABOUTME: Models UDB VisplaneExplorer point results and 64x64 tile storage.
// ABOUTME: Preserves the progressive tile sampling order and packed stat counters used by the plugin.

using System.Drawing;
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
