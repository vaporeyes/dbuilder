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

public sealed record MeshDrawAvailabilityPlan(
    bool IsDisposed,
    int PrimitiveCount,
    bool ShouldDraw);

public sealed record MeshDrawStep(MeshDrawStepKind Kind, PrimitiveType? PrimitiveType = null, int StartIndex = 0, int PrimitiveCount = 0);

public sealed record MeshDisposePlan(IReadOnlyList<MeshDisposeStepKind> Steps);

public sealed class Mesh : IDisposable
{
    private bool _disposed;

    public Mesh(RenderDevice graphics, WorldVertex[] vertexData, int[] indexData)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(vertexData);
        ArgumentNullException.ThrowIfNull(indexData);

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
        if (!BuildDrawAvailabilityPlan(Disposed, PrimitivesCount).ShouldDraw) return;

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
    {
        ArgumentNullException.ThrowIfNull(vertexData);
        ArgumentNullException.ThrowIfNull(indexData);

        return new MeshConstructionPlan(
            vertexData.Length,
            indexData.Length,
            PrimitiveCountFor(indexData.Length),
            new[] { MeshBufferKind.Vertex, MeshBufferKind.Index });
    }

    public static IReadOnlyList<MeshDrawStep> BuildDrawPlan(int primitiveCount)
    {
        if (primitiveCount < 0) throw new ArgumentOutOfRangeException(nameof(primitiveCount));
        if (primitiveCount == 0) return Array.Empty<MeshDrawStep>();

        return new[]
        {
            new MeshDrawStep(MeshDrawStepKind.BindVertexBuffer),
            new MeshDrawStep(MeshDrawStepKind.BindIndexBuffer),
            new MeshDrawStep(MeshDrawStepKind.DrawIndexed, PrimitiveType.TriangleList, StartIndex: 0, PrimitiveCount: primitiveCount),
            new MeshDrawStep(MeshDrawStepKind.UnbindIndexBuffer),
            new MeshDrawStep(MeshDrawStepKind.UnbindVertexBuffer),
        };
    }

    public static MeshDrawAvailabilityPlan BuildDrawAvailabilityPlan(bool isDisposed, int primitiveCount)
    {
        if (primitiveCount < 0) throw new ArgumentOutOfRangeException(nameof(primitiveCount));

        return new MeshDrawAvailabilityPlan(
            isDisposed,
            primitiveCount,
            ShouldDraw: !isDisposed && primitiveCount > 0);
    }

    public static MeshDisposePlan BuildDisposePlan()
        => new(new[] { MeshDisposeStepKind.DisposeVertexBuffer, MeshDisposeStepKind.DisposeIndexBuffer });

    public static int PrimitiveCountFor(int indexCount)
    {
        if (indexCount < 0) throw new ArgumentOutOfRangeException(nameof(indexCount));

        return indexCount / 3;
    }
}
