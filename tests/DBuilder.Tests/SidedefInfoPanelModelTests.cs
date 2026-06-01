// ABOUTME: Verifies structured sidedef info rows used by the editor info panel.
// ABOUTME: Covers topology indexes, side orientation, UDMF flags, and custom field counts.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SidedefInfoPanelModelTests
{
    [Fact]
    public void BuildsSidedefInfoRows()
    {
        var map = new MapSet();
        var sector = map.AddSector();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        var side = map.AddSidedef(line, isFront: true, sector);
        side.HighTexture = "UPPER";
        side.MidTexture = "MID";
        side.LowTexture = "LOWER";
        side.OffsetX = 12;
        side.OffsetY = -4;
        side.SetFlag("wrapmidtex", true);
        side.SetFlag("lightabsolute", true);
        side.Fields["comment"] = "door trim";
        map.BuildIndexes();

        SidedefInfoPanelState state = SidedefInfoPanelModel.Build(map, side);

        Assert.Equal("Sidedef 0", state.Header);
        Assert.Equal("front", Field(state, "Side"));
        Assert.Equal("0", Field(state, "Linedef"));
        Assert.Equal("0", Field(state, "Sector"));
        Assert.Equal("90°", Field(state, "Angle"));
        Assert.Equal("UPPER", Field(state, "Upper texture"));
        Assert.Equal("MID", Field(state, "Middle texture"));
        Assert.Equal("LOWER", Field(state, "Lower texture"));
        Assert.Equal("12", Field(state, "Offset X"));
        Assert.Equal("-4", Field(state, "Offset Y"));
        Assert.Equal("lightabsolute, wrapmidtex", Field(state, "UDMF flags"));
        Assert.Equal("1", Field(state, "Custom fields"));
    }

    [Fact]
    public void BuildsBackSidedefInfoRowsWithMissingSectorFallback()
    {
        var map = new MapSet();
        var line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(0, 64)));
        var side = map.AddSidedef(line, isFront: false, null);

        SidedefInfoPanelState state = SidedefInfoPanelModel.Build(map, side);

        Assert.Equal("back", Field(state, "Side"));
        Assert.Equal("0", Field(state, "Linedef"));
        Assert.Equal("-", Field(state, "Sector"));
        Assert.Equal("0°", Field(state, "Angle"));
        Assert.Equal("none", Field(state, "UDMF flags"));
    }

    private static string Field(SidedefInfoPanelState state, string label)
        => Assert.Single(state.Fields, field => field.Label == label).Value;
}
