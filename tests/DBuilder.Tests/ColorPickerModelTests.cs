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
    [InlineData("#2040FF", 0x20, 0x40, 0xff)]
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
    public void ResolveTypedColorInputUsesRgbFieldsWhenTypedTextIsUnchanged()
    {
        var current = new ColorRgb(32, 64, 255);
        var rgbFields = new ColorRgb(1, 2, 3);

        ColorRgb color = ColorPickerModel.ResolveTypedColorInput(
            current,
            rgbFields,
            "2040FF",
            "0.13 0.25 1.00");

        Assert.Equal(rgbFields, color);
    }

    [Fact]
    public void ResolveTypedColorInputPrefersValidFloatTextOverHexAndRgbFields()
    {
        ColorRgb color = ColorPickerModel.ResolveTypedColorInput(
            new ColorRgb(32, 64, 255),
            new ColorRgb(1, 2, 3),
            "112233",
            "0.5 0.25 1");

        Assert.Equal(new ColorRgb(127, 63, 255), color);
    }

    [Fact]
    public void ResolveTypedColorInputUsesHexWhenFloatTextIsUnchanged()
    {
        ColorRgb color = ColorPickerModel.ResolveTypedColorInput(
            new ColorRgb(32, 64, 255),
            new ColorRgb(1, 2, 3),
            "#112233",
            "0.13 0.25 1.00");

        Assert.Equal(new ColorRgb(0x11, 0x22, 0x33), color);
    }

    [Fact]
    public void ResolveTypedColorInputFallsBackToRgbFieldsWhenChangedTypedTextIsInvalid()
    {
        ColorRgb color = ColorPickerModel.ResolveTypedColorInput(
            new ColorRgb(32, 64, 255),
            new ColorRgb(1, 2, 3),
            "zzzzzz",
            "bad float");

        Assert.Equal(new ColorRgb(1, 2, 3), color);
    }

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
    public void ApplySectorColorEditSeedsPairedFieldFromFirstSectorLikeUdb()
    {
        var first = new Sector();
        first.Fields[ColorPickerModel.FadeColorField] = 0x445566;
        var second = new Sector();
        var sectors = new[] { first, second };

        int count = ColorPickerModel.ApplySectorColorEdit(
            sectors,
            SectorColorField.LightColor,
            new ColorRgb(0x11, 0x22, 0x33),
            removeDefaults: true);

        Assert.Equal(2, count);
        Assert.All(sectors, sector => Assert.Equal(0x112233, sector.Fields[ColorPickerModel.LightColorField]));
        Assert.All(sectors, sector => Assert.Equal(0x445566, sector.Fields[ColorPickerModel.FadeColorField]));
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

    [Fact]
    public void DynamicLightPickerMetadataMatchesUdbSelectionMessages()
    {
        Assert.Equal("No lights found in selection!", ColorPickerModel.NoDynamicLightsWarning);
        Assert.Equal("Select one or more sectors to set color.", ColorPickerModel.NoSelectedSectorsWarning);
        Assert.Equal("Sector colors can only be set if map is in UDMF format!", ColorPickerModel.SectorColorsRequireUdmfWarning);
        Assert.Equal("Editing 1 sector", ColorPickerModel.SectorColorPickerTitle(1));
        Assert.Equal("Editing 2 sectors", ColorPickerModel.SectorColorPickerTitle(2));
        Assert.Equal("Editing 1 light", ColorPickerModel.DynamicLightPickerTitle(1));
        Assert.Equal("Editing 2 lights", ColorPickerModel.DynamicLightPickerTitle(2));
        Assert.Equal(3, ColorPickerModel.FirstDynamicLightRadiusArgument(lightVavoom: false));
        Assert.Equal(0, ColorPickerModel.FirstDynamicLightRadiusArgument(lightVavoom: true));
    }

    [Theory]
    [InlineData(SectorColorField.LightColor, 1, "Set lightcolor on 1 sector to 2040FF.")]
    [InlineData(SectorColorField.FadeColor, 2, "Set fadecolor on 2 sectors to 2040FF.")]
    public void SectorColorAppliedStatusTextFormatsSingularAndPluralCounts(SectorColorField field, int sectorCount, string expected)
        => Assert.Equal(expected, ColorPickerModel.SectorColorAppliedStatusText(field, sectorCount, new ColorRgb(0x20, 0x40, 0xff)));

    [Theory]
    [InlineData(1, "Set 1 dynamic light to 2040FF.")]
    [InlineData(2, "Set 2 dynamic lights to 2040FF.")]
    public void DynamicLightColorAppliedStatusTextFormatsSingularAndPluralCounts(int lightCount, string expected)
        => Assert.Equal(expected, ColorPickerModel.DynamicLightColorAppliedStatusText(lightCount, new ColorRgb(0x20, 0x40, 0xff)));

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void SectorColorEditingRequiresUdmfLikeUdb(bool isUdmf, bool expected)
        => Assert.Equal(expected, ColorPickerModel.CanEditSectorColors(isUdmf));

    [Theory]
    [InlineData(9801, true)]
    [InlineData(9802, true)]
    [InlineData(9804, true)]
    [InlineData(9811, true)]
    [InlineData(9812, true)]
    [InlineData(9814, true)]
    [InlineData(9821, true)]
    [InlineData(9822, true)]
    [InlineData(9824, true)]
    [InlineData(9831, true)]
    [InlineData(9832, true)]
    [InlineData(9834, true)]
    [InlineData(9803, false)]
    public void DynamicLightAngleControlUsesUdbLightNumbers(int lightNumber, bool expected)
        => Assert.Equal(expected, ColorPickerModel.DynamicLightUsesAngleValue(lightNumber));

    [Theory]
    [InlineData(9800, false, DynamicLightColorMode.Standard)]
    [InlineData(9840, false, DynamicLightColorMode.SpotOrSun)]
    [InlineData(9890, false, DynamicLightColorMode.SpotOrSun)]
    [InlineData(1502, true, DynamicLightColorMode.VavoomGeneric)]
    [InlineData(1503, true, DynamicLightColorMode.VavoomColored)]
    public void InternalDynamicLightDefinitionMatchesUdbThingNumbers(int thingType, bool lightVavoom, DynamicLightColorMode colorMode)
    {
        DynamicLightDefinition? definition = ColorPickerModel.InternalDynamicLightDefinitionForThingType(thingType);

        Assert.NotNull(definition);
        Assert.Equal(thingType, definition!.LightNumber);
        Assert.Equal(lightVavoom, definition.LightVavoom);
        Assert.Equal(colorMode, definition.ColorMode);
    }

    [Fact]
    public void InternalDynamicLightDefinitionRejectsNonLightThings()
        => Assert.Null(ColorPickerModel.InternalDynamicLightDefinitionForThingType(3001));

    [Fact]
    public void InternalDynamicLightEditTargetsFilterSelectedInternalLights()
    {
        var light = new Thing(new DBuilder.Geometry.Vector2D(0, 0), 9800);
        light.Args[0] = 1;
        light.Args[1] = 2;
        light.Args[2] = 3;
        light.Args[3] = 128;
        var ordinary = new Thing(new DBuilder.Geometry.Vector2D(64, 0), 3001);

        IReadOnlyList<DynamicLightThingEditTarget> targets =
            ColorPickerModel.InternalDynamicLightEditTargets(new[] { light, ordinary });

        DynamicLightThingEditTarget target = Assert.Single(targets);
        Assert.Same(light, target.Thing);
        Assert.Equal(new ColorRgb(1, 2, 3), ColorPickerModel.GetDynamicLightColor(target.EditTarget.Definition, target.EditTarget.Args, target.EditTarget.Fields));
    }

    [Fact]
    public void HasInternalDynamicLightSelectionMatchesEditableLightThings()
    {
        var ordinary = new Thing(new DBuilder.Geometry.Vector2D(0, 0), 3001);
        var light = new Thing(new DBuilder.Geometry.Vector2D(64, 0), 1503);

        Assert.False(ColorPickerModel.HasInternalDynamicLightSelection(new[] { ordinary }));
        Assert.True(ColorPickerModel.HasInternalDynamicLightSelection(new[] { ordinary, light }));
    }

    [Fact]
    public void ApplyDynamicLightMutationsUpdatesThings()
    {
        var thing = new Thing(new DBuilder.Geometry.Vector2D(0, 0), 9840, angle: 90);
        thing.Args[0] = 0x112233;
        thing.Args[3] = 128;
        thing.Fields["keep"] = "value";
        IReadOnlyList<DynamicLightThingEditTarget> targets = ColorPickerModel.InternalDynamicLightEditTargets(new[] { thing });
        IReadOnlyList<DynamicLightMutation> mutations = ColorPickerModel.SetDynamicLightSelection(
            targets.Select(t => t.EditTarget).ToList(),
            new ColorRgb(10, 20, 30),
            primaryRadius: 256,
            secondaryRadius: 0,
            interval: 0,
            relativeMode: false);

        ColorPickerModel.ApplyDynamicLightMutations(targets, mutations);

        Assert.Equal(0, thing.Args[0]);
        Assert.Equal(256, thing.Args[3]);
        Assert.Equal("0A141E", thing.Fields[ColorPickerModel.DynamicLightPackedColorField]);
        Assert.Equal("value", thing.Fields["keep"]);
    }

    [Fact]
    public void DynamicLightSliderLimitsMatchUdbAbsoluteAndRelativeModes()
    {
        Assert.Equal(new DynamicLightSliderLimits(0, 512, 0, 16384), ColorPickerModel.DynamicLightRadiusLimits(relativeMode: false));
        Assert.Equal(new DynamicLightSliderLimits(-256, 256, -16384, 16384), ColorPickerModel.DynamicLightRadiusLimits(relativeMode: true));
        Assert.Equal(new DynamicLightSliderLimits(0, 359, 0, 16384), ColorPickerModel.DynamicLightIntervalLimits(relativeMode: false));
        Assert.Equal(new DynamicLightSliderLimits(-180, 180, -16384, 16384), ColorPickerModel.DynamicLightIntervalLimits(relativeMode: true));
    }

    [Fact]
    public void DynamicLightSliderValueClampsToUdbNumericLimits()
    {
        DynamicLightSliderLimits absolute = ColorPickerModel.DynamicLightRadiusLimits(relativeMode: false);
        DynamicLightSliderLimits relative = ColorPickerModel.DynamicLightIntervalLimits(relativeMode: true);

        Assert.Equal(0, ColorPickerModel.ClampDynamicLightSliderValue(absolute, -1));
        Assert.Equal(8192, ColorPickerModel.ClampDynamicLightSliderValue(absolute, 8192));
        Assert.Equal(16384, ColorPickerModel.ClampDynamicLightSliderValue(absolute, 20000));
        Assert.Equal(-16384, ColorPickerModel.ClampDynamicLightSliderValue(relative, -20000));
        Assert.Equal(16384, ColorPickerModel.ClampDynamicLightSliderValue(relative, 20000));
    }

    [Fact]
    public void DynamicLightSliderPresentationUsesUdbArgTitles()
    {
        var definition = new DynamicLightDefinition(9801, LightVavoom: false, DynamicLightColorMode.Standard);
        string[] argTitles = ["Red", "Green", "Blue", "Primary radius", "Secondary radius"];

        DynamicLightSliderPresentation presentation = ColorPickerModel.DynamicLightSliderPresentationFor(definition, argTitles);

        Assert.Equal("Primary radius:", presentation.PrimaryLabel);
        Assert.Equal("Secondary radius:", presentation.SecondaryLabel);
        Assert.Equal("Interval:", presentation.IntervalLabel);
        Assert.True(presentation.ShowAllControls);
    }

    [Fact]
    public void DynamicLightSliderPresentationHidesSecondaryControlsForSimpleLights()
    {
        var definition = new DynamicLightDefinition(9803, LightVavoom: true, DynamicLightColorMode.VavoomColored);
        string[] argTitles = ["Radius", "Red", "Green", "Blue", "Unused"];

        DynamicLightSliderPresentation presentation = ColorPickerModel.DynamicLightSliderPresentationFor(definition, argTitles);

        Assert.Equal("Radius:", presentation.PrimaryLabel);
        Assert.Equal("", presentation.SecondaryLabel);
        Assert.Equal("", presentation.IntervalLabel);
        Assert.False(presentation.ShowAllControls);
    }

    [Fact]
    public void DynamicLightPickerStateUsesReferenceThingValuesInAbsoluteMode()
    {
        var definition = new DynamicLightDefinition(9801, LightVavoom: false, DynamicLightColorMode.Standard);

        DynamicLightPickerState state = ColorPickerModel.CreateDynamicLightPickerState(
            definition,
            new[] { 16, 32, 64, 256, 128 },
            angleDoom: 45,
            new Dictionary<string, object>(),
            relativeMode: false);

        Assert.Equal(new ColorRgb(16, 32, 64), state.Color);
        Assert.Equal(256, state.PrimaryRadius);
        Assert.Equal(128, state.SecondaryRadius);
        Assert.Equal(45, state.Interval);
        Assert.True(state.ShowAllControls);
    }

    [Fact]
    public void DynamicLightPickerStateStartsAtZeroInRelativeMode()
    {
        var definition = new DynamicLightDefinition(9801, LightVavoom: false, DynamicLightColorMode.Standard);

        DynamicLightPickerState state = ColorPickerModel.CreateDynamicLightPickerState(
            definition,
            new[] { 16, 32, 64, 256, 128 },
            angleDoom: 45,
            new Dictionary<string, object>(),
            relativeMode: true);

        Assert.Equal(new ColorRgb(16, 32, 64), state.Color);
        Assert.Equal(0, state.PrimaryRadius);
        Assert.Equal(0, state.SecondaryRadius);
        Assert.Equal(0, state.Interval);
        Assert.True(state.ShowAllControls);
    }

    [Fact]
    public void StandardDynamicLightColorUsesFirstThreeArgs()
    {
        var definition = new DynamicLightDefinition(0, LightVavoom: false, DynamicLightColorMode.Standard);

        DynamicLightMutation mutation = ColorPickerModel.SetDynamicLightColor(
            definition,
            new[] { 1, 2, 3, 4, 5 },
            angleDoom: 90,
            new Dictionary<string, object>(),
            new ColorRgb(10, 20, 30));

        Assert.Equal(new[] { 10, 20, 30, 4, 5 }, mutation.Args);
        Assert.Equal(90, mutation.AngleDoom);
        Assert.Empty(mutation.Fields);
    }

    [Fact]
    public void VavoomGenericDynamicLightKeepsColorArgsUnchanged()
    {
        var definition = new DynamicLightDefinition(0, LightVavoom: true, DynamicLightColorMode.VavoomGeneric);

        ColorRgb color = ColorPickerModel.GetDynamicLightColor(definition, new[] { 1, 2, 3, 4, 5 }, new Dictionary<string, object>());
        DynamicLightMutation mutation = ColorPickerModel.SetDynamicLightColor(
            definition,
            new[] { 1, 2, 3, 4, 5 },
            angleDoom: 0,
            new Dictionary<string, object>(),
            new ColorRgb(10, 20, 30));

        Assert.Equal(new ColorRgb(255, 255, 255), color);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, mutation.Args);
    }

    [Fact]
    public void VavoomColoredDynamicLightColorUsesArgsOneThroughThree()
    {
        var definition = new DynamicLightDefinition(0, LightVavoom: true, DynamicLightColorMode.VavoomColored);

        ColorRgb color = ColorPickerModel.GetDynamicLightColor(definition, new[] { 99, 1, 2, 3, 4 }, new Dictionary<string, object>());
        DynamicLightMutation mutation = ColorPickerModel.SetDynamicLightColor(
            definition,
            new[] { 99, 1, 2, 3, 4 },
            angleDoom: 0,
            new Dictionary<string, object>(),
            new ColorRgb(10, 20, 30));

        Assert.Equal(new ColorRgb(1, 2, 3), color);
        Assert.Equal(new[] { 99, 10, 20, 30, 4 }, mutation.Args);
    }

    [Fact]
    public void SpotAndSunDynamicLightColorPrefersArgStringAndWritesPackedString()
    {
        var definition = new DynamicLightDefinition(0, LightVavoom: false, DynamicLightColorMode.SpotOrSun);
        var fields = new Dictionary<string, object>
        {
            [ColorPickerModel.DynamicLightPackedColorField] = "2040FF",
        };

        ColorRgb color = ColorPickerModel.GetDynamicLightColor(definition, new[] { 0x112233, 1, 2, 3, 4 }, fields);
        DynamicLightMutation mutation = ColorPickerModel.SetDynamicLightColor(
            definition,
            new[] { 0x112233, 1, 2, 3, 4 },
            angleDoom: 0,
            fields,
            new ColorRgb(10, 20, 30));

        Assert.Equal(new ColorRgb(0x20, 0x40, 0xff), color);
        Assert.Equal(0, mutation.Args[0]);
        Assert.Equal("0A141E", mutation.Fields[ColorPickerModel.DynamicLightPackedColorField]);
    }

    [Fact]
    public void SpotAndSunDynamicLightColorFallsBackToPackedArg()
    {
        var definition = new DynamicLightDefinition(0, LightVavoom: false, DynamicLightColorMode.SpotOrSun);

        ColorRgb color = ColorPickerModel.GetDynamicLightColor(
            definition,
            new[] { 0x112233, 1, 2, 3, 4 },
            new Dictionary<string, object>());

        Assert.Equal(new ColorRgb(0x11, 0x22, 0x33), color);
    }

    [Fact]
    public void DynamicLightPropertiesMatchUdbAbsoluteMode()
    {
        var definition = new DynamicLightDefinition(9801, LightVavoom: false, DynamicLightColorMode.Standard);

        DynamicLightMutation mutation = ColorPickerModel.SetDynamicLightProperties(
            definition,
            new[] { 1, 2, 3, 4, 5 },
            angleDoom: 90,
            new Dictionary<string, object>(),
            primaryRadius: 256,
            secondaryRadius: 128,
            interval: 405,
            relativeMode: false);

        Assert.Equal(new[] { 1, 2, 3, 256, 128 }, mutation.Args);
        Assert.Equal(45, mutation.AngleDoom);
    }

    [Fact]
    public void DynamicLightPropertiesMatchUdbRelativeMode()
    {
        var definition = new DynamicLightDefinition(9801, LightVavoom: false, DynamicLightColorMode.Standard);
        var fixedValues = new DynamicLightPickerState(
            new ColorRgb(1, 2, 3),
            PrimaryRadius: 100,
            SecondaryRadius: 50,
            Interval: 20,
            ShowAllControls: true,
            ColorPickerModel.DynamicLightRadiusLimits(relativeMode: false),
            ColorPickerModel.DynamicLightRadiusLimits(relativeMode: false),
            ColorPickerModel.DynamicLightIntervalLimits(relativeMode: false));

        DynamicLightMutation mutation = ColorPickerModel.SetDynamicLightProperties(
            definition,
            new[] { 1, 2, 3, 4, 5 },
            angleDoom: 90,
            new Dictionary<string, object>(),
            primaryRadius: -120,
            secondaryRadius: 25,
            interval: -45,
            relativeMode: true,
            fixedValues);

        Assert.Equal(new[] { 1, 2, 3, 0, 75 }, mutation.Args);
        Assert.Equal(335, mutation.AngleDoom);
    }

    [Fact]
    public void DynamicLightSelectionCapturesFixedValuesForRelativeMode()
    {
        var targets = new[]
        {
            new DynamicLightEditTarget(
                new DynamicLightDefinition(9801, LightVavoom: false, DynamicLightColorMode.Standard),
                new[] { 1, 2, 3, 128, 64 },
                AngleDoom: 45,
                new Dictionary<string, object>()),
            new DynamicLightEditTarget(
                new DynamicLightDefinition(9803, LightVavoom: true, DynamicLightColorMode.VavoomColored),
                new[] { 96, 10, 20, 30, 0 },
                AngleDoom: 180,
                new Dictionary<string, object>()),
        };

        IReadOnlyList<DynamicLightPickerState> fixedValues = ColorPickerModel.CaptureDynamicLightFixedValues(targets);

        Assert.Equal(2, fixedValues.Count);
        Assert.Equal(128, fixedValues[0].PrimaryRadius);
        Assert.Equal(64, fixedValues[0].SecondaryRadius);
        Assert.Equal(45, fixedValues[0].Interval);
        Assert.Equal(96, fixedValues[1].PrimaryRadius);
        Assert.Equal(0, fixedValues[1].SecondaryRadius);
        Assert.False(fixedValues[1].ShowAllControls);
    }

    [Fact]
    public void DynamicLightSelectionAppliesColorToEachLightDefinitionLikeUdb()
    {
        var fields = new Dictionary<string, object>
        {
            [ColorPickerModel.DynamicLightPackedColorField] = "112233",
        };
        var targets = new[]
        {
            new DynamicLightEditTarget(
                new DynamicLightDefinition(9801, LightVavoom: false, DynamicLightColorMode.Standard),
                new[] { 1, 2, 3, 128, 64 },
                AngleDoom: 45,
                new Dictionary<string, object>()),
            new DynamicLightEditTarget(
                new DynamicLightDefinition(9803, LightVavoom: true, DynamicLightColorMode.VavoomColored),
                new[] { 96, 1, 2, 3, 0 },
                AngleDoom: 180,
                new Dictionary<string, object>()),
            new DynamicLightEditTarget(
                new DynamicLightDefinition(9804, LightVavoom: false, DynamicLightColorMode.SpotOrSun),
                new[] { 0x445566, 1, 2, 128, 64 },
                AngleDoom: 90,
                fields),
        };

        IReadOnlyList<DynamicLightMutation> mutations =
            ColorPickerModel.SetDynamicLightSelectionColor(targets, new ColorRgb(10, 20, 30));

        Assert.Equal(new[] { 10, 20, 30, 128, 64 }, mutations[0].Args);
        Assert.Equal(new[] { 96, 10, 20, 30, 0 }, mutations[1].Args);
        Assert.Equal(0, mutations[2].Args[0]);
        Assert.Equal("0A141E", mutations[2].Fields[ColorPickerModel.DynamicLightPackedColorField]);
    }

    [Fact]
    public void DynamicLightSelectionAppliesRelativePropertiesPerTargetLikeUdb()
    {
        var targets = new[]
        {
            new DynamicLightEditTarget(
                new DynamicLightDefinition(9801, LightVavoom: false, DynamicLightColorMode.Standard),
                new[] { 1, 2, 3, 100, 50 },
                AngleDoom: 20,
                new Dictionary<string, object>()),
            new DynamicLightEditTarget(
                new DynamicLightDefinition(9803, LightVavoom: true, DynamicLightColorMode.VavoomColored),
                new[] { 80, 1, 2, 3, 0 },
                AngleDoom: 270,
                new Dictionary<string, object>()),
        };
        IReadOnlyList<DynamicLightPickerState> fixedValues = ColorPickerModel.CaptureDynamicLightFixedValues(targets);

        IReadOnlyList<DynamicLightMutation> mutations = ColorPickerModel.SetDynamicLightSelectionProperties(
            targets,
            primaryRadius: -20,
            secondaryRadius: 25,
            interval: -45,
            relativeMode: true,
            fixedValues);

        Assert.Equal(new[] { 1, 2, 3, 80, 75 }, mutations[0].Args);
        Assert.Equal(335, mutations[0].AngleDoom);
        Assert.Equal(new[] { 60, 1, 2, 3, 0 }, mutations[1].Args);
        Assert.Equal(270, mutations[1].AngleDoom);
    }
}
