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

    // TEXTURES-lump composite definitions, keyed by name per usage (newest resource wins).
    private readonly Dictionary<string, TexturesDef> wallDefs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TexturesDef> flatDefs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TexturesDef> spriteDefs = new(StringComparer.OrdinalIgnoreCase);
    private bool defsBuilt;

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

    // Resource set changed: drop cached lookups, definitions and the palette (a newly added IWAD may provide them).
    private void Invalidate()
    {
        flatCache.Clear();
        textureCache.Clear();
        spriteCache.Clear();
        wallDefs.Clear();
        flatDefs.Clear();
        spriteDefs.Clear();
        defsBuilt = false;
        palette = null;
        paletteResolved = false;
    }

    // Parses each resource's TEXTURES lump (oldest first, so newer resources override) into per-usage tables.
    private void EnsureDefs()
    {
        if (defsBuilt) return;
        defsBuilt = true;
        foreach (var reader in readers)
        {
            string? text = reader.GetTextLump("TEXTURES");
            if (text == null) continue;
            foreach (var def in TexturesParser.Parse(text))
            {
                switch (def.Type)
                {
                    case TexturesType.WallTexture: wallDefs[def.Name] = def; break;
                    case TexturesType.Flat: flatDefs[def.Name] = def; break;
                    case TexturesType.Sprite:
                    case TexturesType.Graphic: spriteDefs[def.Name] = def; break;
                    default: // Texture: usable as both a wall and a flat
                        wallDefs[def.Name] = def; flatDefs[def.Name] = def; break;
                }
            }
        }
    }

    // Composes a TEXTURES definition into RGBA by blitting each patch (resolved as a raw single image).
    private ImageData? ComposeTextures(TexturesDef def)
    {
        if (def.Width <= 0 || def.Height <= 0) return null;
        var buf = new byte[def.Width * def.Height * 4]; // transparent
        var pal = Palette;
        foreach (var patch in def.Patches)
        {
            var img = ResolvePatchRaw(patch.Name, pal);
            if (img != null) Blit(buf, def.Width, def.Height, img, patch.X, patch.Y, patch.FlipX, patch.FlipY);
        }
        return new ImageData(def.Width, def.Height, buf);
    }

    // Resolves a patch as a single image across resources (never via TEXTURES defs, so composition can't recurse).
    private ImageData? ResolvePatchRaw(string name, DoomPalette? pal)
    {
        for (int i = readers.Count - 1; i >= 0; i--)
        {
            var img = readers[i].GetSprite(name, pal);
            if (img != null) return img;
        }
        return null;
    }

    private static void Blit(byte[] dst, int dw, int dh, ImageData src, int px, int py, bool flipX, bool flipY)
    {
        for (int sy = 0; sy < src.Height; sy++)
        {
            int dy = py + sy;
            if (dy < 0 || dy >= dh) continue;
            int srcY = flipY ? src.Height - 1 - sy : sy;
            for (int sx = 0; sx < src.Width; sx++)
            {
                int dx = px + sx;
                if (dx < 0 || dx >= dw) continue;
                int srcX = flipX ? src.Width - 1 - sx : sx;
                int si = (srcY * src.Width + srcX) * 4;
                byte a = src.Rgba[si + 3];
                if (a == 0) continue;
                int di = (dy * dw + dx) * 4;
                if (a == 255)
                {
                    dst[di] = src.Rgba[si]; dst[di + 1] = src.Rgba[si + 1];
                    dst[di + 2] = src.Rgba[si + 2]; dst[di + 3] = 255;
                }
                else
                {
                    int ia = 255 - a;
                    dst[di] = (byte)((src.Rgba[si] * a + dst[di] * ia) / 255);
                    dst[di + 1] = (byte)((src.Rgba[si + 1] * a + dst[di + 1] * ia) / 255);
                    dst[di + 2] = (byte)((src.Rgba[si + 2] * a + dst[di + 2] * ia) / 255);
                    dst[di + 3] = Math.Max(dst[di + 3], a);
                }
            }
        }
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

    /// <summary>The text of a named lump (e.g. DECORATE) from every resource that has one, oldest first.</summary>
    public List<string> GetTextLumps(string name)
    {
        var result = new List<string>();
        foreach (var reader in readers)
            if (reader.GetTextLump(name) is { } text) result.Add(text);
        return result;
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
    public ImageData? GetFlat(string name) => Resolve(name, flatCache, flatDefs, static (r, n, p) => r.GetFlat(n, p));

    /// <summary>Resolves a wall texture to RGBA, or null. Cached by name.</summary>
    public ImageData? GetWallTexture(string name) => Resolve(name, textureCache, wallDefs, static (r, n, p) => r.GetWallTexture(n, p));

    /// <summary>Resolves a sprite/patch to RGBA, or null. Tries rotation variants so e.g. TROOA0 finds TROOA1. Cached by name.</summary>
    public ImageData? GetSprite(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "-") return null;
        if (spriteCache.TryGetValue(name, out var cached)) return cached;

        ImageData? result = ResolveCore(name, spriteDefs, static (r, n, p) => r.GetSprite(n, p));
        if (result == null)
            foreach (var variant in RotationVariants(name))
            {
                result = ResolveCore(variant, spriteDefs, static (r, n, p) => r.GetSprite(n, p));
                if (result != null) break;
            }

        spriteCache[name] = result;
        return result;
    }

    // Sprite lumps are SPRITE + frame + rotation. A name asked with rotation 0 may exist only as rotation 1
    // (or vice versa), and a 5-char name (no rotation) needs a digit appended.
    private static IEnumerable<string> RotationVariants(string name)
    {
        if (name.Length == 5) { yield return name + "0"; yield return name + "1"; yield break; }
        if (name.Length >= 6 && char.IsDigit(name[^1]))
        {
            string baseName = name.Substring(0, name.Length - 1);
            char last = name[^1];
            if (last != '1') yield return baseName + "1";
            if (last != '0') yield return baseName + "0";
        }
    }

    private ImageData? Resolve(string name, Dictionary<string, ImageData?> cache, Dictionary<string, TexturesDef> defs,
        Func<IResourceReader, string, DoomPalette?, ImageData?> lookup)
    {
        if (string.IsNullOrEmpty(name) || name == "-") return null;
        if (cache.TryGetValue(name, out var cached)) return cached;
        var result = ResolveCore(name, defs, lookup);
        cache[name] = result;
        return result;
    }

    // Resolves a name without caching: a TEXTURES definition first, then newest-resource single-image lookup.
    private ImageData? ResolveCore(string name, Dictionary<string, TexturesDef> defs,
        Func<IResourceReader, string, DoomPalette?, ImageData?> lookup)
    {
        EnsureDefs();
        if (defs.TryGetValue(name, out var def)) return ComposeTextures(def);

        var pal = Palette;
        for (int i = readers.Count - 1; i >= 0; i--)
        {
            var img = lookup(readers[i], name, pal);
            if (img != null) return img;
        }
        return null;
    }

    public void Dispose()
    {
        foreach (var r in readers) r.Dispose();
        readers.Clear();
    }
}
