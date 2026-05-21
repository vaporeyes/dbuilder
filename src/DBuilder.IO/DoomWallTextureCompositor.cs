// ABOUTME: Composes a Doom wall texture by alpha-blitting its referenced patches onto a transparent canvas at each patch's origin offset.
// ABOUTME: Patches are looked up by PNAMES index then by name in the WAD; their pixels are decoded through DoomPictureReader.

namespace DBuilder.IO;

public static class DoomWallTextureCompositor
{
    /// <summary>
    /// Composes the named wall texture into a Width*Height RGBA8 buffer.  Returns null when any required patch is missing or the def has no usable patches.
    /// Transparent pixels in the source patches and uncovered canvas remain RGBA=0.
    /// </summary>
    public static byte[]? Compose(DoomTextureDef def, DoomPatchNames pnames, WAD wad, DoomPalette palette)
    {
        if (def.Width <= 0 || def.Height <= 0) return null;
        if (def.Patches.Count == 0) return null;

        byte[] canvas = new byte[def.Width * def.Height * 4];
        bool anyPatched = false;

        foreach (var patch in def.Patches)
        {
            if (patch.PatchIndex < 0 || patch.PatchIndex >= pnames.Length) continue;
            string pname = pnames[patch.PatchIndex];
            if (string.IsNullOrEmpty(pname)) continue;

            var lump = wad.FindLump(pname);
            if (lump == null) continue;

            var pic = DoomPictureReader.Decode(lump.Stream.ReadAllBytes(), palette);
            if (pic == null) continue;

            BlitOpaque(canvas, def.Width, def.Height, pic.Rgba8, pic.Width, pic.Height, patch.OriginX, patch.OriginY);
            anyPatched = true;
        }

        return anyPatched ? canvas : null;
    }

    /// <summary>Convenience: locate def by name across multiple lists (typically [TEXTURE1, TEXTURE2]) and compose.</summary>
    public static byte[]? Compose(string textureName, IEnumerable<List<DoomTextureDef>> defLists, DoomPatchNames pnames, WAD wad, DoomPalette palette)
    {
        textureName = textureName.ToUpperInvariant();
        foreach (var list in defLists)
        {
            foreach (var def in list)
            {
                if (def.Name == textureName) return Compose(def, pnames, wad, palette);
            }
        }
        return null;
    }

    // Copies opaque pixels (alpha > 0) from src into dst, with src placed at (dstX, dstY).
    // Pixels outside the dst rectangle are clipped. Transparent src pixels do not overwrite dst.
    private static void BlitOpaque(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, int dstX, int dstY)
    {
        for (int sy = 0; sy < srcH; sy++)
        {
            int dy = dstY + sy;
            if (dy < 0 || dy >= dstH) continue;

            for (int sx = 0; sx < srcW; sx++)
            {
                int dx = dstX + sx;
                if (dx < 0 || dx >= dstW) continue;

                int srcIdx = (sy * srcW + sx) * 4;
                if (src[srcIdx + 3] == 0) continue; // transparent source pixel

                int dstIdx = (dy * dstW + dx) * 4;
                dst[dstIdx + 0] = src[srcIdx + 0];
                dst[dstIdx + 1] = src[srcIdx + 1];
                dst[dstIdx + 2] = src[srcIdx + 2];
                dst[dstIdx + 3] = src[srcIdx + 3];
            }
        }
    }
}
