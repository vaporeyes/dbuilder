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

    [Theory]
    [InlineData(1, 1, "Sound leak path: 1 line, 1 sound-blocking line.")]
    [InlineData(2, 0, "Sound leak path: 2 lines, 0 sound-blocking lines.")]
    public void LeakPathStatusTextFormatsSingularAndPluralLineCounts(int linedefCount, int blockingLinedefCount, string expected)
    {
        var linedefs = Enumerable.Range(0, linedefCount).Select(_ => new Linedef(new Vertex(), new Vertex())).ToArray();
        var blockingLinedefs = linedefs.Take(blockingLinedefCount).ToArray();
        var path = new SoundLeakPath(Array.Empty<Vector2D>(), linedefs, blockingLinedefs);

        Assert.Equal(expected, path.StatusText);
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
    public void ColorConfigurationMetadataMatchesUdbDialogAndAction()
    {
        SoundPropagationActionDescriptor action = SoundPropagationColorSettings.ColorConfigurationAction;
        IReadOnlyList<SoundPropagationColorField> fields = SoundPropagationColorSettings.ColorConfigurationFields;

        Assert.Equal("Color Configuration", SoundPropagationColorSettings.ColorConfigurationTitle);
        Assert.Equal("Reset colors", SoundPropagationColorSettings.ResetColorsText);
        Assert.Equal("soundpropagationcolorconfiguration", action.Id);
        Assert.Equal("Configure colors", action.Title);
        Assert.Equal("soundpropagationmode", action.Category);
        Assert.Equal("Configure colors for sound propagation mode", action.Description);
        Assert.True(action.AllowKeys);
        Assert.True(action.AllowMouse);
        Assert.True(action.AllowScroll);
        Assert.Equal(5, fields.Count);
        Assert.Equal(new SoundPropagationColorField("highlightcolor", "Highlight color:", 0xFF00C000u), fields[0]);
        Assert.Equal(new SoundPropagationColorField("level1color", "Level 1 color:", 0xFF00FF00u), fields[1]);
        Assert.Equal(new SoundPropagationColorField("level2color", "Level 2 color:", 0xFFFFFF00u), fields[2]);
        Assert.Equal(new SoundPropagationColorField("nosoundcolor", "No sound color:", 0xFFA0A0A0u), fields[3]);
        Assert.Equal(new SoundPropagationColorField("blocksoundcolor", "Block sound color:", 0xFFFF0000u), fields[4]);
    }

    [Fact]
    public void LifecycleMetadataMatchesUdbPluginResetEvents()
    {
        SoundPropagationLifecycleDescriptor lifecycle = SoundPropagationColorSettings.Lifecycle;

        Assert.Equal(new[] { "OnMapOpenBegin", "OnMapNewBegin", "OnEditEngage" }, lifecycle.ResetDataEvents);
        Assert.Equal(new[] { "OnMapSaveBegin" }, lifecycle.StaleEnvironmentEvents);
    }

    [Fact]
    public void ModeDescriptorsMatchUdbEditModeMetadata()
    {
        SoundPropagationModeDescriptor propagation = SoundPropagationColorSettings.ModeDescriptor;
        SoundPropagationModeDescriptor environment = SoundEnvironmentModeModel.ModeDescriptor;

        Assert.Equal("Sound Propagation Mode", propagation.DisplayName);
        Assert.Equal("soundpropagationmode", propagation.SwitchAction);
        Assert.Equal("SoundPropagationIcon.png", propagation.ButtonImage);
        Assert.Equal(int.MinValue + 501, propagation.ButtonOrder);
        Assert.Equal("000_editing", propagation.ButtonGroup);
        Assert.Empty(propagation.SupportedMapFormats);
        Assert.True(propagation.UseByDefault);
        Assert.False(propagation.SafeStartMode);
        Assert.False(propagation.Volatile);
        Assert.Equal("gzdb/features/classic_modes/mode_soundpropagation.html", propagation.HelpPath);

        Assert.Equal("Sound Environment Mode", environment.DisplayName);
        Assert.Equal("soundenvironmentmode", environment.SwitchAction);
        Assert.Equal("ZDoomSoundEnvironment.png", environment.ButtonImage);
        Assert.Equal(int.MinValue + 502, environment.ButtonOrder);
        Assert.Equal("000_editing", environment.ButtonGroup);
        Assert.Equal([SoundPropagationColorSettings.UniversalMapSetIo], environment.SupportedMapFormats);
        Assert.True(environment.UseByDefault);
        Assert.False(environment.SafeStartMode);
        Assert.False(environment.Volatile);
        Assert.Equal("gzdb/features/classic_modes/mode_soundenvironment.html", environment.HelpPath);
    }

    [Fact]
    public void ColorSettingsReadAndWriteUdbPluginKeys()
    {
        var settings = new Dictionary<string, object?>
        {
            [SoundPropagationColorSettings.HighlightColorKey] = unchecked((int)0xFF010203u),
            [SoundPropagationColorSettings.Level1ColorKey] = 0xFF040506u,
            [SoundPropagationColorSettings.Level2ColorKey] = "4278650889",
            [SoundPropagationColorSettings.NoSoundColorKey] = 0xFF0A0B0Cu,
            [SoundPropagationColorSettings.BlockSoundColorKey] = "invalid",
        };

        SoundPropagationColorSettings colors = SoundPropagationColorSettings.FromSettings(settings);
        IReadOnlyDictionary<string, object> written = colors.ToSettings();

        Assert.Equal(0xFF010203u, colors.HighlightColor);
        Assert.Equal(0xFF040506u, colors.Level1Color);
        Assert.Equal(0xFF070809u, colors.Level2Color);
        Assert.Equal(0xFF0A0B0Cu, colors.NoSoundColor);
        Assert.Equal(SoundPropagationColorSettings.Default.BlockSoundColor, colors.BlockSoundColor);
        Assert.Equal(unchecked((int)0xFF010203u), written[SoundPropagationColorSettings.HighlightColorKey]);
        Assert.Equal(unchecked((int)0xFF040506u), written[SoundPropagationColorSettings.Level1ColorKey]);
        Assert.Equal(unchecked((int)0xFF070809u), written[SoundPropagationColorSettings.Level2ColorKey]);
        Assert.Equal(unchecked((int)0xFF0A0B0Cu), written[SoundPropagationColorSettings.NoSoundColorKey]);
        Assert.Equal(unchecked((int)SoundPropagationColorSettings.Default.BlockSoundColor), written[SoundPropagationColorSettings.BlockSoundColorKey]);
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

    [Fact]
    public void ReachabilitySummaryFormatsDirectAndSoundBlockingCounts()
    {
        var (map, s) = Chain(4, new[] { false, true, false });
        Dictionary<Sector, int> reach = SoundPropagation.Reachable(map, s[0]);

        SoundPropagationReachSummary summary = SoundPropagation.SummarizeReachability(reach);

        Assert.Equal(4, summary.TotalSectors);
        Assert.Equal(2, summary.DirectSectors);
        Assert.Equal(2, summary.ViaBlockingLineSectors);
        Assert.Equal("Sound reaches 4 sectors: 2 direct, 2 via a sound-blocking line.", summary.StatusText);
    }

    [Fact]
    public void ReachabilitySummaryFormatsSingularSectorCount()
    {
        var (map, s) = Chain(1, Array.Empty<bool>());
        Dictionary<Sector, int> reach = SoundPropagation.Reachable(map, s[0]);

        SoundPropagationReachSummary summary = SoundPropagation.SummarizeReachability(reach);

        Assert.Equal(1, summary.TotalSectors);
        Assert.Equal(1, summary.DirectSectors);
        Assert.Equal(0, summary.ViaBlockingLineSectors);
        Assert.Equal("Sound reaches 1 sector: 1 direct, 0 via a sound-blocking line.", summary.StatusText);
    }

    [Fact]
    public void SoundEnvironmentBoundaryUsesClassicLineIdentificationArgument()
    {
        var (map, _) = Chain(2, new[] { false });
        Linedef line = map.Linedefs[0];

        Assert.False(SoundPropagation.LinedefBlocksSoundEnvironment(line));

        line.Action = 121;
        line.Args[1] = 1;

        Assert.True(SoundPropagation.LinedefBlocksSoundEnvironment(line));
    }

    [Fact]
    public void SoundEnvironmentBoundaryUsesUdmfZoneBoundaryFlag()
    {
        var (map, _) = Chain(2, new[] { false });
        Linedef line = map.Linedefs[0];
        line.Action = 121;
        line.Args[1] = 1;

        Assert.False(SoundPropagation.LinedefBlocksSoundEnvironment(line, udmf: true));

        line.SetFlag(SoundPropagation.DefaultUdmfZoneBoundaryFlag, true);

        Assert.True(SoundPropagation.LinedefBlocksSoundEnvironment(line, udmf: true));
    }

    [Fact]
    public void SoundEnvironmentThingsAreFilteredByTypeAndSectorSet()
    {
        var (map, s) = Chain(3, new[] { false, false });
        Thing environment = map.AddThing(new Vector2D(0, 0), SoundPropagation.SoundEnvironmentThingType);
        environment.Sector = s[0];
        Thing otherEnvironment = map.AddThing(new Vector2D(128, 0), SoundPropagation.SoundEnvironmentThingType);
        otherEnvironment.Sector = s[2];
        Thing notEnvironment = map.AddThing(new Vector2D(0, 0), 3001);
        notEnvironment.Sector = s[0];

        IReadOnlyList<Thing> things = SoundPropagation.GetSoundEnvironmentThings(map, new[] { s[0], s[1] });

        Thing result = Assert.Single(things);
        Assert.Same(environment, result);
    }

    [Fact]
    public void BuildSoundEnvironmentModelGroupsSectorsAcrossNonBoundaryLines()
    {
        var (map, s) = Chain(4, new[] { false, false, false });
        map.Linedefs[1].Action = 121;
        map.Linedefs[1].Args[1] = 1;
        Thing environment = map.AddThing(new Vector2D(0, 0), SoundPropagation.SoundEnvironmentThingType);
        environment.Sector = s[0];
        environment.Args[0] = 1;
        environment.Args[1] = 2;
        var reverbs = new Dictionary<string, SoundEnvironmentReverb>
        {
            ["Stone Room"] = new(1, 2)
        };

        SoundEnvironmentModeModel model = SoundPropagation.BuildSoundEnvironmentModel(map, reverbs);

        SoundEnvironmentInfo info = Assert.Single(model.Environments);
        Assert.Equal(new[] { s[0], s[1] }, info.Sectors);
        Assert.Equal(new[] { environment }, info.Things);
        Assert.Equal(new[] { map.Linedefs[1] }, info.BoundaryLinedefs);
        Assert.Equal(new[] { map.Linedefs[1] }, model.BoundaryLinedefs);
        Assert.Equal(new[] { s[2], s[3] }, model.UnassignedSectors);
        Assert.Equal("Stone Room (1 2)", info.Name);
        Assert.Equal(1, info.Id);
        Assert.Equal(SoundPropagationColorSettings.Default.DomainColorForIndex((1 << 8) + 2), info.Color);
    }

    [Fact]
    public void SoundEnvironmentNameUsesFirstNonDormantThing()
    {
        var (map, s) = Chain(2, new[] { false });
        Thing dormant = map.AddThing(new Vector2D(0, 0), SoundPropagation.SoundEnvironmentThingType);
        dormant.Sector = s[0];
        dormant.Args[0] = 5;
        dormant.Args[1] = 6;
        Thing active = map.AddThing(new Vector2D(32, 0), SoundPropagation.SoundEnvironmentThingType);
        active.Sector = s[1];
        active.Args[0] = 7;
        active.Args[1] = 8;
        SoundPropagation.SetThingDormant(dormant, true, udmf: true);
        var reverbs = new Dictionary<string, SoundEnvironmentReverb>
        {
            ["Hall"] = new(7, 8)
        };

        SoundEnvironmentModeModel model = SoundPropagation.BuildSoundEnvironmentModel(map, reverbs, udmf: true);

        SoundEnvironmentInfo info = Assert.Single(model.Environments);
        Assert.Equal("Hall (7 8)", info.Name);
        Assert.True(SoundPropagation.ThingDormant(dormant, udmf: true));
        Assert.False(SoundPropagation.ThingDormant(active, udmf: true));
    }

    [Fact]
    public void SoundEnvironmentOverlayColorsUseEnvironmentAndUnassignedColors()
    {
        var (map, s) = Chain(2, new[] { true });
        map.Linedefs[0].Action = 121;
        map.Linedefs[0].Args[1] = 1;
        Thing environment = map.AddThing(new Vector2D(0, 0), SoundPropagation.SoundEnvironmentThingType);
        environment.Sector = s[0];
        var colors = SoundPropagationColorSettings.Default with
        {
            HighlightColor = 0xFF010203u,
            NoSoundColor = 0xFF040506u,
            DistinctDomainColors = new[] { 0xFF070809u },
        };

        SoundEnvironmentModeModel model = SoundPropagation.BuildSoundEnvironmentModel(map, colors: colors);
        SoundEnvironmentInfo info = Assert.Single(model.Environments);

        uint[] normal = model.SectorOverlayColors(map.Sectors, colors);
        Assert.Equal(0xFF070809u, normal[0]);
        Assert.Equal(0xFF040506u, normal[1]);

        uint[] highlighted = model.SectorOverlayColors(map.Sectors, colors, info);
        Assert.Equal(0xFF010203u, highlighted[0]);
        Assert.Equal(0xFF040506u, highlighted[1]);
    }

    [Fact]
    public void SoundEnvironmentRowsFlagMultipleActiveThings()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Thing first = map.AddThing(new Vector2D(0, 0), SoundPropagation.SoundEnvironmentThingType);
        Thing second = map.AddThing(new Vector2D(16, 0), SoundPropagation.SoundEnvironmentThingType);
        first.Sector = sector;
        second.Sector = sector;

        SoundEnvironmentModeModel model = SoundPropagation.BuildSoundEnvironmentModel(map);

        Assert.Equal(2, model.WarningCount());
        IReadOnlyList<SoundEnvironmentRow> rows = model.Rows();
        Assert.Equal(5, rows.Count);
        Assert.True(rows[0].Warning);
        Assert.True(rows[1].Warning);
        Assert.True(rows[2].Warning);
        Assert.True(rows[3].Warning);
        Assert.False(rows[4].Warning);
        Assert.Equal("Things (2)", rows[1].Text);
        Assert.Equal($"Thing {first.Index}", rows[2].Text);
        Assert.Equal($"Thing {second.Index}", rows[3].Text);
        Assert.Equal("Linedefs (0)", rows[4].Text);
        Assert.Equal(2, rows[2].Depth);
        Assert.Equal(SoundEnvironmentModeModel.MultipleActiveThingsWarning, rows[2].WarningMessage);
        Assert.Equal(SoundEnvironmentModeModel.MultipleActiveThingsWarning, rows[3].WarningMessage);

        IReadOnlyList<SoundEnvironmentRow> warnings = model.Rows(warningsOnly: true);
        Assert.Equal(rows, warnings);
    }

    [Fact]
    public void SoundEnvironmentRowsWarningsOnlyKeepsAllRowsForWarningEnvironment()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Linedef line = map.AddLinedef(
            map.AddVertex(new Vector2D(0, 0)),
            map.AddVertex(new Vector2D(64, 0)));
        var environment = new SoundEnvironmentInfo(
            new HashSet<Sector>(new[] { sector }, ReferenceEqualityComparer.Instance),
            Array.Empty<Thing>(),
            new[] { line },
            0xFF010203u,
            1,
            "Test Environment");
        var model = new SoundEnvironmentModeModel(
            new[] { environment },
            new HashSet<Sector>(ReferenceEqualityComparer.Instance),
            new HashSet<Linedef>(new[] { line }, ReferenceEqualityComparer.Instance));

        Assert.Equal(1, model.WarningCount());
        IReadOnlyList<SoundEnvironmentRow> rows = model.Rows(warningsOnly: true);

        Assert.Equal(4, rows.Count);
        Assert.True(rows[0].Warning);
        Assert.Equal(environment, rows[0].Environment);
        Assert.Equal("Things (0)", rows[1].Text);
        Assert.False(rows[1].Warning);
        Assert.True(rows[2].Warning);
        Assert.Equal("Linedefs (1)", rows[2].Text);
        Assert.True(rows[3].Warning);
        Assert.Equal(line, rows[3].Linedef);
        Assert.Equal($"Linedef {line.Index}", rows[3].Text);
        Assert.Equal(2, rows[3].Depth);
        Assert.Equal(SoundEnvironmentModeModel.SingleSidedBoundaryWarning, rows[3].WarningMessage);
    }

    [Fact]
    public void SoundEnvironmentRowsLabelDormantThingsByUdbIndex()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Thing thing = map.AddThing(new Vector2D(0, 0), SoundPropagation.SoundEnvironmentThingType);
        thing.Sector = sector;
        SoundPropagation.SetThingDormant(thing, true);

        SoundEnvironmentModeModel model = SoundPropagation.BuildSoundEnvironmentModel(map);

        SoundEnvironmentRow row = Assert.Single(model.Rows(), row => row.Thing == thing);
        Assert.Equal($"Thing {thing.Index} (dormant)", row.Text);
        Assert.Equal(2, row.Depth);
        Assert.Null(row.WarningMessage);
    }

    [Fact]
    public void SoundEnvironmentRowsExplainSameEnvironmentBoundaryWarning()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Linedef line = map.AddLinedef(
            map.AddVertex(new Vector2D(0, 0)),
            map.AddVertex(new Vector2D(64, 0)));
        map.AddSidedef(line, true, sector);
        map.AddSidedef(line, false, sector);
        var environment = new SoundEnvironmentInfo(
            new HashSet<Sector>(new[] { sector }, ReferenceEqualityComparer.Instance),
            Array.Empty<Thing>(),
            new[] { line },
            0xFF010203u,
            1,
            "Test Environment");
        var model = new SoundEnvironmentModeModel(
            new[] { environment },
            new HashSet<Sector>(ReferenceEqualityComparer.Instance),
            new HashSet<Linedef>(new[] { line }, ReferenceEqualityComparer.Instance));

        SoundEnvironmentRow row = Assert.Single(model.Rows(warningsOnly: true), row => row.Linedef == line);
        Assert.Equal(SoundEnvironmentModeModel.SameEnvironmentBoundaryWarning, row.WarningMessage);
    }

    [Fact]
    public void SoundEnvironmentRowsUseUdbTreeGrouping()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Thing thing = map.AddThing(new Vector2D(0, 0), SoundPropagation.SoundEnvironmentThingType);
        thing.Sector = sector;

        SoundEnvironmentModeModel model = SoundPropagation.BuildSoundEnvironmentModel(map);

        IReadOnlyList<SoundEnvironmentRow> rows = model.Rows();
        Assert.Equal(4, rows.Count);
        Assert.Equal(model.Environments[0].Name, rows[0].Text);
        Assert.Equal(0, rows[0].Depth);
        Assert.Equal("Things (1)", rows[1].Text);
        Assert.Equal(1, rows[1].Depth);
        Assert.Equal($"Thing {thing.Index}", rows[2].Text);
        Assert.Equal(2, rows[2].Depth);
        Assert.Equal("Linedefs (0)", rows[3].Text);
        Assert.Equal(1, rows[3].Depth);
    }

    [Fact]
    public void SoundEnvironmentRowsWarningsOnlyHidesEnvironmentWithoutWarnings()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Thing thing = map.AddThing(new Vector2D(0, 0), SoundPropagation.SoundEnvironmentThingType);
        thing.Sector = sector;

        SoundEnvironmentModeModel model = SoundPropagation.BuildSoundEnvironmentModel(map);

        Assert.Empty(model.Rows(warningsOnly: true));
    }

    [Fact]
    public void SoundEnvironmentWarningsOnlyStateMatchesUdbCheckboxTextAndEnablement()
    {
        var empty = new SoundEnvironmentModeModel(
            Array.Empty<SoundEnvironmentInfo>(),
            new HashSet<Sector>(ReferenceEqualityComparer.Instance),
            new HashSet<Linedef>(ReferenceEqualityComparer.Instance));
        var map = new MapSet();
        Sector sector = map.AddSector();
        Linedef warningLine = map.AddLinedef(
            map.AddVertex(new Vector2D(0, 0)),
            map.AddVertex(new Vector2D(64, 0)));
        var warningEnvironment = new SoundEnvironmentInfo(
            new HashSet<Sector>(new[] { sector }, ReferenceEqualityComparer.Instance),
            Array.Empty<Thing>(),
            new[] { warningLine },
            0xFF010203u,
            1,
            "Warning Environment");
        var warnings = new SoundEnvironmentModeModel(
            new[] { warningEnvironment },
            new HashSet<Sector>(ReferenceEqualityComparer.Instance),
            new HashSet<Linedef>(new[] { warningLine }, ReferenceEqualityComparer.Instance));

        Assert.Equal(
            new SoundEnvironmentWarningsOnlyState("Show nodes with warnings only (0)", Enabled: false),
            empty.WarningsOnlyState(checkedState: false));
        Assert.Equal(
            new SoundEnvironmentWarningsOnlyState("Show nodes with warnings only (0)", Enabled: true),
            empty.WarningsOnlyState(checkedState: true));
        Assert.Equal(
            new SoundEnvironmentWarningsOnlyState("Show nodes with warnings only (1)", Enabled: true),
            warnings.WarningsOnlyState(checkedState: false));
    }

    [Fact]
    public void SoundEnvironmentPanelHighlightCollapsesAndExpandsMatchingEnvironment()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        var first = new SoundEnvironmentInfo(
            new HashSet<Sector>(new[] { sector }, ReferenceEqualityComparer.Instance),
            Array.Empty<Thing>(),
            Array.Empty<Linedef>(),
            0xFF010203u,
            2,
            "Second");
        var second = first with { Id = 1, Name = "First" };
        var model = new SoundEnvironmentModeModel(
            new[] { first, second },
            new HashSet<Sector>(ReferenceEqualityComparer.Instance),
            new HashSet<Linedef>(ReferenceEqualityComparer.Instance));

        SoundEnvironmentPanelHighlightPlan plan = model.HighlightEnvironment(environmentId: 2, selectedEnvironmentId: null);

        Assert.False(plan.IgnoredBecauseSelected);
        Assert.True(plan.CollapseAll);
        Assert.Equal(new[] { 1, 2 }, plan.Environments.Select(environment => environment.EnvironmentId));
        SoundEnvironmentPanelEnvironmentState highlighted = Assert.Single(plan.Environments, environment => environment.EnvironmentId == 2);
        Assert.True(highlighted.Highlighted);
        Assert.True(highlighted.Expanded);
        Assert.True(highlighted.EnsureVisible);
        Assert.False(highlighted.Selected);
        SoundEnvironmentPanelEnvironmentState regular = Assert.Single(plan.Environments, environment => environment.EnvironmentId == 1);
        Assert.False(regular.Highlighted);
        Assert.False(regular.Expanded);
        Assert.False(regular.EnsureVisible);
    }

    [Fact]
    public void SoundEnvironmentPanelHighlightDoesNothingWhileNodeSelected()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        var first = new SoundEnvironmentInfo(
            new HashSet<Sector>(new[] { sector }, ReferenceEqualityComparer.Instance),
            Array.Empty<Thing>(),
            Array.Empty<Linedef>(),
            0xFF010203u,
            1,
            "First");
        var second = first with { Id = 2, Name = "Second" };
        var model = new SoundEnvironmentModeModel(
            new[] { first, second },
            new HashSet<Sector>(ReferenceEqualityComparer.Instance),
            new HashSet<Linedef>(ReferenceEqualityComparer.Instance));

        SoundEnvironmentPanelHighlightPlan plan = model.HighlightEnvironment(environmentId: 2, selectedEnvironmentId: 1);

        Assert.True(plan.IgnoredBecauseSelected);
        Assert.False(plan.CollapseAll);
        Assert.DoesNotContain(plan.Environments, environment => environment.Highlighted || environment.Expanded || environment.EnsureVisible);
        Assert.True(Assert.Single(plan.Environments, environment => environment.EnvironmentId == 1).Selected);
        Assert.False(Assert.Single(plan.Environments, environment => environment.EnvironmentId == 2).Selected);
    }

    [Fact]
    public void SoundEnvironmentPanelSelectionTogglesMatchingEnvironment()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        var environment = new SoundEnvironmentInfo(
            new HashSet<Sector>(new[] { sector }, ReferenceEqualityComparer.Instance),
            Array.Empty<Thing>(),
            Array.Empty<Linedef>(),
            0xFF010203u,
            2,
            "Environment");
        var model = new SoundEnvironmentModeModel(
            new[] { environment },
            new HashSet<Sector>(ReferenceEqualityComparer.Instance),
            new HashSet<Linedef>(ReferenceEqualityComparer.Instance));

        Assert.Equal(2, model.SelectEnvironment(requestedEnvironmentId: 2, selectedEnvironmentId: null));
        Assert.Null(model.SelectEnvironment(requestedEnvironmentId: 2, selectedEnvironmentId: 2));
        Assert.Null(model.SelectEnvironment(requestedEnvironmentId: null, selectedEnvironmentId: 2));
        Assert.Equal(2, model.SelectEnvironment(requestedEnvironmentId: 99, selectedEnvironmentId: 2));
        Assert.Null(model.SelectEnvironment(requestedEnvironmentId: 99, selectedEnvironmentId: null));
    }

    [Fact]
    public void SoundEnvironmentHeaderFormatsEmptySingularAndPluralCounts()
    {
        var map = new MapSet();
        Sector sector = map.AddSector();
        Linedef line = map.AddLinedef(
            map.AddVertex(new Vector2D(0, 0)),
            map.AddVertex(new Vector2D(64, 0)));
        var environment = new SoundEnvironmentInfo(
            new HashSet<Sector>(new[] { sector }, ReferenceEqualityComparer.Instance),
            Array.Empty<Thing>(),
            new[] { line },
            0xFF010203u,
            1,
            "Test Environment");
        var model = new SoundEnvironmentModeModel(
            new[] { environment },
            new HashSet<Sector>(new[] { sector }, ReferenceEqualityComparer.Instance),
            new HashSet<Linedef>(new[] { line }, ReferenceEqualityComparer.Instance));
        var plural = new SoundEnvironmentModeModel(
            new[] { environment, environment },
            new HashSet<Sector>(new[] { sector, map.AddSector() }, ReferenceEqualityComparer.Instance),
            new HashSet<Linedef>(new[] { line, map.AddLinedef(map.AddVertex(new Vector2D(128, 0)), map.AddVertex(new Vector2D(192, 0))) }, ReferenceEqualityComparer.Instance));

        Assert.Equal("No sound environments to display.", model.HeaderText(0));
        Assert.Equal("1 sound environment, 1 unassigned sector, 1 boundary linedef.", model.HeaderText(1));
        Assert.Equal("2 sound environments, 2 unassigned sectors, 2 boundary linedefs.", plural.SummaryText());
    }
}
