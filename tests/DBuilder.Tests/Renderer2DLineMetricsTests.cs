// ABOUTME: Verifies UDB Renderer2D line normal and minimum line metric calculations.
// ABOUTME: Pins scale-derived line plotting thresholds against upstream Renderer2D expressions.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class Renderer2DLineMetricsTests
{
    private static string? FindUdbRoot()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "."));
        string sibling = Path.GetFullPath(Path.Combine(repositoryRoot, "..", "UltimateDoomBuilder"));
        if (File.Exists(Path.Combine(sibling, "Source", "Core", "Rendering", "Renderer2D.cs"))) return sibling;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string root = Path.Combine(home, "dev", "repos", "UltimateDoomBuilder");
        return File.Exists(Path.Combine(root, "Source", "Core", "Rendering", "Renderer2D.cs")) ? root : null;
    }

    [Fact]
    public void BuildMatchesUdbScaleViewLineMetrics()
    {
        Renderer2DLineMetrics metrics = Renderer2DLineMetricPlanner.Build(scale: 2.0);

        Assert.Equal(0.5, metrics.ScaleInverse);
        Assert.Equal(5.0, metrics.LineNormalSize);
        Assert.Equal(0.3125, metrics.MinimumLineLength);
        Assert.Equal(10.0, metrics.MinimumLineNormalLength);
    }

    [Fact]
    public void BuildRejectsInvalidScale()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DLineMetricPlanner.Build(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DLineMetricPlanner.Build(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DLineMetricPlanner.Build(double.NaN));
    }

    [Fact]
    public void ShouldPlotLineUsesUdbSquaredLengthThreshold()
    {
        Renderer2DLineMetrics metrics = Renderer2DLineMetricPlanner.Build(scale: 1.0);
        double threshold = metrics.LineNormalSize * Renderer2DLineMetricPlanner.MinimumLineLengthScale;

        Assert.False(Renderer2DLineMetricPlanner.ShouldPlotLine(threshold - 0.001, metrics.LineNormalSize));
        Assert.True(Renderer2DLineMetricPlanner.ShouldPlotLine(threshold, metrics.LineNormalSize));
        Assert.True(Renderer2DLineMetricPlanner.ShouldPlotLine(threshold + 0.001, metrics.LineNormalSize));
    }

    [Fact]
    public void LineMetricExpressionsMatchUdbRenderer2DWhenCloneIsAvailable()
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        string source = File.ReadAllText(Path.Combine(udbRoot, "Source", "Core", "Rendering", "Renderer2D.cs"));

        Assert.Contains("linenormalsize = 10f * scaleinv;", source, StringComparison.Ordinal);
        Assert.Contains("minlinelength = linenormalsize * 0.0625f;", source, StringComparison.Ordinal);
        Assert.Contains("minlinenormallength = linenormalsize * 2f;", source, StringComparison.Ordinal);
        Assert.Contains("if((v2 - v1).GetLengthSq() < linenormalsize * lengthscaler) return;", source, StringComparison.Ordinal);
    }
}
