// ABOUTME: Visual smoke test for the palette + flat + picture pipeline. Loads textures from a WAD (or a synthetic showcase)
// ABOUTME: and displays them in a fixed grid. Each cell renders one decoded texture as a textured quad via DBuilder.Rendering.

using System.Numerics;
using DBuilder.IO;
using DBuilder.Rendering;
using DBuilder.TextureDemo;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SilkVec2I = Silk.NET.Maths.Vector2D<int>;
using DBRenderDevice = DBuilder.Rendering.RenderDevice;
using DBShader = DBuilder.Rendering.Shader;
using DBPrimitiveType = DBuilder.Rendering.PrimitiveType;
using DBVertexBuffer = DBuilder.Rendering.VertexBuffer;
using DBTexture = DBuilder.Rendering.Texture;

// ============================================================
// 1. Resolve texture source: real WAD or synthetic showcase.
// ============================================================
const int MaxTextures = 16; // 4x4 grid

List<LoadedTexture> textures;
string source;

if (args.Length >= 1 && File.Exists(args[0]))
{
    Console.WriteLine($"[load]  Scanning {args[0]} for textures (max {MaxTextures})");
    textures = TextureSource.FromWad(args[0], MaxTextures);
    source = Path.GetFileName(args[0]);
}
else
{
    if (args.Length >= 1) Console.WriteLine($"[load]  File '{args[0]}' not found - using synthetic showcase");
    else                  Console.WriteLine("[load]  No .wad supplied - using synthetic showcase");
    textures = TextureSource.Synthetic();
    source = "synthetic";
}

if (textures.Count == 0)
{
    Console.WriteLine("[load]  No textures found in source");
    return 1;
}

Console.WriteLine($"[load]  {textures.Count} textures from '{source}':");
foreach (var t in textures) Console.WriteLine($"        - {t.Name,-12}  {t.Kind,-8}  {t.Width}x{t.Height}");

// ============================================================
// 2. GL setup + texture uploads.
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
out vec4 frag;
uniform sampler2D tex0;
uniform float useTexture;
void main() {
    vec4 sampled = texture(tex0, v_uv);
    frag = mix(v_color, sampled, useTexture);
}";

var opts = WindowOptions.Default with
{
    Size = new SilkVec2I(1200, 850),
    Title = $"DBuilder texture demo  -  {source}  -  Esc to quit",
    API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
    VSync = true
};

using var window = Window.Create(opts);

DBRenderDevice? device = null;
DBShader? shader = null;
DBVertexBuffer? quadVb = null;
GL? gl = null;
var uploaded = new List<(DBTexture tex, LoadedTexture loaded)>();

window.Load += () =>
{
    gl = GL.GetApi(window);
    device = new DBRenderDevice(gl);
    shader = new DBShader(gl, VertexSrc, FragmentSrc);

    // Pre-upload each decoded texture as a GL texture.
    foreach (var lt in textures)
    {
        var tex = new DBTexture(gl);
        tex.SetPixelsRgba8(lt.Width, lt.Height, lt.Rgba8, generateMipmaps: false);
        uploaded.Add((tex, lt));
    }
    // Set sampling to nearest so Doom textures stay crisp (no blurring).
    device.SetSamplerFilter(TextureFilter.Nearest, TextureFilter.Nearest, MipmapFilter.None);
    device.SetSamplerState(TextureAddress.Clamp);

    quadVb = new DBVertexBuffer(gl);
    // Initialize with a placeholder full-screen quad; per-frame we'll re-upload positions for each cell.
    device.SetBufferData(quadVb, new FlatVertex[6]);

    device.SetViewport(opts.Size.X, opts.Size.Y);
    device.SetCullMode(Cull.None);
    device.SetZEnable(false);
    device.SetAlphaBlendEnable(true);
    device.SetBlendOperation(BlendOperation.Add);
    device.SetSourceBlend(Blend.SourceAlpha);
    device.SetDestinationBlend(Blend.InverseSourceAlpha);

    var input = window.CreateInput();
    foreach (var kb in input.Keyboards)
    {
        kb.KeyDown += (k, key, _) =>
        {
            if (key == Key.Escape) window.Close();
        };
    }

    Console.WriteLine($"[gl]    {gl.GetStringS(StringName.Version)}");
    Console.WriteLine($"[show]  {uploaded.Count} textures in {Math.Min(4, uploaded.Count)}-column grid");
};

window.Resize += sz => device?.SetViewport(sz.X, sz.Y);

window.Render += _ =>
{
    if (device is null || shader is null || quadVb is null) return;

    device.StartRendering(clear: true, clearColorArgb: 0xff181c20);
    device.SetShader(shader);
    device.SetUniform("tex0", 0);
    device.SetUniform("useTexture", 1f);

    var size = window.Size;
    var proj = Matrix4x4.CreateOrthographicOffCenter(0, size.X, size.Y, 0, -1, 1);
    device.SetUniform("projection", proj);

    // Layout: 4-column grid with 220px cells and 20px padding.
    const int cols = 4;
    const float cellSize = 220f;
    const float pad = 20f;
    float gridStartX = (size.X - (cols * cellSize + (cols - 1) * pad)) * 0.5f;
    float gridStartY = 30f;

    for (int i = 0; i < uploaded.Count; i++)
    {
        int row = i / cols;
        int col = i % cols;
        float x0 = gridStartX + col * (cellSize + pad);
        float y0 = gridStartY + row * (cellSize + pad);

        var (tex, loaded) = uploaded[i];

        // Aspect-fit the texture into the cell.
        float scale = Math.Min(cellSize / loaded.Width, cellSize / loaded.Height);
        float w = loaded.Width * scale;
        float h = loaded.Height * scale;
        float cx0 = x0 + (cellSize - w) * 0.5f;
        float cy0 = y0 + (cellSize - h) * 0.5f;
        float cx1 = cx0 + w;
        float cy1 = cy0 + h;

        // Build a textured quad (two triangles).
        var verts = new FlatVertex[]
        {
            new() { x = cx0, y = cy0, z = 0, w = 1, c = unchecked((int)0xffffffff), u = 0, v = 0 },
            new() { x = cx1, y = cy0, z = 0, w = 1, c = unchecked((int)0xffffffff), u = 1, v = 0 },
            new() { x = cx1, y = cy1, z = 0, w = 1, c = unchecked((int)0xffffffff), u = 1, v = 1 },

            new() { x = cx0, y = cy0, z = 0, w = 1, c = unchecked((int)0xffffffff), u = 0, v = 0 },
            new() { x = cx1, y = cy1, z = 0, w = 1, c = unchecked((int)0xffffffff), u = 1, v = 1 },
            new() { x = cx0, y = cy1, z = 0, w = 1, c = unchecked((int)0xffffffff), u = 0, v = 1 },
        };
        device.SetBufferData(quadVb, verts);

        device.SetTexture(0, tex);
        device.SetSamplerFilter(TextureFilter.Nearest, TextureFilter.Nearest, MipmapFilter.None);
        device.SetSamplerState(TextureAddress.Clamp);

        device.SetVertexBuffer(quadVb);
        device.Draw(DBPrimitiveType.TriangleList, 0, 2);
    }

    device.FinishRendering();
};

window.Closing += () =>
{
    foreach (var (tex, _) in uploaded) tex.Dispose();
    quadVb?.Dispose();
    shader?.Dispose();
    device?.Dispose();
};

window.Run();
Console.WriteLine("[exit]");
return 0;
