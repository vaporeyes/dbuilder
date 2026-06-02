// ABOUTME: Builds the configured-field and raw-field slices used by the custom fields editor.
// ABOUTME: Keeps UDB-style fixed universal fields testable outside the Avalonia dialog.

namespace DBuilder.IO;

public sealed record CustomFieldsEditorModel(
    IReadOnlyList<UniversalFieldEditorValue> ConfiguredFields,
    Dictionary<string, object> RawFields);

public static class CustomFieldsEditorModelBuilder
{
    public static CustomFieldsEditorModel Build(
        GameConfiguration? config,
        string? elementType,
        IReadOnlyDictionary<string, object> fields,
        IEnumerable<string>? additionalFieldNames = null)
    {
        var configuredFields = elementType != null
            ? UniversalFieldEditorValues.ForElement(config, elementType, fields, additionalFieldNames)
            : Array.Empty<UniversalFieldEditorValue>();
        var rawFields = UniversalFieldEditorValues.WithoutConfiguredFields(fields, configuredFields);
        return new CustomFieldsEditorModel(configuredFields, rawFields);
    }

    public static void StoreRawFieldTypes(
        MapOptions options,
        GameConfiguration? config,
        string elementType,
        IReadOnlyDictionary<string, object> rawFields)
    {
        foreach (var (fieldName, value) in rawFields)
        {
            if (IsConfiguredField(config, elementType, fieldName)) continue;
            options.SetUniversalFieldType(elementType, fieldName, InferCustomFieldType(value));
        }
    }

    public static int InferCustomFieldType(object? value)
        => value switch
        {
            bool => (int)UniversalType.Boolean,
            double or float => (int)UniversalType.Float,
            int or long or short or byte => (int)UniversalType.Integer,
            _ => (int)UniversalType.String,
        };

    private static bool IsConfiguredField(GameConfiguration? config, string elementType, string fieldName)
        => config?.UniversalFields.TryGetValue(elementType, out var fields) == true
           && fields.ContainsKey(fieldName);
}
