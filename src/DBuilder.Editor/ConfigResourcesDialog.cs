// ABOUTME: Modal editor for UDB-style resources attached to a game configuration.
// ABOUTME: Reuses resource option metadata so config resources load like map resources.

using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class ConfigResourcesDialog : Window
{
    private readonly GameConfiguration? _config;
    private readonly ObservableCollection<DataLocation> _resources;
    private readonly ListBox _list;
    private readonly TextBlock _warnings;

    public DataLocationList ResultResources { get; private set; }

    public ConfigResourcesDialog(ConfigPickerRow row, IEnumerable<DataLocation> resources, GameConfiguration? config)
    {
        _config = config;
        ResultResources = new DataLocationList(resources);
        _resources = new ObservableCollection<DataLocation>(ResultResources);

        Title = $"Configuration Resources - {row.Title}";
        Width = 620;
        Height = 420;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _list = new ListBox { ItemsSource = _resources };
        _warnings = new TextBlock
        {
            Foreground = Brushes.DarkOrange,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
            Margin = new Avalonia.Thickness(0, 8, 0, 8),
        };

        var addFile = new Button { Content = "Add File...", MinWidth = 86 };
        addFile.Click += async (_, _) => await AddFile();
        var addDir = new Button { Content = "Add Directory...", MinWidth = 110 };
        addDir.Click += async (_, _) => await AddDirectory();
        var edit = new Button { Content = "Options...", MinWidth = 86 };
        edit.Click += async (_, _) => await EditSelected();
        var remove = new Button { Content = "Remove", MinWidth = 72 };
        remove.Click += (_, _) => RemoveSelected();

        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        tools.Children.Add(addFile);
        tools.Children.Add(addDir);
        tools.Children.Add(edit);
        tools.Children.Add(remove);

        var ok = new Button { Content = "OK", MinWidth = 72, IsDefault = true };
        ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = "Cancel", MinWidth = 72, IsCancel = true };
        cancel.Click += (_, _) => Close(false);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"),
            Margin = new Avalonia.Thickness(12),
        };
        root.Children.Add(tools);
        Grid.SetRow(_list, 1);
        root.Children.Add(_list);
        Grid.SetRow(_warnings, 2);
        root.Children.Add(_warnings);
        Grid.SetRow(buttons, 3);
        root.Children.Add(buttons);
        Content = root;

        RefreshWarnings();
    }

    private async System.Threading.Tasks.Task AddFile()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add Configuration Resource",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("WAD or PK3") { Patterns = new[] { "*.wad", "*.pk3", "*.pk7", "*.zip", "*.pke", "*.ipk3", "*.ipk7" } },
            },
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path) return;
        await AddResource(new DataLocation(DataLocation.InferType(path), path));
    }

    private async System.Threading.Tasks.Task AddDirectory()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Add Configuration Resource Directory",
            AllowMultiple = false,
        });
        if (folders.Count == 0 || folders[0].TryGetLocalPath() is not { } path) return;
        await AddResource(new DataLocation(DataLocationType.Directory, path));
    }

    private async System.Threading.Tasks.Task AddResource(DataLocation resource)
    {
        ApplyRequiredArchiveDefaults(resource);
        var options = new ResourceOptionsDialog(resource);
        if (await options.ShowDialog<bool>(this))
        {
            _resources.Add(options.ResultLocation);
            RefreshWarnings();
        }
    }

    private async System.Threading.Tasks.Task EditSelected()
    {
        if (_list.SelectedItem is not DataLocation location) return;
        int index = _resources.IndexOf(location);
        if (index < 0) return;

        var options = new ResourceOptionsDialog(location);
        if (await options.ShowDialog<bool>(this))
        {
            _resources[index] = options.ResultLocation;
            RefreshWarnings();
        }
    }

    private void RemoveSelected()
    {
        if (_list.SelectedItem is DataLocation location)
        {
            _resources.Remove(location);
            RefreshWarnings();
        }
    }

    private void ApplyRequiredArchiveDefaults(DataLocation resource)
        => ConfigResourceDefaultsModel.ApplyRequiredArchiveDefaults(_config, resource);

    private void Accept()
    {
        ResultResources = new DataLocationList(_resources);
        Close(true);
    }

    private void RefreshWarnings()
    {
        var warnings = ResourceArchiveWarningModel.BuildWarnings(_config, _resources);
        _warnings.Text = string.Join("\n\n", warnings);
        _warnings.IsVisible = warnings.Count > 0;
    }
}
