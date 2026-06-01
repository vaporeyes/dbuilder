// ABOUTME: Tests UDB-style copy/paste-properties clipboard behavior for selected map elements.
// ABOUTME: Covers property snapshots, selected targets, and option-filtered paste operations.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class PastePropertiesClipboardTests
{
    [Fact]
    public void CopySelectedThingStoresSnapshotIndependentFromSourceChanges()
    {
        MapSet map = new();
        Thing source = map.AddThing(new Vector2D(0, 0), 3004);
        source.Angle = 90;
        source.Selected = true;
        source.Tag = 7;

        PastePropertiesClipboard clipboard = new();
        PastePropertiesCopyResult copy = clipboard.CopySelected(map, PastePropertiesElementKind.Thing);
        source.Tag = 99;
        source.Selected = false;
        Thing target = map.AddThing(new Vector2D(64, 0), 1);
        target.Selected = true;

        PastePropertiesApplyResult paste = clipboard.ApplySelected(
            map,
            PastePropertiesElementKind.Thing,
            enabledKeys: Keys(PastePropertiesKeys.ThingTag));

        Assert.True(copy.Copied);
        Assert.True(paste.Applied);
        Assert.Equal(1, paste.TargetCount);
        Assert.Equal(7, target.Tag);
    }

    [Fact]
    public void ApplySelectedSectorUsesOnlyEnabledProperties()
    {
        MapSet map = new();
        Sector source = map.AddSector();
        source.Selected = true;
        source.FloorHeight = 24;
        source.CeilHeight = 160;
        source.FloorTexture = "FLOOR0_1";
        PastePropertiesClipboard clipboard = new();
        clipboard.CopySelected(map, PastePropertiesElementKind.Sector);

        source.Selected = false;
        Sector target = map.AddSector();
        target.Selected = true;
        target.FloorHeight = 0;
        target.CeilHeight = 96;
        target.FloorTexture = "OLD";

        PastePropertiesApplyResult paste = clipboard.ApplySelected(
            map,
            PastePropertiesElementKind.Sector,
            enabledKeys: Keys(PastePropertiesKeys.SectorFloorHeight));

        Assert.True(paste.Applied);
        Assert.Equal(24, target.FloorHeight);
        Assert.Equal(96, target.CeilHeight);
        Assert.Equal("OLD", target.FloorTexture);
    }

    [Fact]
    public void ApplySelectedLinedefAlsoCopiesMatchingSidedefProperties()
    {
        MapSet map = new();
        Linedef source = AddLineWithSides(map, 0, 0, 64, 0);
        source.Selected = true;
        source.Action = 80;
        source.Front!.MidTexture = "STARTAN3";

        PastePropertiesClipboard clipboard = new();
        clipboard.CopySelected(map, PastePropertiesElementKind.Linedef);

        source.Selected = false;
        Linedef target = AddLineWithSides(map, 0, 64, 64, 64);
        target.Selected = true;
        target.Action = 1;
        target.Front!.MidTexture = "OLD";

        PastePropertiesApplyResult paste = clipboard.ApplySelected(
            map,
            PastePropertiesElementKind.Linedef,
            enabledKeys: Keys(PastePropertiesKeys.LinedefAction, PastePropertiesKeys.SidedefMiddleTexture));

        Assert.True(paste.Applied);
        Assert.Equal(80, target.Action);
        Assert.Equal("STARTAN3", target.Front.MidTexture);
    }

    [Fact]
    public void ApplySelectedSidedefCanUseCopiedLinedefFrontSide()
    {
        MapSet map = new();
        Linedef source = AddLineWithSides(map, 0, 0, 64, 0);
        source.Selected = true;
        source.Front!.HighTexture = "STONE2";

        PastePropertiesClipboard clipboard = new();
        clipboard.CopySelected(map, PastePropertiesElementKind.Linedef);

        source.Selected = false;
        Linedef targetLine = AddLineWithSides(map, 0, 64, 64, 64);
        targetLine.Front!.Selected = true;
        targetLine.Front.HighTexture = "OLD";

        PastePropertiesApplyResult paste = clipboard.ApplySelected(
            map,
            PastePropertiesElementKind.Sidedef,
            enabledKeys: Keys(PastePropertiesKeys.SidedefUpperTexture));

        Assert.True(paste.Applied);
        Assert.Equal("STONE2", targetLine.Front.HighTexture);
    }

    [Fact]
    public void BuildOptionsReflectsCopiedStateAndCurrentMapFormat()
    {
        MapSet map = new();
        Vertex vertex = map.AddVertex(new Vector2D(0, 0));
        vertex.Selected = true;
        PastePropertiesClipboard clipboard = new();
        clipboard.CopySelected(map, PastePropertiesElementKind.Vertex);

        PastePropertiesOptionsResult options = clipboard.BuildOptions(
            [PastePropertiesElementKind.Vertex],
            supportsUdmf: false);

        Assert.False(options.IsAvailable);
        Assert.Equal(PastePropertiesOptionsModel.NoSupportedPropertiesMessage, options.StatusMessage);
    }

    [Fact]
    public void ApplySelectedReportsMissingCopiedProperties()
    {
        MapSet map = new();
        Thing target = map.AddThing(new Vector2D(0, 0), 1);
        target.Selected = true;
        PastePropertiesClipboard clipboard = new();

        PastePropertiesApplyResult result = clipboard.ApplySelected(map, PastePropertiesElementKind.Thing);

        Assert.False(result.Applied);
        Assert.Equal("Copy thing properties first!", result.StatusMessage);
    }

    private static ISet<string> Keys(params string[] keys)
        => keys.ToHashSet(StringComparer.Ordinal);

    private static Linedef AddLineWithSides(MapSet map, double x1, double y1, double x2, double y2)
    {
        Vertex start = map.AddVertex(new Vector2D(x1, y1));
        Vertex end = map.AddVertex(new Vector2D(x2, y2));
        Linedef line = map.AddLinedef(start, end);
        line.AttachFront(map.AddSidedef(line, isFront: true, sector: null));
        line.AttachBack(map.AddSidedef(line, isFront: false, sector: null));
        return line;
    }
}
