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

    [Fact]
    public void EnumEditorStateMatchesUdbSelectionRules()
    {
        var option = new UdbScriptOption(
            "direction",
            "Direction",
            (int)UniversalType.EnumOption,
            "Down",
            "Down",
            new[]
            {
                new UdbScriptEnumValue("1", "Up"),
                new UdbScriptEnumValue("2", "Down"),
            },
            "settings.direction");

        UdbScriptOptionEnumEditorState byText = UdbScriptOptionsUiModel.EnumEditorState(option);

        Assert.True(byText.Visible);
        Assert.Equal("DropDownList", byText.DropDownStyle);
        Assert.False(byText.BrowseButtonVisible);
        Assert.Equal("Down", byText.Text);
        Assert.Equal(new[] { "1:Up", "2:Down" }, byText.Items.Select(item => item.Key + ":" + item.Text).ToArray());
        Assert.Equal("2", byText.SelectedItem?.Key);

        UdbScriptOptionEnumEditorState byValue = UdbScriptOptionsUiModel.EnumEditorState(option with { Value = "1" });

        Assert.Equal("1", byValue.Text);
        Assert.Equal("1", byValue.SelectedItem?.Key);
    }

    [Fact]
    public void EnumEditorStateIsHiddenForNonEnumOptions()
    {
        var option = new UdbScriptOption(
            "length",
            "Length",
            (int)UniversalType.Integer,
            128,
            128,
            Array.Empty<UdbScriptEnumValue>(),
            "settings.length");

        UdbScriptOptionEnumEditorState state = UdbScriptOptionsUiModel.EnumEditorState(option);

        Assert.False(state.Visible);
        Assert.Equal("128", state.Text);
        Assert.Null(state.SelectedItem);
        Assert.Empty(state.Items);
    }

    [Fact]
    public void CellBeginEditPlanShowsEnumEditorForEnumerableOptions()
    {
        var option = new UdbScriptOption(
            "direction",
            "Direction",
            (int)UniversalType.EnumOption,
            "Down",
            "Down",
            new[]
            {
                new UdbScriptEnumValue("1", "Up"),
                new UdbScriptEnumValue("2", "Down"),
            },
            "settings.direction");

        UdbScriptOptionCellBeginEditPlan plan = UdbScriptOptionsUiModel.CellBeginEditPlan(option);

        Assert.True(plan.ReloadTypeHandler);
        Assert.True(plan.ClearEnumSelection);
        Assert.True(plan.ClearEnumItems);
        Assert.True(plan.TagGridRow);
        Assert.True(plan.PositionEnumEditor);
        Assert.True(plan.ShowEnumEditor);
        Assert.True(plan.EnumEditor.Visible);
        Assert.Equal("DropDownList", plan.EnumEditor.DropDownStyle);
        Assert.Equal("2", plan.EnumEditor.SelectedItem?.Key);
    }

    [Fact]
    public void CellBeginEditPlanDoesNothingForNonEnumerableOptions()
    {
        var option = new UdbScriptOption(
            "length",
            "Length",
            (int)UniversalType.Integer,
            128,
            128,
            Array.Empty<UdbScriptEnumValue>(),
            "settings.length");

        UdbScriptOptionCellBeginEditPlan plan = UdbScriptOptionsUiModel.CellBeginEditPlan(option);

        Assert.False(plan.ReloadTypeHandler);
        Assert.False(plan.ClearEnumSelection);
        Assert.False(plan.ClearEnumItems);
        Assert.False(plan.TagGridRow);
        Assert.False(plan.PositionEnumEditor);
        Assert.False(plan.ShowEnumEditor);
        Assert.False(plan.EnumEditor.Visible);
    }

    [Fact]
    public void BrowseButtonStateShowsOnlyForBrowseableNonEnumerableOptions()
    {
        var texture = new UdbScriptOption(
            "wall",
            "Wall texture",
            (int)UniversalType.Texture,
            "STARTAN3",
            "STARTAN3",
            Array.Empty<UdbScriptEnumValue>(),
            "settings.wall");
        var boolean = new UdbScriptOption(
            "enabled",
            "Enabled",
            (int)UniversalType.Boolean,
            true,
            true,
            Array.Empty<UdbScriptEnumValue>(),
            "settings.enabled");

        UdbScriptOptionBrowseButtonState textureState = UdbScriptOptionsUiModel.BrowseButtonState(texture);

        Assert.True(textureState.Visible);
        Assert.False(textureState.EnumEditorVisible);
        Assert.True(textureState.IsBrowseable);
        Assert.False(textureState.IsEnumerable);

        UdbScriptOptionBrowseButtonState booleanState = UdbScriptOptionsUiModel.BrowseButtonState(boolean);

        Assert.False(booleanState.Visible);
        Assert.False(booleanState.IsBrowseable);
        Assert.True(booleanState.IsEnumerable);

        UdbScriptOptionBrowseButtonState stringState = UdbScriptOptionsUiModel.BrowseButtonState(texture with { Type = (int)UniversalType.String });

        Assert.True(stringState.Visible);
        Assert.True(stringState.IsBrowseable);
        Assert.False(stringState.IsEnumerable);

        UdbScriptOptionBrowseButtonState none = UdbScriptOptionsUiModel.BrowseButtonState(null);

        Assert.False(none.Visible);
        Assert.False(none.IsBrowseable);
        Assert.False(none.IsEnumerable);
    }

    [Fact]
    public void SelectionChangedPlanMatchesUdbScriptOptionsControlBranch()
    {
        UdbScriptOptionSelectionChangedPlan plan = UdbScriptOptionsUiModel.SelectionChangedPlan();

        Assert.True(plan.HideBrowseButton);
        Assert.True(plan.ApplyEnumEditor);
        Assert.True(plan.HideEnumEditor);
        Assert.True(plan.UpdateBrowseButton);
    }

    [Fact]
    public void MouseUpPlanFocusesVisibleEnumEditor()
    {
        UdbScriptOptionMouseUpPlan visible = UdbScriptOptionsUiModel.MouseUpPlan(enumEditorVisible: true);

        Assert.True(visible.FocusEnumEditor);
        Assert.True(visible.SelectAllEnumEditorText);

        UdbScriptOptionMouseUpPlan hidden = UdbScriptOptionsUiModel.MouseUpPlan(enumEditorVisible: false);

        Assert.False(hidden.FocusEnumEditor);
        Assert.False(hidden.SelectAllEnumEditorText);
    }

    [Fact]
    public void EnumValidatingPlanAppliesWithoutHidingEditor()
    {
        UdbScriptOptionEnumValidatingPlan plan = UdbScriptOptionsUiModel.EnumValidatingPlan();

        Assert.True(plan.ApplyEnumEditor);
        Assert.False(plan.HideEnumEditor);
    }

    [Fact]
    public void CellClickPlanAppliesEnumsAndBeginsValueEdits()
    {
        UdbScriptOptionCellClickPlan valueColumn = UdbScriptOptionsUiModel.CellClickPlan(columnIndex: 1);

        Assert.True(valueColumn.ApplyEnumEditor);
        Assert.True(valueColumn.HideEnumEditor);
        Assert.True(valueColumn.BeginValueEdit);

        UdbScriptOptionCellClickPlan descriptionColumn = UdbScriptOptionsUiModel.CellClickPlan(columnIndex: 0);

        Assert.True(descriptionColumn.ApplyEnumEditor);
        Assert.True(descriptionColumn.HideEnumEditor);
        Assert.False(descriptionColumn.BeginValueEdit);
    }

    [Fact]
    public void EndEditPlanAppliesEnumsEndsEditAndFocusesGrid()
    {
        UdbScriptOptionEndEditPlan plan = UdbScriptOptionsUiModel.EndEditPlan();

        Assert.True(plan.ApplyEnumEditor);
        Assert.True(plan.HideEnumEditor);
        Assert.True(plan.EndGridEdit);
        Assert.True(plan.FocusGrid);
    }

    [Fact]
    public void BrowseRefreshPlanUpdatesButtonAfterAddingOptionsOrResize()
    {
        UdbScriptOptionBrowseRefreshPlan plan = UdbScriptOptionsUiModel.BrowseRefreshPlan();

        Assert.True(plan.UpdateBrowseButton);
    }

    [Fact]
    public void BrowseClickStateWritesStringValueAndFocusesGridWhenSelected()
    {
        var texture = new UdbScriptOption(
            "wall",
            "Wall texture",
            (int)UniversalType.Texture,
            "STARTAN3",
            "BRICK1",
            Array.Empty<UdbScriptEnumValue>(),
            "settings.wall");
        var angle = new UdbScriptOption(
            "angle",
            "Angle",
            (int)UniversalType.AngleDegrees,
            0,
            90,
            Array.Empty<UdbScriptEnumValue>(),
            "settings.angle");

        UdbScriptOptionBrowseClickState textureState = UdbScriptOptionsUiModel.BrowseClickState(texture);

        Assert.True(textureState.HasSelection);
        Assert.True(textureState.BrowseOption);
        Assert.Equal("BRICK1", textureState.CellValue);
        Assert.False(textureState.UpdateBrowseImage);
        Assert.True(textureState.FocusGrid);

        UdbScriptOptionBrowseClickState angleState = UdbScriptOptionsUiModel.BrowseClickState(angle);

        Assert.Equal("90", angleState.CellValue);
        Assert.True(angleState.UpdateBrowseImage);
        Assert.True(angleState.FocusGrid);
    }

    [Fact]
    public void BrowseClickStateDoesNothingWithoutSelection()
    {
        UdbScriptOptionBrowseClickState state = UdbScriptOptionsUiModel.BrowseClickState(null);

        Assert.False(state.HasSelection);
        Assert.False(state.BrowseOption);
        Assert.Null(state.CellValue);
        Assert.False(state.UpdateBrowseImage);
        Assert.False(state.FocusGrid);
    }

    [Fact]
    public void CommitEditedValueStoresCellValueAndHandlerValue()
    {
        var length = new UdbScriptOption(
            "length",
            "Length",
            (int)UniversalType.Integer,
            128,
            64,
            Array.Empty<UdbScriptEnumValue>(),
            "settings.length");
        var direction = new UdbScriptOption(
            "direction",
            "Direction",
            (int)UniversalType.EnumOption,
            "Down",
            "Down",
            new[]
            {
                new UdbScriptEnumValue("1", "Up"),
                new UdbScriptEnumValue("2", "Down"),
            },
            "settings.direction");

        UdbScriptOptionEditCommitState integer = UdbScriptOptionsUiModel.CommitEditedValue(length, "256");

        Assert.Equal("256", integer.CellValue);
        Assert.Equal(256, integer.OptionValue);

        UdbScriptOptionEditCommitState enumValue = UdbScriptOptionsUiModel.CommitEditedValue(direction, "Up");

        Assert.Equal("Up", enumValue.CellValue);
        Assert.Equal(1, enumValue.OptionValue);
    }

    [Fact]
    public void ApplyEnumEditorUpdatesValueAndHonorsHideFlag()
    {
        var option = new UdbScriptOption(
            "direction",
            "Direction",
            (int)UniversalType.EnumOption,
            "Down",
            "Down",
            new[]
            {
                new UdbScriptEnumValue("1", "Up"),
                new UdbScriptEnumValue("2", "Down"),
            },
            "settings.direction");

        UdbScriptOptionEnumApplyState hidden = UdbScriptOptionsUiModel.ApplyEnumEditor(option, "Up", hide: true);

        Assert.Equal("Up", hidden.CellValue);
        Assert.Equal(1, hidden.OptionValue);
        Assert.False(hidden.EnumEditorVisible);
        Assert.True(hidden.EnumEditorTagCleared);
        Assert.True(hidden.EnumEditorItemsCleared);

        UdbScriptOptionEnumApplyState visible = UdbScriptOptionsUiModel.ApplyEnumEditor(option, "Down", hide: false);

        Assert.Equal("Down", visible.CellValue);
        Assert.Equal(2, visible.OptionValue);
        Assert.True(visible.EnumEditorVisible);
        Assert.False(visible.EnumEditorTagCleared);
        Assert.False(visible.EnumEditorItemsCleared);
    }

    [Fact]
    public void GetScriptOptionsReturnsHandlerValuesAndLastDuplicateWins()
    {
        var direction = new UdbScriptOption(
            "direction",
            "Direction",
            (int)UniversalType.EnumOption,
            "Down",
            "Up",
            new[]
            {
                new UdbScriptEnumValue("1", "Up"),
                new UdbScriptEnumValue("2", "Down"),
            },
            "settings.direction");
        var firstLength = new UdbScriptOption(
            "length",
            "Length",
            (int)UniversalType.Integer,
            128,
            "64",
            Array.Empty<UdbScriptEnumValue>(),
            "settings.length");
        var secondLength = firstLength with { Value = "256" };

        IReadOnlyDictionary<string, object> values = UdbScriptOptionsUiModel.GetScriptOptions(
            new[] { direction, firstLength, secondLength });

        Assert.Equal(2, values.Count);
        Assert.Equal(1, values["direction"]);
        Assert.Equal(256, values["length"]);
    }
}
