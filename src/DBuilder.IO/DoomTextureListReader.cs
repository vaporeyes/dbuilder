// ABOUTME: Parses TEXTURE1/TEXTURE2 lumps - the list of named wall surfaces and their patch references.
// ABOUTME: Auto-detects Doom-classic vs Strife format by reading the initial patchCount field (0 = Doom-classic + 4 unused bytes follow).

using System.IO;

namespace DBuilder.IO;

public static class DoomTextureListReader
{
    /// <summary>Parses a TEXTURE1 or TEXTURE2 lump body. Returns the list of textures defined in the lump.</summary>
    public static List<DoomTextureDef> Parse(byte[] data)
    {
        var result = new List<DoomTextureDef>();
        if (data.Length < 4) return result;

        using var ms = new MemoryStream(data);
        using var r = new BinaryReader(ms);

        uint numTextures = r.ReadUInt32();
        if (numTextures == 0 || numTextures > 100_000) return result;

        // Read the offset table.
        uint[] offsets = new uint[numTextures];
        for (uint i = 0; i < numTextures; i++) offsets[i] = r.ReadUInt32();

        for (uint i = 0; i < numTextures; i++)
        {
            uint off = offsets[i];
            if (off >= data.Length) continue;

            ms.Seek(off, SeekOrigin.Begin);
            var def = ReadOneDef(r, ms);
            if (def != null) result.Add(def);
        }

        return result;
    }

    public static List<DoomTextureDef>? FromWad(WAD wad, string lumpName)
    {
        var lump = wad.FindLump(lumpName);
        if (lump == null) return null;
        return Parse(lump.Stream.ReadAllBytes());
    }

    private static DoomTextureDef? ReadOneDef(BinaryReader r, MemoryStream ms)
    {
        if (ms.Position + 18 > ms.Length) return null;

        byte[] nameBytes = r.ReadBytes(8);
        ushort flags = r.ReadUInt16();
        byte scaleX = r.ReadByte();
        byte scaleY = r.ReadByte();
        short width = r.ReadInt16();
        short height = r.ReadInt16();
        short patchCount = r.ReadInt16();

        DoomTextureFormat format;
        if (patchCount == 0)
        {
            // Doom-classic: the "patchCount" read above is actually the high word of the unused columndirectory field.
            // Skip 2 more unused bytes, then read the real count.
            if (ms.Position + 4 > ms.Length) return null;
            ms.Seek(2, SeekOrigin.Current);
            patchCount = r.ReadInt16();
            format = DoomTextureFormat.DoomClassic;
        }
        else
        {
            format = DoomTextureFormat.Strife;
        }

        // Defensive bounds
        if (width <= 0 || height <= 0 || patchCount <= 0) return null;
        // Doom textures are bounded to a few hundred pixels in practice; reject malformed gigantic ones.
        if (width > 4096 || height > 4096 || patchCount > 4096) return null;

        var patches = new List<DoomTexturePatch>(patchCount);
        int patchRecordSize = (format == DoomTextureFormat.DoomClassic) ? 10 : 6;
        if (ms.Position + (long)patchRecordSize * patchCount > ms.Length) return null;

        for (int p = 0; p < patchCount; p++)
        {
            short originX = r.ReadInt16();
            short originY = r.ReadInt16();
            ushort patchIndex = r.ReadUInt16();
            if (format == DoomTextureFormat.DoomClassic)
            {
                // Skip 4 unused bytes (stepdir + colormap), preserved from 1993 Doom format.
                ms.Seek(4, SeekOrigin.Current);
            }
            patches.Add(new DoomTexturePatch(originX, originY, patchIndex));
        }

        string name = TrimAsciiUpper(nameBytes);
        bool worldPanning = (flags & TX_WORLDPANNING) != 0;
        return new DoomTextureDef(name, width, height, patches, worldPanning, format);
    }

    // Worldpanning flag bit on the TEXTURE1 flags field. Honored by the engine but not by our raster compositor.
    private const ushort TX_WORLDPANNING = 0x8000;

    private static string TrimAsciiUpper(byte[] bytes)
    {
        int end = 0;
        while (end < bytes.Length && bytes[end] != 0) end++;
        return System.Text.Encoding.ASCII.GetString(bytes, 0, end).Trim().ToUpperInvariant();
    }
}
