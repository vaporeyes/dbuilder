// ABOUTME: Render device enums mirroring UltimateDoomBuilder's BuilderNative surface.
// ABOUTME: Names and values are preserved so existing UDB call sites can compile against this port.

namespace DBuilder.Rendering;

public enum VertexFormat : int { Flat = 0, World = 1 }
public enum Cull : int { None = 0, Clockwise = 1 }
public enum Blend : int { InverseSourceAlpha = 0, SourceAlpha = 1, One = 2 }
public enum BlendOperation : int { Add = 0, ReverseSubtract = 1 }
public enum FillMode : int { Solid = 0, Wireframe = 1 }
public enum TextureAddress : int { Wrap = 0, Clamp = 1 }
public enum PrimitiveType : int { LineList = 0, TriangleList = 1, TriangleStrip = 2 }
public enum TextureFilter : int { Nearest = 0, Linear = 1 }
public enum MipmapFilter : int { None = 0, Nearest = 1, Linear = 2 }

public enum RenderPass
{
    Solid = 0,
    Mask = 1,
    Alpha = 2,
    Additive = 3,
}

public enum TextAlignmentX : int
{
    Left = 0,
    Center = 1,
    Right = 2,
}

public enum TextAlignmentY : int
{
    Top = 0,
    Middle = 1,
    Bottom = 2,
}

public readonly struct CommentType
{
    private const string Regular = "";
    private const string Info = "[i]";
    private const string Question = "[?]";
    private const string Problem = "[!]";
    private const string Smile = "[:]";

    public static readonly string[] Types = [Regular, Info, Question, Problem, Smile];
}

public enum UniformType
{
    Vec4f, Vec3f, Vec2f, Float,
    Mat4,
    Vec4i, Vec3i, Vec2i, Int,
    Vec4fArray, Vec3fArray, Vec2fArray
}
