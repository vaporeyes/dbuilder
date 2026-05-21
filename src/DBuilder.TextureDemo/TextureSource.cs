// ABOUTME: Builds the list of textures to display - either by scanning a real .wad or producing a synthetic showcase.
// ABOUTME: A "texture" here is just (Name, Width, Height, byte[] RGBA8) - drop-in for Texture.SetPixelsRgba8.

using DBuilder.IO;

namespace DBuilder.TextureDemo;

public sealed record LoadedTexture(string Name, string Kind, int Width, int Height, byte[] Rgba8);

public static class TextureSource
{
    /// <summary>Scans a WAD for displayable textures. Returns at most <paramref name="maxCount"/> entries.</summary>
    public static List<LoadedTexture> FromWad(string wadPath, int maxCount)
    {
        using var fs = File.OpenRead(wadPath);
        var ms = new MemoryStream();
        fs.CopyTo(ms);
        ms.Position = 0;
        using var wad = new WAD(ms, openreadonly: true, virtualFilename: wadPath);

        var palette = DoomPalette.FromWad(wad);
        if (palette == null)
        {
            Console.WriteLine("[wad]   No PLAYPAL lump - using grayscale fallback palette");
            palette = MakeGrayscalePalette();
        }
        else
        {
            Console.WriteLine("[wad]   PLAYPAL found");
        }

        // Split the budget so the grid mixes both texture kinds rather than filling with flats alone.
        int flatBudget = (maxCount + 1) / 2;   // half of the cells (rounded up)
        int pictureBudget = maxCount - flatBudget;

        var flats = new List<LoadedTexture>();
        var pictures = new List<LoadedTexture>();

        // Pass 1: 64x64 flats anywhere in the WAD.
        for (int i = 0; i < wad.Lumps.Count && flats.Count < flatBudget; i++)
        {
            var lump = wad.Lumps[i];
            if (!DoomFlatReader.LooksLikeFlat(lump.Length)) continue;
            // Skip well-known non-flat lumps that happen to share the 4096-byte size (esp. REJECT/BLOCKMAP from small maps).
            if (IsKnownNonFlatLump(lump.Name)) continue;

            try
            {
                byte[] indexed = lump.Stream.ReadAllBytes();
                byte[] rgba = DoomFlatReader.DecodeRgba8(indexed, palette);
                flats.Add(new LoadedTexture(lump.Name, "flat", DoomFlatReader.Width, DoomFlatReader.Height, rgba));
            }
            catch { /* skip lumps that don't decode */ }
        }

        // Pass 2: column-based pictures (sprites, wall patches, fullscreen graphics).
        for (int i = 0; i < wad.Lumps.Count && pictures.Count < pictureBudget; i++)
        {
            var lump = wad.Lumps[i];
            if (lump.Length < 8) continue;
            if (DoomFlatReader.LooksLikeFlat(lump.Length)) continue; // already handled as flat above
            if (lump.Length == 768 || lump.Length == 8704) continue; // PLAYPAL sizes

            byte[] data = lump.Stream.ReadAllBytes();
            if (!DoomPictureReader.Validate(data)) continue;

            var pic = DoomPictureReader.Decode(data, palette);
            if (pic == null) continue;
            // Skip implausibly large or tiny things to keep the grid sensible
            if (pic.Width < 4 || pic.Height < 4) continue;
            if (pic.Width > 640 || pic.Height > 480) continue;

            pictures.Add(new LoadedTexture(lump.Name, "picture", pic.Width, pic.Height, pic.Rgba8));
        }

        // Interleave flats and pictures so the grid alternates kinds row-by-row.
        var result = new List<LoadedTexture>(maxCount);
        result.AddRange(flats);
        result.AddRange(pictures);
        return result;
    }

    /// <summary>Self-contained synthetic showcase for use when no .wad is supplied. Exercises both decoders end-to-end.</summary>
    public static List<LoadedTexture> Synthetic()
    {
        // Build a real Doom-style palette: 32 grays + 32 reds + 32 greens + 32 blues + 128 spectrum
        var paletteBytes = new byte[768];
        for (int i = 0; i < 32; i++)
        {
            byte v = (byte)(i * 8);
            paletteBytes[i * 3 + 0] = v; paletteBytes[i * 3 + 1] = v; paletteBytes[i * 3 + 2] = v; // grays
            paletteBytes[(i + 32) * 3 + 0] = v; paletteBytes[(i + 32) * 3 + 1] = 0; paletteBytes[(i + 32) * 3 + 2] = 0; // reds
            paletteBytes[(i + 64) * 3 + 0] = 0; paletteBytes[(i + 64) * 3 + 1] = v; paletteBytes[(i + 64) * 3 + 2] = 0; // greens
            paletteBytes[(i + 96) * 3 + 0] = 0; paletteBytes[(i + 96) * 3 + 1] = 0; paletteBytes[(i + 96) * 3 + 2] = v; // blues
        }
        for (int i = 0; i < 128; i++)
        {
            paletteBytes[(i + 128) * 3 + 0] = (byte)(i * 2);
            paletteBytes[(i + 128) * 3 + 1] = (byte)(255 - i * 2);
            paletteBytes[(i + 128) * 3 + 2] = (byte)((i * 7) & 0xFF);
        }
        var palette = DoomPalette.FromBytes(paletteBytes);

        var list = new List<LoadedTexture>();

        // Synthetic flat A: diagonal stripes
        {
            var indexed = new byte[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    indexed[y * 64 + x] = (byte)(((x + y) % 8 == 0) ? 31 : 0);
            list.Add(new LoadedTexture("SYN_BARS", "flat", 64, 64, DoomFlatReader.DecodeRgba8(indexed, palette)));
        }

        // Synthetic flat B: checkerboard
        {
            var indexed = new byte[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    indexed[y * 64 + x] = (byte)((((x / 8) + (y / 8)) % 2 == 0) ? 33 : 96);
            list.Add(new LoadedTexture("SYN_CHECK", "flat", 64, 64, DoomFlatReader.DecodeRgba8(indexed, palette)));
        }

        // Synthetic flat C: radial gradient
        {
            var indexed = new byte[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    int dx = x - 32, dy = y - 32;
                    int d = (int)Math.Sqrt(dx * dx + dy * dy);
                    indexed[y * 64 + x] = (byte)(128 + Math.Min(127, d * 4));
                }
            list.Add(new LoadedTexture("SYN_RADIAL", "flat", 64, 64, DoomFlatReader.DecodeRgba8(indexed, palette)));
        }

        // Synthetic picture: a 24x24 magenta diamond on transparent background.
        // Built by emitting the Doom picture format directly so the picture reader's column/post path is exercised.
        list.Add(BuildSyntheticPicture("SYN_DIAMOND", palette));

        return list;
    }

    private static LoadedTexture BuildSyntheticPicture(string name, DoomPalette palette)
    {
        const int W = 24, H = 24;

        // Compute per-column posts: for each column, find the contiguous Y range that's inside the diamond.
        // Diamond: |x - 11| + |y - 11| <= 11
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write((short)W);
        bw.Write((short)H);
        bw.Write((short)0); // offsetX
        bw.Write((short)0); // offsetY

        // Reserve column-offset slots
        long columnTablePos = ms.Position;
        for (int x = 0; x < W; x++) bw.Write((int)0);

        var columnOffsets = new int[W];

        // Use palette entry 33 (red ramp) for the diamond pixels - visually distinct from the gradient flat.
        const byte fillIndex = 33;

        for (int x = 0; x < W; x++)
        {
            columnOffsets[x] = (int)ms.Position;

            // Find filled Y range in this column
            int yMin = -1, yMax = -1;
            for (int y = 0; y < H; y++)
            {
                bool inside = Math.Abs(x - 11) + Math.Abs(y - 11) <= 11;
                if (inside)
                {
                    if (yMin < 0) yMin = y;
                    yMax = y;
                }
            }

            if (yMin < 0)
            {
                // Empty column - just write the terminator
                bw.Write((byte)0xFF);
                continue;
            }

            int count = yMax - yMin + 1;
            bw.Write((byte)yMin);   // startY
            bw.Write((byte)count);  // count
            bw.Write((byte)0);      // padding
            for (int i = 0; i < count; i++) bw.Write(fillIndex);
            bw.Write((byte)0);      // padding
            bw.Write((byte)0xFF);   // terminator
        }

        // Back-patch the column offset table
        long endPos = ms.Position;
        ms.Position = columnTablePos;
        var bw2 = new BinaryWriter(ms);
        for (int x = 0; x < W; x++) bw2.Write(columnOffsets[x]);
        ms.Position = endPos;

        byte[] picBytes = ms.ToArray();
        var pic = DoomPictureReader.Decode(picBytes, palette)
                  ?? throw new InvalidOperationException("Synthetic picture failed to decode - this is a test bug, not a runtime issue");
        return new LoadedTexture(name, "picture", pic.Width, pic.Height, pic.Rgba8);
    }

    private static bool IsKnownNonFlatLump(string name) => name switch
    {
        // Engine data lumps that can hit the 4096-byte size
        "PLAYPAL" or "COLORMAP" or "PNAMES" or "TEXTURE1" or "TEXTURE2"
        or "GENMIDI" or "DMXGUS" or "DMXGUSC" or "ENDOOM" or "DEMO1" or "DEMO2" or "DEMO3" or "DEMO4"
        // Map auxiliary lumps that can be exactly 4096 bytes for small maps
        or "REJECT" or "BLOCKMAP" or "NODES" or "SEGS" or "SSECTORS"
        or "VERTEXES" or "LINEDEFS" or "SIDEDEFS" or "SECTORS" or "THINGS"
        or "BEHAVIOR" or "TEXTMAP" or "ZNODES" or "SCRIPTS" => true,
        _ => false,
    };

    private static DoomPalette MakeGrayscalePalette()
    {
        var bytes = new byte[768];
        for (int i = 0; i < 256; i++) { bytes[i * 3] = (byte)i; bytes[i * 3 + 1] = (byte)i; bytes[i * 3 + 2] = (byte)i; }
        return DoomPalette.FromBytes(bytes);
    }
}
