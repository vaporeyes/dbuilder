// ABOUTME: Tests for MapAnalysis - the map health checker detecting geometry/structure issues.
// ABOUTME: Verifies a clean sector reports nothing and each defect surfaces its specific issue kind.

using System;
using System.Collections.Generic;
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

    private static (MapSet Map, List<Linedef> Lines) TextureChain(string texture, params double[] xs)
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var vertices = new List<Vertex>();
        foreach (double x in xs) vertices.Add(map.AddVertex(new Vector2D(x, 0)));

        var lines = new List<Linedef>();
        for (int i = 0; i < vertices.Count - 1; i++)
        {
            var line = map.AddLinedef(vertices[i], vertices[i + 1]);
            map.AddSidedef(line, true, sector).MidTexture = texture;
            lines.Add(line);
        }

        map.BuildIndexes();
        return (map, lines);
    }

    private static bool Has(MapSet map, MapIssueKind kind)
        => MapAnalysis.Check(map).Any(i => i.Kind == kind);

    private static (MapSet Map, Linedef Shared) AdjacentSquares()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var c = map.AddVertex(new Vector2D(100, 100));
        var d = map.AddVertex(new Vector2D(0, 100));
        var e = map.AddVertex(new Vector2D(200, 0));
        var f = map.AddVertex(new Vector2D(200, 100));

        SectorBuilder.CreateSector(map, new[] { a, b, c, d });
        SectorBuilder.CreateSector(map, new[] { b, e, f, c });
        map.BuildIndexes();

        var shared = map.Linedefs.Single(line =>
            (ReferenceEquals(line.Start, b) && ReferenceEquals(line.End, c)) ||
            (ReferenceEquals(line.Start, c) && ReferenceEquals(line.End, b)));
        return (map, shared);
    }

    private static void MarkSectorSides(Sector sector, string texture)
    {
        foreach (var side in sector.Sidedefs)
            side.MidTexture = texture;
    }

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
    public void MissingFrontIssueCanFlipLinedef()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 0));
        var line = map.AddLinedef(a, b);
        var back = map.AddSidedef(line, false, sector);
        map.BuildIndexes();
        var issue = Assert.Single(MapAnalysis.Check(map), i => i.Kind == MapIssueKind.LinedefMissingFront);
        var fix = Assert.Single(issue.Fixes);

        Assert.Equal("Flip Linedef", fix.Label);
        Assert.True(fix.Apply(map));

        Assert.Same(back, line.Front);
        Assert.Null(line.Back);
        Assert.Same(b, line.Start);
        Assert.Same(a, line.End);
    }

    [Fact]
    public void MissingFrontIssueCanCreateSidedefFromNeighbor()
    {
        var (map, line) = AdjacentSquares();
        var sourceSector = Assert.IsType<Sector>(line.Front?.Sector);
        MarkSectorSides(sourceSector, "COPYME");
        map.RemoveSidedef(line.Front!);
        map.BuildIndexes();
        var ctx = new MapCheckContext { DoubleSidedFlag = "4" };
        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.LinedefMissingFront);

        Assert.Equal(new[] { "Flip Linedef", "Create Sidedef" }, issue.Fixes.Select(fix => fix.Label).ToArray());
        Assert.True(issue.Fixes[1].Apply(map));

        Assert.NotNull(line.Front);
        Assert.NotNull(line.Back);
        Assert.Same(sourceSector, line.Front!.Sector);
        Assert.Equal("COPYME", line.Front.MidTexture);
        Assert.Equal(4, line.Flags);
    }

    [Fact]
    public void DoubleSidedFlagWithoutBackSidedefIsFlagged()
    {
        var map = Square(true);
        var line = map.Linedefs[0];
        line.UdmfFlags.Add("twosided");
        var ctx = new MapCheckContext { DoubleSidedFlag = "twosided" };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.LinedefNotDoubleSided);
        Assert.Same(line, issue.Target);
    }

    [Fact]
    public void NotDoubleSidedIssueCanClearDoubleSidedFlag()
    {
        var map = Square(true);
        var line = map.Linedefs[0];
        line.UdmfFlags.Add("twosided");
        var ctx = new MapCheckContext { DoubleSidedFlag = "twosided" };
        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.LinedefNotDoubleSided);
        var fix = Assert.Single(issue.Fixes, fix => fix.Label == "Make Single-Sided");

        Assert.Equal("Make Single-Sided", fix.Label);
        Assert.True(fix.Apply(map));

        Assert.DoesNotContain("twosided", line.UdmfFlags);
    }

    [Fact]
    public void NotDoubleSidedIssueCanCreateBackSidedefFromNeighbor()
    {
        var (map, line) = AdjacentSquares();
        var sourceSector = Assert.IsType<Sector>(line.Back?.Sector);
        MarkSectorSides(sourceSector, "COPYME");
        map.RemoveSidedef(line.Back!);
        map.BuildIndexes();
        line.Flags = 4;
        var ctx = new MapCheckContext { DoubleSidedFlag = "4" };
        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.LinedefNotDoubleSided);

        Assert.Equal(new[] { "Make Single-Sided", "Create Sidedef" }, issue.Fixes.Select(fix => fix.Label).ToArray());
        Assert.True(issue.Fixes[1].Apply(map));

        Assert.NotNull(line.Back);
        Assert.Same(sourceSector, line.Back!.Sector);
        Assert.Equal("COPYME", line.Back.MidTexture);
        Assert.Equal(4, line.Flags);
    }

    [Fact]
    public void LinedefWithoutSidedefsIssueCanCreateBothSidesFromNeighbors()
    {
        var (map, line) = AdjacentSquares();
        var frontSector = Assert.IsType<Sector>(line.Front?.Sector);
        var backSector = Assert.IsType<Sector>(line.Back?.Sector);
        MarkSectorSides(frontSector, "FRONTCOPY");
        MarkSectorSides(backSector, "BACKCOPY");
        map.RemoveSidedef(line.Front!);
        map.RemoveSidedef(line.Back!);
        map.BuildIndexes();
        var ctx = new MapCheckContext { DoubleSidedFlag = "4" };
        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.LinedefWithoutSidedefs);

        Assert.Equal(new[] { "Create One Side", "Create Both Sides" }, issue.Fixes.Select(fix => fix.Label).ToArray());
        Assert.True(issue.Fixes[1].Apply(map));

        Assert.NotNull(line.Front);
        Assert.NotNull(line.Back);
        Assert.Same(frontSector, line.Front!.Sector);
        Assert.Same(backSector, line.Back!.Sector);
        Assert.Equal("FRONTCOPY", line.Front.MidTexture);
        Assert.Equal("BACKCOPY", line.Back.MidTexture);
        Assert.Equal(4, line.Flags);
    }

    [Fact]
    public void BackSidedefWithoutDoubleSidedFlagIsFlagged()
    {
        var map = new MapSet();
        var front = map.AddSector();
        var back = map.AddSector();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(128, 0));
        var line = map.AddLinedef(a, b);
        map.AddSidedef(line, true, front);
        map.AddSidedef(line, false, back);
        var ctx = new MapCheckContext { DoubleSidedFlag = "4" };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.LinedefNotSingleSided);
        Assert.Same(line, issue.Target);
    }

    [Fact]
    public void NotSingleSidedIssueCanSetFlagOrRemoveBackSidedef()
    {
        var map = new MapSet();
        var front = map.AddSector();
        var backSector = map.AddSector();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(128, 0));
        var line = map.AddLinedef(a, b);
        map.AddSidedef(line, true, front);
        var back = map.AddSidedef(line, false, backSector);
        var ctx = new MapCheckContext { DoubleSidedFlag = "4" };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.LinedefNotSingleSided);
        Assert.Equal(new[] { "Make Double-Sided", "Remove Sidedef" }, issue.Fixes.Select(fix => fix.Label).ToArray());

        Assert.True(issue.Fixes[0].Apply(map));
        Assert.Equal(4, line.Flags);

        line.Flags = 0;
        issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.LinedefNotSingleSided);
        Assert.True(issue.Fixes[1].Apply(map));

        Assert.Null(line.Back);
        Assert.DoesNotContain(back, map.Sidedefs);
        Assert.True(back.IsDisposed);
    }

    [Fact]
    public void MissingFrontTakesPrecedenceOverDoubleSidedFlagMismatch()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(128, 0));
        var line = map.AddLinedef(a, b);
        map.AddSidedef(line, false, sector);
        var ctx = new MapCheckContext { DoubleSidedFlag = "twosided" };

        var issues = MapAnalysis.Check(map, ctx);
        Assert.Contains(issues, i => i.Kind == MapIssueKind.LinedefMissingFront);
        Assert.DoesNotContain(issues, i => i.Kind == MapIssueKind.LinedefNotSingleSided);
    }

    [Fact]
    public void MapWiderThanSafeBoundaryIsFlagged()
    {
        var map = Square(true);
        map.Vertices[2].Position = new Vector2D(2000, 100);
        var ctx = new MapCheckContext { SafeBoundary = 1024 };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.MapTooBig);
        Assert.Contains("width", issue.Message, StringComparison.Ordinal);
        Assert.NotNull(issue.Focus);
    }

    [Fact]
    public void SafeBoundaryZeroDisablesMapSizeCheck()
    {
        var map = Square(true);
        map.Vertices[2].Position = new Vector2D(2000, 100);
        var ctx = new MapCheckContext { SafeBoundary = 0 };

        Assert.DoesNotContain(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.MapTooBig);
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
    public void OverlappingVerticesIssueCanMergeVertices()
    {
        var map = new MapSet();
        var keep = map.AddVertex(new Vector2D(0, 0));
        var remove = map.AddVertex(new Vector2D(0, 0));
        var end = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(remove, end);
        map.BuildIndexes();

        var issue = Assert.Single(MapAnalysis.Check(map), i => i.Kind == MapIssueKind.OverlappingVertices);
        var fix = Assert.Single(issue.Fixes);

        Assert.Equal("Merge Vertices", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.DoesNotContain(remove, map.Vertices);
        Assert.Same(keep, line.Start);
        Assert.DoesNotContain(MapAnalysis.Check(map), i => i.Kind == MapIssueKind.OverlappingVertices);
    }

    [Fact]
    public void DetectsVertexOverlappingLinedefWithoutSplittingIt()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(128, 0));
        var line = map.AddLinedef(a, b);
        var vertex = map.AddVertex(new Vector2D(64, 0));
        map.BuildIndexes();

        var issue = Assert.Single(MapAnalysis.Check(map), i => i.Kind == MapIssueKind.VertexOverlappingLinedef);
        Assert.Same(vertex, issue.Target);
        Assert.Contains("linedef 0", issue.Message, StringComparison.Ordinal);
        Assert.Same(line, map.Linedefs[0]);
    }

    [Fact]
    public void VertexOverlappingLinedefIssueCanSplitLinedef()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(128, 0));
        var line = map.AddLinedef(a, b);
        var vertex = map.AddVertex(new Vector2D(64, 0));
        map.BuildIndexes();

        var issue = Assert.Single(MapAnalysis.Check(map), i => i.Kind == MapIssueKind.VertexOverlappingLinedef);
        var fix = Assert.Single(issue.Fixes);

        Assert.Equal("Split Linedef", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.Same(vertex, line.End);
        Assert.Contains(map.Linedefs, l => ReferenceEquals(l.Start, vertex) && ReferenceEquals(l.End, b));
        Assert.DoesNotContain(MapAnalysis.Check(map), i => i.Kind == MapIssueKind.VertexOverlappingLinedef);
    }

    [Fact]
    public void LinedefEndpointDoesNotCountAsVertexOverlappingLinedef()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(128, 0));
        map.AddLinedef(a, b);
        map.BuildIndexes();

        Assert.DoesNotContain(MapAnalysis.Check(map), i => i.Kind == MapIssueKind.VertexOverlappingLinedef);
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
    public void DetectsInvalidSectorWithFewerThanThreeUniqueLinedefs()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(128, 0));
        var line = map.AddLinedef(a, b);
        map.AddSidedef(line, true, sector);
        map.AddSidedef(line, false, sector);
        map.BuildIndexes();

        var issue = Assert.Single(MapAnalysis.Check(map), i => i.Kind == MapIssueKind.InvalidSector);
        Assert.Same(sector, issue.Target);
        Assert.DoesNotContain(MapAnalysis.Check(map), i => i.Kind == MapIssueKind.UnclosedSector);
    }

    [Fact]
    public void InvalidSectorDissolveRemovesZeroLengthLinesAndSector()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var vertex = map.AddVertex(new Vector2D(0, 0));
        var line = map.AddLinedef(vertex, vertex);
        map.AddSidedef(line, true, sector);
        map.AddSidedef(line, false, sector);
        map.BuildIndexes();

        var issue = Assert.Single(MapAnalysis.Check(map), i => i.Kind == MapIssueKind.InvalidSector);
        var fix = Assert.Single(issue.Fixes);

        Assert.Equal("Dissolve", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.DoesNotContain(sector, map.Sectors);
        Assert.DoesNotContain(line, map.Linedefs);
    }

    [Fact]
    public void InvalidSectorDissolveMergesIntoNeighbor()
    {
        var map = new MapSet();
        var invalid = map.AddSector();
        var neighbor = map.AddSector();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(128, 0));
        var line1 = map.AddLinedef(a, b);
        var line2 = map.AddLinedef(b, a);
        var invalidSide1 = map.AddSidedef(line1, true, invalid);
        map.AddSidedef(line1, false, neighbor);
        var invalidSide2 = map.AddSidedef(line2, true, invalid);
        map.AddSidedef(line2, false, neighbor);
        map.BuildIndexes();

        var issue = Assert.Single(MapAnalysis.Check(map), i => ReferenceEquals(i.Target, invalid));
        var fix = Assert.Single(issue.Fixes);

        Assert.Equal("Dissolve", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.DoesNotContain(invalid, map.Sectors);
        Assert.Same(neighbor, invalidSide1.Sector);
        Assert.Same(neighbor, invalidSide2.Sector);
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

    [Fact]
    public void UnusedVertexIssueCanDeleteVertex()
    {
        var map = Square(true);
        var vertex = map.AddVertex(new Vector2D(900, 900));
        map.BuildIndexes();
        var issue = MapAnalysis.Check(map).First(i => i.Kind == MapIssueKind.UnusedVertex);
        var fix = Assert.Single(issue.Fixes);

        Assert.Equal("Delete Vertex", fix.Label);
        Assert.True(fix.Apply(map));

        Assert.DoesNotContain(vertex, map.Vertices);
        Assert.True(vertex.IsDisposed);
    }

    // --- context-aware checks ---

    private static bool Has(MapSet map, MapCheckContext ctx, MapIssueKind kind)
        => MapAnalysis.Check(map, ctx).Any(i => i.Kind == kind);

    [Fact]
    public void ContextIsOptional_NoNewIssuesWhenNull()
    {
        // The clean square has "-" textures by default; without a context those must not be flagged.
        Assert.Empty(MapAnalysis.Check(Square(true)));
    }

    [Fact]
    public void OneSidedLineMissingMiddleTexture()
    {
        var map = Square(true); // sidedefs default to "-" textures, one-sided
        var ctx = new MapCheckContext();
        Assert.True(Has(map, ctx, MapIssueKind.MissingTexture));
    }

    [Fact]
    public void MissingTextureIssueCanAddDefaultTexture()
    {
        var map = Square(true);
        var side = map.Sidedefs[0];
        side.MidTexture = "-";
        var ctx = new MapCheckContext
        {
            FixOptions = new MapIssueFixOptions(DefaultWallTexture: "BROWN1"),
        };
        var issue = MapAnalysis.Check(map, ctx).First(i => i.Kind == MapIssueKind.MissingTexture);
        var fix = Assert.Single(issue.Fixes);

        Assert.Equal("Add Default Texture", fix.Label);
        Assert.True(fix.Apply(map));

        Assert.Equal("BROWN1", side.MidTexture);
    }

    [Fact]
    public void MissingTextureIssueCanBrowseTexture()
    {
        var map = Square(true);
        var side = map.Sidedefs[0];
        side.MidTexture = "-";
        var ctx = new MapCheckContext
        {
            BrowseTexture = (_, part) => part == SidedefPart.Middle ? "BROWSED" : null,
        };
        var issue = MapAnalysis.Check(map, ctx).First(i => i.Kind == MapIssueKind.MissingTexture);
        var fix = Assert.Single(issue.Fixes, f => f.Label == "Browse Texture...");

        Assert.True(fix.Apply(map));

        Assert.Equal("BROWSED", side.MidTexture);
    }

    [Fact]
    public void SkyNeighborSuppressesUpperAndLowerMissingTextures()
    {
        var map = new MapSet();
        var upper = map.AddSector();
        upper.CeilHeight = 128;
        upper.FloorHeight = 0;
        var skyCeiling = map.AddSector();
        skyCeiling.CeilHeight = 64;
        skyCeiling.FloorHeight = 0;
        skyCeiling.CeilTexture = "F_SKY1";
        var lower = map.AddSector();
        lower.CeilHeight = 128;
        lower.FloorHeight = 0;
        var skyFloor = map.AddSector();
        skyFloor.CeilHeight = 128;
        skyFloor.FloorHeight = 32;
        skyFloor.FloorTexture = "F_SKY1";
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(128, 0));
        var c = map.AddVertex(new Vector2D(0, 64));
        var d = map.AddVertex(new Vector2D(128, 64));
        var upperLine = map.AddLinedef(a, b);
        var lowerLine = map.AddLinedef(c, d);
        map.AddSidedef(upperLine, true, upper);
        map.AddSidedef(upperLine, false, skyCeiling);
        map.AddSidedef(lowerLine, true, lower);
        map.AddSidedef(lowerLine, false, skyFloor);
        map.BuildIndexes();
        var ctx = new MapCheckContext { IsSkyFlat = n => n == "F_SKY1" };

        Assert.DoesNotContain(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.MissingTexture);
    }

    [Fact]
    public void ActionRequiresUpperTextureFlagsMissingUpperWithoutHeightGap()
    {
        var map = new MapSet();
        var front = map.AddSector();
        front.CeilHeight = 128;
        var back = map.AddSector();
        back.CeilHeight = 128;
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(128, 0));
        var line = map.AddLinedef(a, b);
        line.Action = 271;
        map.AddSidedef(line, true, front);
        map.AddSidedef(line, false, back);
        map.BuildIndexes();
        var ctx = new MapCheckContext { ActionRequiresUpperTexture = action => action == 271 };

        var issues = MapAnalysis.Check(map, ctx).Where(i => i.Kind == MapIssueKind.MissingTexture).ToArray();
        Assert.NotEmpty(issues);
        Assert.All(issues, i => Assert.Contains("upper texture", i.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownTextureFlagged()
    {
        var map = Square(true);
        foreach (var sd in map.Sidedefs) sd.MidTexture = "NOPE99";
        var ctx = new MapCheckContext { TextureExists = n => n == "STARTAN3" };
        Assert.True(Has(map, ctx, MapIssueKind.UnknownTexture));
    }

    [Fact]
    public void UnknownTextureHonorsActionSlotExemption()
    {
        var map = Square(true);
        map.Linedefs[0].Action = 209;
        map.Linedefs[0].Front!.HighTexture = "TRANSFER_HEIGHTS_CONTROL";
        map.Linedefs[0].Front!.MidTexture = "NOPE99";
        var ctx = new MapCheckContext
        {
            TextureExists = _ => false,
            IgnoreUnknownTexture = (action, part) => action == 209 && part == SidedefPart.Upper,
        };

        var issues = MapAnalysis.Check(map, ctx).Where(i => i.Kind == MapIssueKind.UnknownTexture).ToArray();

        Assert.DoesNotContain(issues, i => i.Message.Contains("upper texture", StringComparison.Ordinal));
        Assert.Contains(issues, i => i.Message.Contains("middle texture", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownTextureIssueCanRemoveTexture()
    {
        var map = Square(true);
        var side = map.Linedefs[0].Front!;
        side.MidTexture = "NOPE99";
        var ctx = new MapCheckContext { TextureExists = _ => false };
        var issue = MapAnalysis.Check(map, ctx)
            .First(i => i.Kind == MapIssueKind.UnknownTexture && i.Message.Contains("middle texture", StringComparison.Ordinal));
        var fix = Assert.Single(issue.Fixes, f => f.Label == "Remove Texture");

        Assert.True(fix.Apply(map));

        Assert.Equal("-", side.MidTexture);
    }

    [Fact]
    public void UnknownTextureIssueCanAddDefaultTexture()
    {
        var map = Square(true);
        var side = map.Linedefs[0].Front!;
        side.HighTexture = "NOPE99";
        var ctx = new MapCheckContext
        {
            TextureExists = _ => false,
            FixOptions = new MapIssueFixOptions(DefaultTopTexture: "BROWN1"),
        };
        var issue = MapAnalysis.Check(map, ctx)
            .First(i => i.Kind == MapIssueKind.UnknownTexture && i.Message.Contains("upper texture", StringComparison.Ordinal));
        var fix = Assert.Single(issue.Fixes, f => f.Label == "Add Default Texture");

        Assert.True(fix.Apply(map));

        Assert.Equal("BROWN1", side.HighTexture);
    }

    [Fact]
    public void UnknownTextureIssueCanBrowseTexture()
    {
        var map = Square(true);
        var side = map.Sidedefs[0];
        side.MidTexture = "MISS";
        var ctx = new MapCheckContext
        {
            TextureExists = name => name == "BROWSED",
            BrowseTexture = (_, part) => part == SidedefPart.Middle ? "BROWSED" : null,
        };
        var issue = MapAnalysis.Check(map, ctx)
            .First(i => i.Kind == MapIssueKind.UnknownTexture && i.Message.Contains("middle texture", StringComparison.Ordinal));
        var fix = Assert.Single(issue.Fixes, f => f.Label == "Browse Texture...");

        Assert.True(fix.Apply(map));

        Assert.Equal("BROWSED", side.MidTexture);
    }

    [Fact]
    public void UnusedUpperAndLowerTexturesAreFlaggedWhenWallPartsAreNotRequired()
    {
        var map = new MapSet();
        var front = map.AddSector();
        front.FloorHeight = 0;
        front.CeilHeight = 128;
        var back = map.AddSector();
        back.FloorHeight = 0;
        back.CeilHeight = 128;
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(128, 0));
        var line = map.AddLinedef(a, b);
        map.AddSidedef(line, true, front);
        map.AddSidedef(line, false, back);
        line.Front!.HighTexture = "UNUSEDHI";
        line.Front!.LowTexture = "UNUSEDLO";
        map.BuildIndexes();

        var issues = MapAnalysis.Check(map, new MapCheckContext()).Where(i => i.Kind == MapIssueKind.UnusedTexture).ToArray();

        Assert.Contains(issues, i => i.Message.Contains("upper texture", StringComparison.Ordinal));
        Assert.Contains(issues, i => i.Message.Contains("lower texture", StringComparison.Ordinal));
    }

    [Fact]
    public void UnusedTextureIssueCanRemoveTextureAndUdmfOffsets()
    {
        var map = new MapSet();
        var front = map.AddSector();
        front.FloorHeight = 0;
        front.CeilHeight = 128;
        var back = map.AddSector();
        back.FloorHeight = 0;
        back.CeilHeight = 128;
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(128, 0));
        var line = map.AddLinedef(a, b);
        map.AddSidedef(line, true, front);
        map.AddSidedef(line, false, back);
        var side = line.Front!;
        side.HighTexture = "UNUSEDHI";
        side.Fields["offsetx_top"] = 8;
        side.Fields["scalex_top"] = 2.0;
        map.BuildIndexes();

        var issue = MapAnalysis.Check(map, new MapCheckContext())
            .First(i => i.Kind == MapIssueKind.UnusedTexture && i.Message.Contains("UNUSEDHI", StringComparison.Ordinal));
        var fix = Assert.Single(issue.Fixes);

        Assert.Equal("Remove Texture", fix.Label);
        Assert.True(fix.Apply(map));

        Assert.Equal("-", side.HighTexture);
        Assert.False(side.Fields.ContainsKey("offsetx_top"));
        Assert.False(side.Fields.ContainsKey("scalex_top"));
    }

    [Fact]
    public void RequiredUpperTextureActionDoesNotFlagUpperTextureUnused()
    {
        var map = new MapSet();
        var front = map.AddSector();
        front.CeilHeight = 128;
        var back = map.AddSector();
        back.CeilHeight = 128;
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(128, 0));
        var line = map.AddLinedef(a, b);
        line.Action = 271;
        map.AddSidedef(line, true, front);
        map.AddSidedef(line, false, back);
        line.Front!.HighTexture = "SKYTRANSFER";
        map.BuildIndexes();
        var ctx = new MapCheckContext { ActionRequiresUpperTexture = action => action == 271 };

        Assert.DoesNotContain(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnusedTexture);
    }

    [Fact]
    public void UnknownFlatAndMissingFlatFlagged()
    {
        var map = Square(true);
        map.Sectors[0].FloorTexture = "-";          // missing
        map.Sectors[0].CeilTexture = "WAT99";       // unknown
        var ctx = new MapCheckContext { FlatExists = n => n == "FLOOR4_8" };
        var issues = MapAnalysis.Check(map, ctx);
        Assert.Contains(issues, i => i.Kind == MapIssueKind.MissingFlat);
        Assert.Contains(issues, i => i.Kind == MapIssueKind.UnknownFlat);
    }

    [Fact]
    public void UnknownFlatIssueCanAddDefaultFlat()
    {
        var map = Square(true);
        var sector = map.Sectors[0];
        sector.CeilTexture = "WAT99";
        var ctx = new MapCheckContext
        {
            FlatExists = _ => false,
            FixOptions = new MapIssueFixOptions(DefaultCeilingTexture: "CEIL5_1"),
        };
        var issue = MapAnalysis.Check(map, ctx)
            .First(i => i.Kind == MapIssueKind.UnknownFlat && i.Message.Contains("ceiling flat", StringComparison.Ordinal));
        var fix = Assert.Single(issue.Fixes);

        Assert.Equal("Add Default Flat", fix.Label);
        Assert.True(fix.Apply(map));

        Assert.Equal("CEIL5_1", sector.CeilTexture);
    }

    [Fact]
    public void UnknownFlatIssueCanBrowseFlat()
    {
        var map = Square(true);
        var sector = map.Sectors[0];
        sector.CeilTexture = "WAT99";
        var ctx = new MapCheckContext
        {
            FlatExists = name => name == "BROWSEFL",
            BrowseFlat = (_, ceiling) => ceiling ? "BROWSEFL" : null,
        };
        var issue = MapAnalysis.Check(map, ctx)
            .First(i => i.Kind == MapIssueKind.UnknownFlat && i.Message.Contains("ceiling flat", StringComparison.Ordinal));
        var fix = Assert.Single(issue.Fixes, f => f.Label == "Browse Flat...");

        Assert.True(fix.Apply(map));

        Assert.Equal("BROWSEFL", sector.CeilTexture);
    }

    [Fact]
    public void MissingFlatIssueCanAddDefaultFlat()
    {
        var map = Square(true);
        var sector = map.Sectors[0];
        sector.FloorTexture = "-";
        var ctx = new MapCheckContext
        {
            FixOptions = new MapIssueFixOptions(DefaultFloorTexture: "FLOOR7_2"),
        };
        var issue = MapAnalysis.Check(map, ctx).First(i => i.Kind == MapIssueKind.MissingFlat);
        var fix = Assert.Single(issue.Fixes);

        Assert.Equal("Add Default Flat", fix.Label);
        Assert.True(fix.Apply(map));

        Assert.Equal("FLOOR7_2", sector.FloorTexture);
    }

    [Fact]
    public void MissingFlatIssueCanBrowseFlat()
    {
        var map = Square(true);
        var sector = map.Sectors[0];
        sector.FloorTexture = "-";
        var ctx = new MapCheckContext
        {
            BrowseFlat = (_, ceiling) => ceiling ? null : "BROWSEFL",
        };
        var issue = MapAnalysis.Check(map, ctx).First(i => i.Kind == MapIssueKind.MissingFlat);
        var fix = Assert.Single(issue.Fixes, f => f.Label == "Browse Flat...");

        Assert.True(fix.Apply(map));

        Assert.Equal("BROWSEFL", sector.FloorTexture);
    }

    [Fact]
    public void UnknownThingAndActionFlagged()
    {
        var map = Square(true);
        var thing = map.AddThing(new Vector2D(50, 50), 99999);
        map.Linedefs[0].Action = 4242;
        map.BuildIndexes();
        var ctx = new MapCheckContext { ThingTypeKnown = n => n == 1, ActionKnown = a => a == 11 };
        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnknownThingType);
        var fix = Assert.Single(issue.Fixes);

        Assert.Equal("Delete Thing", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.DoesNotContain(thing, map.Things);
        issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnknownAction);
        fix = Assert.Single(issue.Fixes);
        Assert.Equal("Remove Action", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.Equal(0, map.Linedefs[0].Action);
    }

    [Fact]
    public void ObsoleteThingTypeFlaggedWithMessage()
    {
        var map = Square(true);
        var thing = map.AddThing(new Vector2D(50, 50), 31007);
        var ctx = new MapCheckContext { ThingObsoleteMessage = type => type == 31007 ? "Use ReplacementThing instead" : null };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.ObsoleteThingType);
        var fix = Assert.Single(issue.Fixes);
        Assert.Same(thing, issue.Target);
        Assert.Contains("Use ReplacementThing instead", issue.Message, StringComparison.Ordinal);
        Assert.Equal("Delete Thing", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.DoesNotContain(thing, map.Things);
    }

    [Fact]
    public void UnknownThingIssueCanEditThing()
    {
        var map = Square(true);
        var thing = map.AddThing(new Vector2D(50, 50), 99999);
        var ctx = new MapCheckContext
        {
            ThingTypeKnown = type => type == 1,
            EditThing = edited =>
            {
                edited.Type = 1;
                return true;
            },
        };
        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnknownThingType);

        Assert.Equal(new[] { "Edit Thing...", "Delete Thing" }, issue.Fixes.Select(fix => fix.Label).ToArray());
        Assert.True(issue.Fixes[0].Apply(map));

        Assert.Equal(1, thing.Type);
        Assert.Contains(thing, map.Things);
    }

    [Fact]
    public void ObsoleteThingIssueCanEditThing()
    {
        var map = Square(true);
        var thing = map.AddThing(new Vector2D(50, 50), 31007);
        var ctx = new MapCheckContext
        {
            ThingObsoleteMessage = type => type == 31007 ? "Use ReplacementThing instead" : null,
            EditThing = edited =>
            {
                edited.Type = 3004;
                return true;
            },
        };
        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.ObsoleteThingType);

        Assert.Equal(new[] { "Edit Thing...", "Delete Thing" }, issue.Fixes.Select(fix => fix.Label).ToArray());
        Assert.True(issue.Fixes[0].Apply(map));

        Assert.Equal(3004, thing.Type);
        Assert.Contains(thing, map.Things);
    }

    [Fact]
    public void UnknownLinedefActionIssueCanBrowseAction()
    {
        var map = Square(true);
        map.Linedefs[0].Action = 4242;
        var ctx = new MapCheckContext
        {
            ActionKnown = action => action == 80,
            BrowseAction = action => action == 4242 ? 80 : null,
        };
        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnknownAction);
        var fix = Assert.Single(issue.Fixes, f => f.Label == "Browse Action...");

        Assert.True(fix.Apply(map));

        Assert.Equal(80, map.Linedefs[0].Action);
    }

    [Fact]
    public void UnusedThingFlaggedWithWarnings()
    {
        var map = Square(true);
        var thing = map.AddThing(new Vector2D(50, 50), 3004);
        var ctx = new MapCheckContext
        {
            ThingUnusedWarnings = _ => new[] { "Thing is not used in any skill level." },
            DefaultThingFlags = new[] { "skill1", "skill2" },
        };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnusedThing);
        Assert.Collection(issue.Fixes,
            fix => Assert.Equal("Delete Thing", fix.Label),
            fix => Assert.Equal("Apply default flags", fix.Label));
        Assert.Same(thing, issue.Target);
        Assert.Contains("Thing is not used in any skill level.", issue.Message, StringComparison.Ordinal);
        Assert.True(issue.Fixes[1].Apply(map));
        Assert.Contains("skill1", thing.UdmfFlags);
        Assert.Contains("skill2", thing.UdmfFlags);
        Assert.Contains(thing, map.Things);
        Assert.True(issue.Fixes[0].Apply(map));
        Assert.DoesNotContain(thing, map.Things);
    }

    [Fact]
    public void UsedThingWithNoWarningsIsNotFlagged()
    {
        var map = Square(true);
        map.AddThing(new Vector2D(50, 50), 3004);
        var ctx = new MapCheckContext { ThingUnusedWarnings = _ => Array.Empty<string>() };

        Assert.DoesNotContain(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnusedThing);
    }

    [Fact]
    public void ThingOutsideMapFlaggedWhenTypeRequiresInsideCheck()
    {
        var map = Square(true);
        var thing = map.AddThing(new Vector2D(-32, 50), 3004);
        var ctx = new MapCheckContext { ThingErrorCheck = type => type == 3004 ? 1 : 0 };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.ThingOutsideMap);
        var fix = Assert.Single(issue.Fixes);
        Assert.Same(thing, issue.Target);
        Assert.Contains("outside the map", issue.Message, StringComparison.Ordinal);
        Assert.Equal("Delete Thing", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.DoesNotContain(thing, map.Things);
    }

    [Fact]
    public void ThingOutsideMapIgnoredWhenTypeDoesNotRequireInsideCheck()
    {
        var map = Square(true);
        map.AddThing(new Vector2D(-32, 50), 14);
        var ctx = new MapCheckContext { ThingErrorCheck = _ => 0 };

        Assert.DoesNotContain(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.ThingOutsideMap);
    }

    [Fact]
    public void ThingInsideMapIsNotFlaggedByOutsideCheck()
    {
        var map = Square(true);
        map.AddThing(new Vector2D(50, 50), 3004);
        var ctx = new MapCheckContext { ThingErrorCheck = _ => 1 };

        Assert.DoesNotContain(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.ThingOutsideMap);
    }

    [Fact]
    public void BlockingThingIntersectingOneSidedLineIsFlaggedAsStuck()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var a = map.AddVertex(new Vector2D(64, 0));
        var b = map.AddVertex(new Vector2D(64, 128));
        var line = map.AddLinedef(a, b);
        map.AddSidedef(line, true, sector);
        var thing = map.AddThing(new Vector2D(64, 64), 3004);
        thing.Size = 20;
        var ctx = new MapCheckContext { ThingErrorCheck = _ => 2, ThingBlocking = _ => 2 };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.ThingStuckInLinedef);
        var fix = Assert.Single(issue.Fixes);
        Assert.Same(thing, issue.Target);
        Assert.Contains("linedef 0", issue.Message, StringComparison.Ordinal);
        Assert.Equal("Delete Thing", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.DoesNotContain(thing, map.Things);
    }

    [Fact]
    public void BlockingThingIntersectingPassableTwoSidedLineIsNotFlaggedAsStuck()
    {
        var map = new MapSet();
        var front = map.AddSector();
        var back = map.AddSector();
        var a = map.AddVertex(new Vector2D(64, 0));
        var b = map.AddVertex(new Vector2D(64, 128));
        var line = map.AddLinedef(a, b);
        map.AddSidedef(line, true, front);
        map.AddSidedef(line, false, back);
        var thing = map.AddThing(new Vector2D(64, 64), 3004);
        thing.Size = 20;
        var ctx = new MapCheckContext { ThingErrorCheck = _ => 2, ThingBlocking = _ => 2, ImpassableFlag = "blocking" };

        Assert.DoesNotContain(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.ThingStuckInLinedef);
    }

    [Fact]
    public void BlockingThingIntersectingImpassableTwoSidedLineIsFlaggedAsStuck()
    {
        var map = new MapSet();
        var front = map.AddSector();
        var back = map.AddSector();
        var a = map.AddVertex(new Vector2D(64, 0));
        var b = map.AddVertex(new Vector2D(64, 128));
        var line = map.AddLinedef(a, b);
        line.UdmfFlags.Add("blocking");
        map.AddSidedef(line, true, front);
        map.AddSidedef(line, false, back);
        var thing = map.AddThing(new Vector2D(64, 64), 3004);
        thing.Size = 20;
        var ctx = new MapCheckContext { ThingErrorCheck = _ => 2, ThingBlocking = _ => 2, ImpassableFlag = "blocking" };

        Assert.Contains(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.ThingStuckInLinedef);
    }

    [Fact]
    public void BlockingThingsWithOverlappingFlagsAreFlaggedAsStuck()
    {
        var map = new MapSet();
        var first = map.AddThing(new Vector2D(64, 64), 3004);
        first.Size = 20;
        first.UdmfFlags.Add("skill1");
        var second = map.AddThing(new Vector2D(70, 64), 3004);
        second.Size = 20;
        second.UdmfFlags.Add("skill1");
        var ctx = new MapCheckContext
        {
            ThingErrorCheck = _ => 2,
            ThingBlocking = _ => 2,
            ThingHeight = _ => 56,
            ThingFlagsOverlap = (a, b) => a.UdmfFlags.Overlaps(b.UdmfFlags),
        };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.ThingStuckInThing);
        Assert.Same(first, issue.Target);
        Assert.Contains("thing 1", issue.Message, StringComparison.Ordinal);
        Assert.Collection(issue.Fixes,
            fix => Assert.Equal("Delete 1-st Thing", fix.Label),
            fix => Assert.Equal("Delete 2-nd Thing", fix.Label));
        Assert.True(issue.Fixes[1].Apply(map));
        Assert.Contains(first, map.Things);
        Assert.DoesNotContain(second, map.Things);
    }

    [Fact]
    public void BlockingThingsWithDifferentFlagsAreNotFlaggedAsStuck()
    {
        var map = new MapSet();
        var first = map.AddThing(new Vector2D(64, 64), 3004);
        first.Size = 20;
        first.UdmfFlags.Add("skill1");
        var second = map.AddThing(new Vector2D(70, 64), 3004);
        second.Size = 20;
        second.UdmfFlags.Add("skill2");
        var ctx = new MapCheckContext
        {
            ThingErrorCheck = _ => 2,
            ThingBlocking = _ => 2,
            ThingHeight = _ => 56,
            ThingFlagsOverlap = (a, b) => a.UdmfFlags.Overlaps(b.UdmfFlags),
        };

        Assert.DoesNotContain(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.ThingStuckInThing);
    }

    [Fact]
    public void TrueHeightBlockingThingsSeparatedByHeightAreNotFlaggedAsStuck()
    {
        var map = new MapSet();
        var lower = map.AddThing(new Vector2D(64, 64), 3004);
        lower.Size = 20;
        lower.Height = 0;
        var upper = map.AddThing(new Vector2D(70, 64), 3004);
        upper.Size = 20;
        upper.Height = 80;
        var ctx = new MapCheckContext
        {
            ThingErrorCheck = _ => 2,
            ThingBlocking = _ => 2,
            ThingHeight = _ => 56,
            ThingFlagsOverlap = (_, _) => true,
        };

        Assert.DoesNotContain(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.ThingStuckInThing);
    }

    [Fact]
    public void PolyobjectLineTargetingMissingStartSpotIsFlagged()
    {
        var map = Square(true);
        var line = map.Linedefs[0];
        line.Action = 1;
        line.Args[0] = 7;
        var ctx = new MapCheckContext
        {
            CheckPolyobjects = true,
            LinedefActionId = action => action == 1 ? "Polyobj_StartLine" : null,
            ThingClassName = _ => null,
        };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.InvalidPolyobject);
        Assert.Same(line, issue.Target);
        Assert.Contains("non-existing Polyobject Start Spot (7)", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PolyobjectStartSpotWithoutAnchorIsFlagged()
    {
        var map = Square(true);
        var thing = map.AddThing(new Vector2D(64, 64), 9300);
        thing.Angle = 7;
        var ctx = new MapCheckContext
        {
            CheckPolyobjects = true,
            LinedefActionId = _ => null,
            ThingClassName = type => type == 9300 ? "$polyspawn" : null,
        };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.InvalidPolyobject);
        Assert.Same(thing, issue.Target);
        Assert.Contains("not targeted", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PolyobjectAnchorAndStartSpotWithSameNumberAreValid()
    {
        var map = Square(true);
        var start = map.AddThing(new Vector2D(64, 64), 9300);
        start.Angle = 7;
        var anchor = map.AddThing(new Vector2D(96, 64), 9301);
        anchor.Angle = 7;
        var ctx = new MapCheckContext
        {
            CheckPolyobjects = true,
            LinedefActionId = _ => null,
            ThingClassName = type => type == 9300 ? "$polyspawn" : type == 9301 ? "$polyanchor" : null,
        };

        Assert.DoesNotContain(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.InvalidPolyobject);
    }

    [Fact]
    public void LinedefReferencingUnknownAcsScriptNumberIsFlagged()
    {
        var map = Square(true);
        var line = map.Linedefs[0];
        line.Action = 80;
        line.Args[0] = 12;
        var ctx = new MapCheckContext
        {
            CheckScripts = true,
            ScriptNumberExists = number => number == 1,
        };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnknownLinedefScript);
        Assert.Same(line, issue.Target);
        Assert.Contains("unknown ACS script number \"12\"", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownLinedefScriptIssueCanEditLinedef()
    {
        var map = Square(true);
        var line = map.Linedefs[0];
        line.Action = 80;
        line.Args[0] = 12;
        var ctx = new MapCheckContext
        {
            CheckScripts = true,
            ScriptNumberExists = number => number == 1,
            EditLinedef = edited =>
            {
                edited.Args[0] = 1;
                return true;
            },
        };
        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnknownLinedefScript);
        var fix = Assert.Single(issue.Fixes);

        Assert.Equal("Edit Linedef...", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.Equal(1, line.Args[0]);
    }

    [Fact]
    public void ThingReferencingUnknownNamedAcsScriptIsFlagged()
    {
        var map = Square(true);
        var thing = map.AddThing(new Vector2D(64, 64), 3004);
        thing.Action = 80;
        thing.Fields["arg0str"] = "OpenDoor";
        var ctx = new MapCheckContext
        {
            CheckScripts = true,
            CheckNamedScripts = true,
            ScriptNameExists = name => name == "KnownScript",
        };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnknownThingScript);
        var fix = Assert.Single(issue.Fixes);
        Assert.Same(thing, issue.Target);
        Assert.Contains("unknown ACS script name \"OpenDoor\"", issue.Message, StringComparison.Ordinal);
        Assert.Equal("Delete Thing", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.DoesNotContain(thing, map.Things);
    }

    [Fact]
    public void UnknownThingScriptIssueCanEditThing()
    {
        var map = Square(true);
        var thing = map.AddThing(new Vector2D(50, 50), 1);
        thing.Action = 80;
        thing.Args[0] = 99;
        var ctx = new MapCheckContext
        {
            CheckScripts = true,
            ScriptNumberExists = number => number == 1,
            EditThing = edited =>
            {
                edited.Args[0] = 1;
                return true;
            },
        };
        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnknownThingScript);

        Assert.Equal(new[] { "Edit Thing...", "Delete Thing" }, issue.Fixes.Select(fix => fix.Label).ToArray());
        Assert.True(issue.Fixes[0].Apply(map));

        Assert.Equal(1, thing.Args[0]);
        Assert.Contains(thing, map.Things);
    }

    [Fact]
    public void KnownAcsScriptReferencesAreValid()
    {
        var map = Square(true);
        var line = map.Linedefs[0];
        line.Action = 80;
        line.Args[0] = 12;
        var thing = map.AddThing(new Vector2D(64, 64), 3004);
        thing.Action = 80;
        thing.Fields["arg0str"] = "OpenDoor";
        var ctx = new MapCheckContext
        {
            CheckScripts = true,
            CheckNamedScripts = true,
            ScriptNumberExists = number => number == 12,
            ScriptNameExists = name => name == "OpenDoor",
        };

        var issues = MapAnalysis.Check(map, ctx);
        Assert.DoesNotContain(issues, i => i.Kind == MapIssueKind.UnknownLinedefScript);
        Assert.DoesNotContain(issues, i => i.Kind == MapIssueKind.UnknownThingScript);
    }

    [Fact]
    public void ConnectedWallTextureRunWithWrongOffsetIsFlagged()
    {
        var (map, lines) = TextureChain("WALL", 0, 64, 160, 224);
        lines[1].Front!.OffsetX = 12;
        var ctx = new MapCheckContext
        {
            CheckTextureAlignment = true,
            TextureSize = texture => texture == "WALL" ? (128, 128) : null,
        };

        var issue = MapAnalysis.Check(map, ctx).First(i => i.Kind == MapIssueKind.MisalignedTexture);
        Assert.Same(lines[0], issue.Target);
        Assert.Contains("Texture \"WALL\" is not aligned", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectedWallTextureRunWithExpectedOffsetsIsValid()
    {
        var (map, lines) = TextureChain("WALL", 0, 64, 160, 224);
        lines[1].Front!.OffsetX = 64;
        lines[2].Front!.OffsetX = 32;
        var ctx = new MapCheckContext
        {
            CheckTextureAlignment = true,
            TextureSize = texture => texture == "WALL" ? (128, 128) : null,
        };

        Assert.DoesNotContain(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.MisalignedTexture);
    }

    [Fact]
    public void TextureAlignmentCheckSkipsUnknownTextureDimensions()
    {
        var (map, lines) = TextureChain("WALL", 0, 64, 128);
        lines[1].Front!.OffsetX = 12;
        var ctx = new MapCheckContext
        {
            CheckTextureAlignment = true,
            TextureSize = _ => null,
        };

        Assert.DoesNotContain(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.MisalignedTexture);
    }

    [Fact]
    public void UnknownSectorEffectFlagged()
    {
        var map = Square(true);
        map.Sectors[0].Special = 4242;
        var ctx = new MapCheckContext { SectorEffectKnown = effect => effect == 9 };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnknownSectorEffect);
        var fix = Assert.Single(issue.Fixes);
        Assert.Same(map.Sectors[0], issue.Target);
        Assert.Equal("Remove Effect", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.Equal(0, map.Sectors[0].Special);
    }

    [Fact]
    public void UnknownSectorEffectIssueCanBrowseEffect()
    {
        var map = Square(true);
        map.Sectors[0].Special = 4242;
        var ctx = new MapCheckContext
        {
            SectorEffectKnown = effect => effect == 9,
            BrowseSectorEffect = effect => effect == 4242 ? 9 : null,
        };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnknownSectorEffect);
        var fix = Assert.Single(issue.Fixes, f => f.Label == "Browse Effect...");

        Assert.True(fix.Apply(map));
        Assert.Equal(9, map.Sectors[0].Special);
    }

    [Fact]
    public void UnknownThingActionFlaggedOnlyWhenThingActionsAreEnabled()
    {
        var map = Square(true);
        var thing = map.AddThing(new Vector2D(50, 50), 1);
        thing.Action = 4242;
        var ctx = new MapCheckContext { ActionKnown = action => action == 80 };

        Assert.DoesNotContain(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnknownThingAction);

        ctx = new MapCheckContext { ActionKnown = action => action == 80, CheckThingActions = true };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnknownThingAction);
        var fix = Assert.Single(issue.Fixes);
        Assert.Same(thing, issue.Target);
        Assert.Equal("Remove Action", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.Equal(0, thing.Action);
    }

    [Fact]
    public void UnknownThingActionIssueCanBrowseAction()
    {
        var map = Square(true);
        var thing = map.AddThing(new Vector2D(50, 50), 1);
        thing.Action = 4242;
        var ctx = new MapCheckContext
        {
            ActionKnown = action => action == 80,
            CheckThingActions = true,
            BrowseAction = action => action == 4242 ? 80 : null,
        };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnknownThingAction);
        var fix = Assert.Single(issue.Fixes, f => f.Label == "Browse Action...");

        Assert.True(fix.Apply(map));
        Assert.Equal(80, thing.Action);
    }

    [Fact]
    public void MissingActivationFlaggedForUdmfActionThatRequiresTrigger()
    {
        var map = Square(true);
        map.Linedefs[0].Action = 80;
        var ctx = new MapCheckContext
        {
            CheckMissingActivations = true,
            ActionRequiresActivation = a => a == 80,
            TriggerActivationFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "playeruse" },
        };

        Assert.True(Has(map, ctx, MapIssueKind.MissingActivation));
    }

    [Fact]
    public void MissingActivationIssueCanEditLinedef()
    {
        var map = Square(true);
        var line = map.Linedefs[0];
        line.Action = 80;
        var ctx = new MapCheckContext
        {
            CheckMissingActivations = true,
            ActionRequiresActivation = action => action == 80,
            TriggerActivationFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "playeruse" },
            EditLinedef = edited =>
            {
                edited.UdmfFlags.Add("playeruse");
                return true;
            },
        };
        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.MissingActivation);
        var fix = Assert.Single(issue.Fixes);

        Assert.Equal("Edit Linedef", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.Contains("playeruse", line.UdmfFlags);
    }

    [Fact]
    public void MissingActivationIgnoresNonUdmfAndTriggeredLines()
    {
        var map = Square(true);
        map.Linedefs[0].Action = 80;
        map.Linedefs[0].UdmfFlags.Add("playeruse");
        var ctx = new MapCheckContext
        {
            ActionRequiresActivation = a => a == 80,
            TriggerActivationFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "playeruse" },
        };

        Assert.False(Has(map, ctx, MapIssueKind.MissingActivation));

        ctx = new MapCheckContext
        {
            CheckMissingActivations = true,
            ActionRequiresActivation = a => a == 80,
            TriggerActivationFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "playeruse" },
        };

        Assert.False(Has(map, ctx, MapIssueKind.MissingActivation));
    }

    [Fact]
    public void OverlappingLinedefsFlagged()
    {
        var map = Square(true);
        // Add a second line over the first edge (0,0)-(0,100).
        map.AddLinedef(map.Vertices[0], map.Vertices[1]);
        map.BuildIndexes();
        Assert.True(Has(map, new MapCheckContext(), MapIssueKind.OverlappingLinedefs));
    }

    [Fact]
    public void CrossingLinedefsFlagged()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 100));
        var c = map.AddVertex(new Vector2D(0, 100));
        var d = map.AddVertex(new Vector2D(100, 0));
        map.AddLinedef(a, b);
        map.AddLinedef(c, d);
        map.BuildIndexes();

        Assert.True(Has(map, new MapCheckContext(), MapIssueKind.OverlappingLinedefs));
    }

    [Fact]
    public void CrossingLinedefsInSameSectorOnAllSidesAreAllowed()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(100, 100));
        var c = map.AddVertex(new Vector2D(0, 100));
        var d = map.AddVertex(new Vector2D(100, 0));
        var first = map.AddLinedef(a, b);
        var second = map.AddLinedef(c, d);
        map.AddSidedef(first, true, sector);
        map.AddSidedef(first, false, sector);
        map.AddSidedef(second, true, sector);
        map.AddSidedef(second, false, sector);
        map.BuildIndexes();

        Assert.False(Has(map, new MapCheckContext(), MapIssueKind.OverlappingLinedefs));
    }

    [Fact]
    public void ShortLinedefFlagged()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(0.5, 0));
        map.AddLinedef(a, b);
        map.BuildIndexes();
        var issue = Assert.Single(MapAnalysis.Check(map, new MapCheckContext()), i => i.Kind == MapIssueKind.ShortLinedef);
        Assert.Contains("shorter than 1 map unit", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OneMapUnitLinedefIsNotShort()
    {
        var map = new MapSet();
        var a = map.AddVertex(new Vector2D(0, 0));
        var b = map.AddVertex(new Vector2D(1, 0));
        map.AddLinedef(a, b);
        map.BuildIndexes();
        Assert.False(Has(map, new MapCheckContext(), MapIssueKind.ShortLinedef));
    }

    [Fact]
    public void OffGridVertexFlaggedOnlyWithGrid()
    {
        var map = new MapSet();
        map.AddVertex(new Vector2D(7, 3)); // off a 64 grid
        var withGrid = new MapCheckContext { GridSize = 64 };
        Assert.True(Has(map, withGrid, MapIssueKind.OffGridVertex));
        Assert.False(Has(map, new MapCheckContext { GridSize = 0 }, MapIssueKind.OffGridVertex));
    }

    [Fact]
    public void OffGridVertexIssueCanAlignVertexToGrid()
    {
        var map = new MapSet();
        var vertex = map.AddVertex(new Vector2D(70, 95));
        var ctx = new MapCheckContext { GridSize = 64 };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.OffGridVertex);
        var fix = Assert.Single(issue.Fixes);

        Assert.Equal("Align Vertex", fix.Label);
        Assert.True(fix.Apply(map));
        Assert.Equal(new Vector2D(64, 64), vertex.Position);
        Assert.DoesNotContain(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.OffGridVertex);
    }
}
