// ABOUTME: Tests Doom-style sound propagation and UDB SoundPropagationMode domain models.
// ABOUTME: Covers free flow, block lines, adjacent domains, hunting things, and height-blocked lines.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SoundPropagationTests
{
    private const int Block = SoundPropagation.DefaultSoundBlockBit; // 64

    // A linear chain of sectors; each adjacent pair shares one two-sided linedef. blockAfter[i]==true marks
    // the line between sector i and i+1 as sound-blocking. Returns the sectors in order.
    private static (MapSet map, Sector[] sectors) Chain(int count, bool[] blockBetween)
    {
        var map = new MapSet();
        var sectors = new Sector[count];
        for (int i = 0; i < count; i++)
        {
            sectors[i] = map.AddSector();
            sectors[i].FloorHeight = 0;
            sectors[i].CeilHeight = 128;
        }

        for (int i = 0; i < count - 1; i++)
        {
            var a = map.AddVertex(new Vector2D(i * 64, 0));
            var b = map.AddVertex(new Vector2D(i * 64, 64));
            var line = map.AddLinedef(a, b);
            map.AddSidedef(line, true, sectors[i]);
            map.AddSidedef(line, false, sectors[i + 1]);
            if (blockBetween[i]) line.Flags |= Block;
        }
        map.BuildIndexes();
        return (map, sectors);
    }

    [Fact]
    public void SoundFlowsFreelyThroughOpenLines()
    {
        var (map, s) = Chain(3, new[] { false, false });
        var reach = SoundPropagation.Reachable(map, s[0]);
        Assert.Equal(3, reach.Count);
        Assert.Equal(1, reach[s[0]]);
        Assert.Equal(1, reach[s[1]]);
        Assert.Equal(1, reach[s[2]]);
    }

    [Fact]
    public void CrossingOneBlockLineIsLevelTwo()
    {
        var (map, s) = Chain(3, new[] { false, true }); // block between s1 and s2
        var reach = SoundPropagation.Reachable(map, s[0]);
        Assert.Equal(1, reach[s[0]]);
        Assert.Equal(1, reach[s[1]]);
        Assert.Equal(2, reach[s[2]]); // reached only by crossing the block line
    }

    [Fact]
    public void SecondBlockLineStopsSound()
    {
        var (map, s) = Chain(4, new[] { false, true, true }); // blocks at s1|s2 and s2|s3
        var reach = SoundPropagation.Reachable(map, s[0]);
        Assert.True(reach.ContainsKey(s[2])); // reachable across the first block (level 2)
        Assert.False(reach.ContainsKey(s[3])); // the second block line stops it
    }

    [Fact]
    public void StartingPastABlockHearsBackwardAtLevelOne()
    {
        var (map, s) = Chain(3, new[] { true, false }); // block between s0 and s1
        var reach = SoundPropagation.Reachable(map, s[1]);
        Assert.Equal(1, reach[s[1]]);
        Assert.Equal(1, reach[s[2]]);      // open line
        Assert.Equal(2, reach[s[0]]);      // across the block line
    }

    [Fact]
    public void ClosedDoorHeightBlocksSound()
    {
        var (map, s) = Chain(3, new[] { false, false });
        s[0].FloorHeight = 0;
        s[0].CeilHeight = 64;
        s[1].FloorHeight = 64;
        s[1].CeilHeight = 128;
        s[2].FloorHeight = 0;
        s[2].CeilHeight = 128;

        var reach = SoundPropagation.Reachable(map, s[0]);

        Assert.True(reach.ContainsKey(s[0]));
        Assert.False(reach.ContainsKey(s[1]));
        Assert.False(reach.ContainsKey(s[2]));
    }

    [Fact]
    public void InvalidSectorHeightBlocksSound()
    {
        var (map, s) = Chain(2, new[] { false });
        s[0].FloorHeight = 64;
        s[0].CeilHeight = 64;
        s[1].FloorHeight = 0;
        s[1].CeilHeight = 128;

        var reach = SoundPropagation.Reachable(map, s[0]);

        Assert.True(SoundPropagation.IsBlockedByHeight(map.Linedefs[0]));
        Assert.Single(reach);
        Assert.True(reach.ContainsKey(s[0]));
    }

    [Fact]
    public void BuildModeModelGroupsOpenSectorsIntoDomains()
    {
        var (map, s) = Chain(4, new[] { false, true, false });

        SoundPropagationModeModel model = SoundPropagation.BuildModeModel(map);

        Assert.Equal(2, model.Domains.Count);
        Assert.Same(model.GetDomain(s[0]), model.GetDomain(s[1]));
        Assert.Same(model.GetDomain(s[2]), model.GetDomain(s[3]));
        Assert.NotSame(model.GetDomain(s[0]), model.GetDomain(s[2]));
        Assert.Equal(new[] { map.Linedefs[1] }, model.BlockingLinedefs);
        Assert.Equal(new[] { s[2] }, model.GetDomain(s[0])!.AdjacentSectors);
        Assert.Equal(new[] { s[0], s[1], s[2], s[3] }, model.GetAffectedSectors(s[0]));
    }

    [Fact]
    public void AffectedSectorsDoNotCrossTwoBlockingLines()
    {
        var (map, s) = Chain(4, new[] { false, true, true });

        SoundPropagationModeModel model = SoundPropagation.BuildModeModel(map);

        Assert.Equal(new[] { s[0], s[1], s[2] }, model.GetAffectedSectors(s[0]));
    }

    [Fact]
    public void HuntingThingsExcludeAmbushAndOutsideSectors()
    {
        var (map, s) = Chain(4, new[] { false, true, true });
        var direct = new Thing(new Vector2D(0, 0), 3001) { Sector = s[0] };
        var adjacent = new Thing(new Vector2D(64, 0), 3001) { Sector = s[2] };
        var ambush = new Thing(new Vector2D(64, 0), 3001) { Sector = s[2], Flags = SoundPropagation.DefaultAmbushBit };
        var outside = new Thing(new Vector2D(128, 0), 3001) { Sector = s[3] };
        map.Things.AddRange(new[] { direct, adjacent, ambush, outside });

        SoundPropagationModeModel model = SoundPropagation.BuildModeModel(map);

        Assert.Equal(new[] { direct, adjacent }, model.GetHuntingThings(s[0], map.Things));
    }

    [Fact]
    public void UdmfBlockSoundFlagCreatesDomainBoundary()
    {
        var (map, s) = Chain(3, new[] { false, false });
        map.Linedefs[1].SetFlag(SoundPropagation.DefaultUdmfSoundBlockFlag, true);
        var ambush = new Thing(new Vector2D(64, 0), 3001) { Sector = s[2] };
        ambush.SetFlag("ambush", true);
        map.Things.Add(ambush);

        SoundPropagationModeModel model = SoundPropagation.BuildModeModel(map, udmf: true);

        Assert.Equal(2, model.Domains.Count);
        Assert.Equal(new[] { map.Linedefs[1] }, model.BlockingLinedefs);
        Assert.Empty(model.GetHuntingThings(s[0], map.Things, udmf: true));
    }

    [Fact]
    public void ReachableHonorsUdmfBlockSoundFlag()
    {
        var (map, s) = Chain(3, new[] { false, false });
        map.Linedefs[1].SetFlag(SoundPropagation.DefaultUdmfSoundBlockFlag, true);

        Dictionary<Sector, int> reach = SoundPropagation.Reachable(map, s[0], udmf: true);

        Assert.Equal(1, reach[s[0]]);
        Assert.Equal(1, reach[s[1]]);
        Assert.Equal(2, reach[s[2]]);
    }

    [Fact]
    public void ToggleSoundBlockingFlipsClassicBitLikeUdbModeAction()
    {
        var (map, _) = Chain(2, new[] { false });
        Linedef line = map.Linedefs[0];

        Assert.True(SoundPropagation.ToggleSoundBlocking(line));
        Assert.True((line.Flags & Block) != 0);
        Assert.True(SoundPropagation.IsSoundBlocking(line));

        Assert.False(SoundPropagation.ToggleSoundBlocking(line));
        Assert.Equal(0, line.Flags & Block);
        Assert.False(SoundPropagation.IsSoundBlocking(line));
    }

    [Fact]
    public void ToggleSoundBlockingFlipsUdmfFlagLikeUdbModeAction()
    {
        var (map, _) = Chain(2, new[] { false });
        Linedef line = map.Linedefs[0];

        Assert.True(SoundPropagation.ToggleSoundBlocking(line, udmf: true));
        Assert.True(line.IsFlagSet(SoundPropagation.DefaultUdmfSoundBlockFlag));
        Assert.True(SoundPropagation.IsSoundBlocking(line, udmf: true));
        Assert.Equal(0, line.Flags & Block);

        Assert.False(SoundPropagation.ToggleSoundBlocking(line, udmf: true));
        Assert.False(line.IsFlagSet(SoundPropagation.DefaultUdmfSoundBlockFlag));
        Assert.False(SoundPropagation.IsSoundBlocking(line, udmf: true));
    }

    [Fact]
    public void HeightBlockedSoundLineIsNotAdjacent()
    {
        var (map, s) = Chain(3, new[] { true, false });
        s[1].FloorHeight = 128;
        s[1].CeilHeight = 256;

        SoundPropagationModeModel model = SoundPropagation.BuildModeModel(map);

        Assert.Equal(new[] { s[0] }, model.GetAffectedSectors(s[0]));
    }

    [Fact]
    public void LeakPathReturnsLineMidpointsThroughOneBlockingLine()
    {
        var (map, s) = Chain(3, new[] { false, true });
        var sectors = new HashSet<Sector>(s, ReferenceEqualityComparer.Instance);

        SoundLeakPath? path = SoundPropagation.FindLeakPath(
            s[0],
            new Vector2D(-32, 32),
            s[2],
            new Vector2D(96, 32),
            sectors);

        Assert.NotNull(path);
        Assert.Equal(new[] { map.Linedefs[0], map.Linedefs[1] }, path.Linedefs);
        Assert.Equal(new[] { map.Linedefs[1] }, path.BlockingLinedefs);
        Assert.Equal(new Vector2D(-32, 32), path.Points[0]);
        Assert.Equal(new Vector2D(0, 32), path.Points[1]);
        Assert.Equal(new Vector2D(64, 32), path.Points[2]);
        Assert.Equal(new Vector2D(96, 32), path.Points[3]);
    }

    [Fact]
    public void LeakPathRejectsSecondBlockingLine()
    {
        var (_, s) = Chain(4, new[] { false, true, true });
        var sectors = new HashSet<Sector>(s, ReferenceEqualityComparer.Instance);

        SoundLeakPath? path = SoundPropagation.FindLeakPath(
            s[0],
            new Vector2D(-32, 32),
            s[3],
            new Vector2D(160, 32),
            sectors);

        Assert.Null(path);
    }

    [Fact]
    public void LeakPathRejectsHeightBlockedLine()
    {
        var (_, s) = Chain(2, new[] { false });
        s[1].FloorHeight = 128;
        s[1].CeilHeight = 256;
        var sectors = new HashSet<Sector>(s, ReferenceEqualityComparer.Instance);

        SoundLeakPath? path = SoundPropagation.FindLeakPath(
            s[0],
            new Vector2D(-32, 32),
            s[1],
            new Vector2D(32, 32),
            sectors);

        Assert.Null(path);
    }

    [Fact]
    public void LeakPathRequiresSourceAndDestinationInsideSectors()
    {
        var (_, s) = Chain(2, new[] { false });

        Assert.Throws<ArgumentException>(() => SoundPropagation.FindLeakPath(
            s[0],
            new Vector2D(-32, 32),
            s[1],
            new Vector2D(32, 32),
            new HashSet<Sector>(new[] { s[0] }, ReferenceEqualityComparer.Instance)));
    }

    [Fact]
    public void LeakSearchSectorsIncludeSourceDomainAndAdjacentDomains()
    {
        var (map, s) = Chain(5, new[] { false, true, false, true });
        SoundPropagationModeModel model = SoundPropagation.BuildModeModel(map);

        IReadOnlySet<Sector> sectors = model.GetLeakSearchSectors(s[0]);

        Assert.Contains(s[0], sectors);
        Assert.Contains(s[1], sectors);
        Assert.Contains(s[2], sectors);
        Assert.Contains(s[3], sectors);
        Assert.DoesNotContain(s[4], sectors);
    }

    [Fact]
    public void SectorCenterUsesUniqueBoundaryVertices()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Vertex a = map.AddVertex(new Vector2D(0, 0));
        Vertex b = map.AddVertex(new Vector2D(64, 0));
        Vertex c = map.AddVertex(new Vector2D(64, 64));
        Vertex d = map.AddVertex(new Vector2D(0, 64));

        Linedef ab = map.AddLinedef(a, b);
        Linedef bc = map.AddLinedef(b, c);
        Linedef cd = map.AddLinedef(c, d);
        Linedef da = map.AddLinedef(d, a);
        map.AddSidedef(ab, true, sector);
        map.AddSidedef(bc, true, sector);
        map.AddSidedef(cd, true, sector);
        map.AddSidedef(da, true, sector);
        map.BuildIndexes();

        Assert.Equal(new Vector2D(32, 32), SoundPropagation.SectorCenter(sector));
    }

    [Fact]
    public void DefaultColorSettingsMatchUdbPluginDefaults()
    {
        SoundPropagationColorSettings colors = SoundPropagationColorSettings.Default;

        Assert.Equal(0xFF00C000u, colors.HighlightColor);
        Assert.Equal(0xFF00FF00u, colors.Level1Color);
        Assert.Equal(0xFFFFFF00u, colors.Level2Color);
        Assert.Equal(0xFFA0A0A0u, colors.NoSoundColor);
        Assert.Equal(0xFFFF0000u, colors.BlockSoundColor);
        Assert.Equal(24, colors.DistinctDomainColors.Count);
        Assert.Equal(0xFF84D5A4u, colors.DistinctDomainColors[0]);
        Assert.Equal(0xFF8DD3C7u, colors.DistinctDomainColors[^1]);
    }

    [Fact]
    public void DomainColorForIndexWrapsLikeUdbDomainAssignment()
    {
        SoundPropagationColorSettings colors = SoundPropagationColorSettings.Default;

        Assert.Equal(colors.DistinctDomainColors[0], colors.DomainColorForIndex(0));
        Assert.Equal(colors.DistinctDomainColors[1], colors.DomainColorForIndex(1));
        Assert.Equal(colors.DistinctDomainColors[0], colors.DomainColorForIndex(colors.DistinctDomainColors.Count));
        Assert.Equal(colors.DistinctDomainColors[^1], colors.DomainColorForIndex(-1));
    }

    [Fact]
    public void SectorOverlayColorsUseDomainPaletteWithoutHighlight()
    {
        var (map, s) = Chain(4, new[] { false, true, false });
        SoundPropagationModeModel model = SoundPropagation.BuildModeModel(map);
        SoundPropagationColorSettings colors = SoundPropagationColorSettings.Default;

        uint[] overlay = model.SectorOverlayColors(map.Sectors, highlightedSector: null, colors);

        Assert.Equal(colors.DistinctDomainColors[0], overlay[0]);
        Assert.Equal(colors.DistinctDomainColors[0], overlay[1]);
        Assert.Equal(colors.DistinctDomainColors[1], overlay[2]);
        Assert.Equal(colors.DistinctDomainColors[1], overlay[3]);
    }

    [Fact]
    public void SectorOverlayColorsUseUdbHighlightedDomainLevels()
    {
        var (map, s) = Chain(5, new[] { false, true, false, true });
        SoundPropagationModeModel model = SoundPropagation.BuildModeModel(map);
        SoundPropagationColorSettings colors = SoundPropagationColorSettings.Default;

        uint[] overlay = model.SectorOverlayColors(map.Sectors, s[0], colors);

        Assert.Equal(colors.HighlightColor, overlay[0]);
        Assert.Equal(colors.Level1Color, overlay[1]);
        Assert.Equal(colors.Level2Color, overlay[2]);
        Assert.Equal(colors.Level2Color, overlay[3]);
        Assert.Equal(colors.NoSoundColor, overlay[4]);
    }
}
