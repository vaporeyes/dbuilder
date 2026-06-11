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

    public static ToastAnchor NormalizeAnchor(ToastAnchor anchor)
        => Enum.IsDefined(anchor) ? anchor : DefaultAnchor;

    public static int NormalizeDurationMilliseconds(int? durationMilliseconds)
        => Math.Max(durationMilliseconds ?? DefaultDurationMilliseconds, MinDurationMilliseconds);

    public static int AcceptDurationSecondsText(string? text)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) || seconds <= 0)
            seconds = MinDurationMilliseconds / 1000;

        return NormalizeDurationMilliseconds(seconds * 1000);
    }

    public static string DurationSecondsText(int durationMilliseconds)
        => Math.Max(1, NormalizeDurationMilliseconds(durationMilliseconds) / 1000).ToString(CultureInfo.InvariantCulture);

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
