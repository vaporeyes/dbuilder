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

public sealed record UdbScriptOptionCellBeginEditPlan(
    bool ReloadTypeHandler,
    bool ClearEnumSelection,
    bool ClearEnumItems,
    bool TagGridRow,
    bool PositionEnumEditor,
    bool ShowEnumEditor,
    UdbScriptOptionEnumEditorState EnumEditor);

public sealed record UdbScriptOptionBrowseButtonState(
    bool Visible,
    bool EnumEditorVisible,
    bool IsBrowseable,
    bool IsEnumerable);

public sealed record UdbScriptOptionBrowseClickState(
    bool HasSelection,
    bool BrowseOption,
    string? CellValue,
    bool UpdateBrowseImage,
    bool FocusGrid);

public sealed record UdbScriptOptionEnumApplyState(
    string CellValue,
    object OptionValue,
    bool EnumEditorVisible,
    bool EnumEditorTagCleared,
    bool EnumEditorItemsCleared);

public sealed record UdbScriptOptionEditCommitState(
    object? CellValue,
    object OptionValue);

public sealed record UdbScriptOptionSelectionChangedPlan(
    bool HideBrowseButton,
    bool ApplyEnumEditor,
    bool HideEnumEditor,
    bool UpdateBrowseButton);

public sealed record UdbScriptOptionMouseUpPlan(
    bool FocusEnumEditor,
    bool SelectAllEnumEditorText);

public sealed record UdbScriptOptionEnumValidatingPlan(
    bool ApplyEnumEditor,
    bool HideEnumEditor);

public sealed record UdbScriptOptionCellClickPlan(
    bool ApplyEnumEditor,
    bool HideEnumEditor,
    bool BeginValueEdit);

public sealed record UdbScriptOptionEndEditPlan(
    bool ApplyEnumEditor,
    bool HideEnumEditor,
    bool EndGridEdit,
    bool FocusGrid);

public sealed record UdbScriptOptionBrowseRefreshPlan(
    bool UpdateBrowseButton);

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

    public static UdbScriptOptionCellBeginEditPlan CellBeginEditPlan(UdbScriptOption option)
    {
        UdbScriptOptionEnumEditorState editor = EnumEditorState(option);
        bool enumerable = editor.Visible;

        return new UdbScriptOptionCellBeginEditPlan(
            ReloadTypeHandler: enumerable,
            ClearEnumSelection: enumerable,
            ClearEnumItems: enumerable,
            TagGridRow: enumerable,
            PositionEnumEditor: enumerable,
            ShowEnumEditor: enumerable,
            editor);
    }

    public static UdbScriptOptionBrowseButtonState BrowseButtonState(UdbScriptOption? selectedOption)
    {
        if (selectedOption is null)
            return new UdbScriptOptionBrowseButtonState(false, false, false, false);

        UniversalTypeHandler handler = new UniversalTypeRegistry().CreateHandler(selectedOption.Type, selectedOption.DefaultValue);
        bool visible = handler.IsBrowseable && !handler.IsEnumerable;
        return new UdbScriptOptionBrowseButtonState(visible, EnumEditorVisible: false, handler.IsBrowseable, handler.IsEnumerable);
    }

    public static UdbScriptOptionSelectionChangedPlan SelectionChangedPlan()
        => new(
            HideBrowseButton: true,
            ApplyEnumEditor: true,
            HideEnumEditor: true,
            UpdateBrowseButton: true);

    public static UdbScriptOptionMouseUpPlan MouseUpPlan(bool enumEditorVisible)
        => new(
            FocusEnumEditor: enumEditorVisible,
            SelectAllEnumEditorText: enumEditorVisible);

    public static UdbScriptOptionEnumValidatingPlan EnumValidatingPlan()
        => new(
            ApplyEnumEditor: true,
            HideEnumEditor: false);

    public static UdbScriptOptionCellClickPlan CellClickPlan(int columnIndex)
        => new(
            ApplyEnumEditor: true,
            HideEnumEditor: true,
            BeginValueEdit: columnIndex == 1);

    public static UdbScriptOptionEndEditPlan EndEditPlan()
        => new(
            ApplyEnumEditor: true,
            HideEnumEditor: true,
            EndGridEdit: true,
            FocusGrid: true);

    public static UdbScriptOptionBrowseRefreshPlan BrowseRefreshPlan()
        => new(UpdateBrowseButton: true);

    public static UdbScriptOptionBrowseClickState BrowseClickState(UdbScriptOption? selectedOption)
    {
        if (selectedOption is null)
            return new UdbScriptOptionBrowseClickState(false, false, null, false, false);

        UniversalTypeHandler handler = HandlerFor(selectedOption);
        handler.SetValue(selectedOption.Value);

        return new UdbScriptOptionBrowseClickState(
            HasSelection: true,
            BrowseOption: true,
            handler.GetStringValue(),
            UpdateBrowseImage: handler.HasDynamicImage,
            FocusGrid: true);
    }

    public static UdbScriptOptionEditCommitState CommitEditedValue(
        UdbScriptOption option,
        object? cellValue)
    {
        UniversalTypeHandler handler = HandlerFor(option);
        handler.SetValue(cellValue);

        return new UdbScriptOptionEditCommitState(cellValue, handler.GetValue());
    }

    public static UdbScriptOptionEnumApplyState ApplyEnumEditor(
        UdbScriptOption option,
        string editorText,
        bool hide)
    {
        UniversalTypeHandler handler = HandlerFor(option);
        handler.SetValue(editorText);

        return new UdbScriptOptionEnumApplyState(
            editorText,
            handler.GetValue(),
            EnumEditorVisible: !hide,
            EnumEditorTagCleared: hide,
            EnumEditorItemsCleared: hide);
    }

    public static IReadOnlyDictionary<string, object> GetScriptOptions(IReadOnlyList<UdbScriptOption> options)
    {
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (UdbScriptOption option in options)
        {
            UniversalTypeHandler handler = HandlerFor(option);
            handler.SetValue(option.Value);
            values[option.Name] = handler.GetValue();
        }

        return values;
    }

    private static UniversalTypeHandler HandlerFor(UdbScriptOption option)
        => new UniversalTypeRegistry().CreateHandler(option.Type, option.DefaultValue, enumList: EnumList(option));

    private static EnumListInfo EnumList(UdbScriptOption option)
    {
        var list = new EnumListInfo(option.Name);
        foreach (UdbScriptEnumValue value in option.EnumValues)
            list.Add(new EnumItemInfo(value.Key, value.Label ?? value.Key));

        return list;
    }
}
