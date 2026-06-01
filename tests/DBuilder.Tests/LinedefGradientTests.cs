// ABOUTME: Tests UDB-style selected-linedef brightness gradients for visible sidedef parts.
// ABOUTME: Covers endpoint validation, averaged sidedefs, and UDMF light field writes.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class LinedefGradientTests
{
    [Fact]
    public void ApplyBrightnessRequiresAtLeastThreeLinedefs()
    {
        var lines = new[] { OneSidedLine(100), OneSidedLine(160) };

        LinedefGradientResult result = LinedefGradient.ApplyBrightness(lines);

        Assert.False(result.Applied);
        Assert.Equal(2, result.LinedefCount);
        Assert.Equal("Select at least 3 linedefs first!", result.Message);
        Assert.DoesNotContain("light", lines[0].Front!.Fields.Keys);
    }

    [Fact]
    public void ApplyBrightnessInterpolatesVisibleOneSidedFronts()
    {
        var lines = new[]
        {
            OneSidedLine(100),
            OneSidedLine(160),
            OneSidedLine(160),
            OneSidedLine(220)
        };

        LinedefGradientResult result = LinedefGradient.ApplyBrightness(lines);

        Assert.True(result.Applied);
        Assert.Equal("Created gradient brightness over selected linedefs.", result.Message);
        Assert.Equal(0, lines[0].Front!.GetIntegerField("light"));
        Assert.Equal(-20, lines[1].Front!.GetIntegerField("light"));
        Assert.Equal(20, lines[2].Front!.GetIntegerField("light"));
        Assert.Equal(0, lines[3].Front!.GetIntegerField("light"));
    }

    [Fact]
    public void ApplyBrightnessUsesAbsoluteLightFieldsDirectly()
    {
        var lines = new[]
        {
            OneSidedLine(64, absolute: true, light: 32),
            OneSidedLine(160, absolute: true),
            OneSidedLine(64, absolute: true, light: 128)
        };

        LinedefGradient.ApplyBrightness(lines);

        Assert.Equal([32, 80, 128], lines.Select(line => line.Front!.GetIntegerField("light")).ToArray());
    }

    [Fact]
    public void ApplyBrightnessAveragesVisibleEndpointSides()
    {
        var first = TwoSidedLine(100, 200);
        var middle = TwoSidedLine(160, 160);
        var last = TwoSidedLine(120, 240);
        first.Front!.SetIntegerField("light", 20);
        first.Back!.SetIntegerField("light", -20);
        last.Front!.SetIntegerField("light", 60);
        last.Back!.SetIntegerField("light", -60);

        LinedefGradient.ApplyBrightness(new[] { first, middle, last });

        Assert.Equal(5, middle.Front!.GetIntegerField("light"));
        Assert.Equal(5, middle.Back!.GetIntegerField("light"));
    }

    [Fact]
    public void ApplyBrightnessReportsStartWithoutVisibleParts()
    {
        var lines = new[] { EmptyTwoSidedLine(), OneSidedLine(160), OneSidedLine(220) };

        LinedefGradientResult result = LinedefGradient.ApplyBrightness(lines);

        Assert.False(result.Applied);
        Assert.Equal("Start linedef doesn't have visible parts!", result.Message);
    }

    [Fact]
    public void ApplyBrightnessReportsEndWithoutVisibleParts()
    {
        var lines = new[] { OneSidedLine(100), OneSidedLine(160), EmptyTwoSidedLine() };

        LinedefGradientResult result = LinedefGradient.ApplyBrightness(lines);

        Assert.False(result.Applied);
        Assert.Equal("End linedef doesn't have visible parts!", result.Message);
    }

    private static Linedef OneSidedLine(int sectorBrightness, bool absolute = false, int light = 0)
    {
        var line = NewLine();
        var side = new Sidedef { Sector = new Sector { Brightness = sectorBrightness, FloorHeight = 0, CeilHeight = 128 } };
        if (absolute) side.SetField("lightabsolute", true);
        side.SetIntegerField("light", light);
        line.AttachFront(side);
        return line;
    }

    private static Linedef TwoSidedLine(int frontBrightness, int backBrightness)
    {
        var line = NewLine();
        var front = new Sidedef
        {
            Sector = new Sector { Brightness = frontBrightness, FloorHeight = 0, CeilHeight = 128 },
            MidTexture = "MID"
        };
        var back = new Sidedef
        {
            Sector = new Sector { Brightness = backBrightness, FloorHeight = 0, CeilHeight = 128 },
            MidTexture = "MID"
        };
        line.AttachFront(front);
        line.AttachBack(back);
        return line;
    }

    private static Linedef EmptyTwoSidedLine()
    {
        var line = NewLine();
        line.AttachFront(new Sidedef { Sector = new Sector { FloorHeight = 0, CeilHeight = 128 } });
        line.AttachBack(new Sidedef { Sector = new Sector { FloorHeight = 0, CeilHeight = 128 } });
        return line;
    }

    private static Linedef NewLine()
        => new(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(128, 0)));
}
