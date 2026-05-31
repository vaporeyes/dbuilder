// ABOUTME: Provides UDB-compatible indexed editor colors and assist variants.
// ABOUTME: Reads and writes colors.colorN settings while supplying UDB defaults.
namespace DBuilder.Rendering;

public sealed class ColorCollection
{
    private const float BrightMultiplier = 1.0f;
    private const float BrightAddition = 0.4f;
    private const float DarkMultiplier = 0.9f;
    private const float DarkAddition = -0.2f;

    public const int NumColors = 53;
    public const int NumThingColors = 20;
    public const int ThingColorsOffset = 20;

    public const int BackgroundIndex = 0;
    public const int VerticesIndex = 1;
    public const int LinedefsIndex = 2;
    public const int ModelWireColorIndex = 3;
    public const int InfoLineColorIndex = 4;
    public const int HighlightIndex = 5;
    public const int SelectionIndex = 6;
    public const int IndicationIndex = 7;
    public const int GridIndex = 8;
    public const int Grid64Index = 9;
    public const int Crosshair3DIndex = 10;
    public const int Highlight3DIndex = 11;
    public const int Selection3DIndex = 12;
    public const int ScriptBackgroundIndex = 13;
    public const int LineNumbersIndex = 14;
    public const int PlainTextIndex = 15;
    public const int CommentsIndex = 16;
    public const int KeywordsIndex = 17;
    public const int LiteralsIndex = 18;
    public const int ConstantsIndex = 19;
    public const int ThingColor00Index = 20;
    public const int ThreeDFloorColorIndex = 40;
    public const int ScriptIndicatorIndex = 41;
    public const int ScriptBraceHighlightIndex = 42;
    public const int ScriptBadBraceHighlightIndex = 43;
    public const int ScriptWhitespaceIndex = 44;
    public const int ScriptSelectionForeIndex = 45;
    public const int ScriptSelectionBackIndex = 46;
    public const int StringsIndex = 47;
    public const int IncludesIndex = 48;
    public const int ScriptFoldForeIndex = 49;
    public const int ScriptFoldBackIndex = 50;
    public const int PropertiesIndex = 51;
    public const int GuidelineColorIndex = 52;

    private readonly PixelColor[] _colors;
    private readonly PixelColor[] _brightColors;
    private readonly PixelColor[] _darkColors;
    private readonly byte[] _correctionTable;

    public ColorCollection(IReadOnlyDictionary<string, int>? settings = null, int imageBrightness = 0)
    {
        _colors = new PixelColor[NumColors];
        _brightColors = new PixelColor[NumColors];
        _darkColors = new PixelColor[NumColors];

        for (int i = 0; i < NumColors; i++)
        {
            int argb = settings != null && settings.TryGetValue(SettingKey(i), out int configured) ? configured : 0;
            _colors[i] = PixelColor.FromArgb(argb);
        }

        ApplyDefaults();
        CreateAssistColors();
        _correctionTable = CreateCorrectionTable(imageBrightness);
    }

    public IReadOnlyList<PixelColor> Colors => _colors;
    public IReadOnlyList<PixelColor> BrightColors => _brightColors;
    public IReadOnlyList<PixelColor> DarkColors => _darkColors;
    public IReadOnlyList<byte> CorrectionTable => _correctionTable;

    public PixelColor Background => _colors[BackgroundIndex];
    public PixelColor Vertices => _colors[VerticesIndex];
    public PixelColor Linedefs => _colors[LinedefsIndex];
    public PixelColor Highlight => _colors[HighlightIndex];
    public PixelColor Selection => _colors[SelectionIndex];
    public PixelColor Indication => _colors[IndicationIndex];
    public PixelColor Grid => _colors[GridIndex];
    public PixelColor Grid64 => _colors[Grid64Index];
    public PixelColor ModelWireframe => _colors[ModelWireColorIndex];
    public PixelColor InfoLine => _colors[InfoLineColorIndex];
    public PixelColor Guideline => _colors[GuidelineColorIndex];
    public PixelColor ThreeDFloor => _colors[ThreeDFloorColorIndex];
    public PixelColor Crosshair3D => _colors[Crosshair3DIndex];
    public PixelColor Highlight3D => _colors[Highlight3DIndex];
    public PixelColor Selection3D => _colors[Selection3DIndex];
    public PixelColor ScriptBackground => _colors[ScriptBackgroundIndex];
    public PixelColor ScriptIndicator => _colors[ScriptIndicatorIndex];
    public PixelColor ScriptBraceHighlight => _colors[ScriptBraceHighlightIndex];
    public PixelColor ScriptBadBraceHighlight => _colors[ScriptBadBraceHighlightIndex];
    public PixelColor ScriptWhitespace => _colors[ScriptWhitespaceIndex];
    public PixelColor ScriptSelectionForeColor => _colors[ScriptSelectionForeIndex];
    public PixelColor ScriptSelectionBackColor => _colors[ScriptSelectionBackIndex];
    public PixelColor LineNumbers => _colors[LineNumbersIndex];
    public PixelColor PlainText => _colors[PlainTextIndex];
    public PixelColor Comments => _colors[CommentsIndex];
    public PixelColor Keywords => _colors[KeywordsIndex];
    public PixelColor Properties => _colors[PropertiesIndex];
    public PixelColor Literals => _colors[LiteralsIndex];
    public PixelColor Constants => _colors[ConstantsIndex];
    public PixelColor Strings => _colors[StringsIndex];
    public PixelColor Includes => _colors[IncludesIndex];
    public PixelColor ScriptFoldForeColor => _colors[ScriptFoldForeIndex];
    public PixelColor ScriptFoldBackColor => _colors[ScriptFoldBackIndex];

    public static string SettingKey(int index) => $"colors.color{index}";

    public Dictionary<string, int> SaveSettings()
    {
        var settings = new Dictionary<string, int>(NumColors);
        for (int i = 0; i < NumColors; i++) settings[SettingKey(i)] = _colors[i].ToArgb();
        return settings;
    }

    public static PixelColor CreateBrightVariant(PixelColor color)
        => CreateVariant(color, BrightMultiplier, BrightAddition);

    public static PixelColor CreateDarkVariant(PixelColor color)
        => CreateVariant(color, DarkMultiplier, DarkAddition);

    public static byte[] CreateCorrectionTable(int imageBrightness)
    {
        float gamma = (imageBrightness + 10) * 0.1f;
        float bright = imageBrightness * 5f;
        var table = new byte[256];

        for (int i = 0; i < table.Length; i++)
        {
            float value = i * gamma + bright;
            table[i] = value < 0f ? (byte)0 : value > 255f ? (byte)255 : (byte)value;
        }

        return table;
    }

    public void ApplyColorCorrection(Span<PixelColor> pixels)
        => ApplyColorCorrection(pixels, _correctionTable);

    public static void ApplyColorCorrection(Span<PixelColor> pixels, IReadOnlyList<byte> correctionTable)
    {
        if (correctionTable.Count != 256) throw new ArgumentException("Color correction table must have 256 entries.", nameof(correctionTable));

        for (int i = pixels.Length - 1; i >= 0; i--)
        {
            PixelColor pixel = pixels[i];
            pixels[i] = new PixelColor(pixel.A, correctionTable[pixel.R], correctionTable[pixel.G], correctionTable[pixel.B]);
        }
    }

    private void ApplyDefaults()
    {
        Default(BackgroundIndex, unchecked((int)0xFF000000));
        Default(VerticesIndex, unchecked((int)0xFF51A2FF));
        Default(LinedefsIndex, unchecked((int)0xFFFFFFFF));
        Default(ModelWireColorIndex, unchecked((int)0xFFBF00DF));
        Default(InfoLineColorIndex, unchecked((int)0xFFC6C6FF));
        Default(HighlightIndex, unchecked((int)0xFFFFAC00));
        Default(SelectionIndex, unchecked((int)0xFFFF4000));
        Default(IndicationIndex, unchecked((int)0xFFFFFF80));
        Default(GridIndex, unchecked((int)0xFF464646));
        Default(Grid64Index, unchecked((int)0xFF39392F));
        Default(Crosshair3DIndex, unchecked((int)0xFF00FFFF));
        Default(Highlight3DIndex, unchecked((int)0xFFFFA000));
        Default(Selection3DIndex, unchecked((int)0xFFFF4000));
        Default(ScriptBackgroundIndex, unchecked((int)0xFFFFFFFF));
        Default(LineNumbersIndex, unchecked((int)0xFF2B96AF));
        Default(PlainTextIndex, unchecked((int)0xFF000000));
        Default(CommentsIndex, unchecked((int)0xFF008000));
        Default(KeywordsIndex, unchecked((int)0xFF008ACB));
        Default(LiteralsIndex, unchecked((int)0xFF000077));
        Default(ConstantsIndex, unchecked((int)0xFF804080));
        Default(GuidelineColorIndex, unchecked((int)0xFFFFFF00));

        Default(ThingColor00Index + 0, unchecked((int)0xFF696969));
        Default(ThingColor00Index + 1, unchecked((int)0xFF4169E1));
        Default(ThingColor00Index + 2, unchecked((int)0xFF228B22));
        Default(ThingColor00Index + 3, unchecked((int)0xFF20B2AA));
        Default(ThingColor00Index + 4, unchecked((int)0xFFB22222));
        Default(ThingColor00Index + 5, unchecked((int)0xFF9400D3));
        Default(ThingColor00Index + 6, unchecked((int)0xFFB8860B));
        Default(ThingColor00Index + 7, unchecked((int)0xFFC0C0C0));
        Default(ThingColor00Index + 8, unchecked((int)0xFF808080));
        Default(ThingColor00Index + 9, unchecked((int)0xFF00BFFF));
        Default(ThingColor00Index + 10, unchecked((int)0xFF32CD32));
        Default(ThingColor00Index + 11, unchecked((int)0xFFAFEEEE));
        Default(ThingColor00Index + 12, unchecked((int)0xFFFF6347));
        Default(ThingColor00Index + 13, unchecked((int)0xFFEE82EE));
        Default(ThingColor00Index + 14, unchecked((int)0xFFFFFF00));
        Default(ThingColor00Index + 15, unchecked((int)0xFFF5F5F5));
        Default(ThingColor00Index + 16, unchecked((int)0xFFFFB6C1));
        Default(ThingColor00Index + 17, unchecked((int)0xFFFF8C00));
        Default(ThingColor00Index + 18, unchecked((int)0xFFBDB76B));
        Default(ThingColor00Index + 19, unchecked((int)0xFFDAA520));

        Default(ThreeDFloorColorIndex, unchecked((int)0xFFFF0000));
        Default(ScriptIndicatorIndex, unchecked((int)0xFF00FF00));
        Default(ScriptBraceHighlightIndex, unchecked((int)0xFF00FFFF));
        Default(ScriptBadBraceHighlightIndex, unchecked((int)0xFFFF0000));
        Default(ScriptWhitespaceIndex, unchecked((int)0xFF808080));
        Default(ScriptSelectionForeIndex, unchecked((int)0xFFFFFFFF));
        Default(ScriptSelectionBackIndex, unchecked((int)0xFF3399FF));
        Default(StringsIndex, unchecked((int)0xFF800000));
        Default(IncludesIndex, unchecked((int)0xFF696969));
        Default(ScriptFoldForeIndex, unchecked((int)0xFFA0A0A0));
        Default(ScriptFoldBackIndex, unchecked((int)0xFFFFFFFF));
        Default(PropertiesIndex, unchecked((int)0xFF0099A1));
    }

    private void Default(int index, int argb)
    {
        if (_colors[index].ToArgb() == 0) _colors[index] = PixelColor.FromArgb(argb);
    }

    private void CreateAssistColors()
    {
        for (int i = 0; i < NumColors; i++)
        {
            _brightColors[i] = CreateBrightVariant(_colors[i]);
            _darkColors[i] = CreateDarkVariant(_colors[i]);
        }
    }

    private static PixelColor CreateVariant(PixelColor color, float multiplier, float addition)
        => new(
            255,
            ChannelVariant(color.R, multiplier, addition),
            ChannelVariant(color.G, multiplier, addition),
            ChannelVariant(color.B, multiplier, addition));

    private static byte ChannelVariant(byte channel, float multiplier, float addition)
    {
        float value = Saturate(channel * PixelColor.ByteToFloat * multiplier + addition);
        return (byte)(value * 255.0f);
    }

    private static float Saturate(float value)
    {
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }
}
