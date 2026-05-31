// ABOUTME: Tests GZDoom 3D-floor resolution from Sector_3DFloor control sectors to tagged target sectors.
// ABOUTME: Covers multi-tag sectors, managed UDMF controls, and selected-sector shared floor filtering.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ThreeDFloorsTests
{
    [Fact]
    public void ModeDescriptorsMatchUdbEditModeAttributes()
    {
        ThreeDFloorModeDescriptor floor = ThreeDFloors.ModeDescriptor;
        Assert.Equal("3D Floor Mode", floor.DisplayName);
        Assert.Equal("threedfloorhelpermode", floor.SwitchAction);
        Assert.Equal("ThreeDFloorIcon.png", floor.ButtonImage);
        Assert.Equal(int.MinValue + 501, floor.ButtonOrder);
        Assert.Equal("000_editing", floor.ButtonGroup);
        Assert.True(floor.UseByDefault);
        Assert.False(floor.SafeStartMode);
        Assert.False(floor.Volatile);
        Assert.True(floor.AllowCopyPaste);
        Assert.False(floor.Deprecated);
        Assert.Equal(new[] { "HexenMapSetIO", "UniversalMapSetIO" }, floor.SupportedMapFormats);
        Assert.Equal(new[] { "Effect3DFloorSupport" }, floor.RequiredMapFeatures);

        ThreeDFloorModeDescriptor slope = ThreeDFloors.SlopeModeDescriptor;
        Assert.Equal("Slope Mode", slope.DisplayName);
        Assert.Equal("threedslopemode", slope.SwitchAction);
        Assert.Equal("SlopeModeIcon.png", slope.ButtonImage);
        Assert.Equal(new[] { "UniversalMapSetIO" }, slope.SupportedMapFormats);
        Assert.Equal(new[] { "PlaneEquationSupport" }, slope.RequiredMapFeatures);
        Assert.True(slope.Deprecated);

        ThreeDFloorModeDescriptor drawSlopes = ThreeDFloors.DrawSlopesModeDescriptor;
        Assert.Equal("Draw Slopes Mode", drawSlopes.DisplayName);
        Assert.Equal("drawslopesmode", drawSlopes.SwitchAction);
        Assert.Equal("DrawSlopeModeIcon.png", drawSlopes.ButtonImage);
        Assert.False(drawSlopes.AllowCopyPaste);
        Assert.True(drawSlopes.Volatile);
        Assert.True(drawSlopes.Deprecated);
    }

    [Fact]
    public void ActionDescriptorsMatchUdbActionsConfig()
    {
        Assert.Equal("3D Floor Plugin", ThreeDFloors.ActionCategoryTitle);
        Assert.Equal("threedfloorplugin", ThreeDFloors.ActionCategory);

        string[] ids = ThreeDFloors.ActionDescriptors.Select(action => action.Id).ToArray();
        Assert.Equal(
            new[]
            {
                "threedfloorhelpermode",
                "threedslopemode",
                "drawslopesmode",
                "drawslopepoint",
                "drawfloorslope",
                "drawceilingslope",
                "drawfloorandceilingslope",
                "finishslopedraw",
                "threedflipslope",
                "cyclehighlighted3dfloorup",
                "cyclehighlighted3dfloordown",
                "relocate3dfloorcontrolsectors",
                "select3dfloorcontrolsector",
                "duplicate3dfloorgeometry",
            },
            ids);

        ThreeDFloorActionDescriptor drawPoint = ThreeDFloors.ActionDescriptors.Single(action => action.Id == "drawslopepoint");
        Assert.Equal("Draw slope vertex", drawPoint.Title);
        Assert.True(drawPoint.DisregardShift);
        Assert.True(drawPoint.DisregardControl);
        Assert.Equal(1, drawPoint.DefaultInput);

        ThreeDFloorActionDescriptor cycleUp = ThreeDFloors.ActionDescriptors.Single(action => action.Id == "cyclehighlighted3dfloorup");
        Assert.Equal(131066, cycleUp.DefaultInput);
        Assert.True(cycleUp.AllowScroll);
    }

    [Fact]
    public void ControlSectorAreaSettingsUseUdbDefaultsAndKeys()
    {
        var settings = ThreeDFloorControlSectorAreaSettings.FromDictionary(new Dictionary<string, object?>());

        Assert.Equal(-512.0, settings.OuterLeft);
        Assert.Equal(0.0, settings.OuterRight);
        Assert.Equal(512.0, settings.OuterTop);
        Assert.Equal(0.0, settings.OuterBottom);
        Assert.Equal(64.0, settings.GridSize);
        Assert.Equal(56.0, settings.SectorSize);
        Assert.False(settings.UseCustomTagRange);
        Assert.Equal("controlsectorarea", ThreeDFloorControlSectorAreaSettings.PluginName);
        Assert.Equal("usecustomtagrange", ThreeDFloorControlSectorAreaSettings.UseCustomTagRangeKey);
    }

    [Fact]
    public void ControlSectorAreaSettingsReadAndWriteUdbPersistedValues()
    {
        var source = new Dictionary<string, object?>
        {
            [ThreeDFloorControlSectorAreaSettings.UseCustomTagRangeKey] = true,
            [ThreeDFloorControlSectorAreaSettings.FirstTagKey] = 100,
            [ThreeDFloorControlSectorAreaSettings.LastTagKey] = 120,
            [ThreeDFloorControlSectorAreaSettings.OuterLeftKey] = -1024.0,
            [ThreeDFloorControlSectorAreaSettings.OuterRightKey] = 256.0,
            [ThreeDFloorControlSectorAreaSettings.OuterTopKey] = 768.0,
            [ThreeDFloorControlSectorAreaSettings.OuterBottomKey] = -128.0,
        };

        ThreeDFloorControlSectorAreaSettings settings = ThreeDFloorControlSectorAreaSettings.FromDictionary(source);
        var target = new Dictionary<string, object?>();
        settings.WriteTo(target);

        Assert.True(settings.UseCustomTagRange);
        Assert.Equal(100, settings.FirstTag);
        Assert.Equal(120, settings.LastTag);
        Assert.Equal(-1024.0, settings.OuterLeft);
        Assert.Equal(256.0, settings.OuterRight);
        Assert.Equal(768.0, settings.OuterTop);
        Assert.Equal(-128.0, settings.OuterBottom);
        Assert.Equal(true, target[ThreeDFloorControlSectorAreaSettings.UseCustomTagRangeKey]);
        Assert.Equal(100, target[ThreeDFloorControlSectorAreaSettings.FirstTagKey]);
        Assert.Equal(120, target[ThreeDFloorControlSectorAreaSettings.LastTagKey]);
    }

    [Fact]
    public void ControlSectorAreaSettingsSkipTagRangeWhenDisabled()
    {
        var target = new Dictionary<string, object?>
        {
            [ThreeDFloorControlSectorAreaSettings.FirstTagKey] = 10,
            [ThreeDFloorControlSectorAreaSettings.LastTagKey] = 12,
        };

        new ThreeDFloorControlSectorAreaSettings(UseCustomTagRange: false).WriteTo(target);

        Assert.Equal(false, target[ThreeDFloorControlSectorAreaSettings.UseCustomTagRangeKey]);
        Assert.False(target.ContainsKey(ThreeDFloorControlSectorAreaSettings.FirstTagKey));
        Assert.False(target.ContainsKey(ThreeDFloorControlSectorAreaSettings.LastTagKey));
    }

    [Fact]
    public void ControlSectorAreaSettingsGetNewSectorTagUsesCustomRangeBeforeMapFallback()
    {
        var map = new MapSet();
        map.AddSector().Tag = 10;
        map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0))).Tag = 11;
        map.AddThing(new Vector2D(0, 0), 3001).Tag = 12;
        var custom = new ThreeDFloorControlSectorAreaSettings(UseCustomTagRange: true, FirstTag: 10, LastTag: 13);
        var fallback = new ThreeDFloorControlSectorAreaSettings();

        Assert.Equal(11, custom.GetNewSectorTag(map));
        Assert.Equal(12, custom.GetNewSectorTag(map, new[] { 11 }));
        Assert.Equal(1, fallback.GetNewSectorTag(map));
    }

    [Fact]
    public void ControlSectorAreaSettingsThrowsWhenCustomRangeIsFull()
    {
        var map = new MapSet();
        map.AddSector().Tag = 10;
        map.AddSector().Tag = 11;
        var settings = new ThreeDFloorControlSectorAreaSettings(UseCustomTagRange: true, FirstTag: 10, LastTag: 11);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => settings.GetNewSectorTag(map));

        Assert.Equal("No free tags in the custom range between 10 and 11.", ex.Message);
    }

    [Fact]
    public void ControlSectorAreaSettingsPlansClosedControlSectorLoops()
    {
        var map = new MapSet();
        var settings = new ThreeDFloorControlSectorAreaSettings();

        IReadOnlyList<IReadOnlyList<Vector2D>> loops = settings.GetNewControlSectorVertexLoops(map, 2);

        Assert.Equal(2, loops.Count);
        Assert.Equal(
            new[]
            {
                new Vector2D(-508, 508),
                new Vector2D(-452, 508),
                new Vector2D(-452, 452),
                new Vector2D(-508, 452),
                new Vector2D(-508, 508),
            },
            loops[0]);
        Assert.Equal(new Vector2D(-508, 444), loops[1][0]);
    }

    [Fact]
    public void ControlSectorAreaSettingsSkipsOccupiedControlSectorCells()
    {
        var map = new MapSet();
        AddSquareSector(map, -508, 508, 56);
        var settings = new ThreeDFloorControlSectorAreaSettings();

        IReadOnlyList<IReadOnlyList<Vector2D>> loops = settings.GetNewControlSectorVertexLoops(map, 1);

        Assert.Equal(new Vector2D(-508, 444), loops[0][0]);
    }

    [Fact]
    public void ControlSectorAreaSettingsRelocationIgnoresActiveManagedControls()
    {
        var map = new MapSet();
        Sector control = AddSquareSector(map, -508, 508, 56);
        control.Fields[ThreeDFloorControlSectorAreaSettings.ManagedControlSectorField] = true;
        var settings = new ThreeDFloorControlSectorAreaSettings();

        IReadOnlyList<Vector2D> relocate = settings.GetRelocatePositions(map, 1, new HashSet<Sector> { control });
        IReadOnlyList<IReadOnlyList<Vector2D>> create = settings.GetNewControlSectorVertexLoops(map, 1);

        Assert.Equal(new Vector2D(-508, 508), relocate[0]);
        Assert.Equal(new Vector2D(-508, 444), create[0][0]);
    }

    [Fact]
    public void ControlSectorAreaSettingsThrowsWhenNoPlacementFits()
    {
        var map = new MapSet();
        AddSquareSector(map, -508, 508, 56);
        var settings = new ThreeDFloorControlSectorAreaSettings(OuterLeft: -512, OuterRight: -448, OuterTop: 512, OuterBottom: 448);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => settings.GetNewControlSectorVertexLoops(map, 1));

        Assert.Equal("No space left for control sectors.", ex.Message);
    }

    // A control sector carrying special 160 (arg0=tag) and a target sector tagged the same.
    private static (MapSet map, Sector control, Sector target) Setup(int tag, int alpha)
    {
        var map = new MapSet();
        var control = map.AddSector();
        control.FloorHeight = 0; control.CeilHeight = 32;
        control.FloorTexture = "FFLAT"; control.CeilTexture = "CFLAT"; control.Brightness = 200;

        var target = map.AddSector();
        target.Tag = tag;

        // A linedef inside the control sector with the 3D-floor special.
        var v0 = map.AddVertex(new Vector2D(0, 0));
        var v1 = map.AddVertex(new Vector2D(64, 0));
        var line = map.AddLinedef(v0, v1);
        var sd = map.AddSidedef(line, true, control);
        sd.MidTexture = "SIDETEX";
        line.Action = ThreeDFloors.Sector3DFloorAction;
        line.Args[0] = tag;
        line.Args[1] = 1;       // solid type
        line.Args[3] = alpha;

        // Give the target a couple of sidedefs so it is a real sector (not required by Resolve).
        map.BuildIndexes();
        return (map, control, target);
    }

    [Fact]
    public void ResolvesSlabIntoTaggedSector()
    {
        var (map, control, target) = Setup(tag: 5, alpha: 160);
        var result = ThreeDFloors.Resolve(map);

        Assert.True(result.ContainsKey(target));
        var f = result[target][0];
        Assert.Same(control, f.Control);
        Assert.Equal(0, f.Bottom, 3);
        Assert.Equal(32, f.Top, 3);
        Assert.Equal(160, f.Alpha);
        Assert.Equal("CFLAT", f.TopFlat);     // control ceiling flat = slab top
        Assert.Equal("FFLAT", f.BottomFlat);  // control floor flat = slab bottom
        Assert.Equal("SIDETEX", f.SideTexture);
        Assert.Equal(200, f.Brightness);
    }

    [Fact]
    public void NonMatchingTagYieldsNothing()
    {
        var (map, _, target) = Setup(tag: 5, alpha: 255);
        target.Tag = 6; // no line targets tag 6
        map.BuildIndexes();
        Assert.Empty(ThreeDFloors.Resolve(map));
    }

    [Fact]
    public void NonSpecialLineIsIgnored()
    {
        var (map, _, _) = Setup(tag: 5, alpha: 255);
        foreach (var l in map.Linedefs) l.Action = 0; // remove the 3D-floor special
        Assert.Empty(ThreeDFloors.Resolve(map));
    }

    [Fact]
    public void AlphaIsClamped()
    {
        var (map, _, target) = Setup(tag: 7, alpha: 999);
        var f = ThreeDFloors.Resolve(map)[target][0];
        Assert.Equal(255, f.Alpha);
    }

    [Fact]
    public void ResolveUsesAllSectorTags()
    {
        var (map, control, target) = Setup(tag: 5, alpha: 255);
        target.Tags.Clear();
        target.Tags.Add(0);
        target.Tags.Add(5);

        var result = ThreeDFloors.Resolve(map);

        Assert.True(result.ContainsKey(target));
        Assert.Same(control, result[target][0].Control);
        Assert.Equal(5, result[target][0].TargetTag);
    }

    [Fact]
    public void ManagedUdmfControlFilterSkipsUnmanagedSectors()
    {
        var (map, _, target) = Setup(tag: 5, alpha: 255);

        Assert.Empty(ThreeDFloors.Resolve(map, udmf: true, requireManagedControlSector: true));

        map.Linedefs[0].Front!.Sector!.Fields["user_managed_3d_floor"] = true;

        Assert.True(ThreeDFloors.Resolve(map, udmf: true, requireManagedControlSector: true).ContainsKey(target));
    }

    [Fact]
    public void GetThreeDFloorsReturnsSelectedSectorControls()
    {
        var (map, control, target) = Setup(tag: 5, alpha: 255);
        var other = map.AddSector();
        other.Tag = 5;

        var floors = ThreeDFloors.GetThreeDFloors(map, new[] { target, other });

        var floor = Assert.Single(floors);
        Assert.Same(control, floor.Control);
    }

    [Fact]
    public void GetThreeDFloorsCanFilterToFloorsSharedByAllSelectedSectors()
    {
        var (map, sharedControl, first) = Setup(tag: 5, alpha: 255);
        var second = map.AddSector();
        second.Tag = 5;
        var privateControl = AddControlFloor(map, tag: 9);
        first.Tags.Add(9);

        var all = ThreeDFloors.GetThreeDFloors(map, new[] { first, second });
        var shared = ThreeDFloors.GetThreeDFloors(map, new[] { first, second }, sharedOnly: true);

        Assert.Equal(2, all.Count);
        Assert.Contains(all, floor => ReferenceEquals(privateControl, floor.Control));
        var sharedFloor = Assert.Single(shared);
        Assert.Same(sharedControl, sharedFloor.Control);
    }

    [Fact]
    public void ApplyControlEditUpdatesControlSectorAndActionLines()
    {
        var (map, control, target) = Setup(tag: 5, alpha: 128);
        ThreeDFloor floor = ThreeDFloors.Resolve(map)[target][0];
        ThreeDFloorControlEdit edit = ThreeDFloors.CreateControlEdit(floor) with
        {
            BottomHeight = -16,
            TopHeight = 48,
            BottomFlat = "NEWFLR",
            TopFlat = "NEWCEIL",
            SideTexture = "NEWSIDE",
            Type = 2,
            Flags = 4,
            Alpha = 999,
            Brightness = 144,
            Tags = new[] { 12, 13 },
        };

        int updated = ThreeDFloors.ApplyControlEdit(control, edit);

        Assert.Equal(1, updated);
        Assert.Equal(-16, control.FloorHeight);
        Assert.Equal(48, control.CeilHeight);
        Assert.Equal("NEWFLR", control.FloorTexture);
        Assert.Equal("NEWCEIL", control.CeilTexture);
        Assert.Equal(144, control.Brightness);
        Assert.Equal(new[] { 12, 13 }, control.Tags);
        Assert.Equal("NEWSIDE", control.Sidedefs[0].MidTexture);
        Assert.Equal(2, map.Linedefs[0].Args[1]);
        Assert.Equal(4, map.Linedefs[0].Args[2]);
        Assert.Equal(255, map.Linedefs[0].Args[3]);
    }

    [Fact]
    public void CleanupControlSectorClearsOrphanedActionAndDeletesUnusedControlSector()
    {
        var (map, control, target) = Setup(tag: 5, alpha: 255);
        target.Tags.Clear();

        ThreeDFloorCleanupResult result = ThreeDFloors.CleanupControlSector(map, control);

        Assert.Equal(1, result.ClearedLines);
        Assert.True(result.ControlSectorDeleted);
        Assert.DoesNotContain(control, map.Sectors);
        Assert.Empty(map.Linedefs);
        Assert.Empty(map.Sidedefs);
    }

    [Fact]
    public void CleanupControlSectorKeepsControlSectorWhenAnotherActionLineRemains()
    {
        var (map, control, target) = Setup(tag: 5, alpha: 255);
        target.Tags.Clear();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 32)), map.AddVertex(new Vector2D(64, 32)));
        var side = map.AddSidedef(line, true, control);
        side.MidTexture = "OTHER";
        line.Action = 80;
        map.BuildIndexes();

        ThreeDFloorCleanupResult result = ThreeDFloors.CleanupControlSector(map, control);

        Assert.Equal(1, result.ClearedLines);
        Assert.False(result.ControlSectorDeleted);
        Assert.Contains(control, map.Sectors);
        Assert.Equal(0, map.Linedefs[0].Action);
        Assert.Equal(80, map.Linedefs[1].Action);
    }

    private static Sector AddControlFloor(MapSet map, int tag)
    {
        var control = map.AddSector();
        control.FloorHeight = 8;
        control.CeilHeight = 24;
        var line = map.AddLinedef(map.AddVertex(new Vector2D(tag * 64, 0)), map.AddVertex(new Vector2D(tag * 64 + 32, 0)));
        var side = map.AddSidedef(line, true, control);
        side.MidTexture = "SIDE" + tag;
        line.Action = ThreeDFloors.Sector3DFloorAction;
        line.Args[0] = tag;
        line.Args[3] = 255;
        map.BuildIndexes();
        return control;
    }

    private static Sector AddSquareSector(MapSet map, double left, double top, double size)
    {
        var vertices = new[]
        {
            map.AddVertex(new Vector2D(left, top)),
            map.AddVertex(new Vector2D(left + size, top)),
            map.AddVertex(new Vector2D(left + size, top - size)),
            map.AddVertex(new Vector2D(left, top - size)),
        };

        Sector sector = SectorBuilder.CreateSector(map, vertices)!;
        map.BuildIndexes();
        return sector;
    }
}
