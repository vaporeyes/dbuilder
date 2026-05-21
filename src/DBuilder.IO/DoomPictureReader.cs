// ABOUTME: Doom-picture-format decoder (sprites, wall patches, fullscreen graphics) with transparency.
// ABOUTME: Column-based variable-height layout. Honors mxd's tall-patch fix: post startY is treated as a delta when it doesn't increase, allowing patches > 256 pixels tall.

/*
 * Doom picture format:
 *   header (8 bytes):       int16 width, height, offsetx, offsety
 *   column offsets (4*w):   int32 each, relative to start of picture data
 *
 *   each column is a series of "posts":
 *     byte startY          (0xFF = end-of-column terminator)
 *     byte count
 *     byte padding         (unused; historical artifact)
 *     [count] pixel bytes  (palette indices)
 *     byte padding         (unused; historical artifact)
 *
 *   Transparent pixels live in the gaps between posts and remain RGBA=0.
 *
 * Tall-patch fix (mxd):
 *   When (startY <= previous y) OR (height > 256 AND startY == previous y),
 *   the next post's actual y is (previous_y + startY) - i.e., a delta.
 *   This extends the format's natural 0-255 startY range to support patches
 *   up to ~508 pixels and beyond.
 */

using System.IO;

namespace DBuilder.IO;

public static class DoomPictureReader
{
    /// <summary>
    /// Sniff test for whether the given lump bytes look like a Doom picture.
    /// Cheap structural check; a true validation requires a full decode pass.
    /// </summary>
    public static bool Validate(byte[] data)
    {
        if (data.Length < 8) return false;

        using var ms = new MemoryStream(data);
        using var r = new BinaryReader(ms);

        int width = r.ReadInt16();
        int height = r.ReadInt16();
        r.ReadInt16(); // offsetX
        r.ReadInt16(); // offsetY

        if (width < 1 || height < 1) return false;

        // The column table itself must fit, and each column offset must point past it and within the lump.
        long minColAddr = 8L + width * 4L;
        if (data.Length < minColAddr) return false;

        for (int x = 0; x < width; x++)
        {
            int columnAddr = r.ReadInt32();
            if (columnAddr < minColAddr || columnAddr >= data.Length) return false;
        }

        return true;
    }

    /// <summary>Decodes picture bytes into RGBA8 using the supplied palette. Returns null on malformed data.</summary>
    public static DoomPicture? Decode(byte[] data, DoomPalette palette)
    {
        if (data.Length < 8) return null;

        try
        {
            using var ms = new MemoryStream(data);
            using var r = new BinaryReader(ms);

            int width = r.ReadInt16();
            int height = r.ReadInt16();
            int offsetX = r.ReadInt16();
            int offsetY = r.ReadInt16();

            if (width <= 0 || height <= 0) return null;

            // Read column offset table
            int[] columns = new int[width];
            for (int x = 0; x < width; x++) columns[x] = r.ReadInt32();

            byte[] rgba = new byte[width * height * 4];
            // Bytes default to 0, so transparent (RGBA=0) is the implicit "untouched" state.

            for (int x = 0; x < width; x++)
            {
                int colOffset = columns[x];
                if (colOffset < 0 || colOffset >= data.Length) return null;

                ms.Seek(colOffset, SeekOrigin.Begin);

                int y = r.ReadByte();
                int prevY = y;

                while (y != 0xFF)
                {
                    if (ms.Position >= data.Length) return null;
                    int count = r.ReadByte();
                    if (ms.Position >= data.Length) return null;
                    r.ReadByte(); // unused padding

                    for (int yo = 0; yo < count; yo++)
                    {
                        if (ms.Position >= data.Length) return null;
                        int p = r.ReadByte();
                        int row = y + yo;
                        // Guard against out-of-bounds rows from malformed pictures.
                        if (row < 0 || row >= height) continue;

                        uint color = palette.Colors[p];
                        int dst = (row * width + x) * 4;
                        rgba[dst + 0] = (byte)((color >> 16) & 0xFF); // R
                        rgba[dst + 1] = (byte)((color >>  8) & 0xFF); // G
                        rgba[dst + 2] = (byte)( color        & 0xFF); // B
                        rgba[dst + 3] = (byte)((color >> 24) & 0xFF); // A (palette is opaque)
                    }

                    if (ms.Position >= data.Length) return null;
                    r.ReadByte(); // unused padding

                    if (ms.Position >= data.Length) return null;
                    int next = r.ReadByte();
                    if (next == 0xFF) break;

                    // mxd's tall-patch fix: when the next post doesn't advance Y, treat it as a delta.
                    int actualY;
                    if (next < prevY || (height > 256 && next == prevY))
                        actualY = y + next; // delta from previous
                    else
                        actualY = next;     // absolute (canonical Doom)

                    prevY = next;
                    y = actualY;
                }
            }

            return new DoomPicture(width, height, offsetX, offsetY, rgba);
        }
        catch (EndOfStreamException)
        {
            return null;
        }
    }

    /// <summary>Convenience: decode a named picture lump from a WAD. Returns null when the lump is missing or malformed.</summary>
    public static DoomPicture? Decode(WAD wad, string lumpName, DoomPalette palette)
    {
        var lump = wad.FindLump(lumpName);
        if (lump == null) return null;
        return Decode(lump.Stream.ReadAllBytes(), palette);
    }
}
