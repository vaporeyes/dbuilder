// ABOUTME: Selects configured universal fields for typed property editors and supplies their initial values.
// ABOUTME: Keeps editor dialogs from duplicating config lookup, type-specific field inclusion, and default handling.

namespace DBuilder.IO;

public sealed record UniversalFieldEditorValue(UniversalFieldInfo Field, object? Value);

public static class UniversalFieldEditorValues
{
    public static IReadOnlyList<UniversalFieldEditorValue> ForElement(
        GameConfiguration? config,
        string element,
        IReadOnlyDictionary<string, object> currentFields,
        IEnumerable<string>? additionalFieldNames = null)
    {
        if (config == null || !config.UniversalFields.TryGetValue(element, out var configured))
            return Array.Empty<UniversalFieldEditorValue>();

        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in configured.Keys) names.Add(name);
        if (additionalFieldNames != null)
            foreach (string name in additionalFieldNames)
                if (configured.ContainsKey(name)) names.Add(name);

        var result = new List<UniversalFieldEditorValue>();
        foreach (string name in names)
        {
            var field = configured[name];
            object? value = currentFields.TryGetValue(field.Name, out object? current)
                ? current
                : field.DefaultValue;
            result.Add(new UniversalFieldEditorValue(field, value));
        }

        return result;
    }

    public static Dictionary<string, object> WithoutConfiguredFields(
        IReadOnlyDictionary<string, object> source,
        IEnumerable<UniversalFieldEditorValue> configuredFields)
    {
        var names = new HashSet<string>(
            configuredFields.Select(value => value.Field.Name),
            StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var entry in source)
            if (!names.Contains(entry.Key)) result[entry.Key] = entry.Value;
        return result;
    }

    public static UniversalTypeHandler CreateHandler(UniversalFieldInfo field, object? value)
    {
        var registry = new UniversalTypeRegistry();
        var values = field.InlineEnumItems.Count > 0 ? InlineValues(field) : null;
        var handler = registry.CreateHandler(field.Type, field.DefaultValue, enumList: values);
        handler.SetValue(value);
        return handler;
    }

    public static UniversalTypeHandler CreateHandler(UniversalFieldInfo field)
    {
        var registry = new UniversalTypeRegistry();
        var values = field.InlineEnumItems.Count > 0 ? InlineValues(field) : null;
        return registry.CreateHandler(field.Type, field.DefaultValue, enumList: values);
    }

    private static EnumListInfo InlineValues(UniversalFieldInfo field)
    {
        var values = new EnumListInfo(field.Name);
        foreach (var item in field.InlineEnumItems) values.Add(item);
        return values;
    }
}
