// ABOUTME: Models UDB VisplaneExplorer point results and 64x64 tile storage.
// ABOUTME: Preserves the progressive tile sampling order and packed stat counters used by the plugin.

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

    private static uint MakePointValue(uint visplanes, uint drawsegs, uint solidsegs, uint openings)
    {
        unchecked
        {
            return visplanes + (drawsegs << 8) + (solidsegs << 16) + (openings << 24);
        }
    }
}
