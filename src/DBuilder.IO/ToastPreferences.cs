// ABOUTME: Models UDB-compatible toast notification preference values and action toggles.
// ABOUTME: Keeps toast settings normalized before a live toast manager applies them.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DBuilder.IO;

public enum ToastAnchor
{
    TopLeft = 1,
    TopRight = 2,
    BottomRight = 3,
    BottomLeft = 4,
}

public sealed record ToastActionPreference(string Name, string Title, string Description, bool Enabled);

public static class ToastPreferences
{
    public const int DefaultDurationMilliseconds = 3000;
    public const int MinDurationMilliseconds = 1000;
    public const ToastAnchor DefaultAnchor = ToastAnchor.BottomRight;
    public const string StatusWarningActionName = "status.warning";

    public static IReadOnlyList<ToastActionPreference> DefaultActions { get; } =
    [
        new("builder_autosave", "Autosave", "Notifications related to autosaving", true),
        new("builder_gztoggleenhancedrendering", "Toggle Enhanced Rendering Effects", "Toggles enhanced rendering effects in Visual mode.", true),
        new("builder_gztoggleeventlines", "Toggle Event lines", "Shows patrol-point and interpolation-point event lines.", true),
        new("builder_resourcewarningsanderrors", "Resource warnings and errors", "Notifications for warnings or errors while loading resources.", true),
        new("builder_togglebrightness", "Toggle Full Brightness", "Toggles sector brightness rendering.", true),
        new("builder_togglehighlight", "Toggle Highlight", "Toggles targeted and selected object highlights.", true),
        new(StatusWarningActionName, "Status warnings", "Warning status messages shown by DBuilder workflows.", true),
    ];

    public static ToastAnchor NormalizeAnchor(ToastAnchor anchor)
        => Enum.IsDefined(anchor) ? anchor : DefaultAnchor;

    public static int NormalizeDurationMilliseconds(int? durationMilliseconds)
        => Math.Max(durationMilliseconds ?? DefaultDurationMilliseconds, MinDurationMilliseconds);

    public static bool ShouldShowStatusToast(Settings settings, StatusHistoryKind kind, string actionName = StatusWarningActionName)
    {
        if (!settings.ToastsEnabled || kind != StatusHistoryKind.Warning) return false;
        return !settings.ToastActionSettings.TryGetValue(actionName, out bool enabled) || enabled;
    }

    public static int AcceptDurationSecondsText(string? text)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) || seconds <= 0)
            seconds = MinDurationMilliseconds / 1000;

        return NormalizeDurationMilliseconds(seconds * 1000);
    }

    public static string DurationSecondsText(int durationMilliseconds)
        => Math.Max(1, NormalizeDurationMilliseconds(durationMilliseconds) / 1000).ToString(CultureInfo.InvariantCulture);

    public static IReadOnlyList<ToastActionPreference> RegisteredActions(Settings settings)
        => NormalizeActions(DefaultActions, settings.ToastActionSettings);

    public static string KnownActionNamesText()
        => string.Join(", ", DefaultActions
            .Select(action => action.Name)
            .OrderBy(name => name, StringComparer.Ordinal));

    public static IReadOnlyList<ToastActionPreference> NormalizeActions(
        IEnumerable<ToastActionPreference> registeredActions,
        IReadOnlyDictionary<string, bool>? persistedActions)
    {
        persistedActions ??= new Dictionary<string, bool>(StringComparer.Ordinal);
        return registeredActions
            .Where(action => !string.IsNullOrWhiteSpace(action.Name))
            .GroupBy(action => action.Name.Trim(), StringComparer.Ordinal)
            .Select(group =>
            {
                ToastActionPreference action = group.First();
                string name = group.Key;
                bool enabled = persistedActions.TryGetValue(name, out bool storedEnabled) ? storedEnabled : action.Enabled;
                return new ToastActionPreference(name, action.Title.Trim(), action.Description.Trim(), enabled);
            })
            .OrderBy(action => action.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(action => action.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public static string DisabledActionsText(IReadOnlyDictionary<string, bool>? actionSettings)
    {
        if (actionSettings is null || actionSettings.Count == 0) return "";
        return string.Join(Environment.NewLine, actionSettings
            .Where(pair => !pair.Value)
            .Select(pair => pair.Key)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .OrderBy(key => key, StringComparer.Ordinal));
    }

    public static Dictionary<string, bool> ParseDisabledActionsText(string? text)
    {
        var settings = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text)) return settings;

        foreach (string token in text.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Length > 0)
                settings[token] = false;
        }

        return settings;
    }
}
