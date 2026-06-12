// ABOUTME: Plans UDB-style Renderer2D.RenderRectangle border quads and state.
// ABOUTME: Keeps transformed rectangle outline geometry testable outside the live renderer.

namespace DBuilder.Rendering;

public readonly record struct Renderer2DRectangleDrawPlan(
    FlatQuad[] Quads,
    int PrimitiveCountPerQuad,
    Cull CullMode,
    bool DepthEnabled,
    bool AlphaBlendEnabled,
    bool AlphaTestEnabled,
    bool ResetWorldTransformation,
    ShaderName Shader,
    bool BindWhiteTexture,
    bool BindProvidedTexture,
    bool UseClassicBilinear,
    PrimitiveType PrimitiveType);

public static class Renderer2DRectangleDrawPlanner
{
    public static Renderer2DRectangleDrawPlan BuildFilledPlan(
        double left,
        double top,
        double right,
        double bottom,
        int color,
        bool transformRectangle,
        double translateX,
        double translateY,
        double scale)
    {
        if (double.IsNaN(left)) throw new ArgumentOutOfRangeException(nameof(left));
        if (double.IsNaN(top)) throw new ArgumentOutOfRangeException(nameof(top));
        if (double.IsNaN(right)) throw new ArgumentOutOfRangeException(nameof(right));
        if (double.IsNaN(bottom)) throw new ArgumentOutOfRangeException(nameof(bottom));
        if (double.IsNaN(translateX)) throw new ArgumentOutOfRangeException(nameof(translateX));
        if (double.IsNaN(translateY)) throw new ArgumentOutOfRangeException(nameof(translateY));
        if (scale == 0.0 || double.IsNaN(scale)) throw new ArgumentOutOfRangeException(nameof(scale));

        (double ltX, double ltY) = Project(left, top, transformRectangle, translateX, translateY, scale);
        (double rbX, double rbY) = Project(right, bottom, transformRectangle, translateX, translateY, scale);

        return BuildPlan([Quad(ltX, ltY, rbX, rbY, color)], bindWhiteTexture: true, bindProvidedTexture: false);
    }

    public static Renderer2DRectangleDrawPlan BuildTexturedFilledPlan(
        double left,
        double top,
        double right,
        double bottom,
        int color,
        bool transformRectangle,
        double translateX,
        double translateY,
        double scale)
    {
        if (double.IsNaN(left)) throw new ArgumentOutOfRangeException(nameof(left));
        if (double.IsNaN(top)) throw new ArgumentOutOfRangeException(nameof(top));
        if (double.IsNaN(right)) throw new ArgumentOutOfRangeException(nameof(right));
        if (double.IsNaN(bottom)) throw new ArgumentOutOfRangeException(nameof(bottom));
        if (double.IsNaN(translateX)) throw new ArgumentOutOfRangeException(nameof(translateX));
        if (double.IsNaN(translateY)) throw new ArgumentOutOfRangeException(nameof(translateY));
        if (scale == 0.0 || double.IsNaN(scale)) throw new ArgumentOutOfRangeException(nameof(scale));

        (double ltX, double ltY) = Project(left, top, transformRectangle, translateX, translateY, scale);
        (double rbX, double rbY) = Project(right, bottom, transformRectangle, translateX, translateY, scale);

        return BuildPlan([Quad(ltX, ltY, rbX, rbY, color)], bindWhiteTexture: false, bindProvidedTexture: true);
    }

    public static Renderer2DRectangleDrawPlan BuildBorderPlan(
        double left,
        double top,
        double right,
        double bottom,
        double borderSize,
        int color,
        bool transformRectangle,
        double translateX,
        double translateY,
        double scale)
    {
        if (double.IsNaN(left)) throw new ArgumentOutOfRangeException(nameof(left));
        if (double.IsNaN(top)) throw new ArgumentOutOfRangeException(nameof(top));
        if (double.IsNaN(right)) throw new ArgumentOutOfRangeException(nameof(right));
        if (double.IsNaN(bottom)) throw new ArgumentOutOfRangeException(nameof(bottom));
        if (double.IsNaN(borderSize)) throw new ArgumentOutOfRangeException(nameof(borderSize));
        if (double.IsNaN(translateX)) throw new ArgumentOutOfRangeException(nameof(translateX));
        if (double.IsNaN(translateY)) throw new ArgumentOutOfRangeException(nameof(translateY));
        if (scale == 0.0 || double.IsNaN(scale)) throw new ArgumentOutOfRangeException(nameof(scale));

        (double ltX, double ltY) = Project(left, top, transformRectangle, translateX, translateY, scale);
        (double rbX, double rbY) = Project(right, bottom, transformRectangle, translateX, translateY, scale);
        FlatQuad[] quads =
        [
            Quad(ltX, ltY, rbX, ltY - borderSize, color),
            Quad(ltX, rbY + borderSize, rbX, rbY, color),
            Quad(ltX, ltY - borderSize, ltX + borderSize, rbY + borderSize, color),
            Quad(rbX - borderSize, ltY - borderSize, rbX, rbY + borderSize, color),
        ];

        return BuildPlan(quads, bindWhiteTexture: true, bindProvidedTexture: false);
    }

    private static Renderer2DRectangleDrawPlan BuildPlan(
        FlatQuad[] quads,
        bool bindWhiteTexture,
        bool bindProvidedTexture)
    {
        return new Renderer2DRectangleDrawPlan(
            quads,
            PrimitiveCountPerQuad: 2,
            CullMode: Cull.None,
            DepthEnabled: false,
            AlphaBlendEnabled: false,
            AlphaTestEnabled: false,
            ResetWorldTransformation: true,
            Shader: ShaderName.display2d_normal,
            BindWhiteTexture: bindWhiteTexture,
            BindProvidedTexture: bindProvidedTexture,
            UseClassicBilinear: true,
            PrimitiveType: PrimitiveType.TriangleStrip);
    }

    private static (double X, double Y) Project(
        double x,
        double y,
        bool transformRectangle,
        double translateX,
        double translateY,
        double scale)
        => transformRectangle ? ((x + translateX) * scale, (y + translateY) * -scale) : (x, y);

    private static FlatQuad Quad(double left, double top, double right, double bottom, int color)
    {
        var quad = new FlatQuad(
            PrimitiveType.TriangleStrip,
            (float)left,
            (float)top,
            (float)right,
            (float)bottom);
        quad.SetColors(color);
        return quad;
    }
}
