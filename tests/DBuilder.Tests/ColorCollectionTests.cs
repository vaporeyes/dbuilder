// ABOUTME: Verifies UDB-compatible rendering color collections and pixel color helpers.
// ABOUTME: Covers default palette values, settings keys, and assist color variants.
using DBuilder.Rendering;
using System.Drawing;

namespace DBuilder.Tests;

public sealed class ColorCollectionTests
{
    [Fact]
    public void ColorSettingMatchesUdbNamedColorSurface()
    {
        var setting = new ColorSetting("Highlight", new PixelColor(255, 0x11, 0x22, 0x33));

        Assert.Equal("Highlight", setting.Name);
        Assert.Equal(new PixelColor(255, 0x11, 0x22, 0x33), setting.PixelColor);
        Assert.Equal(Color.FromArgb(unchecked((int)0xFF112233)), setting.Color);
        Assert.Equal(new PixelColor(0x40, 0x11, 0x22, 0x33), setting.WithAlpha(0x40));

        PixelColor pixel = setting;
        Color color = setting;

        Assert.Equal(setting.PixelColor, pixel);
        Assert.Equal(setting.Color, color);

        setting.Color = Color.FromArgb(unchecked((int)0x80102030));
        Assert.Equal(new PixelColor(0x80, 0x10, 0x20, 0x30), setting.PixelColor);

        setting.PixelColor = new PixelColor(255, 1, 2, 3);
        Assert.Equal(Color.FromArgb(unchecked((int)0xFF010203)), setting.Color);
    }

    [Fact]
    public void ColorSettingEqualityUsesNameLikeUdb()
    {
        var first = new ColorSetting("Same", new PixelColor(255, 1, 2, 3));
        var second = new ColorSetting("Same", new PixelColor(255, 9, 8, 7));
        var different = new ColorSetting("Different", new PixelColor(255, 1, 2, 3));

        Assert.True(first.Equals(second));
        Assert.True(first.Equals((object)second));
        Assert.False(first.Equals(different));
        Assert.False(first.Equals(null));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

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
    public void UdbColorCollectionConstantsMatchIndexAliases()
    {
        Assert.Equal(ColorCollection.NumThingColors, ColorCollection.NUM_THING_COLORS);
        Assert.Equal(ColorCollection.ThingColorsOffset, ColorCollection.THING_COLORS_OFFSET);
        Assert.Equal(ColorCollection.BackgroundIndex, ColorCollection.BACKGROUND);
        Assert.Equal(ColorCollection.VerticesIndex, ColorCollection.VERTICES);
        Assert.Equal(ColorCollection.LinedefsIndex, ColorCollection.LINEDEFS);
        Assert.Equal(ColorCollection.ModelWireColorIndex, ColorCollection.MODELWIRECOLOR);
        Assert.Equal(ColorCollection.InfoLineColorIndex, ColorCollection.INFOLINECOLOR);
        Assert.Equal(ColorCollection.HighlightIndex, ColorCollection.HIGHLIGHT);
        Assert.Equal(ColorCollection.SelectionIndex, ColorCollection.SELECTION);
        Assert.Equal(ColorCollection.ThingColor00Index, ColorCollection.THINGCOLOR00);
        Assert.Equal(ColorCollection.ThingColor00Index + 19, ColorCollection.THINGCOLOR19);
        Assert.Equal(ColorCollection.ThreeDFloorColorIndex, ColorCollection.THREEDFLOORCOLOR);
        Assert.Equal(ColorCollection.GuidelineColorIndex, ColorCollection.GUIDELINECOLOR);
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

        Assert.Equal(PixelColor.ByteToFloat, PixelColor.BYTE_TO_FLOAT);
        Assert.Equal(PixelColor.IntBlack, PixelColor.INT_BLACK);
        Assert.Equal(PixelColor.IntWhite, PixelColor.INT_WHITE);
        Assert.Equal(PixelColor.IntWhiteNoAlpha, PixelColor.INT_WHITE_NO_ALPHA);
        Assert.Equal(0x80, color.a);
        Assert.Equal(0x10, color.r);
        Assert.Equal(0x20, color.g);
        Assert.Equal(0x30, color.b);
        Assert.Equal(unchecked((int)0x80102030), color.ToArgb());
        Assert.Equal(unchecked((int)0x80102030), color.ToInt());
        Assert.Equal(color, PixelColor.FromArgb(unchecked((int)0x80102030)));
        Assert.Equal(color, PixelColor.FromInt(unchecked((int)0x80102030)));
        Assert.Equal(color, PixelColor.FromColor(Color.FromArgb(unchecked((int)0x80102030))));
        Assert.Equal(Color.FromArgb(unchecked((int)0x80102030)), color.ToColor());
        Assert.Equal(new PixelColor(0x40, 0x10, 0x20, 0x30), new PixelColor(color, 0x40));
        Assert.Equal(new PixelColor(0x40, 0x10, 0x20, 0x30), color.WithAlpha(0x40));
        Assert.Equal(0x302010, color.ToInversedColorRef());
        Assert.Equal(new Color4(0x10 * PixelColor.ByteToFloat, 0x20 * PixelColor.ByteToFloat, 0x30 * PixelColor.ByteToFloat, 0x80 * PixelColor.ByteToFloat), color.ToColorValue());
        Assert.Equal(new Color4(0x10 * PixelColor.ByteToFloat, 0x20 * PixelColor.ByteToFloat, 0x30 * PixelColor.ByteToFloat, 0.25f), color.ToColorValue(0.25f));
        Assert.Equal("[A=128, R=16, G=32, B=48]", color.ToString());
    }

    [Fact]
    public void CorrectionTableUsesUdbImageBrightnessFormula()
    {
        byte[] darker = ColorCollection.CreateCorrectionTable(-5);
        byte[] neutral = ColorCollection.CreateCorrectionTable(0);
        byte[] brighter = ColorCollection.CreateCorrectionTable(10);

        Assert.Equal(0, darker[0]);
        Assert.Equal(39, darker[128]);
        Assert.Equal(102, darker[255]);
        Assert.Equal(0, neutral[0]);
        Assert.Equal(128, neutral[128]);
        Assert.Equal(255, neutral[255]);
        Assert.Equal(50, brighter[0]);
        Assert.Equal(255, brighter[128]);
        Assert.Equal(255, brighter[255]);
    }

    [Fact]
    public void ApplyColorCorrectionPreservesAlphaAndCorrectsRgbChannels()
    {
        var colors = new ColorCollection(imageBrightness: -5);
        PixelColor[] pixels =
        [
            new(128, 0, 128, 255),
            new(255, 64, 32, 16),
        ];

        colors.ApplyColorCorrection(pixels);

        Assert.Equal(new PixelColor(128, 0, 39, 102), pixels[0]);
        Assert.Equal(new PixelColor(255, 7, 0, 0), pixels[1]);
    }

    [Fact]
    public void ApplyColorCorrectionRejectsWrongTableLength()
    {
        PixelColor[] pixels = [new(255, 1, 2, 3)];

        Assert.Throws<ArgumentException>(() => ColorCollection.ApplyColorCorrection(pixels, [0, 1, 2]));
    }
}
