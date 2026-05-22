// ABOUTME: Interactive 2D map viewer: loads either a real .wad (Doom binary or UDMF text) or falls back to an embedded
// ABOUTME: synthetic UDMF sample, then renders via DBuilder.Rendering with mouse pan, wheel zoom, R-to-reset, color-coded lines + thing markers.
//
// Usage:
//   dotnet run                       # use embedded synthetic UDMF sample
//   dotnet run -- path/to/file.wad   # load first map in the WAD (auto-detects Doom-binary, Hexen-binary, or UDMF)
//   dotnet run -- file.wad MAP05     # load a specific map by marker lump name

using System.IO;
using System.Numerics;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;
using DBuilder.MapViewer;
using DBuilder.Rendering;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Vec2D = DBuilder.Geometry.Vector2D;
using SilkVec2I = Silk.NET.Maths.Vector2D<int>;
using DBRenderDevice = DBuilder.Rendering.RenderDevice;
using DBShader = DBuilder.Rendering.Shader;
using DBPrimitiveType = DBuilder.Rendering.PrimitiveType;
using DBVertexBuffer = DBuilder.Rendering.VertexBuffer;

// ============================================================
// 1. Resolve input source: command-line .wad or embedded sample.
// ============================================================
MapSet? map = null;
string source;
// Sector index -> ARGB color from that sector's floor flat (averaged). Empty when no WAD source / no PLAYPAL.
Dictionary<int, uint> sectorFloorColors = new();
// Unique flat name -> 64x64 RGBA8 bytes decoded through PLAYPAL. Drives the textured sector fills.
Dictionary<string, byte[][]> flatRgba = new(StringComparer.OrdinalIgnoreCase);
// Composed sky wall texture (variable size, typically 256x128) for sectors with F_SKY* ceiling.  Null when no sky is found.
SkyTextureData? sky = null;
// Unique wall texture name -> composed RGBA8 + dimensions for the wall-ribbon overlay.
Dictionary<string, SkyTextureData> wallTextures = new(StringComparer.OrdinalIgnoreCase);

if (args.Length >= 1 && File.Exists(args[0]))
{
    string wadPath = args[0];
    string? requestedMap = args.Length >= 2 ? args[1].ToUpperInvariant() : null;
    map = LoadFromWad(wadPath, requestedMap, out source, out sectorFloorColors, out flatRgba, out sky, out wallTextures);
    int animatedCount = 0;
    foreach (var frames in flatRgba.Values) if (frames.Length > 1) animatedCount++;
    if (animatedCount > 0) Console.WriteLine($"[wad]   {animatedCount} animated flat chains detected");
    if (map == null)
    {
        Console.WriteLine($"[load]  Could not load a map from '{wadPath}'.");
        return 1;
    }
}
else
{
    if (args.Length >= 1)
        Console.WriteLine($"[load]  File '{args[0]}' not found; falling back to embedded sample.");

    map = UdmfMapLoader.Load(SampleMap.Udmf, out var parser);
    if (map == null)
    {
        Console.WriteLine($"UDMF parse failed (line {parser.ErrorLine}): {parser.ErrorDescription}");
        return 1;
    }
    source = "embedded UDMF sample";
}

Console.WriteLine($"[load]  source={source}  ns='{map.Namespace}'  vertices={map.Vertices.Count}  linedefs={map.Linedefs.Count}  sectors={map.Sectors.Count}  things={map.Things.Count}");

static MapSet? LoadFromWad(string path, string? mapName, out string source, out Dictionary<int, uint> sectorFloorColors, out Dictionary<string, byte[][]> flatRgba, out SkyTextureData? sky, out Dictionary<string, SkyTextureData> wallTextures)
{
    source = "";
    sectorFloorColors = new();
    flatRgba = new(StringComparer.OrdinalIgnoreCase);
    sky = null;
    wallTextures = new(StringComparer.OrdinalIgnoreCase);
    using var fs = File.OpenRead(path);
    var ms = new MemoryStream();
    fs.CopyTo(ms);
    ms.Position = 0;

    using var wad = new WAD(ms, openreadonly: true, virtualFilename: path);
    Console.WriteLine($"[wad]   {(wad.IsIWAD ? "IWAD" : "PWAD")}{(wad.IsOfficialIWAD ? " (official)" : "")}  {wad.Lumps.Count} lumps");

    // Resolve map name: explicit -> use it; otherwise scan for the first marker lump that has either TEXTMAP or VERTEXES nearby.
    string? marker = mapName;
    if (marker == null)
    {
        for (int i = 0; i < wad.Lumps.Count; i++)
        {
            var l = wad.Lumps[i];
            if (l.Length != 0) continue;
            // Look one or two lumps ahead for a known map sub-lump.
            for (int j = i + 1; j < Math.Min(i + 6, wad.Lumps.Count); j++)
            {
                string nm = wad.Lumps[j].Name;
                if (nm == "TEXTMAP" || nm == "VERTEXES")
                {
                    marker = l.Name;
                    break;
                }
            }
            if (marker != null) break;
        }
    }

    if (marker == null)
    {
        Console.WriteLine("[wad]   No map markers found.");
        return null;
    }
    Console.WriteLine($"[wad]   Loading map '{marker}'");

    // Check for TEXTMAP (UDMF) before VERTEXES (binary).
    int markerIdx = wad.FindLumpIndex(marker);
    if (markerIdx >= 0)
    {
        for (int j = markerIdx + 1; j < Math.Min(markerIdx + 6, wad.Lumps.Count); j++)
        {
            if (wad.Lumps[j].Name == "TEXTMAP")
            {
                byte[] textBytes = wad.Lumps[j].Stream.ReadAllBytes();
                string udmfText = System.Text.Encoding.ASCII.GetString(textBytes);
                source = $"{Path.GetFileName(path)} [{marker}] UDMF";
                return UdmfMapLoader.Load(udmfText, out _);
            }
            if (wad.Lumps[j].Name == "VERTEXES") break;
        }
    }

    MapSet? loaded;
    if (HexenMapLoader.IsHexenFormat(wad, marker))
    {
        source = $"{Path.GetFileName(path)} [{marker}] Hexen-binary";
        loaded = HexenMapLoader.Load(wad, marker);
    }
    else
    {
        source = $"{Path.GetFileName(path)} [{marker}] Doom-binary";
        loaded = DoomMapLoader.Load(wad, marker);
    }

    if (loaded != null)
    {
        sectorFloorColors = BuildSectorFloorColors(loaded, wad);
        flatRgba = LoadUniqueFlats(loaded, wad);
        sky = LoadSkyTexture(loaded, wad);
        wallTextures = LoadUniqueWallTextures(loaded, wad);
    }
    return loaded;
}

/// <summary>
/// Composes the WAD's sky wall texture (SKY1 / SKY2 / SKY3 / SKY4 / RSKY1, tried in that order) via the
/// PNAMES + TEXTURE1/2 + patch pipeline.  Returns null when the WAD has no recognizable sky texture or no
/// sector in the map needs one.
/// </summary>
static SkyTextureData? LoadSkyTexture(MapSet map, WAD wad)
{
    // Only do the work when at least one sector actually uses sky.
    bool anySky = false;
    foreach (var sector in map.Sectors)
    {
        if (IsSkyName(sector.CeilTexture) || IsSkyName(sector.FloorTexture)) { anySky = true; break; }
    }
    if (!anySky) return null;

    var palette = DoomPalette.FromWad(wad);
    if (palette == null) return null;

    var pnames = DoomPatchNames.FromWad(wad) ?? DoomPatchNames.Empty;
    var lists = new List<List<DoomTextureDef>>();
    var t1 = DoomTextureListReader.FromWad(wad, "TEXTURE1"); if (t1 != null) lists.Add(t1);
    var t2 = DoomTextureListReader.FromWad(wad, "TEXTURE2"); if (t2 != null) lists.Add(t2);
    if (lists.Count == 0) return null;

    // Try common sky names in order. Different episodes/games use different ones.
    string[] candidates = { "SKY1", "SKY2", "SKY3", "SKY4", "RSKY1", "RSKY2", "RSKY3" };
    foreach (var name in candidates)
    {
        foreach (var list in lists)
        {
            foreach (var def in list)
            {
                if (!string.Equals(def.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                byte[]? rgba = DoomWallTextureCompositor.Compose(def, pnames, wad, palette);
                if (rgba != null)
                {
                    Console.WriteLine($"[wad]   sky texture '{def.Name}' composed ({def.Width}x{def.Height})");
                    return new SkyTextureData(rgba, def.Width, def.Height);
                }
            }
        }
    }
    return null;
}

static bool IsSkyName(string? name) => name != null && name.StartsWith("F_SKY", StringComparison.OrdinalIgnoreCase);

/// <summary>Determines the representative wall texture name for a linedef - what the viewer should ribbon-render along it.</summary>
static string? PickWallTextureName(Linedef line)
{
    // One-sided line: front mid is the wall.
    // Two-sided portal: prefer mid (sometimes set as a translucent overlay), then upper/lower.
    var front = line.Front;
    if (front == null) return null;
    string mid = front.MidTexture;
    string upr = front.HighTexture;
    string low = front.LowTexture;
    string Pick(string s) => (string.IsNullOrEmpty(s) || s == "-") ? "" : s;
    string chosen = Pick(mid);
    if (chosen == "") chosen = Pick(low);
    if (chosen == "") chosen = Pick(upr);
    return chosen == "" ? null : chosen;
}

/// <summary>For each unique wall texture referenced by the map's linedefs, compose it via PNAMES + TEXTURE1/2 + patches.</summary>
static Dictionary<string, SkyTextureData> LoadUniqueWallTextures(MapSet map, WAD wad)
{
    var result = new Dictionary<string, SkyTextureData>(StringComparer.OrdinalIgnoreCase);
    var palette = DoomPalette.FromWad(wad);
    if (palette == null) return result;

    var pnames = DoomPatchNames.FromWad(wad) ?? DoomPatchNames.Empty;
    var lists = new List<List<DoomTextureDef>>();
    var t1 = DoomTextureListReader.FromWad(wad, "TEXTURE1"); if (t1 != null) lists.Add(t1);
    var t2 = DoomTextureListReader.FromWad(wad, "TEXTURE2"); if (t2 != null) lists.Add(t2);
    if (lists.Count == 0) return result;

    // Build a name -> def lookup once so we don't scan the lists per query.
    var byName = new Dictionary<string, DoomTextureDef>(StringComparer.OrdinalIgnoreCase);
    foreach (var list in lists)
        foreach (var def in list)
            if (!byName.ContainsKey(def.Name)) byName[def.Name] = def;

    // Collect unique names actually referenced by the map's linedefs.
    var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var line in map.Linedefs)
    {
        string? n = PickWallTextureName(line);
        if (n != null) wanted.Add(n);
    }

    foreach (var name in wanted)
    {
        if (!byName.TryGetValue(name, out var def)) continue;
        byte[]? rgba = DoomWallTextureCompositor.Compose(def, pnames, wad, palette);
        if (rgba != null) result[name] = new SkyTextureData(rgba, def.Width, def.Height);
    }

    Console.WriteLine($"[wad]   composed {result.Count} unique wall textures of {wanted.Count} requested");
    return result;
}

/// <summary>For each unique floor flat name in the map, decode it through PLAYPAL and stash the 64x64 RGBA8 bytes.</summary>
static Dictionary<string, byte[][]> LoadUniqueFlats(MapSet map, WAD wad)
{
    var result = new Dictionary<string, byte[][]>(StringComparer.OrdinalIgnoreCase);
    var palette = DoomPalette.FromWad(wad);
    if (palette == null) return result;

    // Cache per-frame decoded RGBA so animation chains shared across many sectors only decode each frame once.
    var frameCache = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
    byte[]? DecodeFlat(string name)
    {
        if (frameCache.TryGetValue(name, out var cached)) return cached;
        try
        {
            byte[]? rgba = DoomFlatReader.DecodeRgba8(wad, name, palette);
            frameCache[name] = rgba;
            return rgba;
        }
        catch
        {
            frameCache[name] = null;
            return null;
        }
    }

    foreach (var sector in map.Sectors)
    {
        string name = sector.FloorTexture;
        if (string.IsNullOrEmpty(name) || name == "-") continue;
        if (name.StartsWith("F_SKY", StringComparison.OrdinalIgnoreCase)) continue;
        if (result.ContainsKey(name)) continue;

        var chain = FlatAnimations.GetChainStarting(name);
        if (chain != null)
        {
            // Animated: load all frames, rotated so this sector's referenced flat is frame 0.
            var frames = new List<byte[]>(chain.Count);
            foreach (var frameName in chain)
            {
                byte[]? rgba = DecodeFlat(frameName);
                if (rgba != null) frames.Add(rgba);
            }
            // Require at least 2 distinct frames to count as animated; fall back to static if the WAD is missing frames.
            if (frames.Count >= 2) result[name] = frames.ToArray();
            else if (frames.Count == 1) result[name] = new[] { frames[0] };
        }
        else
        {
            byte[]? rgba = DecodeFlat(name);
            if (rgba != null) result[name] = new[] { rgba };
        }
    }

    Console.WriteLine($"[wad]   loaded {result.Count} flat textures for sector fills");
    foreach (var (name, frames) in result)
    {
        if (frames.Length > 1)
            Console.WriteLine($"[wad]   animated chain '{name}' -> {frames.Length} frames");
    }
    return result;
}

/// <summary>For each sector, decode its floor flat through PLAYPAL and average the RGB to a single tint color. Cached by flat name so shared textures decode once.</summary>
static Dictionary<int, uint> BuildSectorFloorColors(MapSet map, WAD wad)
{
    var result = new Dictionary<int, uint>();
    var palette = DoomPalette.FromWad(wad);
    if (palette == null) return result;

    var byFlatName = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

    foreach (var sector in map.Sectors)
    {
        string name = sector.FloorTexture;
        if (string.IsNullOrEmpty(name) || name == "-")
        {
            result[sector.Index] = 0xff202020;
            continue;
        }
        if (name.StartsWith("F_SKY", StringComparison.OrdinalIgnoreCase))
        {
            result[sector.Index] = 0xff3060a0; // sky-ish blue
            continue;
        }

        if (byFlatName.TryGetValue(name, out uint cached))
        {
            result[sector.Index] = cached;
            continue;
        }

        uint color = 0xff404040; // fallback when flat is missing
        try
        {
            byte[]? rgba = DoomFlatReader.DecodeRgba8(wad, name, palette);
            if (rgba != null)
            {
                long sumR = 0, sumG = 0, sumB = 0;
                int count = rgba.Length / 4;
                for (int i = 0; i < rgba.Length; i += 4)
                {
                    sumR += rgba[i + 0];
                    sumG += rgba[i + 1];
                    sumB += rgba[i + 2];
                }
                byte r = (byte)(sumR / count);
                byte g = (byte)(sumG / count);
                byte b = (byte)(sumB / count);
                color = 0xff000000u | ((uint)r << 16) | ((uint)g << 8) | b;
            }
        }
        catch { /* fallback color stays */ }

        byFlatName[name] = color;
        result[sector.Index] = color;
    }

    Console.WriteLine($"[wad]   resolved {byFlatName.Count} unique floor textures across {map.Sectors.Count} sectors");
    return result;
}

var (mapMinX, mapMinY, mapMaxX, mapMaxY) = map.Bounds();
double mapW = mapMaxX - mapMinX;
double mapH = mapMaxY - mapMinY;
double mapCx = (mapMinX + mapMaxX) * 0.5;
double mapCy = (mapMinY + mapMaxY) * 0.5;

// ============================================================
// 2. Build geometry buffers (lines + thing markers).
// ============================================================
const uint colorOneSided = 0xffd0d0d0;     // solid wall
const uint colorTwoSided = 0xff70a0ff;     // portal / two-sided
const uint colorActioned = 0xffffd040;     // any linedef with a special

// Two color modes, toggled by 'F': type-based coloring vs. sector-floor-tinted.
// Tinted mode falls back to type colors when no sector floor data is available.
bool tintBySectorFloor = sectorFloorColors.Count > 0;

uint TypeColor(Linedef l) =>
    l.Action != 0 ? colorActioned :
    (l.Front != null && l.Back != null) ? colorTwoSided :
    colorOneSided;

uint SectorBlendedColor(Linedef l)
{
    // Action-tagged lines always pop to gold so important interactions remain visible.
    if (l.Action != 0) return colorActioned;

    uint? front = null, back = null;
    if (l.Front?.Sector != null && sectorFloorColors.TryGetValue(l.Front.Sector.Index, out uint fc)) front = fc;
    if (l.Back?.Sector  != null && sectorFloorColors.TryGetValue(l.Back.Sector.Index,  out uint bc)) back  = bc;

    if (front == null && back == null) return TypeColor(l);
    if (front != null && back == null) return BrightenForVisibility(front.Value);
    if (back  != null && front == null) return BrightenForVisibility(back.Value);
    return BrightenForVisibility(AverageColor(front!.Value, back!.Value));
}

static uint AverageColor(uint a, uint b)
{
    byte ar = (byte)((a >> 16) & 0xFF), ag = (byte)((a >> 8) & 0xFF), ab = (byte)(a & 0xFF);
    byte br = (byte)((b >> 16) & 0xFF), bg = (byte)((b >> 8) & 0xFF), bb = (byte)(b & 0xFF);
    return 0xff000000u | ((uint)((ar + br) / 2) << 16) | ((uint)((ag + bg) / 2) << 8) | (uint)((ab + bb) / 2);
}

// Doom floor textures are often quite dark; boost lines so they read against the dark window background.
static uint BrightenForVisibility(uint c)
{
    byte r = (byte)((c >> 16) & 0xFF), g = (byte)((c >> 8) & 0xFF), b = (byte)(c & 0xFF);
    r = (byte)Math.Min(255, r + (255 - r) / 2);
    g = (byte)Math.Min(255, g + (255 - g) / 2);
    b = (byte)Math.Min(255, b + (255 - b) / 2);
    return 0xff000000u | ((uint)r << 16) | ((uint)g << 8) | b;
}

uint LineColor(Linedef l) => tintBySectorFloor ? SectorBlendedColor(l) : TypeColor(l);

// Helper: rebuilds the line vertex buffer using the current LineColor mode.
FlatVertex[] BuildLineVerts()
{
    var verts = new FlatVertex[map.Linedefs.Count * 2];
    for (int i = 0; i < map.Linedefs.Count; i++)
    {
        var l = map.Linedefs[i];
        int c = unchecked((int)LineColor(l));
        verts[i * 2 + 0] = MkFV(l.Start.Position, c);
        verts[i * 2 + 1] = MkFV(l.End.Position, c);
    }
    return verts;
}

var lineVerts = BuildLineVerts();

// ============================================================
// 2.5. Triangulate sectors and bucket triangles by floor flat name.
// Each bucket becomes one textured draw call: bind the flat's GL texture, draw all triangles using it.
// Triangles without a known flat (no WAD source, "-" texture, F_SKY) fall back to a flat-tint bucket keyed by "".
// ============================================================
// Flat name -> list of FlatVertex (3 per triangle). "" key = untextured fallback (uses dimmed sector tint as vertex color).
var flatBuckets = new Dictionary<string, List<FlatVertex>>(StringComparer.OrdinalIgnoreCase);
int triangulatedSectors = 0;
int failedSectors = 0;
int totalSectorTris = 0;

foreach (var sector in map.Sectors)
{
    if (sector.Sidedefs.Count == 0) continue;

    Triangulation tri;
    try { tri = Triangulation.Create(sector); }
    catch { failedSectors++; continue; }

    if (tri.Vertices.Count == 0) { failedSectors++; continue; }
    triangulatedSectors++;

    // Sky-ceilinged or sky-floored sectors get routed to a special "__SKY" bucket using the composed sky wall texture.
    // Other sectors use their floor flat (Doom flats tile every 64 world units).
    bool isSky = sky != null && (IsSkyName(sector.CeilTexture) || IsSkyName(sector.FloorTexture));
    string flatName = sector.FloorTexture ?? "-";
    bool textured;
    string bucketKey;
    int texW, texH;
    if (isSky)
    {
        textured = true;
        bucketKey = "__SKY";
        texW = sky!.Width;
        texH = sky.Height;
    }
    else
    {
        textured = flatRgba.ContainsKey(flatName);
        bucketKey = textured ? flatName : "";
        texW = 64;
        texH = 64;
    }

    int vertexColor;
    if (textured)
    {
        // Sky uses full brightness (sky is its own emissive look). Other textures modulate by sector brightness.
        double brightness;
        if (isSky)
        {
            brightness = 1.0;
        }
        else
        {
            brightness = Math.Clamp(sector.Brightness / 255.0, 0.2, 1.0);
        }
        byte bb = (byte)(brightness * 255);
        vertexColor = unchecked((int)(0xff000000u | ((uint)bb << 16) | ((uint)bb << 8) | bb));
    }
    else
    {
        // No flat available - use the dimmed sector-tint we already compute, with useTexture=0.
        uint tint = sectorFloorColors.TryGetValue(sector.Index, out uint c) ? c : 0xff303030;
        vertexColor = unchecked((int)DimColor(tint, 0.55));
    }

    if (!flatBuckets.TryGetValue(bucketKey, out var bucket))
    {
        bucket = new List<FlatVertex>();
        flatBuckets[bucketKey] = bucket;
    }

    // Sloped floors can't tilt in this top-down ortho view, so we surface the slope as a per-vertex
    // brightness gradient: higher floor renders brighter. Flat sectors get factor 1.0 (no change).
    double slopeMinZ = 0, slopeRange = 0;
    if (sector.HasFloorSlope)
    {
        slopeMinZ = double.MaxValue;
        double maxZ = double.MinValue;
        foreach (var p in tri.Vertices)
        {
            double z = sector.GetFloorZ(p);
            if (z < slopeMinZ) slopeMinZ = z;
            if (z > maxZ) maxZ = z;
        }
        slopeRange = maxZ - slopeMinZ;
    }

    for (int i = 0; i < tri.Vertices.Count; i++)
    {
        var p = tri.Vertices[i];
        int c = vertexColor;
        if (sector.HasFloorSlope && slopeRange > 0)
        {
            double t = (sector.GetFloorZ(p) - slopeMinZ) / slopeRange; // 0 at lowest, 1 at highest
            c = ScaleColorRgb(vertexColor, 0.6 + 0.4 * t);
        }
        bucket.Add(new FlatVertex
        {
            x = (float)p.x, y = (float)p.y, z = 0, w = 1,
            c = c,
            u = (float)(p.x / texW),
            v = (float)(p.y / texH),
        });
        totalSectorTris++;
    }
}

static int ScaleColorRgb(int argb, double factor)
{
    uint u = unchecked((uint)argb);
    byte a = (byte)((u >> 24) & 0xFF);
    byte r = (byte)Math.Clamp(((u >> 16) & 0xFF) * factor, 0, 255);
    byte g = (byte)Math.Clamp(((u >> 8) & 0xFF) * factor, 0, 255);
    byte b = (byte)Math.Clamp((u & 0xFF) * factor, 0, 255);
    return unchecked((int)(((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b));
}
if (map.Sectors.Count > 0)
    Console.WriteLine($"[tri]   {triangulatedSectors} of {map.Sectors.Count} sectors triangulated ({totalSectorTris / 3} triangles, {failedSectors} failed, {flatBuckets.Count} flat buckets)");

static uint DimColor(uint c, double factor)
{
    byte r = (byte)((c >> 16) & 0xFF), g = (byte)((c >> 8) & 0xFF), b = (byte)(c & 0xFF);
    r = (byte)(r * factor); g = (byte)(g * factor); b = (byte)(b * factor);
    return 0xff000000u | ((uint)r << 16) | ((uint)g << 8) | b;
}

// ============================================================
// 2.6. Wall texture ribbons - one thin textured quad per linedef along its length, bucketed by wall texture.
// ============================================================
// Wall ribbons are world-space thin rectangles centered on each linedef. UV.u runs along the wall (length/texW),
// UV.v runs across the ribbon (0..1).
const double ribbonHalfThickness = 6.0; // 12 world units total - visible at default zoom on typical Doom maps

var wallBuckets = new Dictionary<string, List<FlatVertex>>(StringComparer.OrdinalIgnoreCase);
foreach (var line in map.Linedefs)
{
    string? texName = PickWallTextureName(line);
    if (texName == null || !wallTextures.TryGetValue(texName, out var tex)) continue;

    var a = line.Start.Position;
    var b = line.End.Position;
    double dx = b.x - a.x;
    double dy = b.y - a.y;
    double len = Math.Sqrt(dx * dx + dy * dy);
    if (len < 0.0001) continue; // skip zero-length linedefs

    // Perpendicular (rotated 90 degrees CCW) normalized to half-thickness.
    double px = -dy / len * ribbonHalfThickness;
    double py = dx / len * ribbonHalfThickness;

    // Quad corners walking start-left, end-left, end-right, start-right.
    var p1 = new Vec2D(a.x + px, a.y + py);
    var p2 = new Vec2D(b.x + px, b.y + py);
    var p3 = new Vec2D(b.x - px, b.y - py);
    var p4 = new Vec2D(a.x - px, a.y - py);

    float uMax = (float)(len / tex.Width);
    // Two-sided lines get a slightly dimmer ribbon so the line overlay still reads.
    bool twoSided = line.Front != null && line.Back != null;
    int color = twoSided ? unchecked((int)0xffa0a0a0) : unchecked((int)0xffffffff);

    if (!wallBuckets.TryGetValue(texName, out var bucket))
    {
        bucket = new List<FlatVertex>();
        wallBuckets[texName] = bucket;
    }

    // Two triangles for the quad: (p1, p2, p3) and (p1, p3, p4).
    bucket.Add(new FlatVertex { x = (float)p1.x, y = (float)p1.y, z = 0, w = 1, c = color, u = 0,    v = 0 });
    bucket.Add(new FlatVertex { x = (float)p2.x, y = (float)p2.y, z = 0, w = 1, c = color, u = uMax, v = 0 });
    bucket.Add(new FlatVertex { x = (float)p3.x, y = (float)p3.y, z = 0, w = 1, c = color, u = uMax, v = 1 });
    bucket.Add(new FlatVertex { x = (float)p1.x, y = (float)p1.y, z = 0, w = 1, c = color, u = 0,    v = 0 });
    bucket.Add(new FlatVertex { x = (float)p3.x, y = (float)p3.y, z = 0, w = 1, c = color, u = uMax, v = 1 });
    bucket.Add(new FlatVertex { x = (float)p4.x, y = (float)p4.y, z = 0, w = 1, c = color, u = 0,    v = 1 });
}
int wallRibbonTriCount = 0;
foreach (var v in wallBuckets.Values) wallRibbonTriCount += v.Count / 3;
if (wallBuckets.Count > 0)
    Console.WriteLine($"[wall]  {wallBuckets.Count} wall texture buckets, {wallRibbonTriCount} ribbon triangles");

// Thing markers: draw each thing as a small filled diamond (4 triangles -> 12 verts)
// Plus a short angle indicator line.
var thingTris = new List<FlatVertex>(map.Things.Count * 12);
var thingLines = new List<FlatVertex>(map.Things.Count * 2);
foreach (var t in map.Things)
{
    uint color = t.Type switch
    {
        1 => 0xff40ff40,        // player start - green
        2014 => 0xff60d0ff,     // health bonus - cyan
        2018 => 0xff8080ff,     // armor - blue
        >= 3000 and < 3100 => 0xffff5050, // monsters - red
        _ => 0xffffffff,
    };
    int c = unchecked((int)color);
    const double s = 12;
    var p = t.Position;
    // 4 triangles forming a filled diamond around the thing's position.
    var n = new Vec2D(p.x, p.y - s);
    var e = new Vec2D(p.x + s, p.y);
    var sV = new Vec2D(p.x, p.y + s);
    var w = new Vec2D(p.x - s, p.y);
    thingTris.Add(MkFV(p, c)); thingTris.Add(MkFV(n, c)); thingTris.Add(MkFV(e, c));
    thingTris.Add(MkFV(p, c)); thingTris.Add(MkFV(e, c)); thingTris.Add(MkFV(sV, c));
    thingTris.Add(MkFV(p, c)); thingTris.Add(MkFV(sV, c)); thingTris.Add(MkFV(w, c));
    thingTris.Add(MkFV(p, c)); thingTris.Add(MkFV(w, c)); thingTris.Add(MkFV(n, c));

    // Angle indicator
    double rad = t.Angle * Math.PI / 180.0;
    var tip = new Vec2D(p.x + Math.Cos(rad) * (s * 1.8), p.y + Math.Sin(rad) * (s * 1.8));
    thingLines.Add(MkFV(p, unchecked((int)0xff202020)));
    thingLines.Add(MkFV(tip, unchecked((int)0xff202020)));
}

// ============================================================
// 3. Camera state - simple 2D pan/zoom.
// ============================================================
double camX = mapCx;
double camY = mapCy;
double camZoom = 1.0; // world-units-per-pixel; recomputed on Load to fit map
bool dragging = false;
double dragLastX = 0, dragLastY = 0;

void ResetCamera(SilkVec2I windowSize)
{
    camX = mapCx;
    camY = mapCy;
    // Fit the map with a small margin.
    double zoomX = mapW / windowSize.X;
    double zoomY = mapH / windowSize.Y;
    camZoom = Math.Max(zoomX, zoomY) * 1.15;
    if (camZoom <= 0) camZoom = 1;
}

// ============================================================
// 4. Window + GL setup.
// ============================================================
const string VertexSrc = @"#version 330 core
layout(location=0) in vec4 a_pos;
layout(location=1) in vec4 a_color;
layout(location=2) in vec2 a_uv;
uniform mat4 projection;
out vec4 v_color;
out vec2 v_uv;
void main() {
    gl_Position = projection * vec4(a_pos.xyz, 1.0);
    v_color = a_color;
    v_uv = a_uv;
}";
const string FragmentSrc = @"#version 330 core
in vec4 v_color;
in vec2 v_uv;
uniform sampler2D tex0;
uniform float useTexture;
out vec4 frag;
void main() {
    vec4 sampled = texture(tex0, v_uv);
    frag = mix(v_color, sampled * v_color, useTexture);
}";

var opts = WindowOptions.Default with
{
    Size = new SilkVec2I(1100, 800),
    Title = "DBuilder map viewer  -  drag to pan  -  wheel to zoom  -  R to reset",
    API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
    VSync = true
};

using var window = Window.Create(opts);

DBRenderDevice? device = null;
DBShader? shader = null;
DBVertexBuffer? linesVb = null;
DBVertexBuffer? thingsTrisVb = null;
DBVertexBuffer? thingsLinesVb = null;
// One textured draw per unique flat. Bucket key "" is the untextured-fallback bucket.  Frames is null for static
// flats (the placeholder is used), length 1 for static-with-real-texture, or length N for animated chains.
var sectorBucketResources = new List<(string FlatName, DBVertexBuffer Vb, int TriCount, DBuilder.Rendering.Texture[]? Frames)>();
bool showSectorFills = totalSectorTris > 0;
// Wall ribbon GL resources: one VB + 1 texture per bucket.
var wallRibbonResources = new List<(string TexName, DBVertexBuffer Vb, int TriCount, DBuilder.Rendering.Texture Tex)>();
bool showWallRibbons = wallBuckets.Count > 0;
bool animationsEnabled = true;
// 1x1 white placeholder so the sampler always has something bound (avoids GL "unloadable sampler" warning during untextured draws).
DBuilder.Rendering.Texture? placeholderTex = null;
GL? gl = null;

window.Load += () =>
{
    gl = GL.GetApi(window);
    device = new DBRenderDevice(gl);
    shader = new DBShader(gl, VertexSrc, FragmentSrc);

    linesVb = new DBVertexBuffer(gl);
    device.SetBufferData(linesVb, lineVerts);

    thingsTrisVb = new DBVertexBuffer(gl);
    device.SetBufferData(thingsTrisVb, thingTris.ToArray());

    thingsLinesVb = new DBVertexBuffer(gl);
    device.SetBufferData(thingsLinesVb, thingLines.ToArray());

    // 1x1 white placeholder texture - bound when drawing untextured geometry to keep the sampler happy.
    placeholderTex = new DBuilder.Rendering.Texture(gl);
    placeholderTex.SetPixelsRgba8(1, 1, new byte[] { 255, 255, 255, 255 }, generateMipmaps: false);
    device.SetTexture(0, placeholderTex);
    // Nearest filtering + no mipmaps - keeps each texture "complete" without needing mipmap chains.
    device.SetSamplerFilter(TextureFilter.Nearest, TextureFilter.Nearest, MipmapFilter.None);
    device.SetSamplerState(TextureAddress.Wrap);

    // Upload per-flat textures + per-bucket vertex buffers.  Animated flats get N textures (one per frame).
    // Sky bucket ("__SKY") gets the composed sky wall texture at its native dimensions.
    foreach (var (name, verts) in flatBuckets)
    {
        var vb = new DBVertexBuffer(gl);
        device.SetBufferData(vb, verts.ToArray());

        DBuilder.Rendering.Texture[]? frames = null;
        if (name == "__SKY" && sky != null)
        {
            var tex = new DBuilder.Rendering.Texture(gl);
            tex.SetPixelsRgba8(sky.Width, sky.Height, sky.Rgba, generateMipmaps: false);
            device.SetTexture(0, tex);
            device.SetSamplerFilter(TextureFilter.Nearest, TextureFilter.Nearest, MipmapFilter.None);
            device.SetSamplerState(TextureAddress.Wrap);
            frames = new[] { tex };
        }
        else if (name != "" && flatRgba.TryGetValue(name, out byte[][]? rgbaFrames))
        {
            frames = new DBuilder.Rendering.Texture[rgbaFrames.Length];
            for (int i = 0; i < rgbaFrames.Length; i++)
            {
                var tex = new DBuilder.Rendering.Texture(gl);
                tex.SetPixelsRgba8(64, 64, rgbaFrames[i], generateMipmaps: false);
                device.SetTexture(0, tex);
                // Per-texture sampler state so any draw using this texture is complete.
                device.SetSamplerFilter(TextureFilter.Nearest, TextureFilter.Nearest, MipmapFilter.None);
                device.SetSamplerState(TextureAddress.Wrap);
                frames[i] = tex;
            }
        }
        sectorBucketResources.Add((name, vb, verts.Count / 3, frames));
    }

    // Upload wall ribbon buckets.
    foreach (var (name, verts) in wallBuckets)
    {
        if (!wallTextures.TryGetValue(name, out var texData)) continue;
        var vb = new DBVertexBuffer(gl);
        device.SetBufferData(vb, verts.ToArray());

        var tex = new DBuilder.Rendering.Texture(gl);
        tex.SetPixelsRgba8(texData.Width, texData.Height, texData.Rgba, generateMipmaps: false);
        device.SetTexture(0, tex);
        device.SetSamplerFilter(TextureFilter.Nearest, TextureFilter.Nearest, MipmapFilter.None);
        device.SetSamplerState(TextureAddress.Wrap);

        wallRibbonResources.Add((name, vb, verts.Count / 3, tex));
    }

    device.SetViewport(opts.Size.X, opts.Size.Y);
    device.SetCullMode(Cull.None);
    device.SetZEnable(false);
    device.SetAlphaBlendEnable(false);

    ResetCamera(opts.Size);

    // Wire up input
    var input = window.CreateInput();
    foreach (var mouse in input.Mice)
    {
        mouse.MouseDown += (m, btn) =>
        {
            if (btn == MouseButton.Left)
            {
                dragging = true;
                dragLastX = m.Position.X;
                dragLastY = m.Position.Y;
            }
        };
        mouse.MouseUp += (m, btn) =>
        {
            if (btn == MouseButton.Left) dragging = false;
        };
        mouse.MouseMove += (m, pos) =>
        {
            if (dragging)
            {
                double dx = pos.X - dragLastX;
                double dy = pos.Y - dragLastY;
                dragLastX = pos.X;
                dragLastY = pos.Y;
                // Screen pixels -> world units.  Doom Y is up, screen Y is down, so flip.
                camX -= dx * camZoom;
                camY += dy * camZoom;
            }
        };
        mouse.Scroll += (m, wheel) =>
        {
            double factor = wheel.Y > 0 ? 0.85 : 1.0 / 0.85;
            camZoom *= factor;
            if (camZoom < 0.05) camZoom = 0.05;
            if (camZoom > 100) camZoom = 100;
        };
    }
    foreach (var kb in input.Keyboards)
    {
        kb.KeyDown += (k, key, _) =>
        {
            if (key == Key.R) ResetCamera(window.Size);
            if (key == Key.Escape) window.Close();
            if (key == Key.F)
            {
                if (sectorFloorColors.Count == 0)
                {
                    Console.WriteLine("[ui]    F: no floor data loaded (sector tinting unavailable)");
                }
                else
                {
                    tintBySectorFloor = !tintBySectorFloor;
                    Console.WriteLine($"[ui]    F: color mode = {(tintBySectorFloor ? "sector floor" : "linedef type")}");
                    lineVerts = BuildLineVerts();
                    device!.SetBufferData(linesVb!, lineVerts);
                }
            }
            if (key == Key.S)
            {
                if (totalSectorTris == 0)
                {
                    Console.WriteLine("[ui]    S: no sector fills available");
                }
                else
                {
                    showSectorFills = !showSectorFills;
                    Console.WriteLine($"[ui]    S: sector fills = {(showSectorFills ? "on" : "off")}");
                }
            }
            if (key == Key.A)
            {
                animationsEnabled = !animationsEnabled;
                Console.WriteLine($"[ui]    A: flat animations = {(animationsEnabled ? "on" : "off")}");
            }
            if (key == Key.W)
            {
                if (wallRibbonResources.Count == 0)
                {
                    Console.WriteLine("[ui]    W: no wall texture data");
                }
                else
                {
                    showWallRibbons = !showWallRibbons;
                    Console.WriteLine($"[ui]    W: wall ribbons = {(showWallRibbons ? "on" : "off")}");
                }
            }
        };
    }

    Console.WriteLine($"[gl]    {gl.GetStringS(StringName.Version)}");
    Console.WriteLine($"[ui]    LMB drag = pan, wheel = zoom, R = reset, F = line colors, S = sector fills, W = wall ribbons, A = flat animations, Esc = quit");
};

window.Resize += sz => device?.SetViewport(sz.X, sz.Y);

window.Render += _ =>
{
    if (device is null || shader is null) return;

    device.StartRendering(clear: true, clearColorArgb: 0xff181c20);
    device.SetShader(shader);

    var size = window.Size;
    double halfW = size.X * 0.5 * camZoom;
    double halfH = size.Y * 0.5 * camZoom;
    var proj = Matrix4x4.CreateOrthographicOffCenter(
        (float)(camX - halfW), (float)(camX + halfW),
        (float)(camY - halfH), (float)(camY + halfH),
        -1, 1);
    device.SetUniform("projection", proj);
    device.SetUniform("tex0", 0);

    // Draw order: sector fills -> wall ribbons -> line overlay -> thing markers (top to bottom in z).
    // Sampler state was configured at load time per-texture so no per-frame setup needed.
    if (showSectorFills)
    {
        // Doom-authentic frame index: 1 frame every 8 game tics at 35 tics/sec.
        double elapsed = window.Time;
        int globalFrame = animationsEnabled ? (int)(elapsed / DBuilder.IO.FlatAnimations.FramePeriodSeconds) : 0;

        foreach (var bucket in sectorBucketResources)
        {
            if (bucket.TriCount == 0) continue;
            // Pick the current frame for animated buckets (length > 1); fall back to frame 0 for static.
            DBuilder.Rendering.Texture? activeTex = null;
            if (bucket.Frames != null && bucket.Frames.Length > 0)
            {
                int idx = bucket.Frames.Length > 1 ? (globalFrame % bucket.Frames.Length) : 0;
                activeTex = bucket.Frames[idx];
            }
            device.SetUniform("useTexture", activeTex != null ? 1f : 0f);
            device.SetTexture(0, activeTex ?? placeholderTex);
            device.SetVertexBuffer(bucket.Vb);
            device.Draw(DBPrimitiveType.TriangleList, 0, bucket.TriCount);
        }
    }

    // Wall texture ribbons under the line overlay.
    if (showWallRibbons)
    {
        device.SetUniform("useTexture", 1f);
        foreach (var bucket in wallRibbonResources)
        {
            if (bucket.TriCount == 0) continue;
            device.SetTexture(0, bucket.Tex);
            device.SetVertexBuffer(bucket.Vb);
            device.Draw(DBPrimitiveType.TriangleList, 0, bucket.TriCount);
        }
    }

    // Lines + things: vertex color only. Keep placeholder bound so the sampler always has something.
    device.SetTexture(0, placeholderTex);
    device.SetUniform("useTexture", 0f);
    device.SetVertexBuffer(linesVb);
    device.Draw(DBPrimitiveType.LineList, 0, lineVerts.Length / 2);

    if (thingsTrisVb != null && thingTris.Count > 0)
    {
        device.SetVertexBuffer(thingsTrisVb);
        device.Draw(DBPrimitiveType.TriangleList, 0, thingTris.Count / 3);
    }
    if (thingsLinesVb != null && thingLines.Count > 0)
    {
        device.SetVertexBuffer(thingsLinesVb);
        device.Draw(DBPrimitiveType.LineList, 0, thingLines.Count / 2);
    }

    device.FinishRendering();
};

window.Closing += () =>
{
    linesVb?.Dispose();
    thingsTrisVb?.Dispose();
    thingsLinesVb?.Dispose();
    foreach (var bucket in sectorBucketResources)
    {
        bucket.Vb.Dispose();
        if (bucket.Frames != null)
        {
            foreach (var f in bucket.Frames) f.Dispose();
        }
    }
    foreach (var bucket in wallRibbonResources)
    {
        bucket.Vb.Dispose();
        bucket.Tex.Dispose();
    }
    placeholderTex?.Dispose();
    shader?.Dispose();
    device?.Dispose();
};

window.Run();
Console.WriteLine("[exit]");
return 0;

static FlatVertex MkFV(Vec2D p, int color)
    => new FlatVertex { x = (float)p.x, y = (float)p.y, z = 0, w = 1, c = color, u = 0, v = 0 };

internal sealed record SkyTextureData(byte[] Rgba, int Width, int Height);
