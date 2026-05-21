// ABOUTME: Vertex layouts matching UDB's FlatVertex and WorldVertex.
// ABOUTME: Stride values are kept identical so buffer-sizing code ports without change.

using System.Runtime.InteropServices;

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
}
