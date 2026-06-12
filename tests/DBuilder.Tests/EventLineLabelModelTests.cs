// ABOUTME: Verifies UDB-style event-line label visibility and action text formatting.
// ABOUTME: Covers BuilderModes label style defaults for action-only, short args, and full args.

using DBuilder.IO;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class EventLineLabelModelTests
{
    [Theory]
    [InlineData(0, false, false, false)]
    [InlineData(1, true, false, true)]
    [InlineData(2, false, true, true)]
    [InlineData(3, true, true, true)]
    public void LabelVisibilityMatchesUdbBuilderModesValues(
        int visibility,
        bool forward,
        bool reverse,
        bool any)
    {
        Assert.Equal(forward, EventLineLabelModel.ShowForwardLabel(visibility));
        Assert.Equal(reverse, EventLineLabelModel.ShowReverseLabel(visibility));
        Assert.Equal(any, EventLineLabelModel.ShowAnyLabel(visibility));
    }

    [Fact]
    public void LinedefActionDescriptionUsesConfiguredLabelStyle()
    {
        GameConfiguration config = Config(lineTagIndicatesSectors: false);
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(64, 0)))
        {
            Action = 80,
            Args = { [0] = 7, [1] = 1 },
        };

        Assert.Equal("80: Door", EventLineLabelModel.ActionDescription(line, config, labelStyle: 0));
        Assert.Equal("80: Door (7, 1)", EventLineLabelModel.ActionDescription(line, config, labelStyle: 1));
        Assert.Equal("80: Door (Sector tag: 7, Speed: Fast)", EventLineLabelModel.ActionDescription(line, config, labelStyle: 2));
    }

    [Fact]
    public void LineTagIndicatesSectorsForcesActionOnlyLabelLikeUdb()
    {
        GameConfiguration config = Config(lineTagIndicatesSectors: true);
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(64, 0)))
        {
            Action = 80,
            Args = { [0] = 7, [1] = 1 },
        };

        Assert.Equal("80: Door", EventLineLabelModel.ActionDescription(line, config, labelStyle: 2));
    }

    [Fact]
    public void ThingWithoutActionUsesThingArgLabels()
    {
        GameConfiguration config = Config(lineTagIndicatesSectors: false);
        var thing = new Thing(new Vector2D(0, 0), 3001)
        {
            Args = { [0] = 3 },
        };

        Assert.Equal("Thing id: 3", EventLineLabelModel.ThingDescription(thing, config, labelStyle: 2));
        Assert.Equal("3", EventLineLabelModel.ThingDescription(thing, config, labelStyle: 1));
    }

    private static GameConfiguration Config(bool lineTagIndicatesSectors)
    {
        string lineTagIndicatesSectorsValue = lineTagIndicatesSectors ? "true" : "false";
        return GameConfiguration.FromText($$"""
            linetagindicatesectors = {{lineTagIndicatesSectorsValue}};
            enums
            {
                speed
                {
                    0 = "Slow";
                    1 = "Fast";
                }
            }
            linedeftypes
            {
                event
                {
                    80
                    {
                        title = "Door";
                        arg0 { title = "Sector tag"; type = {{(int)UniversalType.SectorTag}}; }
                        arg1 { title = "Speed"; type = {{(int)UniversalType.Integer}}; enum = "speed"; }
                    }
                }
            }
            thingtypes
            {
                monsters
                {
                    3001
                    {
                        title = "Imp";
                        arg0 { title = "Thing id"; type = {{(int)UniversalType.ThingTag}}; }
                    }
                }
            }
            """);
    }
}
