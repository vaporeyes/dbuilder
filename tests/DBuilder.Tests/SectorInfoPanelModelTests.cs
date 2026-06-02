// ABOUTME: Verifies structured sector info rows used by the editor info panel.
// ABOUTME: Covers configured effect labels, tags, groups, slopes, and custom field counts.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class SectorInfoPanelModelTests
{
    [Fact]
    public void BuildsSectorInfoRowsWithConfiguredEffect()
    {
        var sector = new Sector
        {
            Index = 7,
            FloorHeight = -16,
            CeilHeight = 128,
            FloorTexture = "FLOOR1",
            CeilTexture = "CEIL1",
            Brightness = 192,
            Special = 9,
            Groups = MapSet.GroupMask(0) | MapSet.GroupMask(2),
            FloorSlope = new Vector3D(0, 1, 0.5),
            FloorSlopeOffset = 12.25,
            CeilSlope = new Vector3D(0, -1, 1),
        };
        sector.Tags.AddRange(new[] { 3, 5 });
        sector.Fields["comment"] = "secret";
        var config = GameConfiguration.FromText("""
        sectortypes
        {
            0 = "None";
            9 = "Secret";
        }
        """);

        SectorInfoPanelState state = SectorInfoPanelModel.Build(sector, config);

        Assert.Equal("Sector 7", state.Header);
        Assert.Equal("-16", Field(state, "Floor height"));
        Assert.Equal("128", Field(state, "Ceiling height"));
        Assert.Equal("FLOOR1", Field(state, "Floor texture"));
        Assert.Equal("CEIL1", Field(state, "Ceiling texture"));
        Assert.Equal("192", Field(state, "Brightness"));
        Assert.Equal("9 - Secret", Field(state, "Effect"));
        Assert.Equal("3, 5", Field(state, "Tags"));
        Assert.Equal("0", Field(state, "Sidedefs"));
        Assert.Equal("1, 3", Field(state, "Groups"));
        Assert.Equal("(0, 1, 0.5) d 12.25", Field(state, "Floor slope"));
        Assert.Equal("(0, -1, 1) d -", Field(state, "Ceiling slope"));
        Assert.Equal("1", Field(state, "Custom fields"));
    }

    [Fact]
    public void BuildsSectorInfoRowsWithFallbacks()
    {
        var sector = new Sector { Index = 2 };

        SectorInfoPanelState state = SectorInfoPanelModel.Build(sector);

        Assert.Equal("Sector 2", state.Header);
        Assert.Equal("0 - None", Field(state, "Effect"));
        Assert.Equal("0", Field(state, "Tags"));
        Assert.Equal("-", Field(state, "Groups"));
        Assert.Equal("flat", Field(state, "Floor slope"));
        Assert.Equal("flat", Field(state, "Ceiling slope"));
        Assert.Equal("0", Field(state, "Custom fields"));
    }

    [Fact]
    public void UsesUnknownEffectFallbackWithoutConfiguration()
    {
        var sector = new Sector { Special = 17 };

        SectorInfoPanelState state = SectorInfoPanelModel.Build(sector);

        Assert.Equal("17 - effect 17", Field(state, "Effect"));
    }

    private static string Field(SectorInfoPanelState state, string label)
        => Assert.Single(state.Fields, field => field.Label == label).Value;
}
