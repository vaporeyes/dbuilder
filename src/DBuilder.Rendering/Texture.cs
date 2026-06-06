// ABOUTME: Managed 2D texture wrapper over a GL texture object.
// ABOUTME: Subset of UDB GLTexture: RGBA8 upload, mipmap generation, dispose; no FBO/PBO/cube paths yet.

using Silk.NET.OpenGL;

namespace DBuilder.Rendering;

public sealed class Texture : IDisposable
{
    private readonly GL _gl;
    internal uint Handle { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool Disposed => Handle == 0;

    public Texture(GL gl)
    {
        _gl = gl;
        Handle = _gl.GenTexture();
        if (Handle == 0) throw new InvalidOperationException("Texture allocation failed.");
    }

    public unsafe void SetPixelsRgba8(int width, int height, ReadOnlySpan<byte> rgba, bool generateMipmaps = true)
    {
        if (rgba.Length < width * height * 4)
            throw new ArgumentException("Pixel buffer too small for declared dimensions", nameof(rgba));

        Width = width;
        Height = height;

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
