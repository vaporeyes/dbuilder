// ABOUTME: Modal dialog listing the maps in the open WAD so the user can switch to any of them.
// ABOUTME: Returns the chosen MapEntry via Selected; ShowDialog<bool> yields true on confirm.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class MapPickerDialog : Window
{
    private readonly ListBox _list;
    private readonly List<MapEntry> _maps;
    private readonly Func<MapEntry, OpenMapSelectionOptions>? _optionsForMap;
    private readonly CheckBox? _strictPatches;
    private readonly CheckBox? _longTextureNames;
    private OpenMapSelectionOptions _currentOptions;

    /// <summary>The map the user chose, or null if cancelled.</summary>
    public MapEntry? Selected { get; private set; }
    public OpenMapSelectionOptions SelectedOptions { get; private set; }

    public MapPickerDialog(List<MapEntry> maps, string? current, Func<MapEntry, OpenMapSelectionOptions>? optionsForMap = null)
    {
        _maps = maps;
        _optionsForMap = optionsForMap;
        Title = "Open Map";
        Width = 320;
        Height = optionsForMap is null ? 440 : 470;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _list = new ListBox
        {
            ItemsSource = maps.Select(m => $"{m.Name}   [{m.Format}]").ToList(),
        };
        int cur = current != null ? maps.FindIndex(m => m.Name == current) : 0;
        _list.SelectedIndex = cur < 0 ? 0 : cur;
        _list.SelectionChanged += (_, _) => UpdateSelectedMapOptions();
        _list.DoubleTapped += (_, _) => Accept();

        _strictPatches = optionsForMap is null
            ? null
            : new CheckBox
            {
                Content = "Strictly load patches between P_START and P_END only for this file",
                Margin = new Avalonia.Thickness(0, 8, 0, 0),
            };
        if (_strictPatches is not null)
            _strictPatches.IsCheckedChanged += (_, _) =>
                _currentOptions = _currentOptions.WithStrictPatches(_strictPatches.IsChecked == true);

        _longTextureNames = optionsForMap is null
            ? null
            : new CheckBox
            {
                Content = "Use long texture names",
                Margin = new Avalonia.Thickness(0, 8, 0, 0),
            };
        if (_longTextureNames is not null)
            _longTextureNames.IsCheckedChanged += (_, _) =>
                _currentOptions = _currentOptions.WithUseLongTextureNames(_longTextureNames.IsChecked == true);

        var ok = new Button { Content = "Open", MinWidth = 72, IsDefault = true };
        ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = "Cancel", MinWidth = 72, IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 8, 0, 0),
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var content = new Grid
        {
            RowDefinitions = _longTextureNames is null
                ? new RowDefinitions("*")
                : new RowDefinitions("*,Auto,Auto"),
        };
        content.Children.Add(_list);
        if (_strictPatches is not null)
        {
            Grid.SetRow(_strictPatches, 1);
            content.Children.Add(_strictPatches);
        }
        if (_longTextureNames is not null)
        {
            Grid.SetRow(_longTextureNames, 2);
            content.Children.Add(_longTextureNames);
        }

        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Avalonia.Thickness(12) };
        root.Children.Add(content);
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);
        Content = root;
        UpdateSelectedMapOptions();
    }

    private void Accept()
    {
        int i = _list.SelectedIndex;
        if (i >= 0 && i < _maps.Count) { Selected = _maps[i]; SelectedOptions = _currentOptions; Close(true); }
        else Close(false);
    }

    private void UpdateSelectedMapOptions()
    {
        if (_optionsForMap is null || _longTextureNames is null) return;
        int i = _list.SelectedIndex;
        _currentOptions = i >= 0 && i < _maps.Count
            ? _optionsForMap(_maps[i])
            : default;
        if (_strictPatches is not null) _strictPatches.IsChecked = _currentOptions.StrictPatches;
        _longTextureNames.IsEnabled = _currentOptions.LongTextureNamesSupported;
        _longTextureNames.IsChecked = _currentOptions.UseLongTextureNames;
    }
}
