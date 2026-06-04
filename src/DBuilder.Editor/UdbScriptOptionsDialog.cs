// ABOUTME: Modal Avalonia dialog for editing persisted UDBScript script options.
// ABOUTME: Applies shared ScriptOptionsControl conversion behavior before exposing edited options.

using Avalonia.Controls;
using Avalonia.Layout;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class UdbScriptOptionsDialog : PropertyDialog
{
    private readonly List<(UdbScriptOption Option, TextBox? Text, ComboBox? Combo)> _editors = new();

    public IReadOnlyList<UdbScriptOption> Options { get; private set; }

    public UdbScriptOptionsDialog(IReadOnlyList<UdbScriptOption> options)
        : base(UdbScriptDockerModel.OptionsLabel)
    {
        Width = 460;
        Options = options;

        foreach (UdbScriptOption option in options)
            AddOptionEditor(option);
    }

    protected override void OnConfirm()
    {
        Options = _editors
            .Select(editor => editor.Option with { Value = EditedValue(editor) })
            .ToArray();
    }

    private static object EditedValue((UdbScriptOption Option, TextBox? Text, ComboBox? Combo) editor)
    {
        object? cellValue = editor.Combo?.SelectedItem is CatalogTextItem item
            ? item.Value
            : editor.Text?.Text ?? "";
        UdbScriptOptionValueCellState cell = UdbScriptOptionsUiModel.ValueCellState(editor.Option, cellValue);
        return UdbScriptOptionsUiModel.CommitEditedValue(editor.Option, cell.Value).OptionValue ?? "";
    }

    private void AddOptionEditor(UdbScriptOption option)
    {
        UdbScriptOptionEnumEditorState enumState = UdbScriptOptionsUiModel.EnumEditorState(option);
        if (enumState.Visible)
        {
            string current = enumState.SelectedItem?.Key ?? option.Value?.ToString() ?? "";
            var combo = AddStringCombo(
                option.Description,
                enumState.Items.Select(item => new CatalogTextItem(item.Key, item.Text)),
                current);
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
