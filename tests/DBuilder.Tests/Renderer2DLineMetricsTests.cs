// ABOUTME: Verifies UDB Renderer2D line normal and minimum line metric calculations.
// ABOUTME: Pins scale-derived line plotting thresholds against upstream Renderer2D expressions.

using DBuilder.Rendering;
using DBuilder.Geometry;

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
    public void BuildLinedefSegmentsMatchesUdbMainLineAndNormalIndicator()
    {
        IReadOnlyList<Renderer2DLinedefSegment> segments = Renderer2DLineMetricPlanner.BuildLinedefSegments(
            new Vector2D(0, 0),
            new Vector2D(10, 0),
            translateX: 1,
            translateY: 2,
            scale: 2,
            viewportHeight: 100);

        Assert.Equal(new[]
        {
            new Renderer2DLinedefSegment(Renderer2DLinedefSegmentKind.Main, 2, 104, 22, 104),
            new Renderer2DLinedefSegment(Renderer2DLinedefSegmentKind.NormalIndicator, 12, 104, 12, 99),
        }, segments);
    }

    [Fact]
    public void BuildLinedefSegmentsSuppressesShortLinesAndShortNormalsLikeUdb()
    {
        Assert.Empty(Renderer2DLineMetricPlanner.BuildLinedefSegments(
            new Vector2D(0, 0),
            new Vector2D(0.1, 0),
            translateX: 0,
            translateY: 0,
            scale: 1,
            viewportHeight: 100));

        IReadOnlyList<Renderer2DLinedefSegment> lineOnly = Renderer2DLineMetricPlanner.BuildLinedefSegments(
            new Vector2D(0, 0),
            new Vector2D(1, 0),
            translateX: 0,
            translateY: 0,
            scale: 1,
            viewportHeight: 100);

        Renderer2DLinedefSegment segment = Assert.Single(lineOnly);
        Assert.Equal(Renderer2DLinedefSegmentKind.Main, segment.Kind);
    }

    [Fact]
    public void BuildLinedefSegmentsMarksExtraFloorsWhenRequested()
    {
        Renderer2DLinedefSegment segment = Assert.Single(Renderer2DLineMetricPlanner.BuildLinedefSegments(
            new Vector2D(0, 0),
            new Vector2D(1, 0),
            translateX: 0,
            translateY: 0,
            scale: 1,
            viewportHeight: 100,
            extraFloor: true,
            markExtraFloors: true));

        Assert.Equal(Renderer2DLinedefSegmentKind.ThreeDFloor, segment.Kind);
    }

    [Fact]
    public void BuildLinedefSegmentsRejectsInvalidViewport()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DLineMetricPlanner.BuildLinedefSegments(
            new Vector2D(0, 0),
            new Vector2D(1, 0),
            translateX: 0,
            translateY: 0,
            scale: 1,
            viewportHeight: -1));
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
        Assert.Contains("if(lengthsq < minlinelength) return;", source, StringComparison.Ordinal);
        Assert.Contains("if(lengthsq < minlinenormallength) return;", source, StringComparison.Ordinal);
        Assert.Contains("(int)((v1.x + mx) - (my * l.LengthInv) * linenormalsize)", source, StringComparison.Ordinal);
        Assert.Contains("TransformY((int)((v1.y + my) + (mx * l.LengthInv) * linenormalsize))", source, StringComparison.Ordinal);
    }
}
