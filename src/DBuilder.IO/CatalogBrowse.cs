// ABOUTME: Builds and filters categorized catalogs (thing types, linedef actions, sector effects) for the browser dialog.
// ABOUTME: Pure grouping/filtering over GameConfiguration data so the UI just renders the result.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DBuilder.IO;

/// <summary>One browsable catalog entry: its number, display title and category (empty when uncategorized).</summary>
public readonly record struct BrowseEntry(int Number, string Title, string Category);

public static class CatalogBrowse
{
    /// <summary>Thing types as browse entries (number, title, category).</summary>
    public static List<BrowseEntry> Things(GameConfiguration cfg)
        => cfg.Things.Values.Select(t => new BrowseEntry(t.Index, t.Title, t.Category)).ToList();

    /// <summary>Linedef actions as browse entries, with a leading "None" (0).</summary>
    public static List<BrowseEntry> LinedefActions(GameConfiguration cfg)
    {
        var list = new List<BrowseEntry> { new(0, "None", "") };
        list.AddRange(cfg.LinedefActions.Values.Select(a => new BrowseEntry(a.Index, a.Title, a.Category)));
        return list;
    }

    /// <summary>Sector effects as browse entries (uncategorized).</summary>
    public static List<BrowseEntry> SectorEffects(GameConfiguration cfg)
        => cfg.SectorEffects.Values.Select(s => new BrowseEntry(s.Index, s.Title, "")).ToList();

    /// <summary>
    /// Filters entries by a query, matching the title, the number, or the category (case-insensitive).
    /// An empty query returns everything.
    /// </summary>
    public static IEnumerable<BrowseEntry> Filter(IEnumerable<BrowseEntry> entries, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return entries;
        string q = query.Trim();
        return entries.Where(e =>
            e.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            e.Number.ToString(CultureInfo.InvariantCulture).Contains(q) ||
            e.Category.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Groups entries by category (sorted), entries within each group sorted by number.</summary>
    public static List<(string Category, List<BrowseEntry> Entries)> Grouped(IEnumerable<BrowseEntry> entries)
        => entries
            .GroupBy(e => string.IsNullOrEmpty(e.Category) ? "(uncategorized)" : e.Category)
            .Select(g => (Category: g.Key, Entries: g.OrderBy(e => e.Number).ToList()))
            .OrderBy(t => t.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
