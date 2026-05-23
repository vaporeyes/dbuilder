// ABOUTME: Tests for MapAnalysis - the map health checker detecting geometry/structure issues.
// ABOUTME: Verifies a clean sector reports nothing and each defect surfaces its specific issue kind.

using System.Linq;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class MapAnalysisTests
{
    // Builds a square; when closed, all four sides have a front sidedef into one sector.
    private static MapSet Square(bool closed)
    {
        var map = new MapSet();
        var s = map.AddSector();
        var v = new[]
        {
            map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(0, 100)),
            map.AddVertex(new Vector2D(100, 100)), map.AddVertex(new Vector2D(100, 0)),
        };
        int sides = closed ? 4 : 3;
        for (int i = 0; i < sides; i++)
        {
            var l = map.AddLinedef(v[i], v[(i + 1) % 4]);
            map.AddSidedef(l, true, s);
        }
        map.BuildIndexes();
        return map;
    }

    private static bool Has(MapSet map, MapIssueKind kind)
        => MapAnalysis.Check(map).Any(i => i.Kind == kind);

    [Fact]
    public void CleanSquareHasNoIssues()
    {
        var issues = MapAnalysis.Check(Square(true));
        Assert.Empty(issues);
    }

    [Fact]
    public void DetectsUnclosedSector()
    {
        Assert.True(Has(Square(false), MapIssueKind.UnclosedSector));
    }

    [Fact]
    public void DetectsZeroLengthLinedef()
    {
        var map = Square(true);
        var p = map.AddVertex(new Vector2D(500, 500));
        var q = map.AddVertex(new Vector2D(500, 500));
        map.AddLinedef(p, q);
        map.BuildIndexes();
        Assert.True(Has(map, MapIssueKind.ZeroLengthLinedef));
    }

    [Fact]
    public void DetectsLinedefWithoutSidedefs()
    {
        var map = Square(true);
        var a = map.AddVertex(new Vector2D(200, 0));
        var b = map.AddVertex(new Vector2D(300, 0));
        map.AddLinedef(a, b); // no sidedefs
        map.BuildIndexes();
        Assert.True(Has(map, MapIssueKind.LinedefWithoutSidedefs));
    }

    [Fact]
    public void DetectsLinedefMissingFront()
    {
        var map = new MapSet();
        var s = map.AddSector();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var l = map.AddLinedef(a, b);
        map.AddSidedef(l, false, s); // back only
        map.BuildIndexes();
        Assert.True(Has(map, MapIssueKind.LinedefMissingFront));
    }

    [Fact]
    public void DetectsOverlappingVertices()
    {
        var map = Square(true);
        map.AddVertex(new Vector2D(0, 0)); // coincident with an existing corner
        map.BuildIndexes();
        Assert.True(Has(map, MapIssueKind.OverlappingVertices));
    }

    [Fact]
    public void DetectsUnusedVertex()
    {
        var map = Square(true);
        map.AddVertex(new Vector2D(900, 900)); // touched by no linedef
        map.BuildIndexes();
        Assert.True(Has(map, MapIssueKind.UnusedVertex));
    }

    [Fact]
    public void DetectsEmptySector()
    {
        var map = Square(true);
        map.AddSector(); // no sidedefs reference it
        map.BuildIndexes();
        Assert.True(Has(map, MapIssueKind.EmptySector));
    }

    [Fact]
    public void LinedefIssueCarriesTargetAndFocus()
    {
        var map = Square(true);
        var a = map.AddVertex(new Vector2D(200, 0));
        var b = map.AddVertex(new Vector2D(300, 0));
        var line = map.AddLinedef(a, b); // no sidedefs
        map.BuildIndexes();
        var issue = MapAnalysis.Check(map).First(i => i.Kind == MapIssueKind.LinedefWithoutSidedefs);
        Assert.Same(line, issue.Target);
        Assert.NotNull(issue.Focus);
        Assert.Equal(250, issue.Focus!.Value.x, 3); // midpoint x of (200,0)-(300,0)
    }

    [Fact]
    public void UnusedVertexIssueTargetsTheVertex()
    {
        var map = Square(true);
        var v = map.AddVertex(new Vector2D(900, 900));
        map.BuildIndexes();
        var issue = MapAnalysis.Check(map).First(i => i.Kind == MapIssueKind.UnusedVertex);
        Assert.Same(v, issue.Target);
        Assert.Equal(900, issue.Focus!.Value.y, 3);
    }
}
