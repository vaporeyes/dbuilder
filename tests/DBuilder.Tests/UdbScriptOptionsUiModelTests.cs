// ABOUTME: Tests UDBScript option grid metadata against upstream ScriptOptionsControl.
// ABOUTME: Covers shared option editor columns, selection, edit mode, and hidden editor controls.

using DBuilder.IO;

namespace DBuilder.Tests;

public class UdbScriptOptionsUiModelTests
{
    [Fact]
    public void GridMetadataMatchesUdbScriptOptionsControl()
    {
        UdbScriptOptionsGridMetadata metadata = UdbScriptOptionsUiModel.GridMetadata();

        Assert.False(metadata.AllowUserToAddRows);
        Assert.False(metadata.AllowUserToDeleteRows);
        Assert.False(metadata.AllowUserToResizeRows);
        Assert.False(metadata.MultiSelect);
        Assert.False(metadata.RowHeadersVisible);
        Assert.Equal("FullRowSelect", metadata.SelectionMode);
        Assert.Equal("EditProgrammatically", metadata.EditMode);
        Assert.False(metadata.EnumEditorInitiallyVisible);
        Assert.False(metadata.BrowseButtonInitiallyVisible);

        Assert.Equal(2, metadata.Columns.Count);
        Assert.Equal(
            ["Description", "Value"],
            metadata.Columns.Select(column => column.Name).ToArray());

        UdbScriptOptionsGridColumn description = metadata.Columns[0];
        Assert.Equal("Description", description.HeaderText);
        Assert.Equal(70.0, description.FillWeight);
        Assert.True(description.ReadOnly);
        Assert.False(description.Sortable);

        UdbScriptOptionsGridColumn value = metadata.Columns[1];
        Assert.Equal("Value", value.HeaderText);
        Assert.Equal(30.0, value.FillWeight);
        Assert.False(value.ReadOnly);
        Assert.False(value.Sortable);
    }

    [Fact]
    public void ValueCellStateResetsEmptyValuesAndStylesDefaults()
    {
        var option = new UdbScriptOption(
            "length",
            "Length",
            (int)UniversalType.Integer,
            128,
            64,
            Array.Empty<UdbScriptEnumValue>(),
            "settings.length");

        UdbScriptOptionValueCellState empty = UdbScriptOptionsUiModel.ValueCellState(option, " ");

        Assert.Equal(128, empty.Value);
        Assert.True(empty.ResetToDefault);
        Assert.Equal("GrayText", empty.ForeColor);

        UdbScriptOptionValueCellState defaultValue = UdbScriptOptionsUiModel.ValueCellState(option, "128");

        Assert.Equal("128", defaultValue.Value);
        Assert.False(defaultValue.ResetToDefault);
        Assert.Equal("GrayText", defaultValue.ForeColor);

        UdbScriptOptionValueCellState edited = UdbScriptOptionsUiModel.ValueCellState(option, 256);

        Assert.Equal(256, edited.Value);
        Assert.False(edited.ResetToDefault);
        Assert.Equal("WindowText", edited.ForeColor);
    }
}
