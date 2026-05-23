// ABOUTME: Modal texture/flat browser - a filterable thumbnail grid; clicking a tile returns its name.
// ABOUTME: Thumbnails come from the ResourceManager (textures or flats); ShowDialog<bool> yields true on pick.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class TextureBrowserDialog : Window
{
    private readonly ResourceManager _resources;
    private readonly bool _flats;
    private readonly IReadOnlyList<string> _names;
    private readonly WrapPanel _grid;
    // Tiles awaiting a decoded thumbnail; loaded incrementally so the dialog opens instantly.
    private readonly List<(string name, Image image)> _pending = new();
    private int _loadIndex;
    private int _generation; // cancels in-flight loading when the filter changes

    /// <summary>The chosen texture/flat name, or null if cancelled.</summary>
    public string? Selected { get; private set; }

    public TextureBrowserDialog(ResourceManager resources, bool flats)
    {
        _resources = resources;
        _flats = flats;
        _names = flats ? resources.GetFlatNames() : resources.GetTextureNames();

        Title = flats ? "Browse Flats" : "Browse Textures";
        Width = 680;
        Height = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var filter = new TextBox { Watermark = "Filter by name..." };
        filter.TextChanged += (_, _) => Populate(filter.Text);

        _grid = new WrapPanel { Orientation = Orientation.Horizontal };
        var scroll = new ScrollViewer { Content = _grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Avalonia.Thickness(8) };
        root.Children.Add(filter);
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        Content = root;

        Populate(null);
    }

    private void Populate(string? filter)
    {
        _grid.Children.Clear();
        _pending.Clear();
        _loadIndex = 0;
        int gen = ++_generation;
        foreach (var name in _names)
        {
            if (!string.IsNullOrEmpty(filter) && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            _grid.Children.Add(Tile(name)); // image filled in lazily
        }
        if (_pending.Count > 0) Dispatcher.UIThread.Post(() => LoadChunk(gen), DispatcherPriority.Background);
    }

    // Decodes a batch of thumbnails, then yields and reschedules so the UI stays responsive.
    private void LoadChunk(int gen)
    {
        if (gen != _generation) return; // a newer filter superseded this load
        int budget = 24;
        while (_loadIndex < _pending.Count && budget-- > 0)
        {
            var (name, image) = _pending[_loadIndex++];
            var img = _flats ? _resources.GetFlat(name) : _resources.GetWallTexture(name);
            image.Source = BitmapConvert.ToBitmap(img);
        }
        if (_loadIndex < _pending.Count) Dispatcher.UIThread.Post(() => LoadChunk(gen), DispatcherPriority.Background);
    }

    private Control Tile(string name)
    {
        var image = new Image { Width = 64, Height = 64, Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
        _pending.Add((name, image));

        var box = new Border
        {
            Width = 68, Height = 68,
            Background = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x16)),
            Child = image,
        };
        var stack = new StackPanel { Width = 74, Spacing = 1, Margin = new Avalonia.Thickness(2) };
        stack.Children.Add(box);
        stack.Children.Add(new TextBlock
        {
            Text = name, FontSize = 10, Foreground = Brushes.Gray,
            MaxWidth = 72, TextTrimming = TextTrimming.CharacterEllipsis,
        });

        var btn = new Button { Content = stack, Padding = new Avalonia.Thickness(2), Background = Brushes.Transparent };
        btn.Click += (_, _) => { Selected = name; Close(true); };
        return btn;
    }
}
