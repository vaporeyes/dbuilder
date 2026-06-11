// ABOUTME: Cross-platform RenderDevice spike using Silk.NET.OpenGL.
// ABOUTME: Subset of UDB's BuilderNative GLRenderDevice API: state, buffers, shader, draw, clear.

using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace DBuilder.Rendering;

public enum CubeMapFace : int
{
    PositiveX = 0,
    PositiveY = 1,
    PositiveZ = 2,
    NegativeX = 3,
    NegativeY = 4,
    NegativeZ = 5,
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

public enum DrawOperationKind
{
    Draw,
    DrawIndexed,
    DrawData,
}

public enum RenderFrameOperationKind
{
    FinishRendering,
    Present,
}

public enum RenderBufferOperationKind
{
    SetFlatVertexData,
    SetWorldVertexData,
    SetIndexData,
    SetVertexLength,
    SetIndexLength,
    SetFlatVertexSubdata,
    SetWorldVertexSubdata,
    SetIndexSubdata,
}

public enum RenderBufferBindingKind
{
    SetVertexBuffer,
    SetIndexBuffer,
}

public enum RenderShaderOperationKind
{
    DeclareUniform,
    DeclareShader,
    CompileShader,
    SetShader,
    SetUniform,
}

public enum RenderStateToggleKind
{
    AlphaBlend,
    AlphaTest,
    Depth,
    DepthWrite,
    MultisampleAntialias,
}

public enum RenderResourceRegistrationKind
{
    Register,
    Unregister,
}

public interface IRenderResource
{
    void UnloadResource();
    void ReloadResource();
}

public sealed record TextureOperationPlan(
    TextureOperationKind Kind,
    int Unit,
    bool HasTexture,
    CubeMapFace? CubeFace = null,
    uint? ColorArgb = null,
    int Width = 0,
    int Height = 0,
    int RequiredByteCount = 0,
    int ProvidedByteCount = 0,
    bool GenerateMipmaps = false);

public sealed record RenderStateTogglePlan(
    RenderStateToggleKind Kind,
    bool Enabled);

public sealed record BlendOperationPlan(
    BlendOperation Operation);

public sealed record BlendFactorPlan(
    Blend SourceBlend,
    Blend DestinationBlend);

public sealed record RasterStatePlan(
    Cull CullMode,
    FillMode FillMode);

public sealed record SamplerFilterPlan(
    TextureFilter MinFilter,
    TextureFilter MagFilter,
    MipmapFilter MipFilter,
    float MaxAnisotropy,
    int Unit);

public sealed record SamplerStatePlan(
    TextureAddress Address,
    int Unit);

public sealed record RenderDeviceSetupSettingsPlan(
    RenderStateTogglePlan AlphaBlend,
    RenderStateTogglePlan AlphaTest,
    Cull CullMode,
    Blend DestinationBlend,
    FillMode FillMode,
    RenderStateTogglePlan MultisampleAntialias,
    Blend SourceBlend,
    RenderStateTogglePlan Depth,
    RenderStateTogglePlan DepthWrite,
    TextureAddress SamplerAddress,
    SamplerFilterPlan SamplerFilter,
    bool InitializePresentation);

public sealed record ViewportPlan(
    int Width,
    int Height);

public sealed record RenderStartPlan(
    bool Clear,
    uint ClearColorArgb,
    bool HasTarget,
    bool UseDepthBuffer);

public sealed record DrawOperationPlan(
    DrawOperationKind Kind,
    PrimitiveType PrimitiveType,
    int StartIndex,
    int PrimitiveCount,
    int InlineVertexCount = 0);

public sealed record RenderFrameOperationPlan(
    RenderFrameOperationKind Kind,
    bool FlushCommands);

public sealed record RenderBufferOperationPlan(
    RenderBufferOperationKind Kind,
    VertexFormat? VertexFormat,
    int ElementCount,
    long ElementOffset,
    long ByteOffset,
    long ByteCount);

public sealed record RenderBufferBindingPlan(
    RenderBufferBindingKind Kind,
    bool HasBuffer,
    VertexFormat? VertexFormat,
    int ElementCount);

public sealed record RenderShaderOperationPlan(
    RenderShaderOperationKind Kind,
    ShaderName? ShaderName = null,
    UniformName? UniformName = null,
    UniformType? UniformType = null,
    string? UniformVariableName = null,
    string? VertexResourceName = null,
    string? FragmentResourceName = null,
    string? ShaderGroupName = null,
    string? ShaderEntryName = null,
    int ValueCount = 0,
    int ValueByteSize = 0,
    float[]? FloatValues = null,
    int[]? IntValues = null);

public sealed record RenderResourceRegistrationPlan(
    RenderResourceRegistrationKind Kind,
    bool WasRegistered,
    bool WillBeRegistered,
    bool ChangesRegistry);

public sealed class RenderDevice : IDisposable
{
    private readonly GL _gl;
    private readonly List<IRenderResource> _resources = new();
    private uint _streamVao;
    private VertexBuffer? _boundVb;
    private IndexBuffer? _boundIb;
    private Shader? _boundShader;
    private RenderShaderOperationPlan? _lastShaderOperation;
    private int _viewportW;
    private int _viewportH;
    private bool _alphaTestEnabled;
    private bool _multisampleAntialiasEnabled;

    public RenderDevice(GL gl)
    {
        ArgumentNullException.ThrowIfNull(gl);

        _gl = gl;
        _streamVao = _gl.GenVertexArray();
    }

    public GL GL => _gl;
    public bool Disposed => _streamVao == 0;
    public bool AlphaTestEnabled => _alphaTestEnabled;
    public bool MultisampleAntialiasEnabled => _multisampleAntialiasEnabled;
    public int RegisteredResourceCount => _resources.Count;
    public RenderShaderOperationPlan? LastShaderOperation => _lastShaderOperation;
    public VertexBuffer? BoundVertexBuffer => _boundVb;
    public IndexBuffer? BoundIndexBuffer => _boundIb;

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

    public void StartRendering(bool clear, Color4 backColor)
        => StartRendering(clear, (uint)backColor.ToArgb());

    public void FinishRendering() { /* nothing to flush yet */ }

    public void Present()
    {
        _gl.Flush();
    }

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

    public bool SetupSettings(bool visualBilinear, bool antialiasingEnabled, float filterAnisotropy)
        => SetupSettings(BuildSetupSettingsPlan(visualBilinear, antialiasingEnabled, filterAnisotropy));

    public bool SetupSettings(RenderDeviceSetupSettingsPlan plan)
    {
        SetAlphaBlendEnable(plan.AlphaBlend.Enabled);
        SetAlphaTestEnable(plan.AlphaTest.Enabled);
        SetCullMode(plan.CullMode);
        SetDestinationBlend(plan.DestinationBlend);
        SetFillMode(plan.FillMode);
        SetMultisampleAntialias(plan.MultisampleAntialias.Enabled);
        SetSourceBlend(plan.SourceBlend);
        SetZEnable(plan.Depth.Enabled);
        SetZWriteEnable(plan.DepthWrite.Enabled);
        SetSamplerState(plan.SamplerAddress);
        SetSamplerFilter(
            plan.SamplerFilter.MinFilter,
            plan.SamplerFilter.MagFilter,
            plan.SamplerFilter.MipFilter,
            plan.SamplerFilter.MaxAnisotropy,
            plan.SamplerFilter.Unit);

        return plan.InitializePresentation;
    }

    public bool RegisterResource(IRenderResource resource)
    {
        if (_resources.Contains(resource)) return false;

        _resources.Add(resource);
        return true;
    }

    public bool UnregisterResource(IRenderResource resource)
        => _resources.Remove(resource);

    public void UnloadRegisteredResources()
    {
        foreach (IRenderResource resource in _resources.ToArray())
        {
            resource.UnloadResource();
        }
    }

    public void ReloadRegisteredResources()
    {
        foreach (IRenderResource resource in _resources.ToArray())
        {
            resource.ReloadResource();
        }
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

    public unsafe void SetBufferData(IndexBuffer buffer, int length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

        buffer.IndexCount = length;
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, buffer.Handle);
        _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)checked((long)length * sizeof(int)), null, BufferUsageARB.StaticDraw);
    }

    public unsafe void SetBufferData(VertexBuffer buffer, int length, VertexFormat format)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

        int stride = VertexStride(format);

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

    public unsafe void SetBufferSubdata(IndexBuffer buffer, long destOffset, int[] data)
    {
        if (destOffset < 0) throw new ArgumentOutOfRangeException(nameof(destOffset));

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, buffer.Handle);
        fixed (int* p = data)
        {
            _gl.BufferSubData(
                BufferTargetARB.ElementArrayBuffer,
                checked((nint)(destOffset * sizeof(int))),
                (nuint)checked((long)data.Length * sizeof(int)),
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

    public static RenderBufferOperationPlan BuildSetBufferDataPlan(FlatVertex[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetFlatVertexData,
            VertexFormat.Flat,
            data.Length,
            ElementOffset: 0,
            ByteOffset: 0,
            ByteCount: checked((long)data.Length * FlatVertex.Stride));
    }

    public static RenderBufferOperationPlan BuildSetBufferDataPlan(WorldVertex[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetWorldVertexData,
            VertexFormat.World,
            data.Length,
            ElementOffset: 0,
            ByteOffset: 0,
            ByteCount: checked((long)data.Length * WorldVertex.Stride));
    }

    public static RenderBufferOperationPlan BuildSetBufferDataPlan(int[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetIndexData,
            VertexFormat: null,
            data.Length,
            ElementOffset: 0,
            ByteOffset: 0,
            ByteCount: checked((long)data.Length * sizeof(int)));
    }

    public static RenderBufferOperationPlan BuildSetIndexBufferDataPlan(int length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

        return new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetIndexLength,
            VertexFormat: null,
            length,
            ElementOffset: 0,
            ByteOffset: 0,
            ByteCount: checked((long)length * sizeof(int)));
    }

    public static RenderBufferOperationPlan BuildSetBufferDataPlan(int length, VertexFormat format)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

        return new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetVertexLength,
            format,
            length,
            ElementOffset: 0,
            ByteOffset: 0,
            ByteCount: checked((long)length * VertexStride(format)));
    }

    public static RenderBufferOperationPlan BuildSetBufferSubdataPlan(long destOffset, FlatVertex[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (destOffset < 0) throw new ArgumentOutOfRangeException(nameof(destOffset));

        return new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetFlatVertexSubdata,
            VertexFormat.Flat,
            data.Length,
            destOffset,
            ByteOffset: checked(destOffset * FlatVertex.Stride),
            ByteCount: checked((long)data.Length * FlatVertex.Stride));
    }

    public static RenderBufferOperationPlan BuildSetBufferSubdataPlan(long destOffset, WorldVertex[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (destOffset < 0) throw new ArgumentOutOfRangeException(nameof(destOffset));

        return new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetWorldVertexSubdata,
            VertexFormat.World,
            data.Length,
            destOffset,
            ByteOffset: checked(destOffset * WorldVertex.Stride),
            ByteCount: checked((long)data.Length * WorldVertex.Stride));
    }

    public static RenderBufferOperationPlan BuildSetBufferSubdataPlan(FlatVertex[] data, long size)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (size < 0 || size > data.Length) throw new ArgumentOutOfRangeException(nameof(size));

        return new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetFlatVertexSubdata,
            VertexFormat.Flat,
            checked((int)size),
            ElementOffset: 0,
            ByteOffset: 0,
            ByteCount: checked(size * FlatVertex.Stride));
    }

    public static RenderBufferOperationPlan BuildSetIndexBufferSubdataPlan(long destOffset, int[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (destOffset < 0) throw new ArgumentOutOfRangeException(nameof(destOffset));

        return new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetIndexSubdata,
            VertexFormat: null,
            data.Length,
            destOffset,
            ByteOffset: checked(destOffset * sizeof(int)),
            ByteCount: checked((long)data.Length * sizeof(int)));
    }

    public static RenderBufferBindingPlan BuildSetVertexBufferPlan(VertexFormat format, int vertexCount)
    {
        if (vertexCount < 0) throw new ArgumentOutOfRangeException(nameof(vertexCount));
        _ = VertexStride(format);

        return new RenderBufferBindingPlan(
            RenderBufferBindingKind.SetVertexBuffer,
            HasBuffer: true,
            VertexFormat: format,
            ElementCount: vertexCount);
    }

    public static RenderBufferBindingPlan BuildReleaseVertexBufferPlan()
        => new(
            RenderBufferBindingKind.SetVertexBuffer,
            HasBuffer: false,
            VertexFormat: null,
            ElementCount: 0);

    public static RenderBufferBindingPlan BuildSetIndexBufferPlan(int indexCount)
    {
        if (indexCount < 0) throw new ArgumentOutOfRangeException(nameof(indexCount));

        return new RenderBufferBindingPlan(
            RenderBufferBindingKind.SetIndexBuffer,
            HasBuffer: true,
            VertexFormat: null,
            ElementCount: indexCount);
    }

    public static RenderBufferBindingPlan BuildReleaseIndexBufferPlan()
        => new(
            RenderBufferBindingKind.SetIndexBuffer,
            HasBuffer: false,
            VertexFormat: null,
            ElementCount: 0);

    private static int VertexStride(VertexFormat format)
        => format switch
        {
            VertexFormat.Flat => FlatVertex.Stride,
            VertexFormat.World => WorldVertex.Stride,
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

    public void SetShader(Shader shader)
    {
        _boundShader = shader;
        _gl.UseProgram(shader.Program);
    }

    public void DeclareUniform(UniformName name, string variableName, UniformType type)
        => _lastShaderOperation = BuildDeclareUniformPlan(name, variableName, type);

    public void DeclareShader(ShaderName name, string vertexResourceName, string fragmentResourceName)
        => _lastShaderOperation = BuildDeclareShaderPlan(name, vertexResourceName, fragmentResourceName);

    public void CompileShader(ShaderName internalName, string groupName, string shaderName)
        => _lastShaderOperation = BuildCompileShaderPlan(internalName, groupName, shaderName);

    public void SetShader(ShaderName shader)
        => _lastShaderOperation = BuildSetShaderPlan(shader);

    public void SetUniform(UniformName uniform, bool value)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, value);

    public void SetUniform(UniformName uniform, float value)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, value);

    public void SetUniform(UniformName uniform, Vector2f value)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, value);

    public void SetUniform(UniformName uniform, Vector3f value)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, value);

    public void SetUniform(UniformName uniform, Vector4f value)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, value);

    public void SetUniform(UniformName uniform, Color4 value)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, value);

    public void SetUniform(UniformName uniform, Matrix matrix)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, matrix);

    public void SetUniform(UniformName uniform, ref Matrix matrix)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, matrix);

    public void SetUniform(UniformName uniform, int value)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, value);

    public void SetUniform(UniformName uniform, Vector2i value)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, value);

    public void SetUniform(UniformName uniform, Vector3i value)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, value);

    public void SetUniform(UniformName uniform, Vector4i value)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, value);

    public void SetUniform(UniformName uniform, Vector2f[] value)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, value);

    public void SetUniform(UniformName uniform, Vector3f[] value)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, value);

    public void SetUniform(UniformName uniform, Vector4f[] value)
        => _lastShaderOperation = BuildSetUniformPlan(uniform, value);

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
        => SetTexture(unit, (BaseTexture?)texture);

    public void SetTexture(int unit, BaseTexture? texture)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + unit);
        TextureTarget target = texture is CubeTexture ? TextureTarget.TextureCubeMap : TextureTarget.Texture2D;
        _gl.BindTexture(target, texture?.Handle ?? 0);
    }

    public void SetTexture(Texture? texture, int unit = 0)
        => SetTexture(unit, texture);

    public void SetTexture(BaseTexture? texture, int unit = 0)
        => SetTexture(unit, texture);

    public void ClearTexture(uint colorArgb, Texture texture)
    {
        if (texture.Width <= 0 || texture.Height <= 0)
            throw new InvalidOperationException("Texture dimensions are not initialized.");

        byte a = (byte)((colorArgb >> 24) & 0xff);
        byte r = (byte)((colorArgb >> 16) & 0xff);
        byte g = (byte)((colorArgb >> 8) & 0xff);
        byte b = (byte)(colorArgb & 0xff);
        byte[] pixels = new byte[checked(texture.Width * texture.Height * 4)];

        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = a;
        }

        texture.SetPixelsRgba8(texture.Width, texture.Height, pixels, generateMipmaps: false);
    }

    public void ClearTexture(Color4 backColor, Texture texture)
        => ClearTexture((uint)backColor.ToArgb(), texture);

    public void SetPixels(Texture texture, int width, int height, byte[] rgba, bool generateMipmaps = true)
        => texture.SetPixelsRgba8(width, height, rgba, generateMipmaps);

    public unsafe void SetPixels(Texture texture, uint* pixelData)
    {
        if (texture.Width <= 0 || texture.Height <= 0)
            throw new InvalidOperationException("Texture dimensions are not initialized.");

        _gl.BindTexture(TextureTarget.Texture2D, texture.Handle);
        _gl.TexSubImage2D(
            TextureTarget.Texture2D,
            0,
            0,
            0,
            (uint)texture.Width,
            (uint)texture.Height,
            PixelFormat.Bgra,
            PixelType.UnsignedByte,
            pixelData);
    }

    public void CopyTexture(CubeTexture destination, CubeMapFace face)
    {
        _gl.BindTexture(TextureTarget.TextureCubeMap, destination.Handle);
        _gl.CopyTexSubImage2D(
            MapCubeFace(face),
            0,
            0,
            0,
            0,
            0,
            (uint)destination.Size,
            (uint)destination.Size);
    }

    public void SetPixels(CubeTexture texture, CubeMapFace face, byte[] rgba, bool generateMipmaps = true)
        => texture.SetPixelsRgba8(face, rgba, generateMipmaps);

    public static TextureOperationPlan BuildSetTexturePlan(Texture? texture, int unit = 0)
        => new(TextureOperationKind.Bind, unit, texture != null);

    public static TextureOperationPlan BuildClearTexturePlan(uint colorArgb, Texture? texture)
        => new(TextureOperationKind.Clear, 0, texture != null, ColorArgb: colorArgb);

    public static TextureOperationPlan BuildClearTexturePlan(Color4 backColor, Texture? texture)
        => BuildClearTexturePlan((uint)backColor.ToArgb(), texture);

    public static TextureOperationPlan BuildCopyTexturePlan(CubeMapFace face, Texture? texture)
        => new(TextureOperationKind.CopyCubeFace, 0, texture != null, CubeFace: face);

    public static TextureOperationPlan BuildSetPixelsPlan(Texture? texture)
        => new(TextureOperationKind.SetPixels2D, 0, texture != null);

    public static TextureOperationPlan BuildSetPixelsPlan(
        Texture? texture,
        int width,
        int height,
        int pixelBufferByteCount,
        bool generateMipmaps = true)
    {
        TexturePixelUploadPlan plan = Texture.BuildRgba8UploadPlan(width, height, pixelBufferByteCount, generateMipmaps);

        return new TextureOperationPlan(
            TextureOperationKind.SetPixels2D,
            Unit: 0,
            HasTexture: texture != null,
            Width: plan.Width,
            Height: plan.Height,
            RequiredByteCount: plan.RequiredByteCount,
            ProvidedByteCount: plan.ProvidedByteCount,
            GenerateMipmaps: plan.GenerateMipmaps);
    }

    public static TextureOperationPlan BuildSetCubePixelsPlan(CubeTexture? texture, CubeMapFace face)
        => new(TextureOperationKind.SetPixelsCubeFace, 0, texture != null, CubeFace: face);

    public static TextureOperationPlan BuildSetCubePixelsPlan(
        CubeTexture? texture,
        CubeMapFace face,
        int size,
        int pixelBufferByteCount,
        bool generateMipmaps = true)
    {
        TexturePixelUploadPlan plan = CubeTexture.BuildRgba8UploadPlan(face, size, pixelBufferByteCount, generateMipmaps);

        return new TextureOperationPlan(
            TextureOperationKind.SetPixelsCubeFace,
            Unit: 0,
            HasTexture: texture != null,
            CubeFace: plan.CubeFace,
            Width: plan.Width,
            Height: plan.Height,
            RequiredByteCount: plan.RequiredByteCount,
            ProvidedByteCount: plan.ProvidedByteCount,
            GenerateMipmaps: plan.GenerateMipmaps);
    }

    public static TextureOperationPlan BuildMapPboPlan(Texture? texture)
        => new(TextureOperationKind.MapPbo, 0, texture != null);

    public static TextureOperationPlan BuildUnmapPboPlan(Texture? texture)
        => new(TextureOperationKind.UnmapPbo, 0, texture != null);

    public static RenderResourceRegistrationPlan BuildResourceRegistrationPlan(
        RenderResourceRegistrationKind kind,
        bool wasRegistered)
    {
        bool willBeRegistered = kind switch
        {
            RenderResourceRegistrationKind.Register => true,
            RenderResourceRegistrationKind.Unregister => false,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        return new(kind, wasRegistered, willBeRegistered, wasRegistered != willBeRegistered);
    }

    public static RenderStateTogglePlan BuildAlphaBlendPlan(bool enabled)
        => new(RenderStateToggleKind.AlphaBlend, enabled);

    public static RenderStateTogglePlan BuildAlphaTestPlan(bool enabled)
        => new(RenderStateToggleKind.AlphaTest, enabled);

    public static RenderStateTogglePlan BuildMultisampleAntialiasPlan(bool enabled)
        => new(RenderStateToggleKind.MultisampleAntialias, enabled);

    public static RenderStateTogglePlan BuildDepthPlan(bool enabled)
        => new(RenderStateToggleKind.Depth, enabled);

    public static RenderStateTogglePlan BuildDepthWritePlan(bool enabled)
        => new(RenderStateToggleKind.DepthWrite, enabled);

    public static BlendOperationPlan BuildBlendOperationPlan(BlendOperation operation)
        => new(operation);

    public static BlendFactorPlan BuildBlendFactorPlan(Blend sourceBlend, Blend destinationBlend)
        => new(sourceBlend, destinationBlend);

    public static RasterStatePlan BuildRasterStatePlan(Cull cullMode, FillMode fillMode)
        => new(cullMode, fillMode);

    public static RenderDeviceSetupSettingsPlan BuildSetupSettingsPlan(
        bool visualBilinear,
        bool antialiasingEnabled,
        float filterAnisotropy)
    {
        TextureFilter magMinFilter = visualBilinear ? TextureFilter.Linear : TextureFilter.Nearest;
        MipmapFilter mipFilter = visualBilinear ? MipmapFilter.Linear : MipmapFilter.Nearest;

        return new(
            AlphaBlend: BuildAlphaBlendPlan(enabled: false),
            AlphaTest: BuildAlphaTestPlan(enabled: false),
            CullMode: Cull.None,
            DestinationBlend: Blend.InverseSourceAlpha,
            FillMode: FillMode.Solid,
            MultisampleAntialias: BuildMultisampleAntialiasPlan(antialiasingEnabled),
            SourceBlend: Blend.SourceAlpha,
            Depth: BuildDepthPlan(enabled: false),
            DepthWrite: BuildDepthWritePlan(enabled: false),
            SamplerAddress: TextureAddress.Wrap,
            SamplerFilter: BuildSamplerFilterPlan(
                magMinFilter,
                magMinFilter,
                mipFilter,
                filterAnisotropy),
            InitializePresentation: true);
    }

    public static SamplerFilterPlan BuildSamplerFilterPlan(TextureFilter filter, int unit = 0)
        => BuildSamplerFilterPlan(filter, filter, MipmapFilter.None, 0.0f, unit);

    public static SamplerStatePlan BuildSamplerStatePlan(TextureAddress address, int unit = 0)
        => new(address, unit);

    public static ViewportPlan BuildViewportPlan(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        return new ViewportPlan(width, height);
    }

    public static RenderStartPlan BuildStartRenderingPlan(bool clear, uint clearColorArgb)
        => new(clear, clearColorArgb, HasTarget: false, UseDepthBuffer: true);

    public static RenderStartPlan BuildStartRenderingPlan(bool clear, Color4 backColor)
        => BuildStartRenderingPlan(clear, (uint)backColor.ToArgb());

    public static RenderStartPlan BuildStartRenderingPlan(bool clear, uint clearColorArgb, Texture? target, bool useDepthBuffer)
        => new(clear, clearColorArgb, target is not null, useDepthBuffer);

    public static RenderStartPlan BuildStartRenderingPlan(bool clear, Color4 backColor, Texture? target, bool useDepthBuffer)
        => BuildStartRenderingPlan(clear, (uint)backColor.ToArgb(), target, useDepthBuffer);

    public static DrawOperationPlan BuildDrawPlan(PrimitiveType type, int startIndex, int primitiveCount)
        => new(DrawOperationKind.Draw, type, startIndex, primitiveCount);

    public static DrawOperationPlan BuildDrawIndexedPlan(PrimitiveType type, int startIndex, int primitiveCount)
        => new(DrawOperationKind.DrawIndexed, type, startIndex, primitiveCount);

    public static DrawOperationPlan BuildDrawDataPlan(PrimitiveType type, int startIndex, int primitiveCount, FlatVertex[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new DrawOperationPlan(DrawOperationKind.DrawData, type, startIndex, primitiveCount, data.Length);
    }

    public static RenderFrameOperationPlan BuildFinishRenderingPlan()
        => new(RenderFrameOperationKind.FinishRendering, FlushCommands: false);

    public static RenderFrameOperationPlan BuildPresentPlan()
        => new(RenderFrameOperationKind.Present, FlushCommands: true);

    public static RenderShaderOperationPlan BuildDeclareUniformPlan(
        UniformName name,
        string variableName,
        UniformType type)
        => new(
            RenderShaderOperationKind.DeclareUniform,
            UniformName: name,
            UniformType: type,
            UniformVariableName: variableName);

    public static RenderShaderOperationPlan BuildDeclareShaderPlan(
        ShaderName name,
        string vertexResourceName,
        string fragmentResourceName)
        => new(
            RenderShaderOperationKind.DeclareShader,
            ShaderName: name,
            VertexResourceName: vertexResourceName,
            FragmentResourceName: fragmentResourceName);

    public static RenderShaderOperationPlan BuildCompileShaderPlan(
        ShaderName internalName,
        string groupName,
        string shaderName)
        => new(
            RenderShaderOperationKind.CompileShader,
            ShaderName: internalName,
            ShaderGroupName: groupName,
            ShaderEntryName: shaderName);

    public static RenderShaderOperationPlan BuildSetShaderPlan(ShaderName shader)
        => new(RenderShaderOperationKind.SetShader, ShaderName: shader);

    public static RenderShaderOperationPlan BuildSetUniformPlan(
        UniformName uniform,
        UniformType type,
        int valueCount = 1)
    {
        if (valueCount < 0) throw new ArgumentOutOfRangeException(nameof(valueCount));

        return new(
            RenderShaderOperationKind.SetUniform,
            UniformName: uniform,
            UniformType: type,
            ValueCount: valueCount,
            ValueByteSize: checked(GetUniformValueByteSize(type, valueCount)));
    }

    public static RenderShaderOperationPlan BuildSetUniformPlan(UniformName uniform, bool value)
        => BuildSetUniformPlan(uniform, UniformType.Float, floatValues: [value ? 1.0f : 0.0f]);

    public static RenderShaderOperationPlan BuildSetUniformPlan(UniformName uniform, float value)
        => BuildSetUniformPlan(uniform, UniformType.Float, floatValues: [value]);

    public static RenderShaderOperationPlan BuildSetUniformPlan(UniformName uniform, Vector2f value)
        => BuildSetUniformPlan(uniform, UniformType.Vec2f, floatValues: [value.X, value.Y]);

    public static RenderShaderOperationPlan BuildSetUniformPlan(UniformName uniform, Vector3f value)
        => BuildSetUniformPlan(uniform, UniformType.Vec3f, floatValues: [value.X, value.Y, value.Z]);

    public static RenderShaderOperationPlan BuildSetUniformPlan(UniformName uniform, Vector4f value)
        => BuildSetUniformPlan(uniform, UniformType.Vec4f, floatValues: [value.X, value.Y, value.Z, value.W]);

    public static RenderShaderOperationPlan BuildSetUniformPlan(UniformName uniform, Color4 value)
        => BuildSetUniformPlan(uniform, UniformType.Vec4f, floatValues: [value.Red, value.Green, value.Blue, value.Alpha]);

    public static RenderShaderOperationPlan BuildSetUniformPlan(UniformName uniform, Matrix value)
        => BuildSetUniformPlan(
            uniform,
            UniformType.Mat4,
            floatValues:
            [
                value.M11, value.M12, value.M13, value.M14,
                value.M21, value.M22, value.M23, value.M24,
                value.M31, value.M32, value.M33, value.M34,
                value.M41, value.M42, value.M43, value.M44,
            ]);

    public static RenderShaderOperationPlan BuildSetUniformPlan(UniformName uniform, int value)
        => BuildSetUniformPlan(uniform, UniformType.Int, intValues: [value]);

    public static RenderShaderOperationPlan BuildSetUniformPlan(UniformName uniform, Vector2i value)
        => BuildSetUniformPlan(uniform, UniformType.Vec2i, intValues: [value.X, value.Y]);

    public static RenderShaderOperationPlan BuildSetUniformPlan(UniformName uniform, Vector3i value)
        => BuildSetUniformPlan(uniform, UniformType.Vec3i, intValues: [value.X, value.Y, value.Z]);

    public static RenderShaderOperationPlan BuildSetUniformPlan(UniformName uniform, Vector4i value)
        => BuildSetUniformPlan(uniform, UniformType.Vec4i, intValues: [value.X, value.Y, value.Z, value.W]);

    public static RenderShaderOperationPlan BuildSetUniformPlan(UniformName uniform, Vector2f[] value)
    {
        float[] values = new float[value.Length * 2];
        for (int i = 0; i < value.Length; i++)
        {
            int index = i * 2;
            values[index] = value[i].X;
            values[index + 1] = value[i].Y;
        }

        return BuildSetUniformPlan(uniform, UniformType.Vec2fArray, value.Length, floatValues: values);
    }

    public static RenderShaderOperationPlan BuildSetUniformPlan(UniformName uniform, Vector3f[] value)
    {
        float[] values = new float[value.Length * 3];
        for (int i = 0; i < value.Length; i++)
        {
            int index = i * 3;
            values[index] = value[i].X;
            values[index + 1] = value[i].Y;
            values[index + 2] = value[i].Z;
        }

        return BuildSetUniformPlan(uniform, UniformType.Vec3fArray, value.Length, floatValues: values);
    }

    public static RenderShaderOperationPlan BuildSetUniformPlan(UniformName uniform, Vector4f[] value)
    {
        float[] values = new float[value.Length * 4];
        for (int i = 0; i < value.Length; i++)
        {
            int index = i * 4;
            values[index] = value[i].X;
            values[index + 1] = value[i].Y;
            values[index + 2] = value[i].Z;
            values[index + 3] = value[i].W;
        }

        return BuildSetUniformPlan(uniform, UniformType.Vec4fArray, value.Length, floatValues: values);
    }

    public static SamplerFilterPlan BuildSamplerFilterPlan(
        TextureFilter min,
        TextureFilter mag,
        MipmapFilter mip,
        float maxAnisotropy,
        int unit = 0)
        => new(min, mag, mip, maxAnisotropy, unit);

    private static RenderShaderOperationPlan BuildSetUniformPlan(
        UniformName uniform,
        UniformType type,
        int valueCount = 1,
        float[]? floatValues = null,
        int[]? intValues = null)
    {
        if (valueCount < 0) throw new ArgumentOutOfRangeException(nameof(valueCount));

        return new(
            RenderShaderOperationKind.SetUniform,
            UniformName: uniform,
            UniformType: type,
            ValueCount: valueCount,
            ValueByteSize: checked(GetUniformValueByteSize(type, valueCount)),
            FloatValues: floatValues,
            IntValues: intValues);
    }

    private static int GetUniformValueByteSize(UniformType type, int valueCount)
    {
        const int FloatSize = sizeof(float);
        const int IntSize = sizeof(int);

        return type switch
        {
            UniformType.Vec4f => 4 * FloatSize,
            UniformType.Vec3f => 3 * FloatSize,
            UniformType.Vec2f => 2 * FloatSize,
            UniformType.Float => FloatSize,
            UniformType.Mat4 => 16 * FloatSize,
            UniformType.Vec4i => 4 * IntSize,
            UniformType.Vec3i => 3 * IntSize,
            UniformType.Vec2i => 2 * IntSize,
            UniformType.Int => IntSize,
            UniformType.Vec4fArray => checked(valueCount * 4 * FloatSize),
            UniformType.Vec3fArray => checked(valueCount * 3 * FloatSize),
            UniformType.Vec2fArray => checked(valueCount * 2 * FloatSize),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

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

    public unsafe void Draw(PrimitiveType type, int startVertex, int primitiveCount, FlatVertex[] data)
    {
        uint tempBuffer = _gl.GenBuffer();
        try
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, tempBuffer);
            fixed (FlatVertex* p = data)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * FlatVertex.Stride), p, BufferUsageARB.StaticDraw);
            }
            ConfigureFlatAttribs();
            _gl.DrawArrays(MapPrim(type), startVertex, (uint)PrimToVerts(type, primitiveCount));
        }
        finally
        {
            _gl.DeleteBuffer(tempBuffer);
            RestoreVertexBufferBinding();
        }
    }

    public unsafe void DrawIndexed(PrimitiveType type, int startIndex, int primitiveCount)
    {
        _gl.DrawElements(MapPrim(type), (uint)PrimToVerts(type, primitiveCount), DrawElementsType.UnsignedInt, (void*)(startIndex * sizeof(int)));
    }

    private unsafe void RestoreVertexBufferBinding()
    {
        if (_boundVb is null)
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            return;
        }

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _boundVb.Handle);
        if (_boundVb.Format == VertexFormat.Flat)
            ConfigureFlatAttribs();
        else
            ConfigureWorldAttribs();
    }

    private static Silk.NET.OpenGL.PrimitiveType MapPrim(PrimitiveType type) => type switch
    {
        PrimitiveType.LineList => Silk.NET.OpenGL.PrimitiveType.Lines,
        PrimitiveType.TriangleList => Silk.NET.OpenGL.PrimitiveType.Triangles,
        PrimitiveType.TriangleStrip => Silk.NET.OpenGL.PrimitiveType.TriangleStrip,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static TextureTarget MapCubeFace(CubeMapFace face)
        => face switch
        {
            CubeMapFace.PositiveX => TextureTarget.TextureCubeMapPositiveX,
            CubeMapFace.PositiveY => TextureTarget.TextureCubeMapPositiveY,
            CubeMapFace.PositiveZ => TextureTarget.TextureCubeMapPositiveZ,
            CubeMapFace.NegativeX => TextureTarget.TextureCubeMapNegativeX,
            CubeMapFace.NegativeY => TextureTarget.TextureCubeMapNegativeY,
            CubeMapFace.NegativeZ => TextureTarget.TextureCubeMapNegativeZ,
            _ => throw new ArgumentOutOfRangeException(nameof(face)),
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
