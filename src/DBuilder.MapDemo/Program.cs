// ABOUTME: End-to-end smoke test: build a synthetic Doom map in code, round-trip through DBuilder.IO's WAD writer/reader,
// ABOUTME: parse the binary VERTEXES + LINEDEFS lumps back into Vector2Ds, render them as 2D lines via DBuilder.Rendering.

using System.IO;
using System.Numerics;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Rendering;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using DBRenderDevice = DBuilder.Rendering.RenderDevice;
using DBShader = DBuilder.Rendering.Shader;
using DBPrimitiveType = DBuilder.Rendering.PrimitiveType;
using Vector2D = DBuilder.Geometry.Vector2D;
using SilkVec2I = Silk.NET.Maths.Vector2D<int>;

// ============================================================
// STEP 1: Build a synthetic map in code.
// Outer rectangle (4 verts), inner rotated triangle (3 verts), one diagonal connector (no new verts).
// Coordinates in Doom map units; we'll scale into screen space at render time.
// ============================================================
var sourceVerts = new List<Vector2D>
{
    // Outer rectangle (CCW): 0..3
    new(-256, -192),
    new( 256, -192),
    new( 256,  192),
    new(-256,  192),
    // Inner triangle: 4..6
    new(  60,  -90),
    new( 100,   60),
    new( -90,   30),
};

// LinedefRaw matches Doom map binary layout exactly: 7 int16s = 14 bytes
var sourceLines = new List<(int v1, int v2)>
{
    // Outer rectangle
    (0, 1), (1, 2), (2, 3), (3, 0),
    // Inner triangle
    (4, 5), (5, 6), (6, 4),
    // Diagonal connector from outer corner to inner triangle vertex
    (0, 4),
};

Console.WriteLine($"[build] {sourceVerts.Count} vertices, {sourceLines.Count} linedefs");

// ============================================================
// STEP 2: Encode to Doom-format VERTEXES + LINEDEFS lumps.
// ============================================================
byte[] EncodeVertexes(IReadOnlyList<Vector2D> verts)
{
    using var ms = new MemoryStream(verts.Count * 4);
    using var w = new BinaryWriter(ms);
    foreach (var v in verts)
    {
        w.Write((short)v.x);
        w.Write((short)v.y);
    }
    return ms.ToArray();
}

byte[] EncodeLinedefs(IReadOnlyList<(int v1, int v2)> lines)
{
    using var ms = new MemoryStream(lines.Count * 14);
    using var w = new BinaryWriter(ms);
    foreach (var (v1, v2) in lines)
    {
        w.Write((short)v1);   // start vertex
        w.Write((short)v2);   // end vertex
        w.Write((short)0);    // flags
        w.Write((short)0);    // special
        w.Write((short)0);    // sector tag
        w.Write((short)-1);   // right sidedef (none)
        w.Write((short)-1);   // left sidedef (none)
    }
    return ms.ToArray();
}

byte[] vertexesBytes = EncodeVertexes(sourceVerts);
byte[] linedefsBytes = EncodeLinedefs(sourceLines);

// ============================================================
// STEP 3: Write to an in-memory WAD using DBuilder.IO.
// Map structure: marker lump "MAP01" (empty), VERTEXES, LINEDEFS.
// ============================================================
var wadBytes = new MemoryStream();
using (var wad = new WAD(wadBytes))
{
    wad.Insert("MAP01", 0, 0); // marker lump

    var vertexesLump = wad.Insert("VERTEXES", 1, vertexesBytes.Length)!;
    vertexesLump.Stream.Write(vertexesBytes, 0, vertexesBytes.Length);

    var linedefsLump = wad.Insert("LINEDEFS", 2, linedefsBytes.Length)!;
    linedefsLump.Stream.Write(linedefsBytes, 0, linedefsBytes.Length);

    wad.WriteHeaders();
}

Console.WriteLine($"[write] WAD size: {wadBytes.Length} bytes");

// ============================================================
// STEP 4: Re-open the WAD, parse lumps back into Vector2Ds + line index pairs.
// ============================================================
List<Vector2D> roundTrippedVerts;
List<(int, int)> roundTrippedLines;

wadBytes.Position = 0;
using (var wad = new WAD(wadBytes, openreadonly: true))
{
    Console.WriteLine($"[read]  {wad.Lumps.Count} lumps:");
    foreach (var lump in wad.Lumps)
        Console.WriteLine($"        - {lump.Name,-10} ({lump.Length} bytes)");

    var vertexesLump = wad.FindLump("VERTEXES")!;
    var linedefsLump = wad.FindLump("LINEDEFS")!;

    byte[] vbytes = vertexesLump.Stream.ReadAllBytes();
    roundTrippedVerts = new List<Vector2D>(vbytes.Length / 4);
    using (var r = new BinaryReader(new MemoryStream(vbytes)))
    {
        for (int i = 0; i < vbytes.Length / 4; i++)
            roundTrippedVerts.Add(new Vector2D(r.ReadInt16(), r.ReadInt16()));
    }

    byte[] lbytes = linedefsLump.Stream.ReadAllBytes();
    roundTrippedLines = new List<(int, int)>(lbytes.Length / 14);
    using (var r = new BinaryReader(new MemoryStream(lbytes)))
    {
        for (int i = 0; i < lbytes.Length / 14; i++)
        {
            short v1 = r.ReadInt16();
            short v2 = r.ReadInt16();
            r.ReadInt16(); // flags
            r.ReadInt16(); // special
            r.ReadInt16(); // sector tag
            r.ReadInt16(); // right sidedef
            r.ReadInt16(); // left sidedef
            roundTrippedLines.Add((v1, v2));
        }
    }
}

Console.WriteLine($"[parse] {roundTrippedVerts.Count} vertices, {roundTrippedLines.Count} linedefs read back");

// Sanity check that the data survived the round trip exactly.
if (roundTrippedVerts.Count != sourceVerts.Count)
    throw new Exception("Vertex count mismatch after round trip");
for (int i = 0; i < sourceVerts.Count; i++)
{
    if ((int)sourceVerts[i].x != (int)roundTrippedVerts[i].x || (int)sourceVerts[i].y != (int)roundTrippedVerts[i].y)
        throw new Exception($"Vertex {i} mismatch: {sourceVerts[i]} != {roundTrippedVerts[i]}");
}
for (int i = 0; i < sourceLines.Count; i++)
{
    if (sourceLines[i] != roundTrippedLines[i])
        throw new Exception($"Linedef {i} mismatch");
}
Console.WriteLine("[check] byte-for-byte round trip OK");

// ============================================================
// STEP 5: Render the parsed map as a line list via DBuilder.Rendering.
// Each linedef becomes two FlatVertex entries; one vertex buffer drives the whole map.
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

// Compute bounds so we can fit the map to the window
double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
foreach (var v in roundTrippedVerts)
{
    if (v.x < minX) minX = v.x;
    if (v.y < minY) minY = v.y;
    if (v.x > maxX) maxX = v.x;
    if (v.y > maxY) maxY = v.y;
}
double mapW = maxX - minX;
double mapH = maxY - minY;
double padding = Math.Max(mapW, mapH) * 0.1;
minX -= padding; minY -= padding; maxX += padding; maxY += padding;

// Build the FlatVertex array — outer rect green, inner triangle yellow, connector magenta
const uint colorOuter     = 0xff00ff66;
const uint colorInner     = 0xffffe000;
const uint colorConnector = 0xffff00ff;
uint ColorFor(int lineIdx)
{
    if (lineIdx < 4) return colorOuter;
    if (lineIdx < 7) return colorInner;
    return colorConnector;
}

var lineVerts = new FlatVertex[roundTrippedLines.Count * 2];
for (int i = 0; i < roundTrippedLines.Count; i++)
{
    var (i1, i2) = roundTrippedLines[i];
    var a = roundTrippedVerts[i1];
    var b = roundTrippedVerts[i2];
    int c = unchecked((int)ColorFor(i));
    lineVerts[i * 2 + 0] = new FlatVertex { x = (float)a.x, y = (float)a.y, z = 0, w = 1, c = c, u = 0, v = 0 };
    lineVerts[i * 2 + 1] = new FlatVertex { x = (float)b.x, y = (float)b.y, z = 0, w = 1, c = c, u = 0, v = 0 };
}

var opts = WindowOptions.Default with
{
    Size = new SilkVec2I(900, 700),
    Title = "DBuilder map demo - synthetic WAD round trip",
    API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
    VSync = true
};

using var window = Window.Create(opts);

DBRenderDevice? device = null;
DBShader? shader = null;
DBuilder.Rendering.VertexBuffer? vb = null;
GL? gl = null;
int framesRendered = 0;

window.Load += () =>
{
    gl = GL.GetApi(window);
    device = new DBRenderDevice(gl);
    shader = new DBShader(gl, VertexSrc, FragmentSrc);

    vb = new DBuilder.Rendering.VertexBuffer(gl);
    device.SetBufferData(vb, lineVerts);

    device.SetViewport(opts.Size.X, opts.Size.Y);
    device.SetCullMode(Cull.None);
    device.SetZEnable(false);
    device.SetAlphaBlendEnable(false);
};

window.Resize += sz => device?.SetViewport(sz.X, sz.Y);

window.Render += _ =>
{
    if (device is null || shader is null || vb is null) return;

    device.StartRendering(clear: true, clearColorArgb: 0xff14181c);
    device.SetShader(shader);

    var size = window.Size;
    // Letterbox the map into the window while preserving aspect ratio.
    double winAspect = size.X / (double)size.Y;
    double mapAspect = (maxX - minX) / (maxY - minY);
    double left, right, top, bottom;
    if (winAspect > mapAspect)
    {
        double extra = ((maxY - minY) * winAspect - (maxX - minX)) * 0.5;
        left = minX - extra; right = maxX + extra; top = maxY; bottom = minY;
    }
    else
    {
        double extra = ((maxX - minX) / winAspect - (maxY - minY)) * 0.5;
        left = minX; right = maxX; top = maxY + extra; bottom = minY - extra;
    }
    // Doom Y points up; flip so positive Y goes up on-screen.
    var proj = Matrix4x4.CreateOrthographicOffCenter((float)left, (float)right, (float)bottom, (float)top, -1, 1);
    device.SetUniform("projection", proj);

    device.SetVertexBuffer(vb);
    device.Draw(DBPrimitiveType.LineList, 0, lineVerts.Length / 2);

    device.FinishRendering();

    framesRendered++;
    if (framesRendered >= 180) window.Close();
};

window.Closing += () =>
{
    vb?.Dispose();
    shader?.Dispose();
    device?.Dispose();
};

window.Run();
Console.WriteLine($"[render] Drew {framesRendered} frames of the round-tripped map.");
Console.WriteLine("OK");
