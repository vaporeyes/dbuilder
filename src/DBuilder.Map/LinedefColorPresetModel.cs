// ABOUTME: Models UDB linedef color presets and matching rules against DBuilder linedefs.
// ABOUTME: Keeps preset metadata testable before the full preset editor and renderer wiring land.

namespace DBuilder.Map;

using System.Globalization;

public sealed record LinedefColorPreset(
    string Name,
    int Color,
    int Action = 0,
    int Activation = 0,
    IReadOnlyList<string>? Flags = null,
    IReadOnlyList<string>? RestrictedFlags = null,
    bool Enabled = true)
{
    public IReadOnlyList<string> RequiredFlags => Flags ?? Array.Empty<string>();
    public IReadOnlyList<string> DisallowedFlags => RestrictedFlags ?? Array.Empty<string>();
}

public static class LinedefColorPresetModel
{
    public const int AnyAction = -1;
    public const int AnyActivation = -1;
    public const int DefaultAnyActionActivation = 0;
    public const int PaleGreenArgb = unchecked((int)0xff98fb98);
    public const byte DefaultDoubleSidedAlpha = 0x80;
    public const string FlagsSeparator = "^";
    public const string ConfigureActionTitle = "Configure Linedefs Colors";
    public const string ConfigureActionDescription = "Shows the Linedef Color Presets setup dialog, which allows you to add, remove and change linedef color presets.";

    public static IReadOnlyList<LinedefColorPreset> DefaultPresets { get; } =
    [
        new("Any action", PaleGreenArgb, AnyAction, DefaultAnyActionActivation, Array.Empty<string>(), Array.Empty<string>(), true),
    ];

    public static IReadOnlyList<LinedefColorPreset> NormalizedPresets(IReadOnlyList<LinedefColorPreset>? presets)
        => presets is { Count: > 0 }
            ? presets
            : DefaultPresets;

    public static string FormatColor(int color)
        => unchecked((uint)color).ToString("X8", CultureInfo.InvariantCulture);

    public static int ParseColor(string? text, int fallback)
    {
        string value = (text ?? "").Trim();
        if (value.StartsWith("#", StringComparison.Ordinal)) value = value[1..];
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) value = value[2..];
        if (uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
            return unchecked((int)hex);
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric))
            return numeric;
        return fallback;
    }

    public static string FormatFlags(IEnumerable<string> flags)
        => string.Join(FlagsSeparator, flags.Where(flag => !string.IsNullOrWhiteSpace(flag)).Select(flag => flag.Trim()));

    public static IReadOnlyList<string> ParseFlags(string? text)
        => (text ?? "")
            .Split(['^', ',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string SavedStatusText(int count)
        => count == 1 ? "Saved 1 linedef color preset." : $"Saved {count} linedef color presets.";

    public static IReadOnlyList<string> ActivePresetNames(IReadOnlyList<LinedefColorPreset> presets)
        => presets
            .Where(preset => preset.Enabled)
            .Select(preset => preset.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

    public static string ToolbarButtonText(IReadOnlyList<LinedefColorPreset> presets, int maxCharacters)
    {
        var names = ActivePresetNames(presets);
        if (names.Count == 0) return "No active presets";

        string text = string.Join(", ", names);
        return text.Length <= maxCharacters ? text : $"{names.Count} {Plural(names.Count, "preset")} active";
    }

    public static IReadOnlyList<LinedefColorPreset> SetPresetEnabled(
        IReadOnlyList<LinedefColorPreset> presets,
        int index,
        bool enabled)
        => presets
            .Select((preset, currentIndex) => currentIndex == index ? preset with { Enabled = enabled } : preset)
            .ToArray();

    public static string ToggleStatusText(LinedefColorPreset preset)
        => $"{(preset.Enabled ? "Enabled" : "Disabled")} linedef color preset: {preset.Name}.";

    public static bool Matches(Linedef line, LinedefColorPreset preset, bool isUdmf = false)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(preset);

        if (!preset.Enabled) return false;
        if (preset.Action != 0)
        {
            if ((preset.Action == AnyAction && line.Action == 0) || (preset.Action != AnyAction && line.Action != preset.Action))
                return false;
        }

        if (!isUdmf && preset.Activation != 0)
        {
            if ((preset.Activation == AnyActivation && line.Activate == 0) || (preset.Activation != AnyActivation && line.Activate != preset.Activation))
                return false;
        }

        foreach (string flag in preset.RequiredFlags)
        {
            if (!line.UdmfFlags.Contains(flag)) return false;
        }

        foreach (string flag in preset.DisallowedFlags)
        {
            if (line.UdmfFlags.Contains(flag)) return false;
        }

        return true;
    }

    public static bool TryGetColor(
        Linedef line,
        IReadOnlyList<LinedefColorPreset> presets,
        bool isUdmf,
        out int color)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(presets);

        foreach (LinedefColorPreset preset in presets)
        {
            if (!Matches(line, preset, isUdmf)) continue;
            color = preset.Color;
            return true;
        }

        color = 0;
        return false;
    }

    public static int WithAlpha(int argb, byte alpha)
        => unchecked((int)(((uint)alpha << 24) | ((uint)argb & 0x00ffffffu)));

    private static string Plural(int count, string noun)
        => count == 1 ? noun : $"{noun}s";
}
