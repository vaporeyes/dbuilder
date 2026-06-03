// ABOUTME: Tests UDB-style selected-thing filtering by thing type.
// ABOUTME: Verifies distinct type discovery and selection narrowing semantics.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class ThingSelectionFilterTests
{
    [Fact]
    public void SelectedTypesReturnsDistinctSortedSelectedThingTypes()
    {
        var map = new MapSet();
        AddThing(map, 3004, selected: true);
        AddThing(map, 1, selected: false);
        AddThing(map, 3001, selected: true);
        AddThing(map, 3004, selected: true);

        IReadOnlyList<int> types = ThingSelectionFilter.SelectedTypes(map);

        Assert.Equal([3001, 3004], types);
    }

    [Fact]
    public void KeepSelectedTypesDeselectsSelectedThingsWithOtherTypes()
    {
        var map = new MapSet();
        Thing keep = AddThing(map, 3004, selected: true);
        Thing remove = AddThing(map, 3001, selected: true);
        Thing untouched = AddThing(map, 9, selected: false);

        int kept = ThingSelectionFilter.KeepSelectedTypes(map, [3004]);

        Assert.Equal(1, kept);
        Assert.True(keep.Selected);
        Assert.False(remove.Selected);
        Assert.False(untouched.Selected);
    }

    [Theory]
    [InlineData(1, "Filtered selected things: 1 thing remains selected.")]
    [InlineData(2, "Filtered selected things: 2 things remain selected.")]
    public void FilterStatusTextFormatsSingularAndPluralCounts(int selectedThingCount, string expected)
        => Assert.Equal(expected, ThingSelectionFilter.FilterStatusText(selectedThingCount));

    private static Thing AddThing(MapSet map, int type, bool selected)
    {
        Thing thing = map.AddThing(new Vector2D(type, type), type);
        thing.Selected = selected;
        return thing;
    }
}
