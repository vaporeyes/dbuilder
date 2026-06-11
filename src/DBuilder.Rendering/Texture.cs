// ABOUTME: Managed texture wrappers over GL texture objects.
// ABOUTME: Tracks UDB-style base, 2D, and cube texture metadata, allocation, upload, and disposal.

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

public abstract class BaseTexture : IDisposable
{
    protected readonly GL Gl;
    internal uint Handle { get; private set; }
    public object? Tag { get; set; }
    public int UserData { get; set; }
    public bool Disposed => Handle == 0;

    protected BaseTexture(GL gl)
    {
        Gl = gl;
        Handle = Gl.GenTexture();
        if (Handle == 0) throw new InvalidOperationException("Texture allocation failed.");
    }

    ~BaseTexture()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (!Disposed)
        {
            Gl.DeleteTexture(Handle);
            Handle = 0;
        }
        GC.SuppressFinalize(this);
    }
}

public sealed class Texture : BaseTexture
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public TextureFormat Format { get; private set; }

    public Texture(GL gl)
        : base(gl)
    {
    }

    public Texture(GL gl, int width, int height, TextureFormat format)
        : this(gl)
    {
        Allocate2D(width, height, format);
    }

    public static TextureAllocationPlan Build2DAllocationPlan(int width, int height, TextureFormat format)
        => new(TextureAllocationKind.Texture2D, width, height, format);

    public static TextureAllocationPlan BuildCubeAllocationPlan(int size)
        => new(TextureAllocationKind.Cube, size, size, TextureFormat.Bgra8);

    public unsafe void Allocate2D(int width, int height, TextureFormat format)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        (InternalFormat internalFormat, PixelFormat pixelFormat, PixelType pixelType) = MapFormat(format);
        Width = width;
        Height = height;
        Format = format;

        Gl.BindTexture(TextureTarget.Texture2D, Handle);
        Gl.TexImage2D(
            TextureTarget.Texture2D,
            0,
            internalFormat,
            (uint)width,
            (uint)height,
            0,
            pixelFormat,
            pixelType,
            null);
    }

    public unsafe void SetPixelsRgba8(int width, int height, ReadOnlySpan<byte> rgba, bool generateMipmaps = true)
    {
        if (rgba.Length < width * height * 4)
            throw new ArgumentException("Pixel buffer too small for declared dimensions", nameof(rgba));

        Width = width;
        Height = height;
        Format = TextureFormat.Rgba8;

        Gl.BindTexture(TextureTarget.Texture2D, Handle);
        fixed (byte* p = rgba)
        {
            Gl.TexImage2D(
                TextureTarget.Texture2D, 0,
                InternalFormat.Rgba8,
                (uint)width, (uint)height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
        if (generateMipmaps)
            Gl.GenerateMipmap(TextureTarget.Texture2D);
    }

    private static (InternalFormat InternalFormat, PixelFormat PixelFormat, PixelType PixelType) MapFormat(TextureFormat format)
        => format switch
        {
            TextureFormat.Rgba8 => (InternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte),
            TextureFormat.Bgra8 => (InternalFormat.Rgba8, PixelFormat.Bgra, PixelType.UnsignedByte),
            TextureFormat.Rg16f => (InternalFormat.RG16f, PixelFormat.RG, PixelType.HalfFloat),
            TextureFormat.Rgba16f => (InternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.HalfFloat),
            TextureFormat.R32f => (InternalFormat.R32f, PixelFormat.Red, PixelType.Float),
            TextureFormat.Rg32f => (InternalFormat.RG32f, PixelFormat.RG, PixelType.Float),
            TextureFormat.Rgb32f => (InternalFormat.Rgb32f, PixelFormat.Rgb, PixelType.Float),
            TextureFormat.Rgba32f => (InternalFormat.Rgba32f, PixelFormat.Rgba, PixelType.Float),
            TextureFormat.D32f_S8 => (InternalFormat.Depth32fStencil8, PixelFormat.DepthStencil, PixelType.Float32UnsignedInt248Rev),
            TextureFormat.D24_S8 => (InternalFormat.Depth24Stencil8, PixelFormat.DepthStencil, PixelType.UnsignedInt248),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
}

public sealed class CubeTexture : BaseTexture
{
    public int Size { get; private set; }
    public TextureFormat Format { get; private set; }

    public unsafe CubeTexture(GL gl, int size)
        : base(gl)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));

        Size = size;
        Format = TextureFormat.Bgra8;
        Gl.BindTexture(TextureTarget.TextureCubeMap, Handle);
        for (int i = 0; i < 6; i++)
        {
            Gl.TexImage2D(
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

        Gl.BindTexture(TextureTarget.TextureCubeMap, Handle);
        fixed (byte* p = rgba)
        {
            Gl.TexImage2D(
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
            Gl.GenerateMipmap(TextureTarget.TextureCubeMap);
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

}
