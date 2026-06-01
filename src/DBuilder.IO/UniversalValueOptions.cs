// ABOUTME: Converts universal type handlers into integer editor option lists for argument/property UIs.
// ABOUTME: Keeps UI controls driven by handler behavior instead of duplicating enum and bool conversion rules.

using System.Globalization;

namespace DBuilder.IO;

public readonly record struct UniversalValueOption(int Value, string Title);

public static class UniversalValueOptions
{
    public static IReadOnlyList<UniversalValueOption> ForIntegerEditor(UniversalTypeHandler handler)
        => handler switch
        {
            BooleanTypeHandler boolean => BooleanOptions(boolean.Values),
            EnumOptionTypeHandler option => NumericOptions(option.Values),
            EnumStringsTypeHandler strings => NumericOptions(strings.Values),
            TagTypeHandler tag => NumericOptions(tag.Values),
            AngleByteTypeHandler => DoomByteAngleOptions(),
            AngleDegreesTypeHandler => DoomDegreeAngleOptions(),
            AngleDegreesFloatTypeHandler => DoomDegreeAngleOptions(),
            _ => Array.Empty<UniversalValueOption>(),
        };

    private static UniversalValueOption[] BooleanOptions(EnumListInfo values)
        => values.Items
            .Select(item => new UniversalValueOption(
                item.Value.StartsWith("t", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                item.Title))
            .ToArray();

    private static UniversalValueOption[] NumericOptions(EnumListInfo values)
        => values.Items
            .Where(item => int.TryParse(item.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Select(item => new UniversalValueOption(item.GetIntValue(), item.Title))
            .ToArray();

    private static UniversalValueOption[] DoomDegreeAngleOptions()
        =>
        [
            new UniversalValueOption(0, "East"),
            new UniversalValueOption(45, "Northeast"),
            new UniversalValueOption(90, "North"),
            new UniversalValueOption(135, "Northwest"),
            new UniversalValueOption(180, "West"),
            new UniversalValueOption(225, "Southwest"),
            new UniversalValueOption(270, "South"),
            new UniversalValueOption(315, "Southeast"),
        ];

    private static UniversalValueOption[] DoomByteAngleOptions()
        =>
        [
            new UniversalValueOption(0, "East"),
            new UniversalValueOption(32, "Northeast"),
            new UniversalValueOption(64, "North"),
            new UniversalValueOption(96, "Northwest"),
            new UniversalValueOption(128, "West"),
            new UniversalValueOption(160, "Southwest"),
            new UniversalValueOption(192, "South"),
            new UniversalValueOption(224, "Southeast"),
        ];
}
