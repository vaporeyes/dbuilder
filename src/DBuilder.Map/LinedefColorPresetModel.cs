// ABOUTME: Models UDB linedef color presets and matching rules against DBuilder linedefs.
// ABOUTME: Keeps preset metadata testable before the full preset editor and renderer wiring land.

namespace DBuilder.Map;

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
    public const string ConfigureActionTitle = "Configure Linedefs Colors";
    public const string ConfigureActionDescription = "Shows the Linedef Color Presets setup dialog, which allows you to add, remove and change linedef color presets.";
    public const string EditorPendingStatusText = "Linedef color preset editor is pending.";

    public static IReadOnlyList<LinedefColorPreset> DefaultPresets { get; } =
    [
        new("Any action", PaleGreenArgb, AnyAction, DefaultAnyActionActivation, Array.Empty<string>(), Array.Empty<string>(), true),
    ];

    public static IReadOnlyList<LinedefColorPreset> NormalizedPresets(IReadOnlyList<LinedefColorPreset>? presets)
        => presets is { Count: > 0 }
            ? presets
            : DefaultPresets;

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
}
