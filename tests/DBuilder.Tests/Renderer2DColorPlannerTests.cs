// ABOUTME: Verifies UDB Renderer2D color selection decisions for things, vertices, and linedefs.
// ABOUTME: Pins selection priority, dynamic light fallback, preset colors, and two-sided alpha behavior.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class Renderer2DColorPlannerTests
{
    [Fact]
    public void DetermineThingColorGivesSelectionPriority()
    {
        PixelColor color = Renderer2DColorPlanner.DetermineThingColor(
            selected: true,
            defaultColor: PixelColor.FromArgb(unchecked((int)0xff101010)),
            selectionColor: PixelColor.FromArgb(unchecked((int)0xffff4000)),
            dynamicLightColor: PixelColor.FromArgb(unchecked((int)0xff010203)));

        Assert.Equal(PixelColor.FromArgb(unchecked((int)0xffff4000)), color);
    }

    [Fact]
    public void DetermineThingColorUsesDynamicLightBeforeDefault()
    {
        PixelColor dynamicLight = Renderer2DColorPlanner.DetermineThingColor(
            selected: false,
            defaultColor: PixelColor.FromArgb(unchecked((int)0xff101010)),
            selectionColor: PixelColor.FromArgb(unchecked((int)0xffff4000)),
            dynamicLightColor: PixelColor.FromArgb(unchecked((int)0xff010203)));
        PixelColor fallback = Renderer2DColorPlanner.DetermineThingColor(
            selected: false,
            defaultColor: PixelColor.FromArgb(unchecked((int)0xff101010)),
            selectionColor: PixelColor.FromArgb(unchecked((int)0xffff4000)));

        Assert.Equal(PixelColor.FromArgb(unchecked((int)0xff010203)), dynamicLight);
        Assert.Equal(PixelColor.FromArgb(unchecked((int)0xff101010)), fallback);
    }

    [Fact]
    public void DetermineVertexColorMatchesUdbColorIndexes()
    {
        Assert.Equal(ColorCollection.SelectionIndex, Renderer2DColorPlanner.DetermineVertexColor(selected: true));
        Assert.Equal(ColorCollection.VerticesIndex, Renderer2DColorPlanner.DetermineVertexColor(selected: false));
    }

    [Fact]
    public void DetermineLinedefColorGivesSelectionPriority()
    {
        PixelColor color = Renderer2DColorPlanner.DetermineLinedefColor(
            selected: true,
            impassable: false,
            linedefColor: PixelColor.FromArgb(unchecked((int)0xffffffff)),
            selectionColor: PixelColor.FromArgb(unchecked((int)0xffff4000)),
            doubleSidedAlpha: 128,
            presetColor: PixelColor.FromArgb(unchecked((int)0xff010203)));

        Assert.Equal(PixelColor.FromArgb(unchecked((int)0xffff4000)), color);
    }

    [Fact]
    public void DetermineLinedefColorKeepsImpassableAlpha()
    {
        PixelColor preset = PixelColor.FromArgb(unchecked((int)0xff010203));

        PixelColor color = Renderer2DColorPlanner.DetermineLinedefColor(
            selected: false,
            impassable: true,
            linedefColor: PixelColor.FromArgb(unchecked((int)0xffffffff)),
            selectionColor: PixelColor.FromArgb(unchecked((int)0xffff4000)),
            doubleSidedAlpha: 128,
            presetColor: preset);

        Assert.Equal(preset, color);
    }

    [Fact]
    public void DetermineLinedefColorAppliesDoubleSidedAlpha()
    {
        PixelColor color = Renderer2DColorPlanner.DetermineLinedefColor(
            selected: false,
            impassable: false,
            linedefColor: PixelColor.FromArgb(unchecked((int)0xff112233)),
            selectionColor: PixelColor.FromArgb(unchecked((int)0xffff4000)),
            doubleSidedAlpha: 64);

        Assert.Equal(PixelColor.FromArgb(unchecked((int)0x40112233)), color);
    }
}
