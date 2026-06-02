// ABOUTME: Verifies structured linedef info rows used by the editor info panel.
// ABOUTME: Covers configured actions, flags, tags, side references, textures, offsets, and args.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class LinedefInfoPanelModelTests
{
    [Fact]
    public void BuildsTwoSidedLinedefInfoRowsWithConfigurationAndArgs()
    {
        var map = new MapSet();
        Sector frontSector = map.AddSector();
        Sector backSector = map.AddSector();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(64, 0)));
        Sidedef front = map.AddSidedef(line, isFront: true, frontSector);
        Sidedef back = map.AddSidedef(line, isFront: false, backSector);
        front.HighTexture = "F_UP";
        front.MidTexture = "F_MID";
        front.LowTexture = "F_LOW";
        front.OffsetX = 12;
        front.OffsetY = -4;
        back.HighTexture = "B_UP";
        back.MidTexture = "B_MID";
        back.LowTexture = "B_LOW";
        back.OffsetX = -8;
        back.OffsetY = 16;
        line.Action = 80;
        line.Flags = 1 | 4;
        line.Tags.AddRange(new[] { 11, 12 });
        line.Args[0] = 3;
        line.Args[1] = 9;
        line.SetFlag("blocking", true);
        line.SetFlag("repeatable", true);
        line.Groups = MapSet.GroupMask(1) | MapSet.GroupMask(4);
        line.Fields["comment"] = "switch";
        map.BuildIndexes();
        var config = GameConfiguration.FromText("""
        linedeftypes
        {
            script
            {
                80
                {
                    title = "Run Script";
                    arg0 { title = "Script"; }
                }
            }
        }
        linedefflags
        {
            1 = "Impassable";
            4 = "Double Sided";
        }
        """);

        LinedefInfoPanelState state = LinedefInfoPanelModel.Build(map, line, config, hasArgs: true);

        Assert.Equal("Linedef 0", state.Header);
        Assert.Equal("80 - Run Script", Field(state, "Action"));
        Assert.Equal("11, 12", Field(state, "Tags"));
        Assert.Equal("64", Field(state, "Length"));
        Assert.Equal("90°", Field(state, "Angle"));
        Assert.Equal("two-sided", Field(state, "Sides"));
        Assert.Equal("0", Field(state, "Front sector"));
        Assert.Equal("1", Field(state, "Back sector"));
        Assert.Equal("U:F_UP M:F_MID L:F_LOW", Field(state, "Front textures"));
        Assert.Equal("U:B_UP M:B_MID L:B_LOW", Field(state, "Back textures"));
        Assert.Equal("12, -4", Field(state, "Front offsets"));
        Assert.Equal("-8, 16", Field(state, "Back offsets"));
        Assert.Equal("Impassable, Double Sided", Field(state, "Flags"));
        Assert.Equal("blocking, repeatable", Field(state, "UDMF flags"));
        Assert.Equal("2, 5", Field(state, "Groups"));
        Assert.Equal("1", Field(state, "Custom fields"));
        Assert.Equal("3", Field(state, "Arg1 (Script)"));
        Assert.Equal("9", Field(state, "Arg2"));
    }

    [Fact]
    public void BuildsOneSidedLinedefInfoRowsWithFallbacks()
    {
        var map = new MapSet();
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(0, 32)))
        {
            Flags = 0x0005,
        };

        LinedefInfoPanelState state = LinedefInfoPanelModel.Build(map, line, hasArgs: false);

        Assert.Equal("Linedef", state.Header);
        Assert.Equal("0 - None", Field(state, "Action"));
        Assert.Equal("0", Field(state, "Tags"));
        Assert.Equal("32", Field(state, "Length"));
        Assert.Equal("180°", Field(state, "Angle"));
        Assert.Equal("one-sided", Field(state, "Sides"));
        Assert.Equal("-", Field(state, "Front sector"));
        Assert.Equal("-", Field(state, "Back sector"));
        Assert.Equal("-", Field(state, "Front textures"));
        Assert.Equal("-", Field(state, "Back textures"));
        Assert.Equal("0x0005", Field(state, "Flags"));
        Assert.Equal("none", Field(state, "UDMF flags"));
        Assert.Equal("-", Field(state, "Groups"));
        Assert.DoesNotContain(state.Fields, field => field.Label.StartsWith("Arg", StringComparison.Ordinal));
    }

    [Fact]
    public void UsesUnknownActionFallbackWithoutConfiguration()
    {
        var map = new MapSet();
        Linedef line = map.AddLinedef(map.AddVertex(new Vector2D(0, 0)), map.AddVertex(new Vector2D(1, 0)));
        line.Action = 17;

        LinedefInfoPanelState state = LinedefInfoPanelModel.Build(map, line, hasArgs: true);

        Assert.Equal("17 - action 17", Field(state, "Action"));
    }

    private static string Field(LinedefInfoPanelState state, string label)
        => Assert.Single(state.Fields, field => field.Label == label).Value;
}
