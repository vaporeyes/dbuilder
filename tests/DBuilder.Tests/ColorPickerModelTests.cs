// ABOUTME: Verifies UDB-style ColorPicker RGB/HSV conversions and display formatting.
// ABOUTME: Covers sector light/fade color field initialization, mutation, and default cleanup.

using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class ColorPickerModelTests
{
    [Theory]
    [InlineData(255, 0, 0, 0, 255, 255)]
    [InlineData(0, 255, 0, 85, 255, 255)]
    [InlineData(0, 0, 255, 170, 255, 255)]
    [InlineData(128, 128, 128, 0, 0, 128)]
    public void RgbToHsvMatchesUdbScaling(int r, int g, int b, int h, int s, int v)
    {
        var hsv = ColorPickerModel.RgbToHsv(new ColorRgb(r, g, b));

        Assert.Equal(new ColorHsv(h, s, v), hsv);
    }

    [Theory]
    [InlineData(0, 255, 255, 255, 0, 0)]
    [InlineData(85, 255, 255, 0, 255, 0)]
    [InlineData(170, 255, 255, 0, 0, 255)]
    [InlineData(0, 0, 128, 128, 128, 128)]
    public void HsvToRgbMatchesUdbScaling(int h, int s, int v, int r, int g, int b)
    {
        var rgb = ColorPickerModel.HsvToRgb(new ColorHsv(h, s, v));

        Assert.Equal(new ColorRgb(r, g, b), rgb);
    }

    [Fact]
    public void PackAndUnpackRgbUseUdmfColorInteger()
    {
        var rgb = new ColorRgb(0x20, 0x40, 0xff);

        int packed = ColorPickerModel.PackRgb(rgb);

        Assert.Equal(0x2040ff, packed);
        Assert.Equal(rgb, ColorPickerModel.UnpackRgb(packed));
    }

    [Fact]
    public void FormatMatchesPickerInfoModes()
    {
        var rgb = new ColorRgb(0x20, 0x40, 0xff);

        Assert.Equal("32 64 255", ColorPickerModel.Format(rgb, ColorPickerInfoMode.Rgb));
        Assert.Equal("2040FF", ColorPickerModel.Format(rgb, ColorPickerInfoMode.Hex));
        Assert.Equal("0.13 0.25 1.00", ColorPickerModel.Format(rgb, ColorPickerInfoMode.Float));
    }

    [Theory]
    [InlineData("2040FF", 0x20, 0x40, 0xff)]
    [InlineData("20-40-ff", 0x20, 0x40, 0xff)]
    [InlineData("  ff0000  ", 0xff, 0, 0)]
    public void TryParseHexMatchesUdbTypedInput(string text, int red, int green, int blue)
    {
        var rgb = ColorPickerModel.TryParse(ColorPickerInfoMode.Hex, text);

        Assert.Equal(new ColorRgb(red, green, blue), rgb);
    }

    [Theory]
    [InlineData("0.5 0.25 1", 127, 63, 255)]
    [InlineData("-0.5 2 -1", 127, 255, 255)]
    [InlineData("1E-1 0 1", 25, 0, 255)]
    public void TryParseFloatTripletMatchesUdbTypedInput(string text, int red, int green, int blue)
    {
        var rgb = ColorPickerModel.TryParse(ColorPickerInfoMode.Float, text);

        Assert.Equal(new ColorRgb(red, green, blue), rgb);
    }

    [Theory]
    [InlineData(ColorPickerInfoMode.Hex, "12345")]
    [InlineData(ColorPickerInfoMode.Hex, "zzzzzz")]
    [InlineData(ColorPickerInfoMode.Float, "0.1 0.2")]
    [InlineData(ColorPickerInfoMode.Float, "0.1 nope 0.3")]
    [InlineData(ColorPickerInfoMode.Rgb, "32 64 255")]
    public void TryParseRejectsInvalidTypedInput(ColorPickerInfoMode mode, string text)
        => Assert.Null(ColorPickerModel.TryParse(mode, text));

    [Fact]
    public void EnsureSectorColorFieldsSeedsMissingFields()
    {
        var first = new Sector();
        var second = new Sector();
        second.Fields[ColorPickerModel.LightColorField] = 0x112233;

        ColorPickerModel.EnsureSectorColorFields(new[] { first, second }, 0xffffff, 0);

        Assert.Equal(0xffffff, first.Fields[ColorPickerModel.LightColorField]);
        Assert.Equal(0, first.Fields[ColorPickerModel.FadeColorField]);
        Assert.Equal(0x112233, second.Fields[ColorPickerModel.LightColorField]);
        Assert.Equal(0, second.Fields[ColorPickerModel.FadeColorField]);
    }

    [Fact]
    public void SectorColorPickerStateKeepsSeparateLightAndFadeDraftsLikeUdb()
    {
        var sector = new Sector();
        sector.Fields[ColorPickerModel.LightColorField] = 0x112233;
        sector.Fields[ColorPickerModel.FadeColorField] = 0x445566;

        SectorColorPickerState state = ColorPickerModel.CreateSectorColorPickerState(sector, SectorColorField.LightColor);

        Assert.Equal(new ColorRgb(0x11, 0x22, 0x33), state.ActiveColor);

        state = ColorPickerModel.SetSectorColorPickerActiveColor(state, new ColorRgb(0xaa, 0xbb, 0xcc));
        state = ColorPickerModel.SwitchSectorColorPickerField(state, SectorColorField.FadeColor);

        Assert.Equal(new ColorRgb(0x44, 0x55, 0x66), state.ActiveColor);

        state = ColorPickerModel.SetSectorColorPickerActiveColor(state, new ColorRgb(0x01, 0x02, 0x03));
        state = ColorPickerModel.SwitchSectorColorPickerField(state, SectorColorField.LightColor);

        Assert.Equal(new ColorRgb(0xaa, 0xbb, 0xcc), state.ActiveColor);
        Assert.Equal(new ColorRgb(0x01, 0x02, 0x03), state.FadeColor);
    }

    [Fact]
    public void SetSectorColorUpdatesSelectedField()
    {
        var sectors = new[] { new Sector(), new Sector() };

        ColorPickerModel.SetSectorColor(sectors, SectorColorField.FadeColor, new ColorRgb(0x10, 0x20, 0x30));

        Assert.All(sectors, sector => Assert.Equal(0x102030, sector.Fields[ColorPickerModel.FadeColorField]));
        Assert.All(sectors, sector => Assert.False(sector.Fields.ContainsKey(ColorPickerModel.LightColorField)));
    }

    [Fact]
    public void RemoveDefaultSectorColorsDropsOnlyDefaultValues()
    {
        var first = new Sector();
        first.Fields[ColorPickerModel.LightColorField] = ColorPickerModel.DefaultLightColor;
        first.Fields[ColorPickerModel.FadeColorField] = ColorPickerModel.DefaultFadeColor;
        var second = new Sector();
        second.Fields[ColorPickerModel.LightColorField] = 0x112233;
        second.Fields[ColorPickerModel.FadeColorField] = 0x445566;

        ColorPickerModel.RemoveDefaultSectorColors(new[] { first, second });

        Assert.Empty(first.Fields);
        Assert.Equal(0x112233, second.Fields[ColorPickerModel.LightColorField]);
        Assert.Equal(0x445566, second.Fields[ColorPickerModel.FadeColorField]);
    }
}
