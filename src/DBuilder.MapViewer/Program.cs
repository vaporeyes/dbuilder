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

if (args.Length >= 1 && File.Exists(args[0]))
{
    string wadPath = args[0];
    string? requestedMap = args.Length >= 2 ? args[1].ToUpperInvariant() : null;
    map = LoadFromWad(wadPath, requestedMap, out source, out sectorFloorColors);
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

static MapSet? LoadFromWad(string path, string? mapName, out string source, out Dictionary<int, uint> sectorFloorColors)
{
    source = "";
    sectorFloorColors = new();
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

    if (loaded != null) sectorFloorColors = BuildSectorFloorColors(loaded, wad);
    return loaded;
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
void main() {
    gl_Position = projection * vec4(a_pos.xyz, 1.0);
    v_color = a_color;
}";
const string FragmentSrc = @"#version 330 core
in vec4 v_color;
out vec4 frag;
void main() { frag = v_color; }";

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
        };
    }

    Console.WriteLine($"[gl]    {gl.GetStringS(StringName.Version)}");
    Console.WriteLine($"[ui]    LMB drag = pan,  wheel = zoom,  R = reset,  F = toggle color mode (currently: {(tintBySectorFloor ? "sector floor" : "linedef type")}),  Esc = quit");
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

    // Lines first, then thing markers on top.
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
    shader?.Dispose();
    device?.Dispose();
};

window.Run();
Console.WriteLine("[exit]");
return 0;

static FlatVertex MkFV(Vec2D p, int color)
    => new FlatVertex { x = (float)p.x, y = (float)p.y, z = 0, w = 1, c = color, u = 0, v = 0 };
