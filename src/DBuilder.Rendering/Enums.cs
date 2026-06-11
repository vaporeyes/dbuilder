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

public enum RenderPass : int
{
    Solid = 0,
    Mask = 1,
    Alpha = 2,
    Additive = 3,
}

public enum RenderLayers : int
{
    None = 0,
    Background = 1,
    Plotter = 2,
    Things = 3,
    Overlay = 4,
    Surface = 5,
}

public enum ModelRenderMode
{
    NONE = 0,
    SELECTION = 1,
    ACTIVE_THINGS_FILTER = 2,
    ALL = 3,
}

public enum LightRenderMode
{
    NONE = 0,
    ALL = 1,
    ALL_ANIMATED = 2,
}

public enum ThingRenderMode
{
    NORMAL = 0,
    MODEL = 1,
    VOXEL = 2,
    WALLSPRITE = 3,
    FLATSPRITE = 4,
}

public enum ViewMode : int
{
    Normal = 0,
    Brightness = 1,
    FloorTextures = 2,
    CeilingTextures = 3,
}

public static class ViewModeMetadata
{
    public const int Count = 4;
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

public enum ShaderName : int
{
    display2d_fsaa,
    display2d_normal,
    display2d_fullbright,
    things2d_thing,
    things2d_sprite,
    things2d_fill,
    world3d_main,
    world3d_fullbright,
    world3d_main_highlight,
    world3d_fullbright_highlight,
    world3d_main_vertexcolor,
    world3d_skybox,
    world3d_main_highlight_vertexcolor,
    world3d_p7,
    world3d_main_fog,
    world3d_p9,
    world3d_main_highlight_fog,
    world3d_p11,
    world3d_main_fog_vertexcolor,
    world3d_p13,
    world3d_main_highlight_fog_vertexcolor,
    world3d_vertex_color,
    world3d_constant_color,
    world3d_slope_handle,
    world3d_classic,
    world3d_p19,
    world3d_classic_highlight,
}

public enum UniformType : int
{
    Vec4f, Vec3f, Vec2f, Float,
    Mat4,
    Vec4i, Vec3i, Vec2i, Int,
    Vec4fArray, Vec3fArray, Vec2fArray
}

public enum UniformName : int
{
    rendersettings,
    projection,
    desaturation,
    highlightcolor,
    view,
    world,
    modelnormal,
    FillColor,
    vertexColor,
    stencilColor,
    lightPosAndRadius,
    lightOrientation,
    light2Radius,
    lightColor,
    ignoreNormals,
    spotLight,
    campos,
    fogsettings,
    fogcolor,
    sectorfogcolor,
    lightsEnabled,
    slopeHandleLength,
    drawPaletted,
    colormapSize,
    sectorLightLevel,
    doomlightlevels,
    skew,
    lightStrengthAndLinearity,
    useLightStrength,
}
