// ABOUTME: Managed 2D texture wrapper over a GL texture object.
// ABOUTME: Tracks UDB-style texture format metadata, RGBA8 upload, mipmap generation, and disposal.

using Silk.NET.OpenGL;

namespace DBuilder.Rendering;

public enum TextureFormat
{
    Rgba8,
    Bgra8,
    Rg16f,
    Rgba16f,
    R32f,
    Rg32f,
    Rgb32f,
    Rgba32f,
    D32f_S8,
    D24_S8,
}

public enum TextureAllocationKind
{
    Texture2D,
    Cube,
}

public sealed record TextureAllocationPlan(
    TextureAllocationKind Kind,
    int Width,
    int Height,
    TextureFormat Format);

public sealed class Texture : IDisposable
{
    private readonly GL _gl;
    internal uint Handle { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public TextureFormat Format { get; private set; }
    public object? Tag { get; set; }
    public int UserData { get; set; }
    public bool Disposed => Handle == 0;

    public Texture(GL gl)
    {
        _gl = gl;
        Handle = _gl.GenTexture();
        if (Handle == 0) throw new InvalidOperationException("Texture allocation failed.");
    }

    public static TextureAllocationPlan Build2DAllocationPlan(int width, int height, TextureFormat format)
        => new(TextureAllocationKind.Texture2D, width, height, format);

    public static TextureAllocationPlan BuildCubeAllocationPlan(int size)
        => new(TextureAllocationKind.Cube, size, size, TextureFormat.Bgra8);

    public unsafe void SetPixelsRgba8(int width, int height, ReadOnlySpan<byte> rgba, bool generateMipmaps = true)
    {
        if (rgba.Length < width * height * 4)
            throw new ArgumentException("Pixel buffer too small for declared dimensions", nameof(rgba));

        Width = width;
        Height = height;
        Format = TextureFormat.Rgba8;

        _gl.BindTexture(TextureTarget.Texture2D, Handle);
        fixed (byte* p = rgba)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D, 0,
                InternalFormat.Rgba8,
                (uint)width, (uint)height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
        if (generateMipmaps)
            _gl.GenerateMipmap(TextureTarget.Texture2D);
    }

    public void Dispose()
    {
        if (!Disposed)
        {
            _gl.DeleteTexture(Handle);
            Handle = 0;
        }
        GC.SuppressFinalize(this);
    }
}

public sealed class CubeTexture : IDisposable
{
    private readonly GL _gl;
    internal uint Handle { get; private set; }
    public int Size { get; private set; }
    public TextureFormat Format { get; private set; }
    public object? Tag { get; set; }
    public int UserData { get; set; }
    public bool Disposed => Handle == 0;

    public unsafe CubeTexture(GL gl, int size)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));

        _gl = gl;
        Handle = _gl.GenTexture();
        if (Handle == 0) throw new InvalidOperationException("Texture allocation failed.");

        Size = size;
        Format = TextureFormat.Bgra8;
        _gl.BindTexture(TextureTarget.TextureCubeMap, Handle);
        for (int i = 0; i < 6; i++)
        {
            _gl.TexImage2D(
                TextureTarget.TextureCubeMapPositiveX + i,
                0,
                InternalFormat.Rgba8,
                (uint)size,
                (uint)size,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                null);
        }
    }

    public unsafe void SetPixelsRgba8(CubeMapFace face, ReadOnlySpan<byte> rgba, bool generateMipmaps = true)
    {
        if (rgba.Length < Size * Size * 4)
            throw new ArgumentException("Pixel buffer too small for declared dimensions", nameof(rgba));

        _gl.BindTexture(TextureTarget.TextureCubeMap, Handle);
        fixed (byte* p = rgba)
        {
            _gl.TexImage2D(
                MapFace(face),
                0,
                InternalFormat.Rgba8,
                (uint)Size,
                (uint)Size,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                p);
        }
        if (generateMipmaps)
            _gl.GenerateMipmap(TextureTarget.TextureCubeMap);
    }

    private static TextureTarget MapFace(CubeMapFace face)
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

    public void Dispose()
    {
        if (!Disposed)
        {
            _gl.DeleteTexture(Handle);
            Handle = 0;
        }
        GC.SuppressFinalize(this);
    }
}
