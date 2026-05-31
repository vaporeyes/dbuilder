// ABOUTME: Verifies UDB-compatible rendering color collections and pixel color helpers.
// ABOUTME: Covers default palette values, settings keys, and assist color variants.
using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class ColorCollectionTests
{
    [Fact]
    public void DefaultsMatchUdbColorCollection()
    {
        var colors = new ColorCollection();

        Assert.Equal(ColorCollection.NumColors, colors.Colors.Count);
        Assert.Equal(unchecked((int)0xFF000000), colors.Background.ToArgb());
        Assert.Equal(unchecked((int)0xFF51A2FF), colors.Vertices.ToArgb());
        Assert.Equal(unchecked((int)0xFFFFFFFF), colors.Linedefs.ToArgb());
        Assert.Equal(unchecked((int)0xFFBF00DF), colors.ModelWireframe.ToArgb());
        Assert.Equal(unchecked((int)0xFFC6C6FF), colors.InfoLine.ToArgb());
        Assert.Equal(unchecked((int)0xFFFFAC00), colors.Highlight.ToArgb());
        Assert.Equal(unchecked((int)0xFFFF4000), colors.Selection.ToArgb());
        Assert.Equal(unchecked((int)0xFF464646), colors.Grid.ToArgb());
        Assert.Equal(unchecked((int)0xFF39392F), colors.Grid64.ToArgb());
        Assert.Equal(unchecked((int)0xFFFF0000), colors.ThreeDFloor.ToArgb());
        Assert.Equal(unchecked((int)0xFF0099A1), colors.Properties.ToArgb());
    }

    [Fact]
    public void ThingColorsMatchUdbNamedColorDefaults()
    {
        var colors = new ColorCollection();

        Assert.Equal(ColorCollection.ThingColor00Index, ColorCollection.ThingColorsOffset);
        Assert.Equal(unchecked((int)0xFF696969), colors.Colors[ColorCollection.ThingColor00Index].ToArgb());
        Assert.Equal(unchecked((int)0xFF4169E1), colors.Colors[ColorCollection.ThingColor00Index + 1].ToArgb());
        Assert.Equal(unchecked((int)0xFF228B22), colors.Colors[ColorCollection.ThingColor00Index + 2].ToArgb());
        Assert.Equal(unchecked((int)0xFFDAA520), colors.Colors[ColorCollection.ThingColor00Index + 19].ToArgb());
    }

    [Fact]
    public void ConfiguredSettingsOverrideDefaultsButZeroUsesDefaults()
    {
        var settings = new Dictionary<string, int>
        {
            [ColorCollection.SettingKey(ColorCollection.SelectionIndex)] = unchecked((int)0xFF123456),
            [ColorCollection.SettingKey(ColorCollection.HighlightIndex)] = 0,
        };

        var colors = new ColorCollection(settings);

        Assert.Equal(unchecked((int)0xFF123456), colors.Selection.ToArgb());
        Assert.Equal(unchecked((int)0xFFFFAC00), colors.Highlight.ToArgb());
    }

    [Fact]
    public void SaveSettingsUsesUdbColorKeys()
    {
        var settings = new ColorCollection().SaveSettings();

        Assert.Equal(ColorCollection.NumColors, settings.Count);
        Assert.Equal(unchecked((int)0xFF000000), settings["colors.color0"]);
        Assert.Equal(unchecked((int)0xFFFFAC00), settings["colors.color5"]);
        Assert.Equal(unchecked((int)0xFF0099A1), settings["colors.color51"]);
    }

    [Fact]
    public void AssistColorsUseUdbBrightAndDarkVariantMath()
    {
        var color = PixelColor.FromArgb(unchecked((int)0xFF204060));
        var bright = ColorCollection.CreateBrightVariant(color);
        var dark = ColorCollection.CreateDarkVariant(color);

        Assert.Equal(unchecked((int)0xFF86A6C6), bright.ToArgb());
        Assert.Equal(unchecked((int)0xFF000623), dark.ToArgb());
    }

    [Fact]
    public void PixelColorPacksArgbAndPreservesUdbHelpers()
    {
        var color = new PixelColor(0x80, 0x10, 0x20, 0x30);

        Assert.Equal(unchecked((int)0x80102030), color.ToArgb());
        Assert.Equal(color, PixelColor.FromArgb(unchecked((int)0x80102030)));
        Assert.Equal(new PixelColor(0x40, 0x10, 0x20, 0x30), color.WithAlpha(0x40));
        Assert.Equal(0x302010, color.ToInversedColorRef());
        Assert.Equal("[A=128, R=16, G=32, B=48]", color.ToString());
    }
}
