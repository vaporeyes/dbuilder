// ABOUTME: Models UDBScript script option grid metadata shared by docker and query option UI.
// ABOUTME: Keeps option editor surface details aligned with upstream ScriptOptionsControl.

namespace DBuilder.IO;

public sealed record UdbScriptOptionsGridColumn(
    string Name,
    string HeaderText,
    double FillWeight,
    bool ReadOnly,
    bool Sortable);

public sealed record UdbScriptOptionsGridMetadata(
    bool AllowUserToAddRows,
    bool AllowUserToDeleteRows,
    bool AllowUserToResizeRows,
    bool MultiSelect,
    bool RowHeadersVisible,
    string SelectionMode,
    string EditMode,
    bool EnumEditorInitiallyVisible,
    bool BrowseButtonInitiallyVisible,
    IReadOnlyList<UdbScriptOptionsGridColumn> Columns);

public sealed record UdbScriptOptionValueCellState(
    object? Value,
    bool ResetToDefault,
    string ForeColor);

public sealed record UdbScriptOptionEnumItem(
    string Key,
    string Text);

public sealed record UdbScriptOptionEnumEditorState(
    bool Visible,
    string DropDownStyle,
    string Text,
    UdbScriptOptionEnumItem? SelectedItem,
    bool BrowseButtonVisible,
    IReadOnlyList<UdbScriptOptionEnumItem> Items);

public static class UdbScriptOptionsUiModel
{
    public const string DescriptionColumnName = "Description";
    public const string ValueColumnName = "Value";
    public const string FullRowSelectMode = "FullRowSelect";
    public const string EditProgrammaticallyMode = "EditProgrammatically";
    public const string DefaultValueForeColor = "GrayText";
    public const string EditedValueForeColor = "WindowText";
    public const string EnumDropDownStyle = "DropDownList";

    public static UdbScriptOptionsGridMetadata GridMetadata()
        => new(
            AllowUserToAddRows: false,
            AllowUserToDeleteRows: false,
            AllowUserToResizeRows: false,
            MultiSelect: false,
            RowHeadersVisible: false,
            SelectionMode: FullRowSelectMode,
            EditMode: EditProgrammaticallyMode,
            EnumEditorInitiallyVisible: false,
            BrowseButtonInitiallyVisible: false,
            Columns:
            [
                new(DescriptionColumnName, "Description", 70.0, ReadOnly: true, Sortable: false),
                new(ValueColumnName, "Value", 30.0, ReadOnly: false, Sortable: false),
            ]);

    public static UdbScriptOptionValueCellState ValueCellState(UdbScriptOption option, object? proposedValue)
    {
        bool resetToDefault = proposedValue is null || string.IsNullOrWhiteSpace(proposedValue.ToString());
        object? value = resetToDefault ? option.DefaultValue : proposedValue;
        string valueText = value?.ToString() ?? "";
        string defaultText = option.DefaultValue.ToString() ?? "";
        string foreColor = valueText == defaultText
            ? DefaultValueForeColor
            : EditedValueForeColor;

        return new UdbScriptOptionValueCellState(value, resetToDefault, foreColor);
    }

    public static UdbScriptOptionEnumEditorState EnumEditorState(UdbScriptOption option)
    {
        string text = option.Value.ToString() ?? "";
        if (option.Type != (int)UniversalType.EnumOption || option.EnumValues.Count == 0)
            return new UdbScriptOptionEnumEditorState(false, EnumDropDownStyle, text, null, false, Array.Empty<UdbScriptOptionEnumItem>());

        UdbScriptOptionEnumItem[] items = option.EnumValues
            .Select(value => new UdbScriptOptionEnumItem(value.Key, value.Label ?? value.Key))
            .ToArray();
        UdbScriptOptionEnumItem? selected = items.FirstOrDefault(item => string.Equals(item.Text, text, StringComparison.OrdinalIgnoreCase))
            ?? items.FirstOrDefault(item => string.Equals(item.Key, text, StringComparison.OrdinalIgnoreCase));

        return new UdbScriptOptionEnumEditorState(true, EnumDropDownStyle, text, selected, false, items);
    }
}
