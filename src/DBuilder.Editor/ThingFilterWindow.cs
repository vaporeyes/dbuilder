// ABOUTME: Non-modal panel of thing-category checkboxes to show/hide categories of things in the 2D view.
// ABOUTME: Toggling a category calls back to the host to update rendering live.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed record ThingFilterCategoryChoice(string Key, string Label);

public sealed class ThingFilterWindow : Window
{
    /// <summary>Raised with (categoryKey, hidden) when a category checkbox is toggled.</summary>
    public event Action<string, bool>? CategoryToggled;
    public event Action<ThingsFilterInfo?>? FilterSelected;

    public ThingFilterWindow(
        IReadOnlyList<string> categories,
        Func<string, bool> isHidden,
        IReadOnlyList<ThingsFilterInfo> filters,
        ThingsFilterInfo? activeFilter)
        : this(categories.Select(category => new ThingFilterCategoryChoice(category, category)).ToList(), isHidden, filters, activeFilter)
    {
    }

    public ThingFilterWindow(
        IReadOnlyList<ThingFilterCategoryChoice> categories,
        Func<string, bool> isHidden,
        IReadOnlyList<ThingsFilterInfo> filters,
        ThingsFilterInfo? activeFilter)
    {
        Title = "Thing Filter";
        Width = 320;
        Height = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var stack = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 3 };
        if (filters.Count > 0)
        {
            stack.Children.Add(new TextBlock { Text = "Configured filter:", Foreground = Brushes.LightSkyBlue, Margin = new Avalonia.Thickness(0, 0, 0, 4) });
            var choices = new List<FilterChoice> { new(null) };
            foreach (var filter in SortedFilters(filters)) choices.Add(new FilterChoice(filter));
            var combo = new ComboBox
            {
                ItemsSource = choices,
                SelectedIndex = Math.Max(0, choices.FindIndex(c => IsActiveFilter(c.Filter, activeFilter))),
                Margin = new Avalonia.Thickness(0, 0, 0, 10),
            };
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is FilterChoice choice) FilterSelected?.Invoke(choice.Filter);
            };
            stack.Children.Add(combo);
        }

        stack.Children.Add(new TextBlock { Text = "Show thing categories:", Foreground = Brushes.LightSkyBlue, Margin = new Avalonia.Thickness(0, 0, 0, 4) });

        foreach (var category in categories)
        {
            string key = category.Key;
            var cb = new CheckBox { Content = category.Label, IsChecked = !isHidden(key) };
            cb.IsCheckedChanged += (_, _) => CategoryToggled?.Invoke(key, cb.IsChecked != true);
            stack.Children.Add(cb);
        }

        Content = new ScrollViewer { Content = stack };
    }

    private static bool IsActiveFilter(ThingsFilterInfo? choice, ThingsFilterInfo? active)
        => choice != null && active != null && string.Equals(choice.Key, active.Key, StringComparison.Ordinal);

    public static IReadOnlyList<ThingsFilterInfo> SortedFilters(IReadOnlyList<ThingsFilterInfo> filters)
        => filters
            .OrderBy(filter => filter.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(filter => filter.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<ThingFilterCategoryChoice> CategoryChoices(GameConfiguration config)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ThingTypeInfo thing in config.Things.Values)
            used.Add(MapControl.ThingCategoryKey(thing.Category));

        return used
            .Select(key => new ThingFilterCategoryChoice(key, CategoryLabel(key, config.ThingCategories)))
            .OrderBy(choice => choice.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string CategoryLabel(string key, IReadOnlyDictionary<string, ThingCategoryInfo> categories)
    {
        if (key == MapControl.ThingCategoryKey("")) return key;
        if (!categories.TryGetValue(key, out ThingCategoryInfo? category)) return key;

        string title = string.IsNullOrWhiteSpace(category.Title) ? LastCategorySegment(key) : category.Title;
        if (string.IsNullOrEmpty(category.ParentKey) || !categories.ContainsKey(category.ParentKey))
            return title;

        return CategoryLabel(category.ParentKey, categories) + " / " + title;
    }

    private static string LastCategorySegment(string key)
    {
        int dot = key.LastIndexOf('.');
        return dot >= 0 ? key[(dot + 1)..] : key;
    }

    private sealed class FilterChoice
    {
        public FilterChoice(ThingsFilterInfo? filter) => Filter = filter;

        public ThingsFilterInfo? Filter { get; }

        public override string ToString()
        {
            if (Filter == null) return "(none)";

            string name = Filter.Invert ? "!" + Filter.Name : Filter.Name;
            return (ThingsFilterDisplayMode)Filter.DisplayMode switch
            {
                ThingsFilterDisplayMode.ClassicModesOnly => name + " [2D]",
                ThingsFilterDisplayMode.VisualModesOnly => name + " [3D]",
                _ => name,
            };
        }
    }
}
