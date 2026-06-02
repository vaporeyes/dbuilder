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

public static class UdbScriptOptionsUiModel
{
    public const string DescriptionColumnName = "Description";
    public const string ValueColumnName = "Value";
    public const string FullRowSelectMode = "FullRowSelect";
    public const string EditProgrammaticallyMode = "EditProgrammatically";

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
}
