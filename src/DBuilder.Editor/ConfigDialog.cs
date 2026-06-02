// ABOUTME: Modal dialog for choosing a UDB game configuration from the configured config directory.
// ABOUTME: Shows parsed game titles when available and preserves a browse path for external cfg files.

using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class ConfigDialog : Window
{
    private readonly ListBox _list;
    private readonly List<ConfigPickerRow> _rows;
    private readonly Settings _settings;

    public string? SelectedPath { get; private set; }
    public bool ResourceListChanged { get; private set; }

    public ConfigDialog(string configDir, string currentName, Settings settings)
    {
        Title = "Game Configuration";
        Width = 520;
        Height = 460;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _settings = settings;

        _rows = ConfigPickerModel.LoadRows(configDir);
        _list = new ListBox { ItemsSource = _rows };
        _list.SelectedIndex = ConfigPickerModel.SelectedIndex(_rows, currentName);
        _list.DoubleTapped += (_, _) => Accept();

        var info = new TextBlock
        {
            Text = _rows.Count == 0 ? $"No .cfg files found in {configDir}." : $"Configurations in {configDir}",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 0, 0, 6),
        };

        var ok = new Button { Content = "Load", MinWidth = 72, IsDefault = true };
        ok.Click += (_, _) => Accept();
        var resources = new Button { Content = "Resources...", MinWidth = 96 };
        resources.Click += async (_, _) => await EditResources();
        var browse = new Button { Content = "Browse...", MinWidth = 86 };
        browse.Click += async (_, _) => await Browse();
        var cancel = new Button { Content = "Cancel", MinWidth = 72, IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 8, 0, 0),
        };
        buttons.Children.Add(resources);
        buttons.Children.Add(browse);
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Avalonia.Thickness(12) };
        root.Children.Add(info);
        Grid.SetRow(_list, 1);
        root.Children.Add(_list);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);
        Content = root;
    }

    private void Accept()
    {
        if (_list.SelectedItem is ConfigPickerRow row)
        {
            SelectedPath = row.Path;
            Close(true);
        }
    }

    private async System.Threading.Tasks.Task EditResources()
    {
        if (_list.SelectedItem is not ConfigPickerRow row) return;

        GameConfiguration? config = null;
        try { config = GameConfiguration.FromFile(row.Path); }
        catch { }

        var dlg = new ConfigResourcesDialog(row, _settings.ResourcesForConfiguration(row.Path), config);
        if (await dlg.ShowDialog<bool>(this))
        {
            _settings.SetResourcesForConfiguration(row.Path, dlg.ResultResources);
            ResourceListChanged = true;
        }
    }

    private async System.Threading.Tasks.Task Browse()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Game Configuration",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Game config") { Patterns = new[] { "*.cfg" } } },
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            SelectedPath = path;
            Close(true);
        }
    }

}
