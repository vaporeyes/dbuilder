// ABOUTME: Non-modal Find & Replace window - pick a category, find matches (selecting them) and replace them.
// ABOUTME: Raises FindRequested / ReplaceRequested / NextFreeTagRequested; the host runs them and reports back via SetResult.

using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class FindReplaceWindow : Window
{
    private readonly ComboBox _category;
    private readonly TextBox _find;
    private readonly TextBox _replace;
    private readonly CheckBox _withinSelection;
    private readonly TextBlock _result;

    public FindCategory Category => _category.SelectedItem is FindCategoryDescriptor descriptor ? descriptor.Category : FindCategory.ThingType;
    public string FindText => _find.Text ?? "";
    public string ReplaceText => _replace.Text ?? "";
    public bool WithinSelection => _withinSelection.IsChecked == true;

    public event Action? FindRequested;
    public event Action? ReplaceRequested;
    public event Action? NextFreeTagRequested;

    public FindReplaceWindow(bool mixTexturesFlats = false)
    {
        Title = "Find & Replace";
        Width = 380;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _category = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = MapSearch.CategoryDescriptors,
        };
        _category.SelectedIndex = 0;
        _find = new TextBox();
        _replace = new TextBox();
        _withinSelection = new CheckBox { Content = "Within current selection" };
        _result = new TextBlock { Foreground = Brushes.LightSkyBlue, TextWrapping = TextWrapping.Wrap, Margin = new Avalonia.Thickness(0, 4, 0, 0) };

        var rows = new StackPanel { Margin = new Avalonia.Thickness(12), Spacing = 6 };
        rows.Children.Add(Labeled("Category", _category));
        rows.Children.Add(Labeled("Find", _find));
        rows.Children.Add(Labeled("Replace", _replace));
        rows.Children.Add(_withinSelection);

        var findBtn = new Button { Content = "Find", MinWidth = 80 };
        findBtn.Click += (_, _) => FindRequested?.Invoke();
        var replaceBtn = new Button { Content = "Replace all", MinWidth = 100 };
        replaceBtn.Click += (_, _) => ReplaceRequested?.Invoke();
        _category.SelectionChanged += (_, _) => replaceBtn.IsEnabled = CanReplaceSelected(mixTexturesFlats);
        replaceBtn.IsEnabled = CanReplaceSelected(mixTexturesFlats);
        var freeTagBtn = new Button { Content = "Next free tag", MinWidth = 110 };
        freeTagBtn.Click += (_, _) => NextFreeTagRequested?.Invoke();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Avalonia.Thickness(0, 6, 0, 0) };
        buttons.Children.Add(freeTagBtn);
        buttons.Children.Add(findBtn);
        buttons.Children.Add(replaceBtn);
        rows.Children.Add(buttons);
        rows.Children.Add(_result);

        Content = rows;
    }

    private bool CanReplaceSelected(bool mixTexturesFlats)
        => _category.SelectedItem is FindCategoryDescriptor descriptor
            && MapSearch.CanReplace(descriptor.Category, mixTexturesFlats);

    /// <summary>Pre-fills the Find box (used by "next free tag").</summary>
    public void SetFindText(string text) => _find.Text = text;

    /// <summary>Shows the outcome of the last find/replace.</summary>
    public void SetResult(string text) => _result.Text = text;

    private static Control Labeled(string label, Control field)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("90,*") };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(field, 1);
        grid.Children.Add(field);
        return grid;
    }
}
