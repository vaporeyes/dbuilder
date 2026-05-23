// ABOUTME: Tests catalog browsing helpers - building entries from a config and filtering/grouping them by category.
// ABOUTME: Drives the categorized Action/Thing/Effect browser dialog.

using System.Linq;
using DBuilder.IO;

namespace DBuilder.Tests;

public class CatalogBrowseTests
{
    private const string Cfg = @"
thingtypes
{
    monsters
    {
        title = ""Monsters"";
        3001 { title = ""Imp""; }
        3002 { title = ""Demon""; }
    }
    keys
    {
        title = ""Keys"";
        5 { title = ""Blue Keycard""; }
    }
}
linedeftypes
{
    doors
    {
        title = ""Doors"";
        1 { title = ""Door Open Wait Close""; }
    }
}
sectortypes
{
    7 = ""Damage 5%"";
}";

    [Fact]
    public void ThingsCarryCategory()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var entries = CatalogBrowse.Things(gc);
        var imp = entries.First(e => e.Number == 3001);
        Assert.Equal("Imp", imp.Title);
        Assert.Equal("monsters", imp.Category);
    }

    [Fact]
    public void LinedefActionsIncludeNone()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var entries = CatalogBrowse.LinedefActions(gc);
        Assert.Contains(entries, e => e.Number == 0 && e.Title == "None");
        Assert.Contains(entries, e => e.Number == 1 && e.Category == "Doors");
    }

    [Fact]
    public void FilterMatchesTitleNumberAndCategory()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var things = CatalogBrowse.Things(gc);
        Assert.Single(CatalogBrowse.Filter(things, "imp"));        // by title
        Assert.Single(CatalogBrowse.Filter(things, "3002"));       // by number
        Assert.Equal(2, CatalogBrowse.Filter(things, "monster").Count()); // by category
        Assert.Equal(3, CatalogBrowse.Filter(things, "").Count());        // empty -> all
    }

    [Fact]
    public void GroupedSortsCategoriesAndEntries()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var groups = CatalogBrowse.Grouped(CatalogBrowse.Things(gc));
        Assert.Equal(new[] { "keys", "monsters" }, groups.Select(g => g.Category)); // alphabetical
        var monsters = groups.First(g => g.Category == "monsters").Entries;
        Assert.Equal(new[] { 3001, 3002 }, monsters.Select(e => e.Number)); // by number
    }

    [Fact]
    public void UncategorizedEntriesGroupTogether()
    {
        var gc = GameConfiguration.FromText(Cfg);
        var groups = CatalogBrowse.Grouped(CatalogBrowse.SectorEffects(gc));
        Assert.Equal("(uncategorized)", groups.Single().Category);
    }
}
