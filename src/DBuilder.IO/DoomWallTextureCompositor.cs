// ABOUTME: Composes a Doom wall texture by alpha-blitting its referenced patches onto a transparent canvas at each patch's origin offset.
// ABOUTME: Patches are looked up by PNAMES index then by name in the WAD; their pixels are decoded through DoomPictureReader.

using System.Collections.Generic;

namespace DBuilder.IO;

public static class DoomWallTextureCompositor
{
    /// <summary>
    /// Composes the named wall texture into a Width*Height RGBA8 buffer.  Returns null when any required patch is missing or the def has no usable patches.
    /// Transparent pixels in the source patches and uncovered canvas remain RGBA=0.
    /// </summary>
    public static byte[]? Compose(
        DoomTextureDef def,
        DoomPatchNames pnames,
        WAD wad,
        DoomPalette palette,
        Func<string, Lump?>? findPatch = null,
        bool fixNegativePatchOffsets = true,
        bool fixMaskedPatchOffsets = true)
    {
        findPatch ??= wad.FindLump;
        return Compose(
            def,
            pnames,
            patchName =>
            {
                var lump = findPatch(patchName);
                if (lump == null) return null;

                var pic = DoomPictureReader.Decode(lump.Stream.ReadAllBytes(), palette);
                return pic == null ? null : new ImageData(pic.Width, pic.Height, pic.Rgba8, pic.OffsetX, pic.OffsetY);
            },
            fixNegativePatchOffsets,
            fixMaskedPatchOffsets);
    }

    public static byte[]? Compose(
        DoomTextureDef def,
        DoomPatchNames pnames,
        Func<string, ImageData?> findPatch,
        bool fixNegativePatchOffsets = true,
        bool fixMaskedPatchOffsets = true)
    {
        if (def.Width <= 0 || def.Height <= 0) return null;
        if (def.Patches.Count == 0) return null;

        byte[] canvas = new byte[def.Width * def.Height * 4];
        var patches = new List<(DoomTexturePatch Patch, ImageData Image)>();

        foreach (var patch in def.Patches)
        {
            if (patch.PatchIndex < 0 || patch.PatchIndex >= pnames.Length) continue;
            string pname = pnames[patch.PatchIndex];
            if (string.IsNullOrEmpty(pname)) continue;

            var image = findPatch(pname);
            if (image == null) continue;

            patches.Add((patch, image));
        }

        if (patches.Count == 0) return null;

        var columnPatches = new int[def.Width];
        var columnMasked = new bool[def.Width];
        if (!fixNegativePatchOffsets || !fixMaskedPatchOffsets)
        {
            foreach (var (patch, image) in patches)
            {
                bool masked = IsMasked(image);
                for (int sx = 0; sx < image.Width; sx++)
                {
                    int x = patch.OriginX + sx;
                    if (x < 0 || x >= def.Width) continue;
                    if (!fixNegativePatchOffsets) columnPatches[x]++;
                    if (!fixMaskedPatchOffsets && masked) columnMasked[x] = true;
                }
            }
        }

        foreach (var (patch, image) in patches)
            BlitClassic(canvas, def.Width, def.Height, image, patch.OriginX, patch.OriginY, fixNegativePatchOffsets, fixMaskedPatchOffsets, columnPatches, columnMasked);

        return canvas;
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

    private static bool IsMasked(ImageData image)
    {
        for (int i = 3; i < image.Rgba.Length; i += 4)
            if (image.Rgba[i] < 255)
                return true;

        return false;
    }

    // Copies opaque pixels from src into dst, including UDB's classic patch-offset compatibility modes.
    private static void BlitClassic(
        byte[] dst,
        int dstW,
        int dstH,
        ImageData src,
        int dstX,
        int dstY,
        bool fixNegativePatchOffsets,
        bool fixMaskedPatchOffsets,
        int[] columnPatches,
        bool[] columnMasked)
    {
        for (int sx = 0; sx < src.Width; sx++)
        {
            int x = dstX + sx;
            int drawHeight = src.Height;

            if (dstY < 0
                && !fixNegativePatchOffsets
                && x >= 0
                && x < columnPatches.Length
                && columnPatches[x] > 1
                && !(!fixMaskedPatchOffsets && x < columnMasked.Length && columnMasked[x]))
            {
                drawHeight = src.Height + dstY;
            }

            for (int sy = 0; sy < drawHeight; sy++)
            {
                int srcIdx = (sy * src.Width + sx) * 4;
                if (src.Rgba[srcIdx + 3] == 0) continue;

                int realY = dstY;
                if (!fixMaskedPatchOffsets && x >= 0 && x < columnMasked.Length && columnMasked[x])
                {
                    if (x < columnPatches.Length && columnPatches[x] == 1) realY = 0;
                }
                else if (dstY < 0 && !fixNegativePatchOffsets)
                {
                    realY = 0;
                }

                int y = realY + sy;
                if (x < 0 || x >= dstW || y < 0 || y >= dstH) continue;

                int dstIdx = (y * dstW + x) * 4;
                dst[dstIdx + 0] = src.Rgba[srcIdx + 0];
                dst[dstIdx + 1] = src.Rgba[srcIdx + 1];
                dst[dstIdx + 2] = src.Rgba[srcIdx + 2];
                dst[dstIdx + 3] = src.Rgba[srcIdx + 3];
            }
        }
    }
}
