// ABOUTME: Cross-platform RenderDevice spike using Silk.NET.OpenGL.
// ABOUTME: Subset of UDB's BuilderNative GLRenderDevice API: state, buffers, shader, draw, clear.

using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace DBuilder.Rendering;

public enum CubeMapFace
{
    PositiveX,
    PositiveY,
    PositiveZ,
    NegativeX,
    NegativeY,
    NegativeZ,
}

public enum TextureOperationKind
{
    Bind,
    Clear,
    CopyCubeFace,
    SetPixels2D,
    SetPixelsCubeFace,
    MapPbo,
    UnmapPbo,
}

public enum RenderStateToggleKind
{
    AlphaTest,
    MultisampleAntialias,
}

public sealed record TextureOperationPlan(
    TextureOperationKind Kind,
    int Unit,
    bool HasTexture,
    CubeMapFace? CubeFace = null,
    uint? ColorArgb = null);

public sealed record RenderStateTogglePlan(
    RenderStateToggleKind Kind,
    bool Enabled);

public sealed record SamplerFilterPlan(
    TextureFilter MinFilter,
    TextureFilter MagFilter,
    MipmapFilter MipFilter,
    float MaxAnisotropy,
    int Unit);

public sealed record RenderStartPlan(
    bool Clear,
    uint ClearColorArgb,
    bool HasTarget,
    bool UseDepthBuffer);

public sealed class RenderDevice : IDisposable
{
    private readonly GL _gl;
    private uint _streamVao;
    private VertexBuffer? _boundVb;
    private IndexBuffer? _boundIb;
    private Shader? _boundShader;
    private int _viewportW;
    private int _viewportH;
    private bool _alphaTestEnabled;
    private bool _multisampleAntialiasEnabled;

    public RenderDevice(GL gl)
    {
        _gl = gl;
        _streamVao = _gl.GenVertexArray();
    }

    public GL GL => _gl;
    public bool Disposed => _streamVao == 0;
    public bool AlphaTestEnabled => _alphaTestEnabled;
    public bool MultisampleAntialiasEnabled => _multisampleAntialiasEnabled;

    public void SetViewport(int width, int height)
    {
        _viewportW = width;
        _viewportH = height;
        _gl.Viewport(0, 0, (uint)width, (uint)height);
    }

    public void StartRendering(bool clear, uint clearColorArgb)
    {
        _gl.BindVertexArray(_streamVao);
        if (clear)
        {
            float a = ((clearColorArgb >> 24) & 0xff) / 255f;
            float r = ((clearColorArgb >> 16) & 0xff) / 255f;
            float g = ((clearColorArgb >> 8) & 0xff) / 255f;
            float b = (clearColorArgb & 0xff) / 255f;
            _gl.ClearColor(r, g, b, a);
            _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        }
    }

    public void FinishRendering() { /* nothing to flush yet */ }

    public void SetCullMode(Cull mode)
    {
        if (mode == Cull.None) _gl.Disable(EnableCap.CullFace);
        else { _gl.Enable(EnableCap.CullFace); _gl.CullFace(TriangleFace.Back); _gl.FrontFace(FrontFaceDirection.CW); }
    }

    public void SetAlphaBlendEnable(bool value)
    {
        if (value) _gl.Enable(EnableCap.Blend); else _gl.Disable(EnableCap.Blend);
    }

    public void SetAlphaTestEnable(bool value)
    {
        _alphaTestEnabled = value;
    }

    public void SetMultisampleAntialias(bool value)
    {
        _multisampleAntialiasEnabled = value;
        if (value) _gl.Enable(EnableCap.Multisample); else _gl.Disable(EnableCap.Multisample);
    }

    public void SetZEnable(bool value)
    {
        if (value) _gl.Enable(EnableCap.DepthTest); else _gl.Disable(EnableCap.DepthTest);
    }

    public void SetZWriteEnable(bool value) => _gl.DepthMask(value);

    public void SetFillMode(FillMode mode)
    {
        // PolygonMode is desktop-GL only; the spike runs on desktop so this is fine.
        _gl.PolygonMode(TriangleFace.FrontAndBack, mode == FillMode.Solid ? PolygonMode.Fill : PolygonMode.Line);
    }

    public unsafe void SetBufferData(VertexBuffer buffer, FlatVertex[] data)
    {
        buffer.Format = VertexFormat.Flat;
        buffer.VertexCount = data.Length;
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer.Handle);
        fixed (FlatVertex* p = data)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * FlatVertex.Stride), p, BufferUsageARB.StaticDraw);
        }
    }

    public unsafe void SetBufferData(VertexBuffer buffer, WorldVertex[] data)
    {
        buffer.Format = VertexFormat.World;
        buffer.VertexCount = data.Length;
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer.Handle);
        fixed (WorldVertex* p = data)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * WorldVertex.Stride), p, BufferUsageARB.StaticDraw);
        }
    }

    public unsafe void SetBufferData(IndexBuffer buffer, int[] data)
    {
        buffer.IndexCount = data.Length;
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, buffer.Handle);
        fixed (int* p = data)
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(data.Length * sizeof(int)), p, BufferUsageARB.StaticDraw);
        }
    }

    public unsafe void SetBufferData(VertexBuffer buffer, int length, VertexFormat format)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

        int stride = format switch
        {
            VertexFormat.Flat => FlatVertex.Stride,
            VertexFormat.World => WorldVertex.Stride,
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        buffer.Format = format;
        buffer.VertexCount = length;
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer.Handle);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)checked((long)length * stride), null, BufferUsageARB.StaticDraw);
    }

    public unsafe void SetBufferSubdata(VertexBuffer buffer, long destOffset, FlatVertex[] data)
    {
        if (destOffset < 0) throw new ArgumentOutOfRangeException(nameof(destOffset));

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer.Handle);
        fixed (FlatVertex* p = data)
        {
            _gl.BufferSubData(
                BufferTargetARB.ArrayBuffer,
                checked((nint)(destOffset * FlatVertex.Stride)),
                (nuint)checked((long)data.Length * FlatVertex.Stride),
                p);
        }
    }

    public unsafe void SetBufferSubdata(VertexBuffer buffer, long destOffset, WorldVertex[] data)
    {
        if (destOffset < 0) throw new ArgumentOutOfRangeException(nameof(destOffset));

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer.Handle);
        fixed (WorldVertex* p = data)
        {
            _gl.BufferSubData(
                BufferTargetARB.ArrayBuffer,
                checked((nint)(destOffset * WorldVertex.Stride)),
                (nuint)checked((long)data.Length * WorldVertex.Stride),
                p);
        }
    }

    public unsafe void SetBufferSubdata(VertexBuffer buffer, FlatVertex[] data, long size)
    {
        if (size < 0 || size > data.Length) throw new ArgumentOutOfRangeException(nameof(size));

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer.Handle);
        fixed (FlatVertex* p = data)
        {
            _gl.BufferSubData(
                BufferTargetARB.ArrayBuffer,
                0,
                (nuint)checked(size * FlatVertex.Stride),
                p);
        }
    }

    public unsafe void SetVertexBuffer(VertexBuffer? buffer)
    {
        _boundVb = buffer;
        if (buffer is null) { _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0); return; }

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer.Handle);
        if (buffer.Format == VertexFormat.Flat)
            ConfigureFlatAttribs();
        else
            ConfigureWorldAttribs();
    }

    private unsafe void ConfigureFlatAttribs()
    {
        // FlatVertex: pos(vec4)@0, color(uint8x4)@16, uv(vec2)@20  (stride 28)
        const uint posLoc = 0, colorLoc = 1, uvLoc = 2;
        const uint normalLoc = 3;
        _gl.EnableVertexAttribArray(posLoc);
        _gl.VertexAttribPointer(posLoc, 4, VertexAttribPointerType.Float, false, (uint)FlatVertex.Stride, (void*)0);
        _gl.EnableVertexAttribArray(colorLoc);
        _gl.VertexAttribPointer(colorLoc, 4, VertexAttribPointerType.UnsignedByte, true, (uint)FlatVertex.Stride, (void*)16);
        _gl.EnableVertexAttribArray(uvLoc);
        _gl.VertexAttribPointer(uvLoc, 2, VertexAttribPointerType.Float, false, (uint)FlatVertex.Stride, (void*)20);
        _gl.DisableVertexAttribArray(normalLoc);
    }

    private unsafe void ConfigureWorldAttribs()
    {
        // WorldVertex: pos(vec3)@0, color(uint8x4)@12, uv(vec2)@16, normal(vec3)@24  (stride 36)
        const uint posLoc = 0, colorLoc = 1, uvLoc = 2, normalLoc = 3;
        _gl.EnableVertexAttribArray(posLoc);
        _gl.VertexAttribPointer(posLoc, 3, VertexAttribPointerType.Float, false, (uint)WorldVertex.Stride, (void*)0);
        _gl.EnableVertexAttribArray(colorLoc);
        _gl.VertexAttribPointer(colorLoc, 4, VertexAttribPointerType.UnsignedByte, true, (uint)WorldVertex.Stride, (void*)12);
        _gl.EnableVertexAttribArray(uvLoc);
        _gl.VertexAttribPointer(uvLoc, 2, VertexAttribPointerType.Float, false, (uint)WorldVertex.Stride, (void*)16);
        _gl.EnableVertexAttribArray(normalLoc);
        _gl.VertexAttribPointer(normalLoc, 3, VertexAttribPointerType.Float, false, (uint)WorldVertex.Stride, (void*)24);
    }

    public void SetIndexBuffer(IndexBuffer? buffer)
    {
        _boundIb = buffer;
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, buffer?.Handle ?? 0);
    }

    public void SetShader(Shader shader)
    {
        _boundShader = shader;
        _gl.UseProgram(shader.Program);
    }

    public void SetUniform(string name, Matrix4x4 m)
    {
        if (_boundShader is null) throw new InvalidOperationException("No shader bound");
        Span<float> data = stackalloc float[16];
        data[0] = m.M11; data[1] = m.M12; data[2] = m.M13; data[3] = m.M14;
        data[4] = m.M21; data[5] = m.M22; data[6] = m.M23; data[7] = m.M24;
        data[8] = m.M31; data[9] = m.M32; data[10] = m.M33; data[11] = m.M34;
        data[12] = m.M41; data[13] = m.M42; data[14] = m.M43; data[15] = m.M44;
        _gl.UniformMatrix4(_boundShader.Uniform(name), 1, false, data);
    }

    public void SetUniform(string name, Vector4 v)
    {
        if (_boundShader is null) throw new InvalidOperationException("No shader bound");
        _gl.Uniform4(_boundShader.Uniform(name), v.X, v.Y, v.Z, v.W);
    }

    public void SetUniform(string name, float value)
    {
        if (_boundShader is null) throw new InvalidOperationException("No shader bound");
        _gl.Uniform1(_boundShader.Uniform(name), value);
    }

    public void SetUniform(string name, int value)
    {
        if (_boundShader is null) throw new InvalidOperationException("No shader bound");
        _gl.Uniform1(_boundShader.Uniform(name), value);
    }

    public void SetTexture(int unit, Texture? texture)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + unit);
        _gl.BindTexture(TextureTarget.Texture2D, texture?.Handle ?? 0);
    }

    public void SetTexture(Texture? texture, int unit = 0)
        => SetTexture(unit, texture);

    public static TextureOperationPlan BuildSetTexturePlan(Texture? texture, int unit = 0)
        => new(TextureOperationKind.Bind, unit, texture != null);

    public static TextureOperationPlan BuildClearTexturePlan(uint colorArgb, Texture? texture)
        => new(TextureOperationKind.Clear, 0, texture != null, ColorArgb: colorArgb);

    public static TextureOperationPlan BuildCopyTexturePlan(CubeMapFace face, Texture? texture)
        => new(TextureOperationKind.CopyCubeFace, 0, texture != null, CubeFace: face);

    public static TextureOperationPlan BuildSetPixelsPlan(Texture? texture)
        => new(TextureOperationKind.SetPixels2D, 0, texture != null);

    public static TextureOperationPlan BuildSetCubePixelsPlan(Texture? texture, CubeMapFace face)
        => new(TextureOperationKind.SetPixelsCubeFace, 0, texture != null, CubeFace: face);

    public static TextureOperationPlan BuildMapPboPlan(Texture? texture)
        => new(TextureOperationKind.MapPbo, 0, texture != null);

    public static TextureOperationPlan BuildUnmapPboPlan(Texture? texture)
        => new(TextureOperationKind.UnmapPbo, 0, texture != null);

    public static RenderStateTogglePlan BuildAlphaTestPlan(bool enabled)
        => new(RenderStateToggleKind.AlphaTest, enabled);

    public static RenderStateTogglePlan BuildMultisampleAntialiasPlan(bool enabled)
        => new(RenderStateToggleKind.MultisampleAntialias, enabled);

    public static SamplerFilterPlan BuildSamplerFilterPlan(TextureFilter filter, int unit = 0)
        => BuildSamplerFilterPlan(filter, filter, MipmapFilter.None, 0.0f, unit);

    public static RenderStartPlan BuildStartRenderingPlan(bool clear, uint clearColorArgb)
        => new(clear, clearColorArgb, HasTarget: false, UseDepthBuffer: true);

    public static RenderStartPlan BuildStartRenderingPlan(bool clear, uint clearColorArgb, Texture? target, bool useDepthBuffer)
        => new(clear, clearColorArgb, target is not null, useDepthBuffer);

    public static SamplerFilterPlan BuildSamplerFilterPlan(
        TextureFilter min,
        TextureFilter mag,
        MipmapFilter mip,
        float maxAnisotropy,
        int unit = 0)
        => new(min, mag, mip, maxAnisotropy, unit);

    public void SetSamplerFilter(TextureFilter filter, int unit = 0)
        => SetSamplerFilter(filter, filter, MipmapFilter.None, unit);

    public void SetSamplerFilter(TextureFilter min, TextureFilter mag, MipmapFilter mip, float maxAnisotropy, int unit = 0)
        => SetSamplerFilter(min, mag, mip, unit);

    public void SetSamplerFilter(TextureFilter min, TextureFilter mag, MipmapFilter mip, int unit = 0)
    {
        // Per-texture parameter state; matches GLTexture semantics in UDB for the spike subset.
        _gl.ActiveTexture(TextureUnit.Texture0 + unit);
        GLEnum minFilter = (min, mip) switch
        {
            (TextureFilter.Nearest, MipmapFilter.None) => GLEnum.Nearest,
            (TextureFilter.Linear,  MipmapFilter.None) => GLEnum.Linear,
            (TextureFilter.Nearest, MipmapFilter.Nearest) => GLEnum.NearestMipmapNearest,
            (TextureFilter.Linear,  MipmapFilter.Nearest) => GLEnum.LinearMipmapNearest,
            (TextureFilter.Nearest, MipmapFilter.Linear) => GLEnum.NearestMipmapLinear,
            (TextureFilter.Linear,  MipmapFilter.Linear) => GLEnum.LinearMipmapLinear,
            _ => GLEnum.Linear
        };
        GLEnum magFilter = mag == TextureFilter.Linear ? GLEnum.Linear : GLEnum.Nearest;
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)minFilter);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)magFilter);
    }

    public void SetSamplerState(TextureAddress address, int unit = 0)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + unit);
        int wrap = address == TextureAddress.Wrap ? (int)GLEnum.Repeat : (int)GLEnum.ClampToEdge;
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, wrap);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, wrap);
    }

    public void SetBlendOperation(BlendOperation op)
    {
        _gl.BlendEquation(op == BlendOperation.Add ? GLEnum.FuncAdd : GLEnum.FuncReverseSubtract);
    }

    public void SetSourceBlend(Blend blend) => ApplyBlendFuncSource(blend);
    public void SetDestinationBlend(Blend blend) => ApplyBlendFuncDest(blend);

    private Blend _srcBlend = Blend.SourceAlpha;
    private Blend _dstBlend = Blend.InverseSourceAlpha;

    private void ApplyBlendFuncSource(Blend b)
    {
        _srcBlend = b;
        _gl.BlendFunc(MapBlend(_srcBlend), MapBlend(_dstBlend));
    }
    private void ApplyBlendFuncDest(Blend b)
    {
        _dstBlend = b;
        _gl.BlendFunc(MapBlend(_srcBlend), MapBlend(_dstBlend));
    }

    private static GLEnum MapBlend(Blend b) => b switch
    {
        Blend.SourceAlpha => GLEnum.SrcAlpha,
        Blend.InverseSourceAlpha => GLEnum.OneMinusSrcAlpha,
        Blend.One => GLEnum.One,
        _ => GLEnum.One
    };

    public void Draw(PrimitiveType type, int startVertex, int primitiveCount)
    {
        _gl.DrawArrays(MapPrim(type), startVertex, (uint)PrimToVerts(type, primitiveCount));
    }

    public unsafe void DrawIndexed(PrimitiveType type, int startIndex, int primitiveCount)
    {
        _gl.DrawElements(MapPrim(type), (uint)PrimToVerts(type, primitiveCount), DrawElementsType.UnsignedInt, (void*)(startIndex * sizeof(int)));
    }

    private static Silk.NET.OpenGL.PrimitiveType MapPrim(PrimitiveType type) => type switch
    {
        PrimitiveType.LineList => Silk.NET.OpenGL.PrimitiveType.Lines,
        PrimitiveType.TriangleList => Silk.NET.OpenGL.PrimitiveType.Triangles,
        PrimitiveType.TriangleStrip => Silk.NET.OpenGL.PrimitiveType.TriangleStrip,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static int PrimToVerts(PrimitiveType type, int primCount) => type switch
    {
        PrimitiveType.LineList => primCount * 2,
        PrimitiveType.TriangleList => primCount * 3,
        PrimitiveType.TriangleStrip => primCount + 2,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public void Dispose()
    {
        if (!Disposed)
        {
            _gl.DeleteVertexArray(_streamVao);
            _streamVao = 0;
        }
        GC.SuppressFinalize(this);
    }
}
