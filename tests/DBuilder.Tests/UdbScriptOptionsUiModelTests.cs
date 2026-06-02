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
}
