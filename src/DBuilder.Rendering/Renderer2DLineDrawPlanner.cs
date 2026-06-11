// ABOUTME: Plans UDB-style Renderer2D.RenderLine thick-line vertices and state.
// ABOUTME: Keeps transformed triangle-strip line geometry testable outside the live renderer.

namespace DBuilder.Rendering;

public readonly record struct Renderer2DLineDrawPlan(
    FlatVertex[] Vertices,
    int PrimitiveCount,
    Cull CullMode,
    bool DepthEnabled,
    bool AlphaBlendEnabled,
    bool AlphaTestEnabled,
    bool ResetWorldTransformation,
    ShaderName Shader,
    bool BindWhiteTexture,
    PrimitiveType PrimitiveType);

public static class Renderer2DLineDrawPlanner
{
    public static Renderer2DLineDrawPlan BuildDrawPlan(
        double startX,
        double startY,
        double endX,
        double endY,
        double thickness,
        int color,
        bool transformCoordinates,
        double translateX,
        double translateY,
        double scale)
    {
        if (double.IsNaN(startX)) throw new ArgumentOutOfRangeException(nameof(startX));
        if (double.IsNaN(startY)) throw new ArgumentOutOfRangeException(nameof(startY));
        if (double.IsNaN(endX)) throw new ArgumentOutOfRangeException(nameof(endX));
        if (double.IsNaN(endY)) throw new ArgumentOutOfRangeException(nameof(endY));
        if (thickness < 0 || double.IsNaN(thickness)) throw new ArgumentOutOfRangeException(nameof(thickness));
        if (double.IsNaN(translateX)) throw new ArgumentOutOfRangeException(nameof(translateX));
        if (double.IsNaN(translateY)) throw new ArgumentOutOfRangeException(nameof(translateY));
        if (scale == 0.0 || double.IsNaN(scale)) throw new ArgumentOutOfRangeException(nameof(scale));

        (double sx, double sy) = Project(startX, startY, transformCoordinates, translateX, translateY, scale);
        (double ex, double ey) = Project(endX, endY, transformCoordinates, translateX, translateY, scale);
        double dx = ex - sx;
        double dy = ey - sy;
        double lengthSquared = dx * dx + dy * dy;
        double normalScale = lengthSquared > 0.0 ? thickness / Math.Sqrt(lengthSquared) : 0.0;
        double nx = dx * normalScale;
        double ny = dy * normalScale;

        FlatVertex[] vertices =
        [
            Vertex(sx - nx + ny, sy - ny - nx, color),
            Vertex(sx - nx - ny, sy - ny + nx, color),
            Vertex(ex + nx + ny, ey + ny - nx, color),
            Vertex(ex + nx - ny, ey + ny + nx, color),
        ];

        return new Renderer2DLineDrawPlan(
            vertices,
            PrimitiveCount: 2,
            CullMode: Cull.None,
            DepthEnabled: false,
            AlphaBlendEnabled: false,
            AlphaTestEnabled: false,
            ResetWorldTransformation: true,
            Shader: ShaderName.display2d_normal,
            BindWhiteTexture: true,
            PrimitiveType: PrimitiveType.TriangleStrip);
    }

    private static (double X, double Y) Project(
        double x,
        double y,
        bool transformCoordinates,
        double translateX,
        double translateY,
        double scale)
        => transformCoordinates ? ((x + translateX) * scale, (y + translateY) * -scale) : (x, y);

    private static FlatVertex Vertex(double x, double y, int color)
        => new()
        {
            x = (float)x,
            y = (float)y,
            z = 0.0f,
            c = color,
        };
}
