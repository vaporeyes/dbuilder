// ABOUTME: Non-modal panel of thing-category checkboxes to show/hide categories of things in the 2D view.
// ABOUTME: Toggling a category calls back to the host to update rendering live.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

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
            foreach (var filter in filters) choices.Add(new FilterChoice(filter));
            var combo = new ComboBox
            {
                ItemsSource = choices,
                SelectedIndex = Math.Max(0, choices.FindIndex(c => ReferenceEquals(c.Filter, activeFilter))),
                Margin = new Avalonia.Thickness(0, 0, 0, 10),
            };
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is FilterChoice choice) FilterSelected?.Invoke(choice.Filter);
            };
            stack.Children.Add(combo);
        }

        stack.Children.Add(new TextBlock { Text = "Show thing categories:", Foreground = Brushes.LightSkyBlue, Margin = new Avalonia.Thickness(0, 0, 0, 4) });

        foreach (var cat in categories)
        {
            string key = cat;
            var cb = new CheckBox { Content = cat, IsChecked = !isHidden(cat) };
            cb.IsCheckedChanged += (_, _) => CategoryToggled?.Invoke(key, cb.IsChecked != true);
            stack.Children.Add(cb);
        }

        Content = new ScrollViewer { Content = stack };
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
