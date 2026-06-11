// ABOUTME: Builds UDB-style flat textured quads for immediate 2D rendering calls.
// ABOUTME: Preserves FlatQuad vertex order, UV mapping, color assignment, and draw dispatch.

using System.Drawing;

namespace DBuilder.Rendering;

public sealed class FlatQuad
{
    private readonly FlatVertex[] _vertices;
    private readonly PrimitiveType _type;
    private readonly int _numVertices;

    public FlatVertex[] Vertices => _vertices;
    public PrimitiveType Type => _type;

    public FlatQuad(PrimitiveType type, float left, float top, float right, float bottom)
    {
        (_type, _numVertices, _vertices) = Initialize(type);

        switch (type)
        {
            case PrimitiveType.TriangleList:
                SetTriangleListCoordinates(left, top, right, bottom, 0f, 0f, 1f, 1f);
                break;
            case PrimitiveType.TriangleStrip:
                SetTriangleStripCoordinates(left, top, right, bottom, 0f, 0f, 1f, 1f);
                break;
        }
    }

    public FlatQuad(PrimitiveType type, float left, float top, float right, float bottom, float textureWidth, float textureHeight)
    {
        (_type, _numVertices, _vertices) = Initialize(type);

        float twd = 1f / textureWidth;
        float thd = 1f / textureHeight;

        switch (type)
        {
            case PrimitiveType.TriangleList:
                SetTriangleListCoordinates(left, top, right, bottom, twd, thd, 1f - twd, 1f - thd);
                break;
            case PrimitiveType.TriangleStrip:
                SetTriangleStripCoordinates(left, top, right, bottom, twd, thd, 1f - twd, 1f - thd);
                break;
        }
    }

    public FlatQuad(PrimitiveType type, RectangleF position, float textureLeft, float textureTop, float textureRight, float textureBottom)
    {
        (_type, _numVertices, _vertices) = Initialize(type);

        switch (type)
        {
            case PrimitiveType.TriangleList:
                SetTriangleListCoordinates(position.Left, position.Top, position.Right, position.Bottom, textureLeft, textureTop, textureRight, textureBottom);
                break;
            case PrimitiveType.TriangleStrip:
                SetTriangleStripCoordinates(position.Left, position.Top, position.Right, position.Bottom, textureLeft, textureTop, textureRight, textureBottom);
                break;
        }
    }

    public FlatQuad(PrimitiveType type, float left, float top, float right, float bottom, float textureLeft, float textureTop, float textureRight, float textureBottom)
    {
        (_type, _numVertices, _vertices) = Initialize(type);

        switch (type)
        {
            case PrimitiveType.TriangleList:
                SetTriangleListCoordinates(left, top, right, bottom, textureLeft, textureTop, textureRight, textureBottom);
                break;
            case PrimitiveType.TriangleStrip:
                SetTriangleStripCoordinates(left, top, right, bottom, textureLeft, textureTop, textureRight, textureBottom);
                break;
        }
    }

    public void SetColors(int color)
    {
        for (int i = 0; i < _numVertices; i++)
            _vertices[i].c = color;
    }

    public void SetColors(int leftTop, int rightTop, int leftBottom, int rightBottom)
    {
        switch (_type)
        {
            case PrimitiveType.TriangleList:
                _vertices[0].c = leftTop;
                _vertices[1].c = rightTop;
                _vertices[2].c = leftBottom;
                _vertices[3].c = leftBottom;
                _vertices[4].c = rightTop;
                _vertices[5].c = rightBottom;
                break;
            case PrimitiveType.TriangleStrip:
                _vertices[0].c = leftTop;
                _vertices[1].c = rightTop;
                _vertices[2].c = leftBottom;
                _vertices[3].c = rightBottom;
                break;
        }
    }

    public void Render(RenderDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        device.Draw(_type, 0, 2, _vertices);
    }

    private void SetTriangleListCoordinates(float vertexLeft, float vertexTop, float vertexRight, float vertexBottom,
        float textureLeft, float textureTop, float textureRight, float textureBottom)
    {
        _vertices[0].x = vertexLeft;
        _vertices[0].y = vertexTop;
        _vertices[1].x = vertexRight;
        _vertices[1].y = vertexTop;
        _vertices[2].x = vertexLeft;
        _vertices[2].y = vertexBottom;
        _vertices[3].x = vertexLeft;
        _vertices[3].y = vertexBottom;
        _vertices[4].x = vertexRight;
        _vertices[4].y = vertexTop;
        _vertices[5].x = vertexRight;
        _vertices[5].y = vertexBottom;

        _vertices[0].u = textureLeft;
        _vertices[0].v = textureTop;
        _vertices[1].u = textureRight;
        _vertices[1].v = textureTop;
        _vertices[2].u = textureLeft;
        _vertices[2].v = textureBottom;
        _vertices[3].u = textureLeft;
        _vertices[3].v = textureBottom;
        _vertices[4].u = textureRight;
        _vertices[4].v = textureTop;
        _vertices[5].u = textureRight;
        _vertices[5].v = textureBottom;
    }

    private void SetTriangleStripCoordinates(float vertexLeft, float vertexTop, float vertexRight, float vertexBottom,
        float textureLeft, float textureTop, float textureRight, float textureBottom)
    {
        _vertices[0].x = vertexLeft;
        _vertices[0].y = vertexTop;
        _vertices[1].x = vertexRight;
        _vertices[1].y = vertexTop;
        _vertices[2].x = vertexLeft;
        _vertices[2].y = vertexBottom;
        _vertices[3].x = vertexRight;
        _vertices[3].y = vertexBottom;

        _vertices[0].u = textureLeft;
        _vertices[0].v = textureTop;
        _vertices[1].u = textureRight;
        _vertices[1].v = textureTop;
        _vertices[2].u = textureLeft;
        _vertices[2].v = textureBottom;
        _vertices[3].u = textureRight;
        _vertices[3].v = textureBottom;
    }

    private static (PrimitiveType Type, int NumVertices, FlatVertex[] Vertices) Initialize(PrimitiveType type)
    {
        int numVertices = type switch
        {
            PrimitiveType.TriangleList => 6,
            PrimitiveType.TriangleStrip => 4,
            _ => throw new NotSupportedException("Unsupported PrimitiveType"),
        };

        var vertices = new FlatVertex[numVertices];

        for (int i = 0; i < numVertices; i++)
            vertices[i].c = -1;

        return (type, numVertices, vertices);
    }
}
