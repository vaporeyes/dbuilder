// ABOUTME: Models UDB-style event-line label visibility and action-argument text.
// ABOUTME: Keeps BuilderModes association label behavior testable outside renderer code.

using System.Globalization;
using DBuilder.Map;

namespace DBuilder.IO;

public static class EventLineLabelModel
{
    public static bool ShowForwardLabel(int visibility)
        => visibility == 1 || visibility == 3;

    public static bool ShowReverseLabel(int visibility)
        => visibility == 2 || visibility == 3;

    public static bool ShowAnyLabel(int visibility)
        => visibility > 0;

    public static string ActionDescription(
        Linedef line,
        GameConfiguration config,
        int labelStyle)
        => ActionDescription(line.Action, line.Args, config.GetLinedefAction(line.Action), config, labelStyle);

    public static string ThingDescription(
        Thing thing,
        GameConfiguration config,
        int labelStyle)
    {
        if (thing.Action > 0)
            return ActionDescription(thing.Action, thing.Args, config.GetLinedefAction(thing.Action), config, labelStyle);

        return ArgumentsDescription(
            thing.Args,
            config.GetThing(thing.Type)?.Args,
            config,
            labelStyle);
    }

    private static string ActionDescription(
        int action,
        int[] actionArgs,
        LinedefActionInfo? actionInfo,
        GameConfiguration config,
        int labelStyle)
    {
        if (action <= 0) return "";

        string description = action.ToString(CultureInfo.InvariantCulture) + ": " + config.LinedefActionTitle(action);
        if (labelStyle == 0 || config.LineTagIndicatesSectors) return description;

        string args = ArgumentsDescription(actionArgs, actionInfo?.Args, config, labelStyle);
        return string.IsNullOrEmpty(args) ? description + " ()" : description + " (" + args + ")";
    }

    private static string ArgumentsDescription(
        int[] actionArgs,
        IReadOnlyList<ArgInfo>? args,
        GameConfiguration config,
        int labelStyle)
    {
        if (args == null || args.Count == 0) return "";

        var parts = new List<string>();
        int count = Math.Min(Math.Min(actionArgs.Length, args.Count), 5);
        for (int i = 0; i < count; i++)
        {
            ArgInfo arg = args[i];
            if (!arg.Used) continue;

            string value = ArgumentValueText(actionArgs[i], arg, config, labelStyle);
            parts.Add(labelStyle == 2
                ? arg.Title + ": " + value
                : value);
        }

        return string.Join(", ", parts);
    }

    private static string ArgumentValueText(
        int value,
        ArgInfo arg,
        GameConfiguration config,
        int labelStyle)
    {
        string numeric = value.ToString(CultureInfo.InvariantCulture);
        if (labelStyle != 2) return numeric;

        EnumItemInfo? enumItem = arg.InlineEnumItems.FirstOrDefault(item => item.Value == numeric)
            ?? (arg.Enum == null ? null : config.GetArgEnumList(arg)?.GetByEnumIndex(numeric));

        return enumItem?.ToString() ?? numeric;
    }
}
