// ABOUTME: Models UDB AutomapMode line visibility, color classification, presets, and flag toggles.
// ABOUTME: Keeps renderer/UI decisions separate so editor surfaces can reuse the same automap rules.

namespace DBuilder.Map;

using DBuilder.Geometry;

public enum AutomapColorPreset
{
    Doom,
    Hexen,
    Strife,
}

public enum AutomapLineColorKind
{
    Lock,
    Secret,
    HiddenFlag,
    SingleSided,
    FloorDifference,
    CeilingDifference,
    MatchingHeight,
    Invisible,
    Normal,
}

public enum AutomapHighlightKind
{
    None,
    Linedef,
    Sector,
}

public enum AutomapPresentationLayerKind
{
    Surface,
    Overlay,
    Grid,
    Geometry,
}

public enum AutomapPresentationBlendMode
{
    Mask,
    Alpha,
}

public readonly record struct AutomapColor(byte Alpha, byte Red, byte Green, byte Blue);

public readonly record struct AutomapModeDescriptor(
    string DisplayName,
    string SwitchAction,
    string ButtonImage,
    int ButtonOrder,
    string ButtonGroup,
    bool UseByDefault);

public readonly record struct AutomapPresentationLayer(
    AutomapPresentationLayerKind Kind,
    AutomapPresentationBlendMode BlendMode,
    double Alpha = 1,
    bool GeometryOnly = false);

public sealed record AutomapPresentationDescriptor(
    bool DrawMapCenter,
    bool SkipHiddenSectors,
    IReadOnlyList<AutomapPresentationLayer> Layers);

public readonly record struct AutomapPalette(
    AutomapColor SingleSided,
    AutomapColor Secret,
    AutomapColor FloorDifference,
    AutomapColor CeilingDifference,
    AutomapColor HiddenFlag,
    AutomapColor Invisible,
    AutomapColor MatchingHeight,
    AutomapColor Background);

public sealed record AutomapModeSettings(
    bool ShowHiddenLines = false,
    bool ShowSecretSectors = false,
    bool ShowLocks = true,
    bool ShowTextures = true,
    AutomapColorPreset ColorPreset = AutomapColorPreset.Doom);

public readonly record struct AutomapSectorEffectData(int Effect, IReadOnlySet<int> GeneralizedBits)
{
    public static AutomapSectorEffectData FromSectorSpecial(int special)
        => new(special, new HashSet<int>());
}

public sealed record AutomapModeOptions(
    bool ShowHiddenLines = false,
    bool ShowSecretSectors = false,
    bool ShowLocks = true,
    bool InvertLineVisibility = false,
    bool IsUdmf = true,
    bool IsDoom = true,
    Func<Sector, AutomapSectorEffectData>? SectorEffectData = null,
    IReadOnlyDictionary<int, int>? LockableActionArgs = null,
    IReadOnlyDictionary<int, AutomapColor>? LockColors = null);

public readonly record struct AutomapLineStyle(
    bool IsValid,
    AutomapLineColorKind Kind,
    AutomapColor Color);

public sealed record AutomapHighlightResult(
    AutomapHighlightKind Kind,
    Linedef? Line,
    Sector? Sector,
    IReadOnlyList<Linedef> Lines);

public readonly record struct AutomapRenderLine(
    Linedef Line,
    AutomapColor Color,
    bool IsHighlight);

public sealed record AutomapRenderPlan(
    IReadOnlyList<AutomapRenderLine> Lines,
    bool RenderTexturedSurfaces,
    AutomapColor? BackgroundColor);

public static class AutomapModeModel
{
    public const double LineLengthScaler = 0.001;
    public const string ShowHiddenLinesSettingKey = "automapmode.showhiddenlines";
    public const string ShowSecretSectorsSettingKey = "automapmode.showsecretsectors";
    public const string ShowLocksSettingKey = "automapmode.showlocks";
    public const string ShowTexturesSettingKey = "automapmode.showtextures";
    public const string ColorPresetSettingKey = "automapmode.colorpreset";
    public const string SecretFlag = "secret";
    public const string HiddenFlag = "dontdraw";
    public const int ClassicSecretFlagBit = 32;
    public const int ClassicHiddenFlagBit = 128;
    public const string TexturedAutomapHiddenSectorFlag = "hidden";
    public static AutomapColor DefaultInfoLineColor { get; } = new(255, 198, 198, 255);
    public static AutomapColor DefaultHighlightColor { get; } = new(255, 255, 172, 0);

    public static AutomapModeDescriptor ModeDescriptor { get; } = new(
        "Automap Mode",
        "automapmode",
        "automap.png",
        int.MinValue + 503,
        "000_editing",
        true);

    public static AutomapModeSettings DefaultSettings { get; } = new();

    public static AutomapPresentationDescriptor Presentation { get; } = new(
        DrawMapCenter: false,
        SkipHiddenSectors: true,
        new[]
        {
            new AutomapPresentationLayer(AutomapPresentationLayerKind.Surface, AutomapPresentationBlendMode.Mask),
            new AutomapPresentationLayer(AutomapPresentationLayerKind.Overlay, AutomapPresentationBlendMode.Mask),
            new AutomapPresentationLayer(AutomapPresentationLayerKind.Grid, AutomapPresentationBlendMode.Mask),
            new AutomapPresentationLayer(AutomapPresentationLayerKind.Geometry, AutomapPresentationBlendMode.Alpha, 1, true),
        });

    public static AutomapPalette Palette(AutomapColorPreset preset) => preset switch
    {
        AutomapColorPreset.Hexen => new AutomapPalette(
            new(255, 89, 64, 27),
            new(255, 255, 0, 255),
            new(255, 208, 176, 133),
            new(255, 103, 59, 31),
            new(255, 192, 192, 192),
            new(255, 108, 108, 108),
            new(255, 108, 108, 108),
            new(255, 163, 129, 84)),
        AutomapColorPreset.Strife => new AutomapPalette(
            new(255, 199, 195, 195),
            new(255, 255, 0, 255),
            new(255, 55, 59, 91),
            new(255, 108, 108, 108),
            new(255, 0, 87, 130),
            new(255, 192, 192, 192),
            new(255, 112, 112, 160),
            new(255, 0, 0, 0)),
        _ => new AutomapPalette(
            new(255, 252, 0, 0),
            new(255, 255, 0, 255),
            new(255, 188, 120, 72),
            new(255, 252, 252, 0),
            new(255, 192, 192, 192),
            new(255, 192, 192, 192),
            new(255, 108, 108, 108),
            new(255, 0, 0, 0)),
    };

    public static AutomapModeOptions ToOptions(AutomapModeSettings settings, bool invertLineVisibility = false, bool isUdmf = true, bool isDoom = true)
        => new(
            settings.ShowHiddenLines,
            settings.ShowSecretSectors,
            settings.ShowLocks,
            invertLineVisibility,
            isUdmf,
            isDoom);

    public static List<Linedef> GetValidLinedefs(MapSet map, AutomapModeOptions options)
        => map.Linedefs.Where(line => IsLineValid(line, options)).ToList();

    public static AutomapHighlightResult PlanHighlight(
        MapSet map,
        ICollection<Linedef> validLinedefs,
        Vector2D mouseMapPosition,
        double highlightRange,
        double rendererScale,
        bool editSectors)
    {
        if (editSectors)
        {
            Sector? sector = map.GetSectorAt(mouseMapPosition);
            return sector == null
                ? new AutomapHighlightResult(AutomapHighlightKind.None, null, null, Array.Empty<Linedef>())
                : new AutomapHighlightResult(AutomapHighlightKind.Sector, null, sector, SectorBoundaryLines(sector));
        }

        double scale = rendererScale <= 0 ? 1 : rendererScale;
        Linedef? line = MapSet.NearestLinedefRange(validLinedefs, mouseMapPosition, highlightRange / scale);
        return line == null
            ? new AutomapHighlightResult(AutomapHighlightKind.None, null, null, Array.Empty<Linedef>())
            : new AutomapHighlightResult(AutomapHighlightKind.Linedef, line, null, new[] { line });
    }

    public static AutomapRenderPlan BuildRenderPlan(
        MapSet map,
        AutomapModeOptions options,
        AutomapModeSettings settings,
        AutomapPalette palette,
        AutomapHighlightResult? highlight = null,
        bool editSectors = false,
        AutomapColor? infoLineColor = null,
        AutomapColor? highlightColor = null)
    {
        var lines = new List<AutomapRenderLine>();
        foreach (var line in map.Linedefs)
        {
            AutomapLineStyle style = DetermineLineStyle(line, options, palette);
            if (style.IsValid) lines.Add(new AutomapRenderLine(line, style.Color, IsHighlight: false));
        }

        if (highlight is { } h)
        {
            if (h.Kind == AutomapHighlightKind.Linedef && h.Line != null && IsLineValid(h.Line, options))
            {
                lines.Add(new AutomapRenderLine(h.Line, infoLineColor ?? DefaultInfoLineColor, IsHighlight: true));
            }
            else if (h.Kind == AutomapHighlightKind.Sector)
            {
                AutomapColor color = highlightColor ?? DefaultHighlightColor;
                foreach (var line in h.Lines)
                    lines.Add(new AutomapRenderLine(line, color, IsHighlight: true));
            }
        }

        bool showTextures = settings.ShowTextures || editSectors;
        return new AutomapRenderPlan(
            lines,
            RenderTexturedSurfaces: showTextures,
            BackgroundColor: showTextures ? null : palette.Background);
    }

    public static AutomapLineStyle DetermineLineStyle(Linedef line, AutomapModeOptions options, AutomapPalette palette)
    {
        bool valid = IsLineValid(line, options);
        AutomapLineColorKind kind = DetermineLineKind(line, options);
        return new AutomapLineStyle(valid, kind, ColorForKind(kind, palette, ResolveLockColor(line, options)));
    }

    public static bool IsLineValid(Linedef line, AutomapModeOptions options)
    {
        if (options.ShowHiddenLines ^ options.InvertLineVisibility) return true;
        if (IsHiddenFlagSet(line, options.IsUdmf)) return false;
        if (line.Back == null || line.Front == null || IsSecretFlagSet(line, options.IsUdmf)) return true;
        if (line.Front.Sector == null || line.Back.Sector == null) return true;
        if (line.Front.Sector.FloorHeight != line.Back.Sector.FloorHeight) return true;
        if (line.Front.Sector.CeilHeight != line.Back.Sector.CeilHeight) return true;

        return false;
    }

    public static AutomapLineColorKind DetermineLineKind(Linedef line, AutomapModeOptions options)
    {
        if (options.ShowLocks && ResolveLockColor(line, options) != null) return AutomapLineColorKind.Lock;
        if (options.ShowSecretSectors && IsAdjacentToSecretSector(line, options)) return AutomapLineColorKind.Secret;
        if (IsHiddenFlagSet(line, options.IsUdmf)) return AutomapLineColorKind.HiddenFlag;
        if (line.Back == null || line.Front == null || IsSecretFlagSet(line, options.IsUdmf)) return AutomapLineColorKind.SingleSided;
        if (line.Front.Sector == null || line.Back.Sector == null) return AutomapLineColorKind.SingleSided;
        if (line.Front.Sector.FloorHeight != line.Back.Sector.FloorHeight) return AutomapLineColorKind.FloorDifference;
        if (line.Front.Sector.CeilHeight != line.Back.Sector.CeilHeight) return AutomapLineColorKind.CeilingDifference;
        if (line.Front.Sector.CeilHeight == line.Back.Sector.CeilHeight && line.Front.Sector.FloorHeight == line.Back.Sector.FloorHeight)
            return AutomapLineColorKind.MatchingHeight;
        if (options.ShowHiddenLines ^ options.InvertLineVisibility) return AutomapLineColorKind.Invisible;
        return AutomapLineColorKind.Normal;
    }

    public static bool IsSectorVisible(Sector? sector)
        => sector != null && !sector.IsFlagSet(TexturedAutomapHiddenSectorFlag);

    public static bool IsSectorSecret(Sector sector, bool isDoom, Func<Sector, AutomapSectorEffectData>? sectorEffectData = null)
    {
        AutomapSectorEffectData data = sectorEffectData?.Invoke(sector) ?? AutomapSectorEffectData.FromSectorSpecial(sector.Special);
        return isDoom
            ? data.Effect == 9 || data.GeneralizedBits.Contains(128)
            : data.GeneralizedBits.Contains(1024);
    }

    public static void ToggleSecretFlag(Linedef line)
        => line.SetFlag(SecretFlag, !line.IsFlagSet(SecretFlag));

    public static void ToggleHiddenFlag(Linedef line)
        => line.SetFlag(HiddenFlag, !line.IsFlagSet(HiddenFlag));

    public static void ToggleSecretFlag(Linedef line, bool isUdmf)
    {
        if (isUdmf) ToggleSecretFlag(line);
        else ToggleClassicFlag(line, ClassicSecretFlagBit);
    }

    public static void ToggleHiddenFlag(Linedef line, bool isUdmf)
    {
        if (isUdmf) ToggleHiddenFlag(line);
        else ToggleClassicFlag(line, ClassicHiddenFlagBit);
    }

    public static void ToggleTexturedAutomapHiddenFlag(Sector sector)
        => sector.SetFlag(TexturedAutomapHiddenSectorFlag, !sector.IsFlagSet(TexturedAutomapHiddenSectorFlag));

    private static bool IsAdjacentToSecretSector(Linedef line, AutomapModeOptions options)
        => line.Front?.Sector != null && IsSectorSecret(line.Front.Sector, options.IsDoom, options.SectorEffectData)
        || line.Back?.Sector != null && IsSectorSecret(line.Back.Sector, options.IsDoom, options.SectorEffectData);

    private static bool IsSecretFlagSet(Linedef line, bool isUdmf)
        => isUdmf ? line.IsFlagSet(SecretFlag) : (line.Flags & ClassicSecretFlagBit) != 0;

    private static bool IsHiddenFlagSet(Linedef line, bool isUdmf)
        => isUdmf ? line.IsFlagSet(HiddenFlag) : (line.Flags & ClassicHiddenFlagBit) != 0;

    private static void ToggleClassicFlag(Linedef line, int bit)
    {
        if ((line.Flags & bit) != 0) line.Flags &= ~bit;
        else line.Flags |= bit;
    }

    private static List<Linedef> SectorBoundaryLines(Sector sector)
    {
        var lines = new List<Linedef>();
        var seen = new HashSet<Linedef>();
        foreach (var side in sector.Sidedefs)
        {
            if (side.Line == null || !seen.Add(side.Line)) continue;
            lines.Add(side.Line);
        }
        return lines;
    }

    private static AutomapColor ColorForKind(AutomapLineColorKind kind, AutomapPalette palette, AutomapColor? lockColor)
        => kind switch
        {
            AutomapLineColorKind.Lock => lockColor ?? palette.SingleSided,
            AutomapLineColorKind.Secret => palette.Secret,
            AutomapLineColorKind.HiddenFlag => palette.HiddenFlag,
            AutomapLineColorKind.SingleSided => palette.SingleSided,
            AutomapLineColorKind.FloorDifference => palette.FloorDifference,
            AutomapLineColorKind.CeilingDifference => palette.CeilingDifference,
            AutomapLineColorKind.MatchingHeight => palette.MatchingHeight,
            AutomapLineColorKind.Invisible => palette.Invisible,
            _ => new AutomapColor(255, 255, 255, 255),
        };

    private static AutomapColor? ResolveLockColor(Linedef line, AutomapModeOptions options)
    {
        int lockNumber = 0;
        if (options.IsUdmf && line.Fields.TryGetValue("locknumber", out object? value))
            lockNumber = CoerceInt(value) ?? 0;

        if (lockNumber == 0
            && line.Action != 0
            && options.LockableActionArgs?.TryGetValue(line.Action, out int argIndex) == true
            && argIndex >= 0
            && argIndex < line.Args.Length)
            lockNumber = line.Args[argIndex];

        if (lockNumber != 0 && options.LockColors?.TryGetValue(lockNumber, out AutomapColor color) == true)
            return color;

        return null;
    }

    private static int? CoerceInt(object? value)
        => value switch
        {
            int i => i,
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            short s => s,
            byte b => b,
            string text when int.TryParse(text, out int i) => i,
            _ => null,
        };
}
