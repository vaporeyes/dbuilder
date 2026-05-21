// ABOUTME: COLORMAP lump - Doom's lighting model lookup tables.
// ABOUTME: 34 subtables of 256 bytes each remap each palette index for a specific light level (0=brightest, 31=darkest, 32=invulnerability, 33=reserved).

namespace DBuilder.IO;

public sealed class DoomColormap
{
    /// <summary>Number of light-level subtables in this colormap (standard Doom = 34).</summary>
    public int LevelCount { get; }

    /// <summary>Raw colormap data, length = LevelCount * 256. Index as <c>Data[level * 256 + paletteIndex]</c>.</summary>
    public byte[] Data { get; }

    /// <summary>Canonical Doom subtable count: 32 lighting tiers + 1 invulnerability + 1 reserved.</summary>
    public const int StandardLevelCount = 34;

    /// <summary>Subtable size in bytes (one byte per palette index).</summary>
    public const int LevelSize = 256;

    public DoomColormap(byte[] data, int levelCount)
    {
        Data = data;
        LevelCount = levelCount;
    }

    /// <summary>Applies the colormap at a specific light level to an indexed pixel value, returning the remapped palette index.</summary>
    public byte Lookup(int level, byte paletteIndex)
    {
        if (level < 0 || level >= LevelCount) throw new ArgumentOutOfRangeException(nameof(level), $"Level {level} out of range [0, {LevelCount})");
        return Data[level * LevelSize + paletteIndex];
    }

    /// <summary>Returns a fresh copy of the 256-byte remap table for the given level.</summary>
    public byte[] LevelTable(int level)
    {
        if (level < 0 || level >= LevelCount) throw new ArgumentOutOfRangeException(nameof(level));
        var copy = new byte[LevelSize];
        System.Array.Copy(Data, level * LevelSize, copy, 0, LevelSize);
        return copy;
    }
}
