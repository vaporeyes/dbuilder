// ABOUTME: Vertex layouts matching UDB's FlatVertex and WorldVertex.
// ABOUTME: Stride values are kept identical so buffer-sizing code ports without change.

using System.Runtime.InteropServices;
using DBuilder.Geometry;

namespace DBuilder.Rendering;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FlatVertex
{
    public float x;
    public float y;
    public float z;
    public float w;
    public int c;     // ARGB packed
    public float u;
    public float v;

    public const int Stride = 7 * 4;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct WorldVertex
{
    public float x;
    public float y;
    public float z;
    public int c;
    public float u;
    public float v;
    public float nx;
    public float ny;
    public float nz;

    public const int Stride = 9 * 4;

    public WorldVertex(float x, float y, float z, int c, float u, float v)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.c = c;
        this.u = u;
        this.v = v;
        nx = 0.0f;
        ny = 0.0f;
        nz = 0.0f;
    }

    public WorldVertex(Vector3D position, int c, Vector2D texture)
    {
        x = (float)position.x;
        y = (float)position.y;
        z = (float)position.z;
        this.c = c;
        u = (float)texture.x;
        v = (float)texture.y;
        nx = 0.0f;
        ny = 0.0f;
        nz = 0.0f;
    }

    public WorldVertex(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        c = -1;
        u = 0.0f;
        v = 0.0f;
        nx = 0.0f;
        ny = 0.0f;
        nz = 0.0f;
    }

    public WorldVertex(Vector3D position)
    {
        x = (float)position.x;
        y = (float)position.y;
        z = (float)position.z;
        c = -1;
        u = 0.0f;
        v = 0.0f;
        nx = 0.0f;
        ny = 0.0f;
        nz = 0.0f;
    }
}
