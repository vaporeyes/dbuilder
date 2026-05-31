// ABOUTME: Models UDB AutomapMode line visibility, color classification, presets, and flag toggles.
// ABOUTME: Keeps renderer/UI decisions separate so editor surfaces can reuse the same automap rules.

namespace DBuilder.Map;

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

public readonly record struct AutomapColor(byte Alpha, byte Red, byte Green, byte Blue);

public readonly record struct AutomapPalette(
    AutomapColor SingleSided,
    AutomapColor Secret,
    AutomapColor FloorDifference,
    AutomapColor CeilingDifference,
    AutomapColor HiddenFlag,
    AutomapColor Invisible,
    AutomapColor MatchingHeight,
    AutomapColor Background);

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
    bool IsUdmf = false,
    bool IsDoom = true,
    Func<Sector, AutomapSectorEffectData>? SectorEffectData = null,
    IReadOnlyDictionary<int, int>? LockableActionArgs = null,
    IReadOnlyDictionary<int, AutomapColor>? LockColors = null);

public readonly record struct AutomapLineStyle(
    bool IsValid,
    AutomapLineColorKind Kind,
    AutomapColor Color);

public static class AutomapModeModel
{
    public const string SecretFlag = "secret";
    public const string HiddenFlag = "dontdraw";
    public const string TexturedAutomapHiddenSectorFlag = "hidden";

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

    public static AutomapLineStyle DetermineLineStyle(Linedef line, AutomapModeOptions options, AutomapPalette palette)
    {
        bool valid = IsLineValid(line, options);
        AutomapLineColorKind kind = DetermineLineKind(line, options);
        return new AutomapLineStyle(valid, kind, ColorForKind(kind, palette, ResolveLockColor(line, options)));
    }

    public static bool IsLineValid(Linedef line, AutomapModeOptions options)
    {
        if (options.ShowHiddenLines ^ options.InvertLineVisibility) return true;
        if (line.IsFlagSet(HiddenFlag)) return false;
        if (line.Back == null || line.Front == null || line.IsFlagSet(SecretFlag)) return true;
        if (line.Front.Sector == null || line.Back.Sector == null) return true;
        if (line.Front.Sector.FloorHeight != line.Back.Sector.FloorHeight) return true;
        if (line.Front.Sector.CeilHeight != line.Back.Sector.CeilHeight) return true;

        return false;
    }

    public static AutomapLineColorKind DetermineLineKind(Linedef line, AutomapModeOptions options)
    {
        if (options.ShowLocks && ResolveLockColor(line, options) != null) return AutomapLineColorKind.Lock;
        if (options.ShowSecretSectors && IsAdjacentToSecretSector(line, options)) return AutomapLineColorKind.Secret;
        if (line.IsFlagSet(HiddenFlag)) return AutomapLineColorKind.HiddenFlag;
        if (line.Back == null || line.Front == null || line.IsFlagSet(SecretFlag)) return AutomapLineColorKind.SingleSided;
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

    public static void ToggleTexturedAutomapHiddenFlag(Sector sector)
        => sector.SetFlag(TexturedAutomapHiddenSectorFlag, !sector.IsFlagSet(TexturedAutomapHiddenSectorFlag));

    private static bool IsAdjacentToSecretSector(Linedef line, AutomapModeOptions options)
        => line.Front?.Sector != null && IsSectorSecret(line.Front.Sector, options.IsDoom, options.SectorEffectData)
        || line.Back?.Sector != null && IsSectorSecret(line.Back.Sector, options.IsDoom, options.SectorEffectData);

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
