// ABOUTME: Doom-format flat (floor/ceiling texture) reader.  Flats are raw 64x64 indexed-color tiles with no header.
// ABOUTME: Produces a RGBA8 byte[] suitable for Texture.SetPixelsRgba8 by mapping through a DoomPalette.

namespace DBuilder.IO;

public static class DoomFlatReader
{
    public const int Width = 64;
    public const int Height = 64;
    public const int RawSize = Width * Height; // 4096

    /// <summary>True if the lump body looks like a Doom flat (exactly 4096 bytes, or a multiple thereof for animated flats).</summary>
    public static bool LooksLikeFlat(int byteCount) => byteCount == RawSize;

    /// <summary>Decodes a 64x64 indexed-color flat into RGBA8 bytes through the supplied palette.</summary>
    public static byte[] DecodeRgba8(byte[] indexedData, DoomPalette palette)
    {
        if (indexedData.Length < RawSize)
            throw new System.IO.IOException($"Flat data too short: got {indexedData.Length} bytes, need {RawSize}.");

        // Trim to exactly one tile - some "animated" flats stack frames; the first is enough for visualization.
        byte[] firstFrame = indexedData.Length == RawSize ? indexedData : indexedData[..RawSize];
        return palette.IndexedToRgba8(firstFrame);
    }

    /// <summary>Convenience: decode a named flat lump from a WAD.  Returns null when the lump is missing or the wrong size.</summary>
    public static byte[]? DecodeRgba8(WAD wad, string lumpName, DoomPalette palette)
    {
        var lump = wad.FindLump(lumpName);
        if (lump == null) return null;
        byte[] data = lump.Stream.ReadAllBytes();
        if (!LooksLikeFlat(data.Length) && data.Length < RawSize) return null;
        return DecodeRgba8(data, palette);
    }
}
