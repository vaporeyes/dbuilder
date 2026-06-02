// ABOUTME: Verifies editor property-command selection eligibility rules.
// ABOUTME: Keeps single-element property dialog availability aligned across map element types.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class EditorPropertySelectionTests
{
    [Theory]
    [InlineData(1, 0, 0, 0, 0)]
    [InlineData(0, 1, 0, 0, 0)]
    [InlineData(0, 0, 1, 0, 0)]
    [InlineData(0, 0, 0, 1, 0)]
    [InlineData(0, 0, 0, 0, 1)]
    public void CanEditSingleSelectedElement(int vertices, int linedefs, int sidedefs, int sectors, int things)
        => Assert.True(EditorPropertySelection.CanEdit(vertices, linedefs, sidedefs, sectors, things));

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(1, 1, 0, 0, 0)]
    [InlineData(0, 1, 1, 0, 0)]
    [InlineData(0, 0, 0, 1, 1)]
    [InlineData(2, 0, 0, 0, 0)]
    public void CannotEditEmptyOrMixedSelection(int vertices, int linedefs, int sidedefs, int sectors, int things)
        => Assert.False(EditorPropertySelection.CanEdit(vertices, linedefs, sidedefs, sectors, things));

    [Theory]
    [InlineData(0, 1, 0, 0, 0)]
    [InlineData(0, 0, 0, 1, 0)]
    [InlineData(0, 0, 0, 0, 1)]
    public void CanEditFlagsForSingleFlaggableSelection(int vertices, int linedefs, int sidedefs, int sectors, int things)
        => Assert.True(EditorPropertySelection.CanEditFlags(vertices, linedefs, sidedefs, sectors, things));

    [Theory]
    [InlineData(1, 0, 0, 0, 0)]
    [InlineData(0, 0, 1, 0, 0)]
    [InlineData(0, 1, 0, 1, 0)]
    [InlineData(0, 0, 0, 0, 0)]
    public void CannotEditFlagsForNonFlaggableOrMixedSelection(int vertices, int linedefs, int sidedefs, int sectors, int things)
        => Assert.False(EditorPropertySelection.CanEditFlags(vertices, linedefs, sidedefs, sectors, things));

    [Theory]
    [InlineData(1, 0, 0, 0, 0)]
    [InlineData(0, 1, 0, 0, 0)]
    [InlineData(0, 0, 1, 0, 0)]
    [InlineData(0, 0, 0, 1, 0)]
    [InlineData(0, 0, 0, 0, 1)]
    public void CanEditCustomFieldsForSingleFieldedSelectionWhenFormatSupportsFields(
        int vertices,
        int linedefs,
        int sidedefs,
        int sectors,
        int things)
        => Assert.True(EditorPropertySelection.CanEditCustomFields(true, vertices, linedefs, sidedefs, sectors, things));

    [Theory]
    [InlineData(false, 1, 0, 0, 0, 0)]
    [InlineData(false, 0, 0, 1, 0, 0)]
    [InlineData(true, 0, 0, 0, 0, 0)]
    [InlineData(true, 1, 1, 0, 0, 0)]
    [InlineData(true, 1, 0, 1, 0, 0)]
    [InlineData(true, 0, 1, 0, 1, 0)]
    [InlineData(true, 0, 0, 0, 1, 1)]
    public void CannotEditCustomFieldsWhenFormatDoesNotSupportFieldsOrSelectionIsNotSingle(
        bool supportsCustomFields,
        int vertices,
        int linedefs,
        int sidedefs,
        int sectors,
        int things)
        => Assert.False(EditorPropertySelection.CanEditCustomFields(supportsCustomFields, vertices, linedefs, sidedefs, sectors, things));
}
