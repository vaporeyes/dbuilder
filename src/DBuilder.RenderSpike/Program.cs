// ABOUTME: Cross-platform render spike host: opens a GL window via Silk.NET and exercises the RenderDevice.
// ABOUTME: Draws a vertex-colored triangle plus a textured, alpha-blended quad to validate the ported surface.

using System.Numerics;
using DBuilder.Rendering;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Shader = DBuilder.Rendering.Shader;
using PrimitiveType = DBuilder.Rendering.PrimitiveType;
using Texture = DBuilder.Rendering.Texture;

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
    frag = mix(v_color, sampled * v_color, useTexture);
}";

var opts = WindowOptions.Default with
{
    Size = new Vector2D<int>(960, 600),
    Title = "DBuilder render spike",
    API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
    VSync = true
};

using var window = Window.Create(opts);

RenderDevice? device = null;
Shader? shader = null;
VertexBuffer? triVb = null;
IndexBuffer? triIb = null;
VertexBuffer? quadVb = null;
IndexBuffer? quadIb = null;
Texture? checkerTex = null;
GL? gl = null;
int framesRendered = 0;

window.Load += () =>
{
    gl = GL.GetApi(window);
    device = new RenderDevice(gl);
    shader = new Shader(gl, VertexSrc, FragmentSrc);

    // Triangle (untextured, vertex-colored)
    triVb = new VertexBuffer(gl);
    triIb = new IndexBuffer(gl);
    var triVerts = new FlatVertex[]
    {
        new() { x = 100, y = 100, z = 0, w = 1, c = unchecked((int)0xffff0000), u = 0, v = 0 },
        new() { x = 800, y = 100, z = 0, w = 1, c = unchecked((int)0xff00ff00), u = 1, v = 0 },
        new() { x = 450, y = 500, z = 0, w = 1, c = unchecked((int)0xff0000ff), u = 0, v = 1 },
    };
    device.SetBufferData(triVb, triVerts);
    device.SetBufferData(triIb, new[] { 0, 1, 2 });

    // Quad (textured + alpha-blended)
    quadVb = new VertexBuffer(gl);
    quadIb = new IndexBuffer(gl);
    var quadVerts = new FlatVertex[]
    {
        new() { x = 250, y = 200, z = 0, w = 1, c = unchecked((int)0xc0ffffff), u = 0, v = 0 },
        new() { x = 650, y = 200, z = 0, w = 1, c = unchecked((int)0xc0ffffff), u = 1, v = 0 },
        new() { x = 650, y = 480, z = 0, w = 1, c = unchecked((int)0xc0ffffff), u = 1, v = 1 },
        new() { x = 250, y = 480, z = 0, w = 1, c = unchecked((int)0xc0ffffff), u = 0, v = 1 },
    };
    device.SetBufferData(quadVb, quadVerts);
    device.SetBufferData(quadIb, new[] { 0, 1, 2, 0, 2, 3 });

    // Procedural 64x64 checker, RGBA8
    const int N = 64;
    var pixels = new byte[N * N * 4];
    for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            bool light = ((x >> 3) + (y >> 3)) % 2 == 0;
            int i = (y * N + x) * 4;
            pixels[i + 0] = light ? (byte)230 : (byte)50;
            pixels[i + 1] = light ? (byte)230 : (byte)50;
            pixels[i + 2] = light ? (byte)200 : (byte)80;
            pixels[i + 3] = 255;
        }
    checkerTex = new Texture(gl);
    checkerTex.SetPixelsRgba8(N, N, pixels);
    device.SetTexture(0, checkerTex);
    device.SetSamplerFilter(TextureFilter.Linear, TextureFilter.Linear, MipmapFilter.Linear);
    device.SetSamplerState(TextureAddress.Wrap);

    device.SetViewport(opts.Size.X, opts.Size.Y);
    device.SetCullMode(Cull.None);
    device.SetZEnable(false);

    Console.WriteLine($"GL_VENDOR  : {gl.GetStringS(StringName.Vendor)}");
    Console.WriteLine($"GL_RENDERER: {gl.GetStringS(StringName.Renderer)}");
    Console.WriteLine($"GL_VERSION : {gl.GetStringS(StringName.Version)}");
};

window.Resize += sz =>
{
    device?.SetViewport(sz.X, sz.Y);
};

window.Render += _ =>
{
    if (device is null || shader is null) return;

    device.StartRendering(clear: true, clearColorArgb: 0xff202830);
    device.SetShader(shader);

    var size = window.Size;
    var proj = Matrix4x4.CreateOrthographicOffCenter(0, size.X, size.Y, 0, -1, 1);
    device.SetUniform("projection", proj);
    device.SetUniform("tex0", 0);

    // Pass 1: vertex-colored triangle
    device.SetAlphaBlendEnable(false);
    device.SetUniform("useTexture", 0f);
    device.SetVertexBuffer(triVb);
    device.SetIndexBuffer(triIb);
    device.DrawIndexed(PrimitiveType.TriangleList, 0, 1);

    // Pass 2: textured, alpha-blended quad on top
    device.SetAlphaBlendEnable(true);
    device.SetBlendOperation(BlendOperation.Add);
    device.SetSourceBlend(Blend.SourceAlpha);
    device.SetDestinationBlend(Blend.InverseSourceAlpha);
    device.SetTexture(0, checkerTex);
    device.SetUniform("useTexture", 1f);
    device.SetVertexBuffer(quadVb);
    device.SetIndexBuffer(quadIb);
    device.DrawIndexed(PrimitiveType.TriangleList, 0, 2);

    device.FinishRendering();

    framesRendered++;
    if (framesRendered >= 120) window.Close();
};

window.Closing += () =>
{
    checkerTex?.Dispose();
    triVb?.Dispose();
    triIb?.Dispose();
    quadVb?.Dispose();
    quadIb?.Dispose();
    shader?.Dispose();
    device?.Dispose();
};

window.Run();
Console.WriteLine($"Spike OK, rendered {framesRendered} frames.");
