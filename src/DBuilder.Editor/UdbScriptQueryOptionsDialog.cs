// ABOUTME: Modal Avalonia dialog for runtime UDBScript QueryOptions prompts.
// ABOUTME: Writes edited query option values back into the shared QueryOptions model on OK.

using Avalonia.Controls;
using Avalonia.Layout;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class UdbScriptQueryOptionsDialog : PropertyDialog
{
    private readonly UdbScriptQueryOptionsModel _model;
    private readonly List<(UdbScriptOption Option, TextBox? Text, ComboBox? Combo)> _editors = new();

    public UdbScriptQueryOptionsDialog(UdbScriptQueryOptionsModel model)
        : base(UdbScriptQueryOptionsModel.PromptTitle)
    {
        _model = model;
        Width = 460;

        foreach (UdbScriptOption option in model.Options)
            AddOptionEditor(option);
    }

    protected override void OnConfirm()
    {
        foreach (var editor in _editors)
        {
            object value = editor.Combo?.SelectedItem is CatalogTextItem item
                ? item.Value
                : editor.Text?.Text ?? "";
            _model.SetValue(editor.Option.Name, value);
        }
    }

    private void AddOptionEditor(UdbScriptOption option)
    {
        if (option.Type == (int)UniversalType.EnumOption && option.EnumValues.Count > 0)
        {
            var combo = AddStringCombo(
                option.Description,
                option.EnumValues.Select(value => new CatalogTextItem(value.Key, value.Label ?? value.Key)),
                option.Value?.ToString() ?? "");
            _editors.Add((option, null, combo));
            return;
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*") };
        grid.Children.Add(new TextBlock { Text = option.Description, VerticalAlignment = VerticalAlignment.Center });
        var box = new TextBox { Text = option.Value?.ToString() ?? "" };
        Grid.SetColumn(box, 1);
        grid.Children.Add(box);
        AddCustomRow(grid);
        _editors.Add((option, box, null));
    }
}
