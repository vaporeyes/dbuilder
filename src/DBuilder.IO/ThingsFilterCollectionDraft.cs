// ABOUTME: Editable collection model for UDB-compatible custom things filters.
// ABOUTME: Mirrors ThingsFiltersForm list behavior without depending on editor UI controls.

using System.Collections.Generic;

namespace DBuilder.IO;

public sealed class ThingsFilterCollectionDraft
{
    private readonly List<ThingsFilterDraftEntry> filters = new();

    public IReadOnlyList<ThingsFilterDraftEntry> Filters => filters;

    public static ThingsFilterCollectionDraft FromFilters(IEnumerable<ThingsFilterInfo> source)
    {
        var collection = new ThingsFilterCollectionDraft();
        foreach (var filter in source)
            collection.filters.Add(new ThingsFilterDraftEntry(filter.Key, ThingsFilterDraft.FromInfo(filter)));
        collection.SortByName();
        return collection;
    }

    public ThingsFilterDraftEntry AddNew()
    {
        var entry = new ThingsFilterDraftEntry(NextKey(), new ThingsFilterDraft());
        filters.Add(entry);
        return entry;
    }

    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= filters.Count) return false;
        filters.RemoveAt(index);
        return true;
    }

    public void SortByName()
        => filters.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Draft.Name, right.Draft.Name));

    public ThingsFilterInfo? FindByName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var entry in filters)
        {
            if (!entry.Draft.IsValid()) continue;
            if (string.Equals(entry.Draft.Name, name, StringComparison.Ordinal)) return entry.Draft.ToInfo(entry.Key);
        }
        return null;
    }

    public ThingsFilterInfo? FindByKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (var entry in filters)
        {
            if (!entry.Draft.IsValid()) continue;
            if (string.Equals(entry.Key, key, StringComparison.Ordinal)) return entry.Draft.ToInfo(entry.Key);
        }
        return null;
    }

    public void WriteSettings(Configuration configuration, string path = "thingsfilters")
    {
        configuration.DeleteSetting(path);
        foreach (var entry in filters)
        {
            if (!entry.Draft.IsValid()) continue;
            entry.Draft.WriteSettings(configuration, path + "." + entry.Key);
        }
    }

    private string NextKey()
    {
        for (int i = 0; ; i++)
        {
            string key = "filter" + i;
            if (!ContainsKey(key)) return key;
        }
    }

    private bool ContainsKey(string key)
    {
        foreach (var filter in filters)
            if (string.Equals(filter.Key, key, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

public sealed record ThingsFilterDraftEntry(string Key, ThingsFilterDraft Draft);
