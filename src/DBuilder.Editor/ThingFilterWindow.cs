// ABOUTME: Non-modal panel of thing-category checkboxes to show/hide categories of things in the 2D view.
// ABOUTME: Toggling a category calls back to the host to update rendering live.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;

namespace DBuilder.Editor;

public sealed class ThingFilterWindow : Window
{
    /// <summary>Raised with (categoryKey, hidden) when a category checkbox is toggled.</summary>
    public event Action<string, bool>? CategoryToggled;

    public ThingFilterWindow(IReadOnlyList<string> categories, Func<string, bool> isHidden)
    {
        Title = "Thing Filter";
        Width = 260;
        Height = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var stack = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 3 };
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
}
