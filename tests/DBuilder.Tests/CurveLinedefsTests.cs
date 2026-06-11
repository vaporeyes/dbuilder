// ABOUTME: Covers UDB-style Curve Linedefs point generation and selected-line materialization.
// ABOUTME: Keeps the curve command geometry behavior verified independently of editor UI.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class CurveLinedefsTests
{
    [Fact]
    public void DefaultOptionsMatchUdbCurveLinedefsMode()
    {
        var options = new CurveLinedefsOptions();

        Assert.Equal(8, options.Vertices);
        Assert.Equal(128, options.Distance);
        Assert.Equal(180, options.Angle);
        Assert.False(options.FixedCurve);
        Assert.True(options.FixedCurveOutwards);
    }

    [Fact]
    public void OptionsUseUdbSettingKeysAndPanelLimits()
    {
        var source = new Dictionary<string, object?>
        {
            [CurveLinedefsOptions.VerticesKey] = 999,
            [CurveLinedefsOptions.DistanceKey] = -20000,
            [CurveLinedefsOptions.AngleKey] = 999,
            [CurveLinedefsOptions.FixedCurveKey] = true,
            [CurveLinedefsOptions.FixedCurveOutwardsKey] = false,
        };

        CurveLinedefsOptions options = CurveLinedefsOptions.FromDictionary(source);

        Assert.Equal(CurveLinedefsOptions.MaxVertices, options.Vertices);
        Assert.Equal(CurveLinedefsOptions.MinDistance, options.Distance);
        Assert.Equal(CurveLinedefsOptions.MaxAngle, options.Angle);
        Assert.True(options.FixedCurve);
        Assert.False(options.FixedCurveOutwards);

        var target = new Dictionary<string, object?>();
        options.WriteTo(target);

        Assert.Equal(200, target[CurveLinedefsOptions.VerticesKey]);
        Assert.Equal(-10000, target[CurveLinedefsOptions.DistanceKey]);
        Assert.Equal(350, target[CurveLinedefsOptions.AngleKey]);
        Assert.Equal(true, target[CurveLinedefsOptions.FixedCurveKey]);
        Assert.Equal(false, target[CurveLinedefsOptions.FixedCurveOutwardsKey]);
    }

    [Fact]
    public void PanelResetReturnsUdbCurveLinedefsDefaults()
    {
        CurveLinedefsOptions options = new CurveLinedefsOptions(
            Vertices: 24,
            Distance: -64,
            Angle: 90,
            FixedCurve: true,
            FixedCurveOutwards: false);

        Assert.Equal(new CurveLinedefsOptions(), CurveLinedefsOptions.Defaults());
        Assert.Equal(new CurveLinedefsOptions(), options.Reset());
    }

    [Fact]
    public void PanelFlipNegatesDistanceForNonFixedCurves()
    {
        var options = new CurveLinedefsOptions(
            Distance: 128,
            FixedCurve: false,
            FixedCurveOutwards: false);

        CurveLinedefsOptions flipped = options.Flipped();

        Assert.Equal(-128, flipped.Distance);
        Assert.False(flipped.FixedCurve);
        Assert.False(flipped.FixedCurveOutwards);
    }

    [Fact]
    public void PanelFlipTogglesDirectionForFixedCurves()
    {
        var options = new CurveLinedefsOptions(
            Distance: 128,
            FixedCurve: true,
            FixedCurveOutwards: true);

        CurveLinedefsOptions flipped = options.Flipped();

        Assert.Equal(128, flipped.Distance);
        Assert.True(flipped.FixedCurve);
        Assert.False(flipped.FixedCurveOutwards);
    }

    [Fact]
    public void GenerateCurvePointsClampsVerticesToLineLengthOverFour()
    {
        var map = new MapSet();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(16, 0)));

        IReadOnlyList<Vector2D> points = CurveLinedefs.GenerateCurvePoints(line);

        Assert.Equal(4, points.Count);
    }

    [Fact]
    public void GenerateCurvePointsUsesUdbDefaultBulge()
    {
        var map = new MapSet();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(128, 0)));

        IReadOnlyList<Vector2D> points = CurveLinedefs.GenerateCurvePoints(line);

        Assert.Equal(8, points.Count);
        Assert.All(points, point => Assert.True(point.x > 0 && point.x < 128));
        Assert.All(points, point => Assert.True(point.y < 0));
        Assert.True(Math.Abs(points[3].y) > Math.Abs(points[0].y));
        Assert.True(Math.Abs(points[4].y) > Math.Abs(points[7].y));
    }

    [Fact]
    public void ApplyToSelectedLinedefsSplitsLineThroughGeneratedPoints()
    {
        var map = new MapSet();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(128, 0)));
        line.Selected = true;

        CurveLinedefsResult result = CurveLinedefs.ApplyToSelectedLinedefs(map);

        Assert.Equal(1, result.CurvedLinedefs);
        Assert.Equal(8, result.InsertedVertices);
        Assert.Equal(9, map.Linedefs.Count);
        Assert.Equal(10, map.Vertices.Count);
        Assert.NotEqual(0, line.End.Position.y);
    }

    [Fact]
    public void ApplyUsesPersistedCurveLinedefOptions()
    {
        var map = new MapSet();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(128, 0)));
        line.Selected = true;

        CurveLinedefsResult result = CurveLinedefs.ApplyToSelectedLinedefs(
            map,
            new CurveLinedefsOptions(Vertices: 2, Distance: 32));

        Assert.Equal(1, result.CurvedLinedefs);
        Assert.Equal(2, result.InsertedVertices);
        Assert.Equal(3, map.Linedefs.Count);
    }

    [Fact]
    public void ApplyMarksCurvedGeometryAndClearsOldMarks()
    {
        var map = new MapSet();
        var oldMarkedVertex = map.AddVertex(new Vector2D(-64, 0));
        var oldMarkedLine = map.AddLinedef(map.AddVertex(new Vector2D(-32, 0)), oldMarkedVertex);
        oldMarkedVertex.Marked = true;
        oldMarkedLine.Marked = true;
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(128, 0)));
        line.Selected = true;

        CurveLinedefs.ApplyToSelectedLinedefs(map, new CurveLinedefsOptions(Vertices: 2, Distance: 32));

        Assert.False(oldMarkedVertex.Marked);
        Assert.False(oldMarkedLine.Marked);
        Assert.Equal(3, map.Linedefs.Count(linedef => linedef.Marked));
        Assert.Equal(4, map.Vertices.Count(vertex => vertex.Marked));
        Assert.All(map.Linedefs.Where(linedef => linedef.Marked), linedef =>
        {
            Assert.True(linedef.Start.Marked);
            Assert.True(linedef.End.Marked);
        });
    }

    [Fact]
    public void ApplyCanRunUdbPostProcessingAfterCurveMaterialization()
    {
        var map = new MapSet();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0.0004, 0)), map.AddVertex(new Vector2D(128.0004, 0)));
        line.Selected = true;

        CurveLinedefs.ApplyToSelectedLinedefs(
            map,
            new CurveLinedefsOptions(Vertices: 2, Distance: 37),
            MergeGeometryMode.Classic,
            snapToAccuracy: true);

        Assert.All(map.Vertices, vertex => Assert.Equal(Math.Round(vertex.Position.x, 3), vertex.Position.x));
        Assert.All(map.Vertices, vertex => Assert.Equal(Math.Round(vertex.Position.y, 3), vertex.Position.y));
        Assert.All(map.Vertices, vertex => Assert.NotEmpty(vertex.Linedefs));
    }
}
