// ABOUTME: UDB-style sector surface entry and update data for buffered 2D floor and ceiling rendering.
// ABOUTME: Keeps surface manager chunk metadata and bounding-box behavior testable before full manager parity.

namespace DBuilder.Rendering;

public sealed record SurfaceBounds(float Left, float Top, float Width, float Height)
{
    public float Right => Left + Width;
    public float Bottom => Top + Height;

    public bool Intersects(SurfaceBounds other)
        => Left < other.Right
        && Right > other.Left
        && Top < other.Bottom
        && Bottom > other.Top;
}

public sealed class SurfaceEntry
{
    public SurfaceEntry(int numVertices, int bufferIndex, int vertexOffset)
    {
        NumVertices = numVertices;
        BufferIndex = bufferIndex;
        VertexOffset = vertexOffset;
    }

    public SurfaceEntry(SurfaceEntry oldEntry)
        : this(oldEntry.NumVertices, oldEntry.BufferIndex, oldEntry.VertexOffset)
    {
    }

    public int NumVertices { get; set; }
    public int BufferIndex { get; set; }
    public int VertexOffset { get; set; }
    public SurfaceBounds Bounds { get; private set; } = new(0, 0, 0, 0);
    public FlatVertex[] FloorVertices { get; set; } = Array.Empty<FlatVertex>();
    public FlatVertex[] CeilingVertices { get; set; } = Array.Empty<FlatVertex>();
    public long FloorTexture { get; set; }
    public long CeilingTexture { get; set; }
    public bool Hidden { get; set; }
    public double Desaturation { get; set; }

    public void UpdateBounds()
    {
        if (FloorVertices.Length == 0)
        {
            Bounds = new SurfaceBounds(0, 0, 0, 0);
            return;
        }

        float left = float.MaxValue;
        float right = float.MinValue;
        float top = float.MaxValue;
        float bottom = float.MinValue;

        foreach (FlatVertex vertex in FloorVertices)
        {
            if (vertex.x < left) left = vertex.x;
            if (vertex.x > right) right = vertex.x;
            if (vertex.y < top) top = vertex.y;
            if (vertex.y > bottom) bottom = vertex.y;
        }

        Bounds = new SurfaceBounds(left, top, right - left, bottom - top);
    }
}

public sealed class SurfaceEntryCollection : List<SurfaceEntry>
{
    public int TotalVertices { get; set; }
}

public sealed class SurfaceUpdate
{
    public SurfaceUpdate(int numVertices, bool updateFloor, bool updateCeiling)
    {
        NumVertices = numVertices;
        FloorVertices = updateFloor ? new FlatVertex[numVertices] : null;
        CeilingVertices = updateCeiling ? new FlatVertex[numVertices] : null;
    }

    public int NumVertices { get; }
    public FlatVertex[]? FloorVertices { get; set; }
    public FlatVertex[]? CeilingVertices { get; set; }
    public long FloorTexture { get; set; }
    public long CeilingTexture { get; set; }
    public bool Hidden { get; set; }
    public double Desaturation { get; set; }
}
