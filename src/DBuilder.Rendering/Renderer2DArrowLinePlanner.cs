// ABOUTME: Plans UDB-style Renderer2D.RenderArrows line-list vertices and state.
// ABOUTME: Keeps event/link arrow projection and arrowhead geometry testable.

namespace DBuilder.Rendering;

public readonly record struct Renderer2DArrowLine(
    double StartX,
    double StartY,
    double EndX,
    double EndY,
    int Color,
    bool RenderArrowhead = true);

public readonly record struct Renderer2DArrowLineDrawPlan(
    FlatVertex[] Vertices,
    int PointCount,
    int PrimitiveCount,
    Cull CullMode,
    bool DepthEnabled,
    bool AlphaBlendEnabled,
    bool AlphaTestEnabled,
    bool ResetWorldTransformation,
    ShaderName Shader,
    bool BindWhiteTexture,
    PrimitiveType PrimitiveType);

public static class Renderer2DArrowLinePlanner
{
    public const float ArrowheadHalfAngle = 0.46f;
    public const float ArrowheadScreenLength = 16.0f;

    public static Renderer2DArrowLineDrawPlan BuildDrawPlan(
        IReadOnlyList<Renderer2DArrowLine> lines,
        bool transformCoordinates,
        double translateX,
        double translateY,
        double scale,
        double windowWidth,
        double windowHeight)
    {
        if (lines == null) throw new ArgumentNullException(nameof(lines));
        if (double.IsNaN(translateX)) throw new ArgumentOutOfRangeException(nameof(translateX));
        if (double.IsNaN(translateY)) throw new ArgumentOutOfRangeException(nameof(translateY));
        if (scale == 0.0 || double.IsNaN(scale)) throw new ArgumentOutOfRangeException(nameof(scale));
        if (windowWidth < 0 || double.IsNaN(windowWidth)) throw new ArgumentOutOfRangeException(nameof(windowWidth));
        if (windowHeight < 0 || double.IsNaN(windowHeight)) throw new ArgumentOutOfRangeException(nameof(windowHeight));

        var vertices = new List<FlatVertex>(lines.Count * 6);
        foreach (Renderer2DArrowLine line in lines)
        {
            (double startX, double startY) = Project(line.StartX, line.StartY, transformCoordinates, translateX, translateY, scale);
            (double endX, double endY) = Project(line.EndX, line.EndY, transformCoordinates, translateX, translateY, scale);
            if (!IsVisible(startX, startY, endX, endY, windowWidth, windowHeight)) continue;

            FlatVertex endVertex = Vertex(endX, endY, line.Color);
            vertices.Add(Vertex(startX, startY, line.Color));
            vertices.Add(endVertex);

            if (!line.RenderArrowhead) continue;

            double angle = LineAngle(line.StartX, line.StartY, line.EndX, line.EndY);
            double scaler = ArrowheadScreenLength / scale;
            (double arrow1X, double arrow1Y) = Transform(
                line.EndX - scaler * Math.Sin(angle - ArrowheadHalfAngle),
                line.EndY + scaler * Math.Cos(angle - ArrowheadHalfAngle),
                translateX,
                translateY,
                scale);
            (double arrow2X, double arrow2Y) = Transform(
                line.EndX - scaler * Math.Sin(angle + ArrowheadHalfAngle),
                line.EndY + scaler * Math.Cos(angle + ArrowheadHalfAngle),
                translateX,
                translateY,
                scale);

            vertices.Add(endVertex);
            vertices.Add(Vertex(arrow1X, arrow1Y, line.Color));
            vertices.Add(endVertex);
            vertices.Add(Vertex(arrow2X, arrow2Y, line.Color));
        }

        int pointCount = vertices.Count;
        return new Renderer2DArrowLineDrawPlan(
            vertices.ToArray(),
            pointCount,
            pointCount / 2,
            CullMode: Cull.None,
            DepthEnabled: false,
            AlphaBlendEnabled: false,
            AlphaTestEnabled: false,
            ResetWorldTransformation: true,
            Shader: ShaderName.display2d_normal,
            BindWhiteTexture: true,
            PrimitiveType: PrimitiveType.LineList);
    }

    private static (double X, double Y) Project(
        double x,
        double y,
        bool transformCoordinates,
        double translateX,
        double translateY,
        double scale)
        => transformCoordinates ? Transform(x, y, translateX, translateY, scale) : (x, y);

    private static (double X, double Y) Transform(double x, double y, double translateX, double translateY, double scale)
        => ((x + translateX) * scale, (y + translateY) * -scale);

    private static bool IsVisible(
        double startX,
        double startY,
        double endX,
        double endY,
        double windowWidth,
        double windowHeight)
    {
        double maxX = Math.Max(startX, endX);
        double minX = Math.Min(startX, endX);
        double maxY = Math.Max(startY, endY);
        double minY = Math.Min(startY, endY);
        double dx = endX - startX;
        double dy = endY - startY;
        double lengthSquared = dx * dx + dy * dy;

        return lengthSquared >= ThingBatchRenderPlanner.MinimumSpriteRadius
            && maxX > 0.0
            && minX < windowWidth
            && maxY > 0.0
            && minY < windowHeight;
    }

    private static double LineAngle(double startX, double startY, double endX, double endY)
        => -Math.Atan2(-(endY - startY), endX - startX) + Math.PI * 0.5;

    private static FlatVertex Vertex(double x, double y, int color)
        => new()
        {
            x = (float)x,
            y = (float)y,
            c = color,
        };
}
