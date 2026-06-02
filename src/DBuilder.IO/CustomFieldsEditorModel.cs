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
}
