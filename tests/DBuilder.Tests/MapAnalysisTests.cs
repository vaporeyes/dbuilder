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
    public void UnknownThingAndActionFlagged()
    {
        var map = Square(true);
        map.AddThing(new Vector2D(50, 50), 99999);
        map.Linedefs[0].Action = 4242;
        map.BuildIndexes();
        var ctx = new MapCheckContext { ThingTypeKnown = n => n == 1, ActionKnown = a => a == 11 };
        Assert.True(Has(map, ctx, MapIssueKind.UnknownThingType));
        Assert.True(Has(map, ctx, MapIssueKind.UnknownAction));
    }

    [Fact]
    public void ObsoleteThingTypeFlaggedWithMessage()
    {
        var map = Square(true);
        var thing = map.AddThing(new Vector2D(50, 50), 31007);
        var ctx = new MapCheckContext { ThingObsoleteMessage = type => type == 31007 ? "Use ReplacementThing instead" : null };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.ObsoleteThingType);
        Assert.Same(thing, issue.Target);
        Assert.Contains("Use ReplacementThing instead", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownSectorEffectFlagged()
    {
        var map = Square(true);
        map.Sectors[0].Special = 4242;
        var ctx = new MapCheckContext { SectorEffectKnown = effect => effect == 9 };

        var issue = Assert.Single(MapAnalysis.Check(map, ctx), i => i.Kind == MapIssueKind.UnknownSectorEffect);
        Assert.Same(map.Sectors[0], issue.Target);
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
        Assert.Same(thing, issue.Target);
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
        var b = map.AddVertex(new Vector2D(4, 0)); // length 4 < default 8
        map.AddLinedef(a, b);
        map.BuildIndexes();
        Assert.True(Has(map, new MapCheckContext { ShortLinedefLength = 8 }, MapIssueKind.ShortLinedef));
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
}
