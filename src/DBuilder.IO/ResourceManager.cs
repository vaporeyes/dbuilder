// ABOUTME: Resolves flat/wall-texture/sprite names to RGBA images across one or more resources (WAD or PK3).
// ABOUTME: Searches newest-first (last-added overrides), caches by name, and resolves the palette from any resource.

/*
 * Resources are searched newest-first (the last-added overrides earlier ones), matching Doom's IWAD-then-PWAD
 * load order. WAD textures are composed against their own PNAMES/TEXTUREx; PK3 resources resolve folder-based
 * PNG (or Doom-format) entries under flats/, textures/, patches/, sprites/, graphics/.
 *
 * This produces CPU-side RGBA8 (ImageData); GL upload stays with the rendering host. Not yet handled: lazy/
 * threaded loading, sprite offsets, hi-res replacements and TEXTURES-lump composite definitions.
 */

using System;
using System.Collections.Generic;
using System.IO;

namespace DBuilder.IO;

/// <summary>A decoded image: RGBA8 bytes (row-major, 4 bytes per pixel) with dimensions.</summary>
public sealed record ImageData(int Width, int Height, byte[] Rgba);

public sealed class ResourceManager : IDisposable
{
    private readonly List<IResourceReader> readers = new();

    private DoomPalette? palette;
    private bool paletteResolved;

    private readonly Dictionary<string, ImageData?> flatCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageData?> textureCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageData?> spriteCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Adds a caller-owned WAD as a resource (highest priority = added last).</summary>
    public void AddResource(WAD wad) { readers.Add(new WadResourceReader(wad, owns: false)); Invalidate(); }

    /// <summary>Opens a WAD or PK3 (zip) file read-only and adds it as a resource (highest priority); the manager disposes it.</summary>
    public void AddResource(string path) => Add(path, asBase: false);

    /// <summary>Adds a resource at the lowest priority (e.g. the base IWAD, beneath an already-loaded PWAD).</summary>
    public void AddBaseResource(string path) => Add(path, asBase: true);

    private void Add(string path, bool asBase)
    {
        IResourceReader reader = LooksLikeZip(path)
            ? new Pk3ResourceReader(File.OpenRead(path), ownsStream: true)
            : new WadResourceReader(new WAD(path, openreadonly: true), owns: true);
        if (asBase) readers.Insert(0, reader); else readers.Add(reader);
        Invalidate();
    }

    // Resource set changed: drop cached lookups and re-resolve the palette (a newly added IWAD may provide it).
    private void Invalidate()
    {
        flatCache.Clear();
        textureCache.Clear();
        spriteCache.Clear();
        palette = null;
        paletteResolved = false;
    }

    private static bool LooksLikeZip(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".pk3" or ".pk7" or ".zip" or ".pkz") return true;
        if (ext == ".wad") return false;
        // Fall back to the file signature ("PK\x03\x04").
        try
        {
            using var fs = File.OpenRead(path);
            return fs.Length >= 4 && fs.ReadByte() == 'P' && fs.ReadByte() == 'K' && fs.ReadByte() == 3 && fs.ReadByte() == 4;
        }
        catch { return false; }
    }

    /// <summary>The active palette (first PLAYPAL found searching newest resource first), or null.</summary>
    public DoomPalette? Palette
    {
        get
        {
            if (!paletteResolved)
            {
                paletteResolved = true;
                for (int i = readers.Count - 1; i >= 0 && palette == null; i--)
                    palette = readers[i].GetPalette();
            }
            return palette;
        }
    }

    /// <summary>Resolves a flat to RGBA, or null. Cached by name.</summary>
    public ImageData? GetFlat(string name) => Resolve(name, flatCache, static (r, n, p) => r.GetFlat(n, p));

    /// <summary>Resolves a wall texture to RGBA, or null. Cached by name.</summary>
    public ImageData? GetWallTexture(string name) => Resolve(name, textureCache, static (r, n, p) => r.GetWallTexture(n, p));

    /// <summary>Resolves a sprite/patch to RGBA, or null. Cached by name.</summary>
    public ImageData? GetSprite(string name) => Resolve(name, spriteCache, static (r, n, p) => r.GetSprite(n, p));

    private ImageData? Resolve(string name, Dictionary<string, ImageData?> cache,
        Func<IResourceReader, string, DoomPalette?, ImageData?> lookup)
    {
        if (string.IsNullOrEmpty(name) || name == "-") return null;
        if (cache.TryGetValue(name, out var cached)) return cached;

        var pal = Palette;
        ImageData? result = null;
        // Newest resource wins; PK3/PNG entries resolve even when no palette is present.
        for (int i = readers.Count - 1; i >= 0 && result == null; i--)
            result = lookup(readers[i], name, pal);

        cache[name] = result;
        return result;
    }

    public void Dispose()
    {
        foreach (var r in readers) r.Dispose();
        readers.Clear();
    }
}
