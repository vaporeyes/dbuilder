// ABOUTME: Modal dialog for editing arbitrary UDMF custom fields on one selected map element.
// ABOUTME: Uses the same key-value text format as property dialogs so fields round-trip consistently.

using System.Collections.Generic;
using Avalonia.Controls;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class CustomFieldsDialog : PropertyDialog
{
    private readonly UniversalFieldEditors? _fieldEditors;
    private readonly TextBox _fields;

    public Dictionary<string, object> ResultFields { get; private set; } = new();

    public CustomFieldsDialog(
        string elementName,
        IReadOnlyDictionary<string, object> fields,
        GameConfiguration? config = null,
        string? elementType = null,
        IEnumerable<string>? additionalFieldNames = null,
        ResourceManager? resources = null)
        : base("Custom Fields", elementName)
    {
        ResultFields = new Dictionary<string, object>(fields);

        var model = CustomFieldsEditorModelBuilder.Build(config, elementType, fields, additionalFieldNames);
        _fieldEditors = AddUniversalFieldEditors(model.ConfiguredFields, out _, config, resources);
        _fields = AddTextArea("UDMF fields", UdmfFields.Format(model.RawFields));
    }

    protected override void OnConfirm()
    {
        ResultFields = UdmfFields.Parse(_fields.Text);
        _fieldEditors?.Apply(ResultFields);
    }
}
