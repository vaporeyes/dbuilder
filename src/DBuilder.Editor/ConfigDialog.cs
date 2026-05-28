// ABOUTME: Modal dialog for choosing a UDB game configuration from the configured config directory.
// ABOUTME: Shows parsed game titles when available and preserves a browse path for external cfg files.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class ConfigDialog : Window
{
    private readonly ListBox _list;
    private readonly List<ConfigRow> _rows;

    public string? SelectedPath { get; private set; }

    public ConfigDialog(string configDir, string currentName)
    {
        Title = "Game Configuration";
        Width = 520;
        Height = 460;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _rows = LoadRows(configDir);
        _list = new ListBox { ItemsSource = _rows };
        int current = _rows.FindIndex(row => string.Equals(row.Title, currentName, StringComparison.OrdinalIgnoreCase));
        _list.SelectedIndex = current >= 0 ? current : (_rows.Count > 0 ? 0 : -1);
        _list.DoubleTapped += (_, _) => Accept();

        var info = new TextBlock
        {
            Text = _rows.Count == 0 ? $"No .cfg files found in {configDir}." : $"Configurations in {configDir}",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 0, 0, 6),
        };

        var ok = new Button { Content = "Load", MinWidth = 72, IsDefault = true };
        ok.Click += (_, _) => Accept();
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
        if (_list.SelectedItem is ConfigRow row)
        {
            SelectedPath = row.Path;
            Close(true);
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

    private static List<ConfigRow> LoadRows(string configDir)
    {
        if (!Directory.Exists(configDir)) return new List<ConfigRow>();

        return Directory.EnumerateFiles(configDir, "*.cfg", SearchOption.AllDirectories)
            .Where(path => !IsIncludeFile(configDir, path))
            .Select(ReadRow)
            .OrderBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsIncludeFile(string configDir, string path)
    {
        string relative = Path.GetRelativePath(configDir, path);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => string.Equals(part, "Includes", StringComparison.OrdinalIgnoreCase));
    }

    private static ConfigRow ReadRow(string path)
    {
        string fallback = Path.GetFileNameWithoutExtension(path);
        try
        {
            var cfg = new Configuration(path);
            string title = cfg.ReadSetting("game", fallback) ?? fallback;
            string engine = cfg.ReadSetting("engine", "") ?? "";
            return new ConfigRow(title, engine, path);
        }
        catch
        {
            return new ConfigRow(fallback, "", path);
        }
    }

    private sealed record ConfigRow(string Title, string Engine, string Path)
    {
        public override string ToString()
            => string.IsNullOrWhiteSpace(Engine) ? Title : $"{Title} [{Engine}]";
    }
}
