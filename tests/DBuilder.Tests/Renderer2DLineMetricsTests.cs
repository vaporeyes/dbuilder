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
    public void BuildPlotLinePlanMatchesUdbTransformedPlotterCoordinates()
    {
        PixelColor color = PixelColor.FromArgb(unchecked((int)0xff123456));

        Renderer2DPlotLinePlan plan = Renderer2DLineMetricPlanner.BuildPlotLinePlan(
            new Vector2D(0, 0),
            new Vector2D(10, 0),
            color,
            translateX: 1,
            translateY: 2,
            scale: 2,
            viewportHeight: 100);

        Assert.True(plan.ShouldDraw);
        Assert.Equal(2, plan.StartX);
        Assert.Equal(104, plan.StartY);
        Assert.Equal(22, plan.EndX);
        Assert.Equal(104, plan.EndY);
        Assert.Equal(color, plan.Color);
    }

    [Fact]
    public void BuildPlotLinePlanSuppressesShortLinesLikeUdb()
    {
        Renderer2DPlotLinePlan plan = Renderer2DLineMetricPlanner.BuildPlotLinePlan(
            new Vector2D(0, 0),
            new Vector2D(0.1, 0),
            PixelColor.FromArgb(unchecked((int)0xff123456)),
            translateX: 0,
            translateY: 0,
            scale: 1,
            viewportHeight: 100);

        Assert.False(plan.ShouldDraw);
        Assert.Equal(0, plan.StartX);
        Assert.Equal(0, plan.StartY);
        Assert.Equal(0, plan.EndX);
        Assert.Equal(0, plan.EndY);
    }

    [Fact]
    public void BuildPlotLinePlanHonorsUdbLengthScaler()
    {
        Renderer2DPlotLinePlan defaultScaler = Renderer2DLineMetricPlanner.BuildPlotLinePlan(
            new Vector2D(0, 0),
            new Vector2D(1, 0),
            PixelColor.FromArgb(unchecked((int)0xff123456)),
            translateX: 0,
            translateY: 0,
            scale: 1,
            viewportHeight: 100);
        Renderer2DPlotLinePlan largerScaler = Renderer2DLineMetricPlanner.BuildPlotLinePlan(
            new Vector2D(0, 0),
            new Vector2D(1, 0),
            PixelColor.FromArgb(unchecked((int)0xff123456)),
            translateX: 0,
            translateY: 0,
            scale: 1,
            viewportHeight: 100,
            lengthScaler: 2);

        Assert.True(defaultScaler.ShouldDraw);
        Assert.False(largerScaler.ShouldDraw);
    }

    [Fact]
    public void BuildPlotLinePlanRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DLineMetricPlanner.BuildPlotLinePlan(
            new Vector2D(0, 0),
            new Vector2D(1, 0),
            PixelColor.FromArgb(unchecked((int)0xff123456)),
            translateX: 0,
            translateY: 0,
            scale: 1,
            viewportHeight: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DLineMetricPlanner.BuildPlotLinePlan(
            new Vector2D(0, 0),
            new Vector2D(1, 0),
            PixelColor.FromArgb(unchecked((int)0xff123456)),
            translateX: 0,
            translateY: 0,
            scale: 1,
            viewportHeight: 100,
            lengthScaler: double.NaN));
    }

    [Theory]
    [InlineData(0.1, 1.0, 0)]
    [InlineData(1.0, 1.0, 2)]
    [InlineData(1.0, 2.0, 3)]
    [InlineData(10.0, 1.0, 4)]
    [InlineData(1.0, -1.0, 0)]
    public void BuildVertexSizeMatchesUdbScaleViewFormula(double scale, double vertexScale2D, int expected)
    {
        Assert.Equal(expected, Renderer2DLineMetricPlanner.BuildVertexSize(scale, vertexScale2D));
    }

    [Fact]
    public void BuildPlotVertexPlanMatchesUdbTransformedPlotterCoordinates()
    {
        Renderer2DPlotVertexPlan plan = Renderer2DLineMetricPlanner.BuildPlotVertexPlan(
            new Vector2D(3, 4),
            colorIndex: 7,
            checkMode: true,
            shouldRenderVertices: true,
            translateX: 1,
            translateY: 2,
            scale: 2,
            viewportHeight: 100,
            vertexScale2D: 1);

        Assert.True(plan.ShouldDraw);
        Assert.Equal(8, plan.X);
        Assert.Equal(112, plan.Y);
        Assert.Equal(3, plan.Size);
        Assert.Equal(7, plan.ColorIndex);
    }

    [Fact]
    public void BuildPlotVertexPlanUsesUdbCheckModeGate()
    {
        Renderer2DPlotVertexPlan gated = Renderer2DLineMetricPlanner.BuildPlotVertexPlan(
            new Vector2D(3, 4),
            colorIndex: 7,
            checkMode: true,
            shouldRenderVertices: false,
            translateX: 1,
            translateY: 2,
            scale: 2,
            viewportHeight: 100,
            vertexScale2D: 1);
        Renderer2DPlotVertexPlan forced = Renderer2DLineMetricPlanner.BuildPlotVertexPlan(
            new Vector2D(3, 4),
            colorIndex: 7,
            checkMode: false,
            shouldRenderVertices: false,
            translateX: 1,
            translateY: 2,
            scale: 2,
            viewportHeight: 100,
            vertexScale2D: 1);

        Assert.False(gated.ShouldDraw);
        Assert.Equal(3, gated.Size);
        Assert.True(forced.ShouldDraw);
        Assert.Equal(8, forced.X);
        Assert.Equal(112, forced.Y);
    }

    [Fact]
    public void BuildVertexSizeRejectsInvalidScaleInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DLineMetricPlanner.BuildVertexSize(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DLineMetricPlanner.BuildVertexSize(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DLineMetricPlanner.BuildVertexSize(double.NaN, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DLineMetricPlanner.BuildVertexSize(1, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renderer2DLineMetricPlanner.BuildPlotVertexPlan(
            new Vector2D(0, 0),
            colorIndex: 0,
            checkMode: true,
            shouldRenderVertices: true,
            translateX: 0,
            translateY: 0,
            scale: 1,
            viewportHeight: -1,
            vertexScale2D: 1));
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
        Assert.Contains("vertexsize = (int)(1.7f * General.Settings.GZVertexScale2D * scale + 0.5f);", source, StringComparison.Ordinal);
        Assert.Contains("if(vertexsize < 0) vertexsize = 0;", source, StringComparison.Ordinal);
        Assert.Contains("if(vertexsize > 4) vertexsize = 4;", source, StringComparison.Ordinal);
        Assert.Contains("public void PlotVertex(Vertex v, int colorindex, bool checkMode = true)", source, StringComparison.Ordinal);
        Assert.Contains("public void PlotVertexAt(Vector2D v, int colorindex, bool checkMode = true)", source, StringComparison.Ordinal);
        Assert.Contains("public void PlotVerticesSet(ICollection<Vertex> vertices, bool checkMode = true)", source, StringComparison.Ordinal);
        Assert.Contains("if (checkMode && !ShouldRenderVertices)", source, StringComparison.Ordinal);
        Assert.Contains("plotter.DrawVertexSolid((int)nv.x, TransformY((int)nv.y), vertexsize, ref General.Colors.Colors[colorindex], ref General.Colors.BrightColors[colorindex], ref General.Colors.DarkColors[colorindex]);", source, StringComparison.Ordinal);
        Assert.Contains("public void PlotLine(Vector2D start, Vector2D end, PixelColor c, float lengthscaler)", source, StringComparison.Ordinal);
        Assert.Contains("if((v2 - v1).GetLengthSq() < linenormalsize * lengthscaler) return;", source, StringComparison.Ordinal);
        Assert.Contains("plotter.DrawLineSolid((int)v1.x, TransformY((int)v1.y), (int)v2.x, TransformY((int)v2.y), ref c);", source, StringComparison.Ordinal);
        Assert.Contains("if(lengthsq < minlinelength) return;", source, StringComparison.Ordinal);
        Assert.Contains("if(lengthsq < minlinenormallength) return;", source, StringComparison.Ordinal);
        Assert.Contains("(int)((v1.x + mx) - (my * l.LengthInv) * linenormalsize)", source, StringComparison.Ordinal);
        Assert.Contains("TransformY((int)((v1.y + my) + (mx * l.LengthInv) * linenormalsize))", source, StringComparison.Ordinal);
    }
}
