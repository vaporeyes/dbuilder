// ABOUTME: Verifies Open Map picker presentation ordering for discovered WAD and PK3 maps.
// ABOUTME: Keeps UDB-style chooser sorting covered without opening the Avalonia dialog.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class MapPickerModelTests
{
    [Fact]
    public void SortForPickerOrdersMapNamesLikeUdbChooser()
    {
        var maps = new List<MapEntry>
        {
            new("MAP10", MapFormat.Doom),
            new("E1M1", MapFormat.Doom),
            new("MAP01", MapFormat.Udmf),
            new("map01", MapFormat.Doom),
        };

        var sorted = MapPickerModel.SortForPicker(maps);

        Assert.Equal(
            new[] { "E1M1:Doom", "map01:Doom", "MAP01:Udmf", "MAP10:Doom" },
            sorted.Select(map => $"{map.Name}:{map.Format}").ToArray());
    }
}
