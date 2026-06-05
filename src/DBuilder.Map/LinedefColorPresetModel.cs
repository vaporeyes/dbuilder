// ABOUTME: Models UDB linedef color presets and matching rules against DBuilder linedefs.
// ABOUTME: Keeps preset metadata testable before the full preset editor and renderer wiring land.

namespace DBuilder.Map;

public sealed record LinedefColorPreset(
    string Name,
    int Color,
    int Action = LinedefColorPresetModel.AnyAction,
    int Activation = LinedefColorPresetModel.AnyActivation,
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
    public const string ConfigureActionTitle = "Configure Linedefs Colors";
    public const string ConfigureActionDescription = "Shows the Linedef Color Presets setup dialog, which allows you to add, remove and change linedef color presets.";
    public const string EditorPendingStatusText = "Linedef color preset editor is pending.";

    public static IReadOnlyList<LinedefColorPreset> DefaultPresets { get; } =
    [
        new("Any action", PaleGreenArgb, AnyAction, DefaultAnyActionActivation, Array.Empty<string>(), Array.Empty<string>(), true),
    ];

    public static bool Matches(Linedef line, LinedefColorPreset preset)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(preset);

        if (!preset.Enabled) return false;
        if (preset.Action != AnyAction && line.Action != preset.Action) return false;
        if (preset.Activation != AnyActivation && line.Activate != preset.Activation) return false;

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
}
