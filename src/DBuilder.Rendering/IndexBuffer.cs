// ABOUTME: Managed wrapper around a GL element-array buffer object.
// ABOUTME: Mirrors UDB's IndexBuffer; 32-bit indices to match the source renderer.

using Silk.NET.OpenGL;

namespace DBuilder.Rendering;

public sealed class IndexBuffer : IDisposable
{
    private readonly GL _gl;
    internal uint Handle { get; private set; }
    public int IndexCount { get; internal set; }
    public bool Disposed => Handle == 0;

    public IndexBuffer(GL gl)
    {
        _gl = gl;
        Handle = _gl.GenBuffer();
        if (Handle == 0) throw new InvalidOperationException("IndexBuffer allocation failed.");
    }

    public void Dispose()
    {
        if (!Disposed)
        {
            _gl.DeleteBuffer(Handle);
            Handle = 0;
        }
        GC.SuppressFinalize(this);
    }
}
