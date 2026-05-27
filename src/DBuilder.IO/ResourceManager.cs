// ABOUTME: Resolves flat/wall-texture/sprite names to RGBA images across one or more resources (WAD or PK3).
// ABOUTME: Searches newest-first (last-added overrides), caches by name, and resolves the palette from any resource.

/*
 * Resources are searched newest-first (the last-added overrides earlier ones), matching Doom's IWAD-then-PWAD
 * load order. WAD textures are composed against their own PNAMES/TEXTUREx; PK3 resources resolve folder-based
 * PNG (or Doom-format) entries under flats/, textures/, patches/, sprites/, graphics/.
 *
 * This produces CPU-side RGBA8 (ImageData); GL upload stays with the rendering host. Not yet handled: lazy/
 * threaded loading, full sprite offsets and advanced TEXTURES-lump composite definitions.
 */

using System;
using System.Collections.Generic;
using System.IO;

namespace DBuilder.IO;

/// <summary>
/// A decoded image: RGBA8 bytes (row-major, 4 bytes per pixel) with dimensions and optional render offsets
/// (sprite hot-spot: OffsetX from the left, OffsetY from the top; 0,0 means unset/centered).
/// </summary>
public sealed record ImageData(int Width, int Height, byte[] Rgba, int OffsetX = 0, int OffsetY = 0);

public sealed class ResourceTextureSetInfo
{
    private readonly HashSet<string> textures;
    private readonly HashSet<string> flats;

    public ResourceTextureSetInfo(string name, IEnumerable<string> textures, IEnumerable<string> flats)
    {
        Name = name;
        this.textures = new HashSet<string>(textures, StringComparer.OrdinalIgnoreCase);
        this.flats = new HashSet<string>(flats, StringComparer.OrdinalIgnoreCase);
    }

    public string Name { get; }
    public IReadOnlyCollection<string> Textures => textures;
    public IReadOnlyCollection<string> Flats => flats;

    public bool TextureExists(string name) => textures.Contains(name);
    public bool FlatExists(string name) => flats.Contains(name);

    public void MixTexturesAndFlats()
    {
        var flatNames = new List<string>(flats);
        foreach (string texture in textures) flats.Add(texture);
        foreach (string flat in flatNames) textures.Add(flat);
    }
}

public sealed class ResourceManager : IDisposable
{
    private readonly List<IResourceReader> readers = new();

    private DoomPalette? palette;
    private bool paletteResolved;

    private readonly Dictionary<string, ImageData?> flatCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageData?> textureCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageData?> spriteCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VoxelDefinition> voxelDefs = new(StringComparer.OrdinalIgnoreCase);
    private bool voxelDefsBuilt;
    private readonly List<Modeldef> modelDefs = new();
    private bool modelDefsBuilt;
    private Gldefs? gldefs;
    private bool gldefsBuilt;
    private MapInfo? mapInfo;
    private bool mapInfoBuilt;

    // TEXTURES-lump composite definitions, keyed by name per usage (newest resource wins).
    private readonly Dictionary<string, TexturesDef> wallDefs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TexturesDef> flatDefs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TexturesDef> spriteDefs = new(StringComparer.OrdinalIgnoreCase);
    private bool defsBuilt;

    // ANIMDEFS camera textures are virtual images that UDB exposes as both wall textures and flats.
    private readonly Dictionary<string, CameraTextureDef> cameraTextures = new(StringComparer.OrdinalIgnoreCase);
    private bool cameraTexturesBuilt;

    // ANIMDEFS sequences: each animated name maps to (the ordered frame names, per-frame tics, this name's phase).
    private readonly Dictionary<string, (List<string> Seq, int Tics, int Phase)> flatAnims = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (List<string> Seq, int Tics, int Phase)> texAnims = new(StringComparer.OrdinalIgnoreCase);
    private bool animsBuilt;
    private const double TicsPerSecond = 35.0; // Doom runs at 35 tics/second

    // Switch off<->on texture pairs (from SWITCHES/ANIMDEFS); vanilla pairs fall back to the SW1/SW2 convention.
    private readonly Dictionary<string, string> switchPairs = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string>? textureNameSet;
    private bool switchesBuilt;

    /// <summary>Adds a caller-owned WAD as a resource (highest priority = added last).</summary>
    public void AddResource(WAD wad) { readers.Add(new WadResourceReader(wad, owns: false)); Invalidate(); }

    /// <summary>Opens a WAD or PK3 (zip) file read-only and adds it as a resource (highest priority); the manager disposes it.</summary>
    public void AddResource(string path) => Add(path, asBase: false);

    /// <summary>Adds a resource at the lowest priority (e.g. the base IWAD, beneath an already-loaded PWAD).</summary>
    public void AddBaseResource(string path) => Add(path, asBase: true);

    private void Add(string path, bool asBase)
    {
        IResourceReader reader =
            Directory.Exists(path) ? new DirectoryResourceReader(path)
            : LooksLikeZip(path) ? new Pk3ResourceReader(File.OpenRead(path), ownsStream: true, displayName: Path.GetFileName(path))
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
        voxelDefs.Clear();
        voxelDefsBuilt = false;
        modelDefs.Clear();
        modelDefsBuilt = false;
        gldefs = null;
        gldefsBuilt = false;
        mapInfo = null;
        mapInfoBuilt = false;
        wallDefs.Clear();
        flatDefs.Clear();
        spriteDefs.Clear();
        defsBuilt = false;
        cameraTextures.Clear();
        cameraTexturesBuilt = false;
        flatAnims.Clear();
        texAnims.Clear();
        animsBuilt = false;
        switchPairs.Clear();
        textureNameSet = null;
        switchesBuilt = false;
        palette = null;
        paletteResolved = false;
    }

    /// <summary>
    /// The paired texture for a switch (the on texture for an off SW1 texture, and vice versa), or null.
    /// Uses explicit SWITCHES/ANIMDEFS pairs, falling back to the SW1/SW2 naming convention when the partner exists.
    /// </summary>
    public string? GetSwitchPair(string name)
    {
        EnsureSwitches();
        if (switchPairs.TryGetValue(name, out var p)) return p;
        if (name.Length > 3)
        {
            string prefix = name.Substring(0, 3);
            string? partner = prefix.Equals("SW1", StringComparison.OrdinalIgnoreCase) ? "SW2" + name.Substring(3)
                            : prefix.Equals("SW2", StringComparison.OrdinalIgnoreCase) ? "SW1" + name.Substring(3) : null;
            if (partner != null && (textureNameSet ??= new HashSet<string>(GetTextureNames(), StringComparer.OrdinalIgnoreCase)).Contains(partner))
                return partner;
        }
        return null;
    }

    private void EnsureSwitches()
    {
        if (switchesBuilt) return;
        switchesBuilt = true;
        void Add(string off, string on) { switchPairs[off] = on; switchPairs[on] = off; }
        foreach (var bytes in GetLumpBytesAll("SWITCHES"))
            foreach (var sw in BoomSwitches.Parse(bytes)) Add(sw.OffTexture, sw.OnTexture);
        foreach (var text in GetTextLumps("ANIMDEFS"))
            foreach (var sw in AnimdefsParser.Parse(text).Switches) Add(sw.OffTexture, sw.OnTexture);
    }

    /// <summary>True when any ANIMDEFS animations were defined (so the view should keep redrawing).</summary>
    public bool HasAnimations { get { EnsureAnimations(); return flatAnims.Count > 0 || texAnims.Count > 0; } }

    /// <summary>The flat name to display at the given time for an animated base, else the name unchanged.</summary>
    public string CurrentFlatFrame(string name, double seconds) => CurrentFrame(flatAnims, name, seconds);

    /// <summary>The wall-texture name to display at the given time for an animated base, else the name unchanged.</summary>
    public string CurrentTextureFrame(string name, double seconds) => CurrentFrame(texAnims, name, seconds);

    private string CurrentFrame(Dictionary<string, (List<string> Seq, int Tics, int Phase)> anims, string name, double seconds)
    {
        EnsureAnimations();
        if (!anims.TryGetValue(name, out var a) || a.Seq.Count < 2) return name;
        int step = (int)(seconds * TicsPerSecond / a.Tics);
        return a.Seq[((a.Phase + step) % a.Seq.Count + a.Seq.Count) % a.Seq.Count];
    }

    // Builds the per-name frame sequences from (in increasing priority) the vanilla Doom defaults, the Boom
    // ANIMATED lump, and the ZDoom ANIMDEFS lump. Ranges are resolved against the (sorted) name lists.
    private void EnsureAnimations()
    {
        if (animsBuilt) return;
        animsBuilt = true;
        var flatNames = GetFlatNames();
        var texNames = GetTextureNames();

        // 1) Hardcoded vanilla animations (IWADs ship no animation lump). Doom + Heretic; absent names ignored.
        foreach (var e in BoomAnimated.DoomDefaults)
            AddRangeAnim(e.IsTexture, e.First, e.Last, e.Tics, flatNames, texNames);
        foreach (var e in BoomAnimated.HereticDefaults)
            AddRangeAnim(e.IsTexture, e.First, e.Last, e.Tics, flatNames, texNames);

        // 2) Boom ANIMATED binary lumps (PWADs), oldest first.
        foreach (var bytes in GetLumpBytesAll("ANIMATED"))
            foreach (var e in BoomAnimated.Parse(bytes))
                AddRangeAnim(e.IsTexture, e.First, e.Last, e.Tics, flatNames, texNames);

        // 3) ZDoom ANIMDEFS (highest priority; also supports explicit frame blocks).
        foreach (var text in GetTextLumps("ANIMDEFS"))
        {
            foreach (var def in AnimdefsParser.Parse(text).Animations)
            {
                bool flat = def.Kind == AnimKind.Flat;
                var names = ExpandAnim(def, flat ? flatNames : texNames);
                if (names.Count < 2) continue;
                int tics = def.IsRange ? def.RangeTics : (def.Frames.Count > 0 ? def.Frames[0].Tics : 8);
                if (tics <= 0) tics = 8;
                Register(flat ? flatAnims : texAnims, names, tics);
            }
        }
    }

    private void AddRangeAnim(bool isTexture, string first, string last, int tics,
        IReadOnlyList<string> flatNames, IReadOnlyList<string> texNames)
    {
        var names = ExpandAnim(new AnimationDef { Kind = isTexture ? AnimKind.Texture : AnimKind.Flat, FirstName = first, RangeLast = last },
                               isTexture ? texNames : flatNames);
        if (names.Count < 2) return;
        Register(isTexture ? texAnims : flatAnims, names, tics <= 0 ? 8 : tics);
    }

    private static void Register(Dictionary<string, (List<string> Seq, int Tics, int Phase)> map, List<string> names, int tics)
    {
        for (int k = 0; k < names.Count; k++) map[names[k]] = (names, tics, k);
    }

    private List<byte[]> GetLumpBytesAll(string name)
    {
        var result = new List<byte[]>();
        foreach (var r in readers)
            if (r.GetLumpBytes(name) is { } b) result.Add(b);
        return result;
    }

    // Resolves an animation's ordered frame names. A range slices the (sorted) name list from first..last inclusive.
    private static List<string> ExpandAnim(AnimationDef def, IReadOnlyList<string> ordered)
    {
        if (!def.IsRange)
        {
            var list = new List<string>(def.Frames.Count);
            foreach (var f in def.Frames) list.Add(f.Texture);
            return list;
        }
        int first = IndexOf(ordered, def.FirstName), last = IndexOf(ordered, def.RangeLast!);
        if (first < 0 || last < 0 || last < first) return new List<string>();
        var result = new List<string>(last - first + 1);
        for (int i = first; i <= last; i++) result.Add(ordered[i]);
        return result;
    }

    private static int IndexOf(IReadOnlyList<string> list, string name)
    {
        for (int i = 0; i < list.Count; i++)
            if (string.Equals(list[i], name, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
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

    private void EnsureCameraTextures()
    {
        if (cameraTexturesBuilt) return;
        cameraTexturesBuilt = true;
        foreach (var text in GetTextLumps("ANIMDEFS"))
        {
            foreach (var texture in AnimdefsParser.Parse(text).CameraTextures)
                cameraTextures[texture.Name] = texture;
        }
    }

    private void EnsureVoxelDefs()
    {
        if (voxelDefsBuilt) return;
        voxelDefsBuilt = true;
        foreach (var text in GetTextLumps("VOXELDEF"))
        {
            foreach (var entry in VoxeldefParser.Parse(text).Entries)
                voxelDefs[entry.Key] = entry.Value;
        }
    }

    private void EnsureModelDefs()
    {
        if (modelDefsBuilt) return;
        modelDefsBuilt = true;
        foreach (var text in GetTextLumps("MODELDEF"))
            modelDefs.AddRange(ModeldefParser.Parse(text));
    }

    private void EnsureGldefs()
    {
        if (gldefsBuilt) return;
        gldefsBuilt = true;
        gldefs = new Gldefs();
        foreach (var reader in readers)
        {
            string? text = reader.GetTextLump("GLDEFS");
            if (text == null) continue;
            var parsed = GldefsParser.Parse(text, reader.GetTextResource);
            MergeGldefs(gldefs, parsed);
        }
    }

    public Gldefs GetGldefs()
    {
        EnsureGldefs();
        return gldefs!;
    }

    private void EnsureMapInfo()
    {
        if (mapInfoBuilt) return;
        mapInfoBuilt = true;
        mapInfo = new MapInfo();
        foreach (var reader in readers)
        {
            foreach (string lumpName in new[] { "MAPINFO", "ZMAPINFO" })
            {
                string? text = reader.GetTextLump(lumpName);
                if (text == null) continue;
                mapInfo.MergeFrom(MapInfo.Parse(text, reader.GetTextResource));
            }
        }
    }

    public MapInfo GetMapInfo()
    {
        EnsureMapInfo();
        return mapInfo!;
    }

    private static void MergeGldefs(Gldefs target, Gldefs source)
    {
        foreach (var light in source.Lights) target.Lights[light.Key] = light.Value;
        target.Objects.AddRange(source.Objects);
        target.GlowFlats.AddRange(source.GlowFlats);
        target.GlowTextures.AddRange(source.GlowTextures);
        foreach (var glow in source.Glows) target.Glows[glow.Key] = glow.Value;
        foreach (var skybox in source.Skyboxes) target.Skyboxes[skybox.Key] = skybox.Value;
    }

    /// <summary>MODELDEF blocks discovered from loaded resources, oldest resource first.</summary>
    public IReadOnlyList<Modeldef> GetModelDefs()
    {
        EnsureModelDefs();
        return modelDefs;
    }

    /// <summary>Raw model or skin bytes by path, searching newest resource first.</summary>
    public byte[]? GetModelResourceBytes(string path)
    {
        for (int i = readers.Count - 1; i >= 0; i--)
            if (readers[i].GetModelResourceBytes(path) is { } bytes)
                return bytes;
        return null;
    }

    /// <summary>Raw model or skin bytes using a MODELDEF block path plus a referenced file name.</summary>
    public byte[]? GetModelResourceBytes(Modeldef def, string file) => GetModelResourceBytes(CombineModelPath(def.Path, file));

    public static string CombineModelPath(string path, string file)
    {
        string normalizedFile = file.Replace('\\', '/').TrimStart('/');
        string normalizedPath = path.Replace('\\', '/').Trim('/');
        return normalizedPath.Length == 0 ? normalizedFile : normalizedPath + "/" + normalizedFile;
    }

    /// <summary>All directly discoverable voxel model names across resources, sorted and de-duplicated.</summary>
    public IReadOnlyList<string> GetVoxelNames()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reader in readers)
            foreach (var name in reader.VoxelNames())
                set.Add(name);
        var list = new List<string>(set);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    /// <summary>Raw voxel model bytes, searching newest resource first.</summary>
    public byte[]? GetVoxelBytes(string name)
    {
        for (int i = readers.Count - 1; i >= 0; i--)
            if (readers[i].GetVoxelBytes(name) is { } bytes)
                return bytes;
        return null;
    }

    /// <summary>The voxel model name mapped to a sprite by VOXELDEF, or null when none is declared.</summary>
    public string? GetVoxelModelForSprite(string sprite)
    {
        EnsureVoxelDefs();
        string normalized = NormalizeSpriteForVoxel(sprite);
        return voxelDefs.TryGetValue(normalized, out var definition) ? definition.ModelName : null;
    }

    private static ImageData CreateCameraTexture(CameraTextureDef texture)
    {
        var rgba = new byte[texture.Width * texture.Height * 4];
        for (int i = 0; i < texture.Width * texture.Height; i++)
            rgba[i * 4 + 3] = 255;

        int line = Math.Max(1, Math.Min(texture.Width, texture.Height) / 32);
        int arm = Math.Max(line * 4, Math.Min(texture.Width, texture.Height) / 6);
        DrawCameraCorner(rgba, texture.Width, texture.Height, 0, 0, line, arm);
        DrawCameraCorner(rgba, texture.Width, texture.Height, texture.Width - 1, 0, line, arm);
        DrawCameraCorner(rgba, texture.Width, texture.Height, 0, texture.Height - 1, line, arm);
        DrawCameraCorner(rgba, texture.Width, texture.Height, texture.Width - 1, texture.Height - 1, line, arm);
        DrawCameraRec(rgba, texture.Width, texture.Height, line);
        return new ImageData(texture.Width, texture.Height, rgba);
    }

    private ImageData? CreateVoxelSprite(string sprite)
    {
        string normalized = NormalizeSpriteForVoxel(sprite);
        string? model = GetVoxelModelForSprite(normalized);
        if (model == null && GetVoxelBytes(normalized) != null) model = normalized;
        if (model == null || GetVoxelBytes(model) == null) return null;

        const int size = 64;
        var rgba = new byte[size * size * 4];
        for (int i = 0; i < size * size; i++)
        {
            rgba[i * 4] = 12;
            rgba[i * 4 + 1] = 14;
            rgba[i * 4 + 2] = 18;
            rgba[i * 4 + 3] = 255;
        }

        for (int y = 10; y < 54; y++)
        {
            int half = y < 32 ? y - 10 : 53 - y;
            for (int x = 32 - half; x <= 32 + half; x++)
            {
                int i = (y * size + x) * 4;
                rgba[i] = (byte)(40 + y * 2);
                rgba[i + 1] = (byte)(120 + x);
                rgba[i + 2] = 220;
            }
        }

        for (int y = 22; y < 42; y++)
            for (int x = 20; x < 44; x++)
                if (((x + y) & 3) == 0)
                {
                    int i = (y * size + x) * 4;
                    rgba[i] = 236;
                    rgba[i + 1] = 236;
                    rgba[i + 2] = 120;
                }

        return new ImageData(size, size, rgba, OffsetX: size / 2, OffsetY: size - 1);
    }

    private static string NormalizeSpriteForVoxel(string sprite)
    {
        string name = Path.GetFileNameWithoutExtension(sprite.Replace('\\', '/')).ToUpperInvariant();
        return name.Length > 4 ? name.Substring(0, 4) : name;
    }

    private static void DrawCameraCorner(byte[] rgba, int width, int height, int x, int y, int line, int arm)
    {
        int xDirection = x == 0 ? 1 : -1;
        int yDirection = y == 0 ? 1 : -1;
        for (int i = 0; i < arm; i++)
        {
            for (int t = 0; t < line; t++)
            {
                SetCameraPixel(rgba, width, height, x + i * xDirection, y + t * yDirection, 64, 224, 180);
                SetCameraPixel(rgba, width, height, x + t * xDirection, y + i * yDirection, 64, 224, 180);
            }
        }
    }

    private static void DrawCameraRec(byte[] rgba, int width, int height, int line)
    {
        int size = Math.Max(line * 3, Math.Min(width, height) / 14);
        int margin = Math.Max(line * 2, Math.Min(width, height) / 20);
        for (int y = margin; y < margin + size; y++)
            for (int x = margin; x < margin + size; x++)
                SetCameraPixel(rgba, width, height, x, y, 224, 48, 48);
    }

    private static void SetCameraPixel(byte[] rgba, int width, int height, int x, int y, byte red, byte green, byte blue)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) return;
        int i = (y * width + x) * 4;
        rgba[i] = red;
        rgba[i + 1] = green;
        rgba[i + 2] = blue;
        rgba[i + 3] = 255;
    }

    // Composes a TEXTURES definition into RGBA by blitting each patch (resolved as a raw single image).
    private ImageData? ComposeTextures(TexturesDef def)
    {
        if (def.Width <= 0 || def.Height <= 0) return null;
        var buf = new byte[def.Width * def.Height * 4]; // transparent
        var pal = Palette;
        foreach (var patch in def.Patches)
        {
            if (patch.Skip) continue;
            var img = ResolvePatchRaw(patch.Name, pal);
            if (img != null) Blit(buf, def.Width, def.Height, img, patch);
        }
        return new ImageData(def.Width, def.Height, buf, def.OffsetX, def.OffsetY);
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

    private static void Blit(byte[] dst, int dw, int dh, ImageData src, TexturesPatch patch)
    {
        int outWidth = patch.Rotation is 90 or 270 ? src.Height : src.Width;
        int outHeight = patch.Rotation is 90 or 270 ? src.Width : src.Height;
        int patchAlpha = (int)Math.Round(Math.Clamp(patch.Alpha, 0.0, 1.0) * 255.0);

        for (int sy = 0; sy < outHeight; sy++)
        {
            int dy = patch.Y + sy;
            if (dy < 0 || dy >= dh) continue;
            for (int sx = 0; sx < outWidth; sx++)
            {
                int dx = patch.X + sx;
                if (dx < 0 || dx >= dw) continue;
                MapPatchPixel(sx, sy, src.Width, src.Height, patch, out int srcX, out int srcY);
                int si = (srcY * src.Width + srcX) * 4;
                int a = src.Rgba[si + 3] * patchAlpha / 255;
                if (a == 0) continue;

                byte sr = src.Rgba[si];
                byte sg = src.Rgba[si + 1];
                byte sb = src.Rgba[si + 2];
                ApplyPatchBlend(patch, ref sr, ref sg, ref sb);

                int di = (dy * dw + dx) * 4;
                if (a == 255)
                {
                    dst[di] = sr; dst[di + 1] = sg;
                    dst[di + 2] = sb; dst[di + 3] = 255;
                }
                else
                {
                    int ia = 255 - a;
                    dst[di] = (byte)((sr * a + dst[di] * ia) / 255);
                    dst[di + 1] = (byte)((sg * a + dst[di + 1] * ia) / 255);
                    dst[di + 2] = (byte)((sb * a + dst[di + 2] * ia) / 255);
                    dst[di + 3] = (byte)Math.Max(dst[di + 3], a);
                }
            }
        }
    }

    private static void MapPatchPixel(int x, int y, int width, int height, TexturesPatch patch, out int sourceX, out int sourceY)
    {
        switch (patch.Rotation)
        {
            case 90:
                sourceX = y;
                sourceY = height - 1 - x;
                break;
            case 180:
                sourceX = width - 1 - x;
                sourceY = height - 1 - y;
                break;
            case 270:
                sourceX = width - 1 - y;
                sourceY = x;
                break;
            default:
                sourceX = x;
                sourceY = y;
                break;
        }

        if (patch.FlipX) sourceX = width - 1 - sourceX;
        if (patch.FlipY) sourceY = height - 1 - sourceY;
    }

    private static void ApplyPatchBlend(TexturesPatch patch, ref byte red, ref byte green, ref byte blue)
    {
        if (patch.BlendStyle == TexturesPatchBlendStyle.Blend)
        {
            red = (byte)(red * patch.BlendRed / 255);
            green = (byte)(green * patch.BlendGreen / 255);
            blue = (byte)(blue * patch.BlendBlue / 255);
        }
        else if (patch.BlendStyle == TexturesPatchBlendStyle.Tint)
        {
            double tint = patch.BlendAlpha / 255.0;
            double inverse = 1.0 - tint;
            red = (byte)((red / 255.0 * inverse + patch.BlendRed / 255.0 * tint) * 255.0);
            green = (byte)((green / 255.0 * inverse + patch.BlendGreen / 255.0 * tint) * 255.0);
            blue = (byte)((blue / 255.0 * inverse + patch.BlendBlue / 255.0 * tint) * 255.0);
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

    /// <summary>All wall-texture names across resources (incl. TEXTURES defs), sorted and de-duplicated.</summary>
    public IReadOnlyList<string> GetTextureNames() => CollectNames(static r => r.TextureNames(), wallDefs, includeCameraTextures: true);

    /// <summary>All flat names across resources (incl. TEXTURES Flat defs), sorted and de-duplicated.</summary>
    public IReadOnlyList<string> GetFlatNames() => CollectNames(static r => r.FlatNames(), flatDefs, includeCameraTextures: true);

    public IReadOnlyList<ResourceTextureSetInfo> GetResourceTextureSets()
    {
        var sets = new List<ResourceTextureSetInfo>(readers.Count);
        foreach (var reader in readers)
        {
            var textures = new List<string>(reader.TextureNames());
            var flats = new List<string>(reader.FlatNames());
            if (reader.GetTextLump("ANIMDEFS") is { } text)
            {
                foreach (var texture in AnimdefsParser.Parse(text).CameraTextures)
                {
                    textures.Add(texture.Name);
                    flats.Add(texture.Name);
                }
            }
            sets.Add(new ResourceTextureSetInfo(reader.DisplayName, textures, flats));
        }
        return sets;
    }

    private List<string> CollectNames(Func<IResourceReader, IEnumerable<string>> select, Dictionary<string, TexturesDef> defs, bool includeCameraTextures)
    {
        EnsureDefs();
        if (includeCameraTextures) EnsureCameraTextures();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in readers) foreach (var n in select(r)) set.Add(n);
        foreach (var k in defs.Keys) set.Add(k);
        if (includeCameraTextures) foreach (var k in cameraTextures.Keys) set.Add(k);
        var list = new List<string>(set);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
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
    public ImageData? GetFlat(string name) => Resolve(name, flatCache, flatDefs, static (r, n, p) => r.GetFlat(n, p), includeCameraTextures: true);

    /// <summary>Resolves a wall texture to RGBA, or null. Cached by name.</summary>
    public ImageData? GetWallTexture(string name) => Resolve(name, textureCache, wallDefs, static (r, n, p) => r.GetWallTexture(n, p), includeCameraTextures: true);

    /// <summary>Resolves a sprite/patch to RGBA, or null. Tries rotation variants so e.g. TROOA0 finds TROOA1. Cached by name.</summary>
    public ImageData? GetSprite(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "-") return null;
        if (spriteCache.TryGetValue(name, out var cached)) return cached;

        ImageData? result = ResolveCore(name, spriteDefs, static (r, n, p) => r.GetSprite(n, p), includeCameraTextures: false);
        if (result == null)
            foreach (var variant in RotationVariants(name))
            {
                result = ResolveCore(variant, spriteDefs, static (r, n, p) => r.GetSprite(n, p), includeCameraTextures: false);
                if (result != null) break;
            }
        result ??= CreateVoxelSprite(name);

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
        Func<IResourceReader, string, DoomPalette?, ImageData?> lookup, bool includeCameraTextures)
    {
        if (string.IsNullOrEmpty(name) || name == "-") return null;
        if (cache.TryGetValue(name, out var cached)) return cached;
        var result = ResolveCore(name, defs, lookup, includeCameraTextures);
        cache[name] = result;
        return result;
    }

    // Resolves a name without caching: virtual camera textures first, then TEXTURES definitions and resource images.
    private ImageData? ResolveCore(string name, Dictionary<string, TexturesDef> defs,
        Func<IResourceReader, string, DoomPalette?, ImageData?> lookup, bool includeCameraTextures)
    {
        if (includeCameraTextures)
        {
            EnsureCameraTextures();
            if (cameraTextures.TryGetValue(name, out var texture)) return CreateCameraTexture(texture);
        }

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
