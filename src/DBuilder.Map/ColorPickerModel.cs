// ABOUTME: Ports UDB ColorPicker color conversion and sector color field behavior.
// ABOUTME: Keeps RGB/HSV math, display formatting, and UDMF color fields independent of picker UI.

using System.Globalization;

namespace DBuilder.Map;

public readonly record struct ColorRgb(int Red, int Green, int Blue);

public readonly record struct ColorHsv(int Hue, int Saturation, int Value);

public enum ColorPickerInfoMode
{
    Rgb,
    Hex,
    Float,
}

public enum SectorColorField
{
    LightColor,
    FadeColor,
}

public enum DynamicLightColorMode
{
    Standard,
    VavoomGeneric,
    VavoomColored,
    SpotOrSun,
}

public sealed record SectorColorPickerState(
    ColorRgb LightColor,
    ColorRgb FadeColor,
    SectorColorField ActiveField)
{
    public ColorRgb ActiveColor => ActiveField == SectorColorField.LightColor ? LightColor : FadeColor;
}

public sealed record DynamicLightDefinition(
    int LightNumber,
    bool LightVavoom,
    DynamicLightColorMode ColorMode);

public sealed record DynamicLightSliderLimits(
    int TrackMinimum,
    int TrackMaximum,
    int NumericMinimum,
    int NumericMaximum);

public sealed record DynamicLightSliderPresentation(
    string PrimaryLabel,
    string SecondaryLabel,
    string IntervalLabel,
    bool ShowAllControls);

public sealed record DynamicLightPickerState(
    ColorRgb Color,
    int PrimaryRadius,
    int SecondaryRadius,
    int Interval,
    bool ShowAllControls,
    DynamicLightSliderLimits PrimaryLimits,
    DynamicLightSliderLimits SecondaryLimits,
    DynamicLightSliderLimits IntervalLimits);

public sealed record DynamicLightMutation(
    IReadOnlyList<int> Args,
    int AngleDoom,
    IReadOnlyDictionary<string, object> Fields);

public sealed record DynamicLightEditTarget(
    DynamicLightDefinition Definition,
    IReadOnlyList<int> Args,
    int AngleDoom,
    IReadOnlyDictionary<string, object> Fields);

public sealed record DynamicLightThingEditTarget(
    Thing Thing,
    DynamicLightEditTarget EditTarget);

public static class ColorPickerModel
{
    public const int DefaultLightColor = 0xffffff;
    public const int DefaultFadeColor = 0;
    public const string LightColorField = "lightcolor";
    public const string FadeColorField = "fadecolor";
    public const string DynamicLightPackedColorField = "arg0str";
    public const string NoDynamicLightsWarning = "No lights found in selection!";
    public const string SectorColorsRequireUdmfWarning = "Sector colors can only be set if map is in UDMF format!";

    private static readonly HashSet<int> LightsUsingAngleValue =
    [
        9801,
        9802,
        9804,
        9811,
        9812,
        9814,
        9821,
        9822,
        9824,
        9831,
        9832,
        9834,
    ];

    private static readonly HashSet<int> InternalPointLights =
    [
        9800,
        9801,
        9802,
        9803,
        9804,
        9810,
        9811,
        9812,
        9813,
        9814,
        9820,
        9821,
        9822,
        9823,
        9824,
        9830,
        9831,
        9832,
        9833,
        9834,
        9876,
        9877,
        9878,
        9879,
    ];

    private static readonly HashSet<int> InternalSpotLights =
    [
        9840,
        9841,
        9842,
        9843,
        8944,
        9850,
        9851,
        9852,
        9853,
        8954,
        9860,
        9861,
        9862,
        9863,
        8964,
        9870,
        9871,
        9872,
        9873,
        8974,
        9881,
        9882,
        9883,
        9884,
    ];

    public static ColorRgb HsvToRgb(int hue, int saturation, int value)
        => HsvToRgb(new ColorHsv(hue, saturation, value));

    public static ColorRgb HsvToRgb(ColorHsv hsv)
    {
        float r = 0;
        float g = 0;
        float b = 0;

        float h = ((float)hsv.Hue / 255 * 360) % 360;
        float s = (float)hsv.Saturation / 255;
        float v = (float)hsv.Value / 255;

        if (s == 0)
        {
            r = v;
            g = v;
            b = v;
        }
        else
        {
            float sectorPos = h / 60;
            int sectorNumber = (int)Math.Floor(sectorPos);
            float fractionalSector = sectorPos - sectorNumber;

            float p = v * (1 - s);
            float q = v * (1 - (s * fractionalSector));
            float t = v * (1 - (s * (1 - fractionalSector)));

            switch (sectorNumber)
            {
                case 0:
                    r = v;
                    g = t;
                    b = p;
                    break;
                case 1:
                    r = q;
                    g = v;
                    b = p;
                    break;
                case 2:
                    r = p;
                    g = v;
                    b = t;
                    break;
                case 3:
                    r = p;
                    g = q;
                    b = v;
                    break;
                case 4:
                    r = t;
                    g = p;
                    b = v;
                    break;
                case 5:
                    r = v;
                    g = p;
                    b = q;
                    break;
            }
        }

        return new ColorRgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    public static ColorHsv RgbToHsv(ColorRgb rgb)
    {
        float r = (float)rgb.Red / 255;
        float g = (float)rgb.Green / 255;
        float b = (float)rgb.Blue / 255;

        float min = Math.Min(Math.Min(r, g), b);
        float max = Math.Max(Math.Max(r, g), b);
        float v = max;
        float delta = max - min;

        float h;
        float s;
        if (max == 0 || delta == 0)
        {
            s = 0;
            h = 0;
        }
        else
        {
            s = delta / max;
            if (r == max) h = (g - b) / delta;
            else if (g == max) h = 2 + (b - r) / delta;
            else h = 4 + (r - g) / delta;
        }

        h *= 60;
        if (h < 0) h += 360;

        return new ColorHsv((int)(h / 360 * 255), (int)(s * 255), (int)(v * 255));
    }

    public static int PackRgb(ColorRgb rgb)
        => rgb.Red << 16 | rgb.Green << 8 | rgb.Blue;

    public static ColorRgb UnpackRgb(int value)
        => new((value >> 16) & 0xff, (value >> 8) & 0xff, value & 0xff);

    public static string DynamicLightPickerTitle(int selectedLightCount)
        => $"Editing {selectedLightCount} light{(selectedLightCount > 1 ? "s" : "")}";

    public static string SectorColorPickerTitle(int selectedSectorCount)
        => $"Editing {selectedSectorCount} sector{(selectedSectorCount > 1 ? "s" : "")}";

    public static bool CanEditSectorColors(bool isUdmf)
        => isUdmf;

    public static bool DynamicLightUsesAngleValue(int lightNumber)
        => LightsUsingAngleValue.Contains(lightNumber);

    public static DynamicLightDefinition? InternalDynamicLightDefinitionForThingType(int thingType)
    {
        if (thingType == 1502) return new DynamicLightDefinition(thingType, LightVavoom: true, DynamicLightColorMode.VavoomGeneric);
        if (thingType == 1503) return new DynamicLightDefinition(thingType, LightVavoom: true, DynamicLightColorMode.VavoomColored);
        if (thingType == 9890 || InternalSpotLights.Contains(thingType)) return new DynamicLightDefinition(thingType, LightVavoom: false, DynamicLightColorMode.SpotOrSun);
        if (InternalPointLights.Contains(thingType)) return new DynamicLightDefinition(thingType, LightVavoom: false, DynamicLightColorMode.Standard);
        return null;
    }

    public static IReadOnlyList<DynamicLightThingEditTarget> InternalDynamicLightEditTargets(IEnumerable<Thing> things)
    {
        var targets = new List<DynamicLightThingEditTarget>();
        foreach (Thing thing in things)
        {
            DynamicLightDefinition? definition = InternalDynamicLightDefinitionForThingType(thing.Type);
            if (definition == null) continue;

            targets.Add(new DynamicLightThingEditTarget(
                thing,
                new DynamicLightEditTarget(definition, thing.Args, thing.Angle, thing.Fields)));
        }

        return targets;
    }

    public static int FirstDynamicLightRadiusArgument(bool lightVavoom)
        => lightVavoom ? 0 : 3;

    public static DynamicLightSliderLimits DynamicLightRadiusLimits(bool relativeMode)
        => relativeMode
            ? new DynamicLightSliderLimits(-256, 256, -16384, 16384)
            : new DynamicLightSliderLimits(0, 512, 0, 16384);

    public static DynamicLightSliderLimits DynamicLightIntervalLimits(bool relativeMode)
        => relativeMode
            ? new DynamicLightSliderLimits(-180, 180, -16384, 16384)
            : new DynamicLightSliderLimits(0, 359, 0, 16384);

    public static DynamicLightSliderPresentation DynamicLightSliderPresentationFor(
        DynamicLightDefinition definition,
        IReadOnlyList<string> argTitles)
    {
        int firstArg = FirstDynamicLightRadiusArgument(definition.LightVavoom);
        bool showAllControls = DynamicLightUsesAngleValue(definition.LightNumber);

        return new DynamicLightSliderPresentation(
            SliderLabel(argTitles, firstArg),
            showAllControls ? SliderLabel(argTitles, 4) : "",
            showAllControls ? "Interval:" : "",
            showAllControls);
    }

    public static DynamicLightPickerState CreateDynamicLightPickerState(
        DynamicLightDefinition definition,
        IReadOnlyList<int> args,
        int angleDoom,
        IReadOnlyDictionary<string, object> fields,
        bool relativeMode)
    {
        bool showAllControls = DynamicLightUsesAngleValue(definition.LightNumber);
        var radiusLimits = DynamicLightRadiusLimits(relativeMode);
        var intervalLimits = DynamicLightIntervalLimits(relativeMode);

        return new DynamicLightPickerState(
            GetDynamicLightColor(definition, args, fields),
            relativeMode ? 0 : GetArgument(args, FirstDynamicLightRadiusArgument(definition.LightVavoom)),
            relativeMode ? 0 : showAllControls ? GetArgument(args, 4) : 0,
            relativeMode ? 0 : showAllControls ? angleDoom : 0,
            showAllControls,
            radiusLimits,
            radiusLimits,
            intervalLimits);
    }

    public static ColorRgb GetDynamicLightColor(
        DynamicLightDefinition definition,
        IReadOnlyList<int> args,
        IReadOnlyDictionary<string, object> fields)
        => definition.ColorMode switch
        {
            DynamicLightColorMode.VavoomGeneric => new ColorRgb(255, 255, 255),
            DynamicLightColorMode.VavoomColored => new ColorRgb(GetArgument(args, 1), GetArgument(args, 2), GetArgument(args, 3)),
            DynamicLightColorMode.SpotOrSun => GetSpotOrSunDynamicLightColor(args, fields),
            _ => new ColorRgb(GetArgument(args, 0), GetArgument(args, 1), GetArgument(args, 2)),
        };

    public static DynamicLightMutation SetDynamicLightColor(
        DynamicLightDefinition definition,
        IReadOnlyList<int> args,
        int angleDoom,
        IReadOnlyDictionary<string, object> fields,
        ColorRgb color)
    {
        int[] updatedArgs = CopyArguments(args, 5);
        var updatedFields = new Dictionary<string, object>(fields, StringComparer.OrdinalIgnoreCase);

        switch (definition.ColorMode)
        {
            case DynamicLightColorMode.VavoomGeneric:
                break;
            case DynamicLightColorMode.VavoomColored:
                updatedArgs[1] = color.Red;
                updatedArgs[2] = color.Green;
                updatedArgs[3] = color.Blue;
                break;
            case DynamicLightColorMode.SpotOrSun:
                updatedArgs[0] = 0;
                updatedFields[DynamicLightPackedColorField] = Format(color, ColorPickerInfoMode.Hex);
                break;
            default:
                updatedArgs[0] = color.Red;
                updatedArgs[1] = color.Green;
                updatedArgs[2] = color.Blue;
                break;
        }

        return new DynamicLightMutation(updatedArgs, angleDoom, updatedFields);
    }

    public static DynamicLightMutation SetDynamicLightProperties(
        DynamicLightDefinition definition,
        IReadOnlyList<int> args,
        int angleDoom,
        IReadOnlyDictionary<string, object> fields,
        int primaryRadius,
        int secondaryRadius,
        int interval,
        bool relativeMode,
        DynamicLightPickerState? fixedValues = null)
    {
        bool showAllControls = DynamicLightUsesAngleValue(definition.LightNumber);
        int firstArg = FirstDynamicLightRadiusArgument(definition.LightVavoom);
        int[] updatedArgs = CopyArguments(args, 5);

        if (relativeMode)
        {
            DynamicLightPickerState reference = fixedValues ?? CreateDynamicLightPickerState(definition, args, angleDoom, fields, false);
            updatedArgs[firstArg] = Math.Max(0, reference.PrimaryRadius + primaryRadius);
            if (showAllControls)
            {
                updatedArgs[4] = Math.Max(0, reference.SecondaryRadius + secondaryRadius);
                angleDoom = ClampAngle(reference.Interval + interval);
            }
        }
        else
        {
            if (primaryRadius != -1) updatedArgs[firstArg] = primaryRadius;
            if (showAllControls)
            {
                updatedArgs[4] = secondaryRadius;
                angleDoom = ClampAngle(interval);
            }
        }

        return new DynamicLightMutation(updatedArgs, angleDoom, new Dictionary<string, object>(fields, StringComparer.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<DynamicLightPickerState> CaptureDynamicLightFixedValues(
        IReadOnlyList<DynamicLightEditTarget> targets)
    {
        var fixedValues = new List<DynamicLightPickerState>(targets.Count);
        foreach (DynamicLightEditTarget target in targets)
            fixedValues.Add(CreateDynamicLightPickerState(
                target.Definition,
                target.Args,
                target.AngleDoom,
                target.Fields,
                relativeMode: false));

        return fixedValues;
    }

    public static IReadOnlyList<DynamicLightMutation> SetDynamicLightSelectionColor(
        IReadOnlyList<DynamicLightEditTarget> targets,
        ColorRgb color)
    {
        var mutations = new List<DynamicLightMutation>(targets.Count);
        foreach (DynamicLightEditTarget target in targets)
            mutations.Add(SetDynamicLightColor(
                target.Definition,
                target.Args,
                target.AngleDoom,
                target.Fields,
                color));

        return mutations;
    }

    public static IReadOnlyList<DynamicLightMutation> SetDynamicLightSelectionProperties(
        IReadOnlyList<DynamicLightEditTarget> targets,
        int primaryRadius,
        int secondaryRadius,
        int interval,
        bool relativeMode,
        IReadOnlyList<DynamicLightPickerState>? fixedValues = null)
    {
        var mutations = new List<DynamicLightMutation>(targets.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            DynamicLightEditTarget target = targets[i];
            DynamicLightPickerState? fixedValue =
                fixedValues != null && i < fixedValues.Count ? fixedValues[i] : null;

            mutations.Add(SetDynamicLightProperties(
                target.Definition,
                target.Args,
                target.AngleDoom,
                target.Fields,
                primaryRadius,
                secondaryRadius,
                interval,
                relativeMode,
                fixedValue));
        }

        return mutations;
    }

    public static IReadOnlyList<DynamicLightMutation> SetDynamicLightSelection(
        IReadOnlyList<DynamicLightEditTarget> targets,
        ColorRgb color,
        int primaryRadius,
        int secondaryRadius,
        int interval,
        bool relativeMode,
        IReadOnlyList<DynamicLightPickerState>? fixedValues = null)
    {
        var mutations = new List<DynamicLightMutation>(targets.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            DynamicLightEditTarget target = targets[i];
            DynamicLightPickerState? fixedValue =
                fixedValues != null && i < fixedValues.Count ? fixedValues[i] : null;
            DynamicLightMutation properties = SetDynamicLightProperties(
                target.Definition,
                target.Args,
                target.AngleDoom,
                target.Fields,
                primaryRadius,
                secondaryRadius,
                interval,
                relativeMode,
                fixedValue);
            mutations.Add(SetDynamicLightColor(target.Definition, properties.Args, properties.AngleDoom, properties.Fields, color));
        }

        return mutations;
    }

    public static void ApplyDynamicLightMutations(
        IReadOnlyList<DynamicLightThingEditTarget> targets,
        IReadOnlyList<DynamicLightMutation> mutations)
    {
        int count = Math.Min(targets.Count, mutations.Count);
        for (int i = 0; i < count; i++)
        {
            Thing thing = targets[i].Thing;
            DynamicLightMutation mutation = mutations[i];
            Array.Clear(thing.Args);
            for (int arg = 0; arg < thing.Args.Length && arg < mutation.Args.Count; arg++) thing.Args[arg] = mutation.Args[arg];
            thing.Angle = mutation.AngleDoom;
            thing.Fields.Clear();
            foreach (var field in mutation.Fields) thing.Fields[field.Key] = field.Value;
        }
    }

    public static string Format(ColorRgb rgb, ColorPickerInfoMode mode)
        => mode switch
        {
            ColorPickerInfoMode.Rgb => $"{rgb.Red} {rgb.Green} {rgb.Blue}",
            ColorPickerInfoMode.Hex => $"{rgb.Red:X02}{rgb.Green:X02}{rgb.Blue:X02}",
            ColorPickerInfoMode.Float => string.Join(
                " ",
                ((float)Math.Round(rgb.Red / 255f, 2)).ToString("F02", CultureInfo.InvariantCulture),
                ((float)Math.Round(rgb.Green / 255f, 2)).ToString("F02", CultureInfo.InvariantCulture),
                ((float)Math.Round(rgb.Blue / 255f, 2)).ToString("F02", CultureInfo.InvariantCulture)),
            _ => "",
        };

    public static ColorRgb? TryParse(ColorPickerInfoMode mode, string text)
        => mode switch
        {
            ColorPickerInfoMode.Hex => TryParseHex(text),
            ColorPickerInfoMode.Float => TryParseFloatTriplet(text),
            _ => null,
        };

    public static ColorRgb ResolveTypedColorInput(
        ColorRgb current,
        ColorRgb rgbFields,
        string? hexText,
        string? floatText)
    {
        string originalFloat = Format(current, ColorPickerInfoMode.Float);
        ColorRgb? floatColor = TryParse(ColorPickerInfoMode.Float, floatText ?? "");
        if (TypedTextChanged(floatText, originalFloat) && floatColor.HasValue) return floatColor.Value;

        string originalHex = Format(current, ColorPickerInfoMode.Hex);
        ColorRgb? hexColor = TryParse(ColorPickerInfoMode.Hex, NormalizeHexText(hexText));
        if (TypedTextChanged(hexText, originalHex) && hexColor.HasValue) return hexColor.Value;

        return rgbFields;
    }

    public static void EnsureSectorColorFields(IEnumerable<Sector> sectors, int lightColor, int fadeColor)
    {
        foreach (var sector in sectors)
        {
            if (!sector.Fields.ContainsKey(LightColorField)) sector.Fields[LightColorField] = lightColor;
            if (!sector.Fields.ContainsKey(FadeColorField)) sector.Fields[FadeColorField] = fadeColor;
        }
    }

    public static int GetSectorColor(Sector sector, SectorColorField field)
        => field == SectorColorField.LightColor
            ? sector.GetIntegerField(LightColorField, DefaultLightColor)
            : sector.GetIntegerField(FadeColorField, DefaultFadeColor);

    public static SectorColorPickerState CreateSectorColorPickerState(Sector sector, SectorColorField activeField)
        => new(
            UnpackRgb(GetSectorColor(sector, SectorColorField.LightColor)),
            UnpackRgb(GetSectorColor(sector, SectorColorField.FadeColor)),
            activeField);

    public static SectorColorPickerState SwitchSectorColorPickerField(SectorColorPickerState state, SectorColorField field)
        => state with { ActiveField = field };

    public static SectorColorPickerState SetSectorColorPickerActiveColor(SectorColorPickerState state, ColorRgb color)
        => state.ActiveField == SectorColorField.LightColor
            ? state with { LightColor = color }
            : state with { FadeColor = color };

    public static void SetSectorColor(IEnumerable<Sector> sectors, SectorColorField field, ColorRgb rgb)
    {
        string key = field == SectorColorField.LightColor ? LightColorField : FadeColorField;
        int value = PackRgb(rgb);
        foreach (var sector in sectors)
            sector.Fields[key] = value;
    }

    public static int ApplySectorColorEdit(IReadOnlyList<Sector> sectors, SectorColorField field, ColorRgb rgb, bool removeDefaults)
    {
        if (sectors.Count == 0) return 0;

        int lightColor = GetSectorColor(sectors[0], SectorColorField.LightColor);
        int fadeColor = GetSectorColor(sectors[0], SectorColorField.FadeColor);
        EnsureSectorColorFields(sectors, lightColor, fadeColor);
        SetSectorColor(sectors, field, rgb);
        if (removeDefaults) RemoveDefaultSectorColors(sectors);
        return sectors.Count;
    }

    public static void RemoveDefaultSectorColors(IEnumerable<Sector> sectors)
    {
        foreach (var sector in sectors)
        {
            if (sector.GetIntegerField(LightColorField, DefaultLightColor) == DefaultLightColor)
                sector.Fields.Remove(LightColorField);

            if (sector.GetIntegerField(FadeColorField, DefaultFadeColor) == DefaultFadeColor)
                sector.Fields.Remove(FadeColorField);
        }
    }

    private static ColorRgb? TryParseHex(string text)
    {
        string hexColor = NormalizeHexText(text).Replace("-", "", StringComparison.Ordinal);
        if (hexColor.Length != 6) return null;

        if (!int.TryParse(hexColor[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int red)) return null;
        if (!int.TryParse(hexColor.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int green)) return null;
        if (!int.TryParse(hexColor.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int blue)) return null;

        return new ColorRgb(red, green, blue);
    }

    private static ColorRgb? TryParseFloatTriplet(string text)
    {
        string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return null;

        if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float red)) return null;
        if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float green)) return null;
        if (!float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float blue)) return null;

        return new ColorRgb(FloatComponentToByte(red), FloatComponentToByte(green), FloatComponentToByte(blue));
    }

    private static bool TypedTextChanged(string? text, string original)
        => NormalizeTypedText(text) != NormalizeTypedText(original);

    private static string NormalizeTypedText(string? text)
        => (text ?? "").Trim().ToUpperInvariant();

    private static string NormalizeHexText(string? text)
        => (text ?? "").Trim().TrimStart('#');

    private static int FloatComponentToByte(float value)
        => (int)(Math.Clamp(Math.Abs(value), 0.0f, 1.0f) * 255);

    private static ColorRgb GetSpotOrSunDynamicLightColor(IReadOnlyList<int> args, IReadOnlyDictionary<string, object> fields)
    {
        if (fields.TryGetValue(DynamicLightPackedColorField, out object? textValue))
        {
            ColorRgb? parsed = TryParseHex(textValue.ToString() ?? "");
            if (parsed.HasValue) return parsed.Value;
        }

        return UnpackRgb(GetArgument(args, 0) & 0xffffff);
    }

    private static int GetArgument(IReadOnlyList<int> args, int index)
        => index >= 0 && index < args.Count ? args[index] : 0;

    private static string SliderLabel(IReadOnlyList<string> argTitles, int index)
        => (index >= 0 && index < argTitles.Count ? argTitles[index] : "") + ":";

    private static int[] CopyArguments(IReadOnlyList<int> args, int minimumLength)
    {
        int[] copy = new int[Math.Max(args.Count, minimumLength)];
        for (int i = 0; i < args.Count; i++)
            copy[i] = args[i];
        return copy;
    }

    private static int ClampAngle(int angle)
    {
        int normalized = angle % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}
