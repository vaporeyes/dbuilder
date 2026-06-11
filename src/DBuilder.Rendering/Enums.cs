// ABOUTME: Render device enums mirroring UltimateDoomBuilder's BuilderNative surface.
// ABOUTME: Names and values are preserved so existing UDB call sites can compile against this port.

namespace DBuilder.Rendering;

public enum VertexFormat { Flat, World }
public enum Cull { None, Clockwise }
public enum Blend { InverseSourceAlpha, SourceAlpha, One }
public enum BlendOperation { Add, ReverseSubtract }
public enum FillMode { Solid, Wireframe }
public enum TextureAddress { Wrap, Clamp }
public enum PrimitiveType { LineList, TriangleList, TriangleStrip }
public enum TextureFilter { Nearest, Linear }
public enum MipmapFilter { None, Nearest, Linear }

public enum RenderPass
{
    Solid = 0,
    Mask = 1,
    Alpha = 2,
    Additive = 3,
}

public enum UniformType
{
    Vec4f, Vec3f, Vec2f, Float,
    Mat4,
    Vec4i, Vec3i, Vec2i, Int,
    Vec4fArray, Vec3fArray, Vec2fArray
}
