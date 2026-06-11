// ABOUTME: UDB-style owned world-vertex mesh wrapper around vertex and index buffers.
// ABOUTME: Uploads mesh data, draws indexed triangles, and disposes owned GL buffers.

namespace DBuilder.Rendering;

public enum MeshBufferKind
{
    Vertex,
    Index,
}

public enum MeshDrawStepKind
{
    BindVertexBuffer,
    BindIndexBuffer,
    DrawIndexed,
    UnbindIndexBuffer,
    UnbindVertexBuffer,
}

public enum MeshDisposeStepKind
{
    DisposeVertexBuffer,
    DisposeIndexBuffer,
}

public sealed record MeshConstructionPlan(
    int VertexCount,
    int IndexCount,
    int PrimitiveCount,
    IReadOnlyList<MeshBufferKind> Uploads);

public sealed record MeshDrawStep(MeshDrawStepKind Kind, PrimitiveType? PrimitiveType = null, int StartIndex = 0, int PrimitiveCount = 0);

public sealed record MeshDisposePlan(IReadOnlyList<MeshDisposeStepKind> Steps);

public sealed class Mesh : IDisposable
{
    private bool _disposed;

    public Mesh(RenderDevice graphics, WorldVertex[] vertexData, int[] indexData)
    {
        Vertices = new VertexBuffer(graphics.GL);
        Indices = new IndexBuffer(graphics.GL);
        graphics.SetBufferData(Vertices, vertexData);
        graphics.SetBufferData(Indices, indexData);
        PrimitivesCount = PrimitiveCountFor(indexData.Length);
    }

    public int PrimitivesCount { get; }
    public bool Disposed => _disposed;

    internal VertexBuffer Vertices { get; private set; }
    internal IndexBuffer Indices { get; private set; }

    ~Mesh()
    {
        Dispose();
    }

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
        if (_disposed) return;

        Vertices.Dispose();
        Indices.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public static MeshConstructionPlan BuildConstructionPlan(WorldVertex[] vertexData, int[] indexData)
        => new(
            vertexData.Length,
            indexData.Length,
            PrimitiveCountFor(indexData.Length),
            new[] { MeshBufferKind.Vertex, MeshBufferKind.Index });

    public static IReadOnlyList<MeshDrawStep> BuildDrawPlan(int primitiveCount)
        => new[]
        {
            new MeshDrawStep(MeshDrawStepKind.BindVertexBuffer),
            new MeshDrawStep(MeshDrawStepKind.BindIndexBuffer),
            new MeshDrawStep(MeshDrawStepKind.DrawIndexed, PrimitiveType.TriangleList, StartIndex: 0, PrimitiveCount: primitiveCount),
            new MeshDrawStep(MeshDrawStepKind.UnbindIndexBuffer),
            new MeshDrawStep(MeshDrawStepKind.UnbindVertexBuffer),
        };

    public static MeshDisposePlan BuildDisposePlan()
        => new(new[] { MeshDisposeStepKind.DisposeVertexBuffer, MeshDisposeStepKind.DisposeIndexBuffer });

    public static int PrimitiveCountFor(int indexCount)
        => indexCount / 3;
}
