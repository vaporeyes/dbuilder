// ABOUTME: Verifies structured thing info rows used by the editor info panel.
// ABOUTME: Covers configured labels, flags, argument rows, UDMF flags, and Doom-format arg omission.

using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ThingInfoPanelModelTests
{
    [Fact]
    public void BuildsThingInfoRowsWithConfigurationAndArgs()
    {
        var map = new MapSet();
        Thing thing = map.AddThing(new Vector2D(12.4, -8.6), 3001);
        thing.Height = 16.2;
        thing.Angle = 90;
        thing.Pitch = 5;
        thing.Roll = -10;
        thing.ScaleX = 1.25;
        thing.ScaleY = 0.5;
        thing.Tag = 42;
        thing.Action = 80;
        thing.Flags = 1 | 8;
        thing.Args[0] = 7;
        thing.Args[1] = 3;
        thing.SetFlag("friendly", true);
        thing.SetFlag("ambush", true);
        thing.Groups = MapSet.GroupMask(0) | MapSet.GroupMask(3);
        thing.Fields["comment"] = "patrol";
        var config = GameConfiguration.FromText("""
        thingtypes
        {
            monsters
            {
                3001
                {
                    title = "Imp";
                    arg0 { title = "Target"; }
                }
            }
        }
        linedeftypes
        {
            script
            {
                80 { title = "Run Script"; }
            }
        }
        thingflags
        {
            1 = "Easy";
            8 = "Ambush players";
        }
        """);

        ThingInfoPanelState state = ThingInfoPanelModel.Build(map, thing, config, hasArgs: true);

        Assert.Equal("Thing 0", state.Header);
        Assert.Equal("3001 - Imp", Field(state, "Type"));
        Assert.Equal("80 - Run Script", Field(state, "Action"));
        Assert.Equal("(12, -9, 16)", Field(state, "Position"));
        Assert.Equal("90°", Field(state, "Angle"));
        Assert.Equal("5° / -10°", Field(state, "Pitch / roll"));
        Assert.Equal("1.25 x 0.5", Field(state, "Scale"));
        Assert.Equal("42", Field(state, "Tag"));
        Assert.Equal("Easy, Ambush players", Field(state, "Flags"));
        Assert.Equal("ambush, friendly", Field(state, "UDMF flags"));
        Assert.Equal("1, 4", Field(state, "Groups"));
        Assert.Equal("1", Field(state, "Custom fields"));
        Assert.Equal("7", Field(state, "Arg1 (Target)"));
        Assert.Equal("3", Field(state, "Arg2"));
    }

    [Fact]
    public void BuildsThingInfoRowsWithFallbacks()
    {
        var map = new MapSet();
        var thing = new Thing(new Vector2D(1, 2), 9999)
        {
            Flags = 0x0005,
        };

        ThingInfoPanelState state = ThingInfoPanelModel.Build(map, thing, hasArgs: false);

        Assert.Equal("Thing", state.Header);
        Assert.Equal("9999 - type 9999", Field(state, "Type"));
        Assert.Equal("0 - None", Field(state, "Action"));
        Assert.Equal("0x0005", Field(state, "Flags"));
        Assert.Equal("none", Field(state, "UDMF flags"));
        Assert.Equal("-", Field(state, "Groups"));
        Assert.DoesNotContain(state.Fields, field => field.Label.StartsWith("Arg", StringComparison.Ordinal));
    }

    [Fact]
    public void UsesUnknownActionFallbackWithoutConfiguration()
    {
        var map = new MapSet();
        var thing = map.AddThing(new Vector2D(), 1);
        thing.Action = 17;

        ThingInfoPanelState state = ThingInfoPanelModel.Build(map, thing, hasArgs: true);

        Assert.Equal("17 - action 17", Field(state, "Action"));
    }

    private static string Field(ThingInfoPanelState state, string label)
        => Assert.Single(state.Fields, field => field.Label == label).Value;
}
