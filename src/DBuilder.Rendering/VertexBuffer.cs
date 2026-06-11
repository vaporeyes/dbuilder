// ABOUTME: Managed wrapper around a GL vertex buffer object.
// ABOUTME: Mirrors UDB's VertexBuffer so call sites that allocate, upload, and dispose port unchanged.

using Silk.NET.OpenGL;

namespace DBuilder.Rendering;

public sealed class VertexBuffer : IDisposable
{
    private readonly GL _gl;
    internal uint Handle { get; private set; }
    public VertexFormat Format { get; internal set; } = VertexFormat.Flat;
    public int VertexCount { get; internal set; }
    public bool Disposed => Handle == 0;

    public VertexBuffer(GL gl)
    {
        _gl = gl;
        Handle = _gl.GenBuffer();
        if (Handle == 0) throw new InvalidOperationException("VertexBuffer allocation failed.");
    }

    ~VertexBuffer()
    {
        Dispose();
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
