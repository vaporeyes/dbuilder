// ABOUTME: UDB-style owned world-vertex mesh wrapper around vertex and index buffers.
// ABOUTME: Uploads mesh data, draws indexed triangles, and disposes owned GL buffers.

namespace DBuilder.Rendering;

public sealed class Mesh : IDisposable
{
    public Mesh(RenderDevice graphics, WorldVertex[] vertexData, int[] indexData)
    {
        Vertices = new VertexBuffer(graphics.GL);
        Indices = new IndexBuffer(graphics.GL);
        graphics.SetBufferData(Vertices, vertexData);
        graphics.SetBufferData(Indices, indexData);
        PrimitivesCount = indexData.Length / 3;
    }

    public int PrimitivesCount { get; }

    internal VertexBuffer Vertices { get; private set; }
    internal IndexBuffer Indices { get; private set; }

    public void Draw(RenderDevice device)
    {
        device.SetVertexBuffer(Vertices);
        device.SetIndexBuffer(Indices);
        device.DrawIndexed(PrimitiveType.TriangleList, 0, PrimitivesCount);
        device.SetIndexBuffer(null);
        device.SetVertexBuffer(null);
    }

    public void Dispose()
    {
        Vertices.Dispose();
        Indices.Dispose();
        GC.SuppressFinalize(this);
    }
}
