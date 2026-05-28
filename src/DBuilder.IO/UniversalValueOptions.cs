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
}
