// ABOUTME: Modal dialog for editing arbitrary UDMF custom fields on one selected map element.
// ABOUTME: Uses the same key-value text format as property dialogs so fields round-trip consistently.

using System.Collections.Generic;
using Avalonia.Controls;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class CustomFieldsDialog : PropertyDialog
{
    private readonly TextBox _fields;

    public Dictionary<string, object> ResultFields { get; private set; } = new();

    public CustomFieldsDialog(string elementName, IReadOnlyDictionary<string, object> fields)
        : base("Custom Fields", elementName)
    {
        ResultFields = new Dictionary<string, object>(fields);
        _fields = AddTextArea("UDMF fields", UdmfFields.Format(fields));
    }

    protected override void OnConfirm()
    {
        ResultFields = UdmfFields.Parse(_fields.Text);
    }
}
