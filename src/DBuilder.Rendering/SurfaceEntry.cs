// ABOUTME: UDB-style sector surface entry and update data for buffered 2D floor and ceiling rendering.
// ABOUTME: Keeps surface manager chunk metadata and bounding-box behavior testable before full manager parity.

using System.Drawing;

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

    public int numvertices
    {
        get => NumVertices;
        set => NumVertices = value;
    }

    public int bufferindex
    {
        get => BufferIndex;
        set => BufferIndex = value;
    }

    public RectangleF bbox
    {
        get => new(Bounds.Left, Bounds.Top, Bounds.Width, Bounds.Height);
        set => Bounds = new SurfaceBounds(value.Left, value.Top, value.Width, value.Height);
    }

    public int vertexoffset
    {
        get => VertexOffset;
        set => VertexOffset = value;
    }

    public FlatVertex[] floorvertices
    {
        get => FloorVertices;
        set => FloorVertices = value;
    }

    public FlatVertex[] ceilvertices
    {
        get => CeilingVertices;
        set => CeilingVertices = value;
    }

    public long floortexture
    {
        get => FloorTexture;
        set => FloorTexture = value;
    }

    public long ceiltexture
    {
        get => CeilingTexture;
        set => CeilingTexture = value;
    }

    public bool hidden
    {
        get => Hidden;
        set => Hidden = value;
    }

    public double desaturation
    {
        get => Desaturation;
        set => Desaturation = value;
    }

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

    public void UpdateBBox() => UpdateBounds();
}

public sealed class SurfaceEntryCollection : List<SurfaceEntry>
{
    public int totalvertices;

    public int TotalVertices
    {
        get => totalvertices;
        set => totalvertices = value;
    }

    public bool ApplyUpdate(SurfaceUpdate update)
    {
        bool reallocated = Count > 0 && TotalVertices != update.NumVertices;
        if (reallocated)
            Clear();

        if (Count == 0 && update.NumVertices > 0)
        {
            if (update.FloorVertices == null || update.CeilingVertices == null)
                throw new InvalidOperationException("Surface creation requires floor and ceiling vertices.");

            int offset = 0;
            foreach (int chunk in SurfaceManagerPlan.SplitSectorVertexCount(update.NumVertices))
            {
                var entry = new SurfaceEntry(chunk, bufferIndex: -1, vertexOffset: -1)
                {
                    FloorVertices = CopyRange(update.FloorVertices, offset, chunk),
                    CeilingVertices = CopyRange(update.CeilingVertices, offset, chunk),
                    FloorTexture = update.FloorTexture,
                    CeilingTexture = update.CeilingTexture,
                    Hidden = update.Hidden,
                    Desaturation = update.Desaturation,
                };
                entry.UpdateBounds();
                Add(entry);
                offset += chunk;
            }
        }
        else
        {
            int offset = 0;
            foreach (SurfaceEntry entry in this)
            {
                if (update.FloorVertices != null)
                {
                    Array.Copy(update.FloorVertices, offset, entry.FloorVertices, 0, entry.NumVertices);
                    entry.FloorTexture = update.FloorTexture;
                }

                if (update.CeilingVertices != null)
                {
                    Array.Copy(update.CeilingVertices, offset, entry.CeilingVertices, 0, entry.NumVertices);
                    entry.CeilingTexture = update.CeilingTexture;
                }

                entry.Hidden = update.Hidden;
                entry.Desaturation = update.Desaturation;
                entry.UpdateBounds();
                offset += entry.NumVertices;
            }
        }

        TotalVertices = update.NumVertices;
        return reallocated;
    }

    private static FlatVertex[] CopyRange(FlatVertex[] vertices, int offset, int count)
    {
        var copy = new FlatVertex[count];
        Array.Copy(vertices, offset, copy, 0, count);
        return copy;
    }
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

    public int numvertices => NumVertices;

    public FlatVertex[]? floorvertices
    {
        get => FloorVertices;
        set => FloorVertices = value;
    }

    public FlatVertex[]? ceilvertices
    {
        get => CeilingVertices;
        set => CeilingVertices = value;
    }

    public long floortexture
    {
        get => FloorTexture;
        set => FloorTexture = value;
    }

    public long ceiltexture
    {
        get => CeilingTexture;
        set => CeilingTexture = value;
    }

    public bool hidden
    {
        get => Hidden;
        set => Hidden = value;
    }

    public double desaturation
    {
        get => Desaturation;
        set => Desaturation = value;
    }
}
