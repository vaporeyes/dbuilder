// ABOUTME: Models UDB Renderer2D scale-derived line plotting metrics.
// ABOUTME: Keeps 2D line normal and minimum line thresholds testable outside the live renderer.

namespace DBuilder.Rendering;

public readonly record struct Renderer2DLineMetrics(
    double ScaleInverse,
    double LineNormalSize,
    double MinimumLineLength,
    double MinimumLineNormalLength);

public static class Renderer2DLineMetricPlanner
{
    public const double LineNormalScreenSize = 10.0;
    public const double MinimumLineLengthScale = 0.0625;
    public const double MinimumLineNormalLengthScale = 2.0;

    public static Renderer2DLineMetrics Build(double scale)
    {
        if (scale <= 0 || double.IsNaN(scale)) throw new ArgumentOutOfRangeException(nameof(scale));

        double scaleInverse = 1.0 / scale;
        double lineNormalSize = LineNormalScreenSize * scaleInverse;
        return new Renderer2DLineMetrics(
            scaleInverse,
            lineNormalSize,
            lineNormalSize * MinimumLineLengthScale,
            lineNormalSize * MinimumLineNormalLengthScale);
    }

    public static bool ShouldPlotLine(double screenLengthSquared, double lineNormalSize, double lengthScaler = MinimumLineLengthScale)
        => screenLengthSquared >= lineNormalSize * lengthScaler;
}
