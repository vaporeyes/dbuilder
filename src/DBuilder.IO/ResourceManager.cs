// ABOUTME: Focused port of UDB's DataManager - resolves flat/wall-texture/sprite names to RGBA images across one or more WADs.
// ABOUTME: A small subset of UDB's 3882-line DataManager: priority lookup (later resources override), per-name caching, palette/PNAMES/TEXTURE resolution.

/*
 * Resources are searched newest-first (the last-added WAD overrides earlier ones), matching Doom's
 * IWAD-then-PWAD load order. Wall textures are composed against the WAD that defines them (so each
 * texture uses its own PNAMES); patches missing from that WAD are skipped by the compositor.
 *
 * This produces CPU-side RGBA8 (ImageData); GL upload stays with the rendering host. The full UDB
 * DataManager additionally handles PK3/directory resources, lazy/threaded image loading, sprites
 * offsets, hi-res replacements and TEXTURES-lump definitions - layered on later as needed.
 */

using System;
using System.Collections.Generic;

namespace DBuilder.IO;

/// <summary>A decoded image: RGBA8 bytes (row-major, 4 bytes per pixel) with dimensions.</summary>
public sealed record ImageData(int Width, int Height, byte[] Rgba);

public sealed class ResourceManager : IDisposable
{
    private readonly List<WAD> wads = new();
    private readonly List<WAD> owned = new();

    private DoomPalette? palette;
    private bool paletteResolved;

    private readonly Dictionary<string, ImageData?> flatCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageData?> textureCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageData?> spriteCache = new(StringComparer.OrdinalIgnoreCase);

    // Per-WAD lazily-parsed texture catalogs and patch-name tables.
    private readonly Dictionary<WAD, Dictionary<string, DoomTextureDef>> texDefs = new();
    private readonly Dictionary<WAD, DoomPatchNames> patchNames = new();

    /// <summary>Adds a caller-owned WAD as a resource (highest priority = added last).</summary>
    public void AddResource(WAD wad) => wads.Add(wad);

    /// <summary>Opens a WAD file read-only and adds it as a resource; the manager disposes it.</summary>
    public void AddResource(string path)
    {
        var wad = new WAD(path, openreadonly: true);
        owned.Add(wad);
        wads.Add(wad);
    }

    /// <summary>The active palette (first PLAYPAL found searching newest resource first), or null.</summary>
    public DoomPalette? Palette
    {
        get
        {
            if (!paletteResolved)
            {
                paletteResolved = true;
                for (int i = wads.Count - 1; i >= 0; i--)
                {
                    var p = DoomPalette.FromWad(wads[i]);
                    if (p != null) { palette = p; break; }
                }
            }
            return palette;
        }
    }

    /// <summary>Resolves a 64x64 flat to RGBA, or null if not found / no palette. Cached by name.</summary>
    public ImageData? GetFlat(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "-") return null;
        if (flatCache.TryGetValue(name, out var cached)) return cached;

        ImageData? result = null;
        var pal = Palette;
        if (pal != null)
        {
            for (int i = wads.Count - 1; i >= 0 && result == null; i--)
            {
                byte[]? rgba = DoomFlatReader.DecodeRgba8(wads[i], name, pal);
                if (rgba != null) result = new ImageData(DoomFlatReader.Width, DoomFlatReader.Height, rgba);
            }
        }
        flatCache[name] = result;
        return result;
    }

    /// <summary>Resolves a composed wall texture to RGBA, or null. Cached by name.</summary>
    public ImageData? GetWallTexture(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "-") return null;
        if (textureCache.TryGetValue(name, out var cached)) return cached;

        ImageData? result = null;
        var pal = Palette;
        if (pal != null)
        {
            for (int i = wads.Count - 1; i >= 0 && result == null; i--)
            {
                var defs = TexDefsFor(wads[i]);
                if (!defs.TryGetValue(name, out var def)) continue;
                byte[]? rgba = DoomWallTextureCompositor.Compose(def, PatchNamesFor(wads[i]), wads[i], pal);
                if (rgba != null) result = new ImageData(def.Width, def.Height, rgba);
            }
        }
        textureCache[name] = result;
        return result;
    }

    /// <summary>Resolves a sprite/patch (picture format) to RGBA, or null. Cached by name.</summary>
    public ImageData? GetSprite(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (spriteCache.TryGetValue(name, out var cached)) return cached;

        ImageData? result = null;
        var pal = Palette;
        if (pal != null)
        {
            for (int i = wads.Count - 1; i >= 0 && result == null; i--)
            {
                var pic = DoomPictureReader.Decode(wads[i], name, pal);
                if (pic != null) result = new ImageData(pic.Width, pic.Height, pic.Rgba8);
            }
        }
        spriteCache[name] = result;
        return result;
    }

    private Dictionary<string, DoomTextureDef> TexDefsFor(WAD wad)
    {
        if (texDefs.TryGetValue(wad, out var d)) return d;
        d = new Dictionary<string, DoomTextureDef>(StringComparer.OrdinalIgnoreCase);
        foreach (var lumpName in new[] { "TEXTURE1", "TEXTURE2" })
        {
            var list = DoomTextureListReader.FromWad(wad, lumpName);
            if (list == null) continue;
            foreach (var def in list) d[def.Name] = def; // later TEXTURE lumps override earlier
        }
        texDefs[wad] = d;
        return d;
    }

    private DoomPatchNames PatchNamesFor(WAD wad)
    {
        if (patchNames.TryGetValue(wad, out var p)) return p;
        p = DoomPatchNames.FromWad(wad) ?? DoomPatchNames.Empty;
        patchNames[wad] = p;
        return p;
    }

    public void Dispose()
    {
        foreach (var w in owned) w.Dispose();
        owned.Clear();
        wads.Clear();
    }
}
