// ABOUTME: Programmatic Avalonia property dialogs for editing a selected thing, linedef or sector.
// ABOUTME: Each dialog exposes its edited values via properties (read on OK) so the caller can snapshot for undo only when confirmed.

using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Editor;

// Shared helpers for building simple label/input forms without XAML.
public abstract class PropertyDialog : Window
{
    protected bool Confirmed;
    private readonly StackPanel _rows;
    private readonly TextBlock? _liveLabel;

    protected PropertyDialog(string title, string? liveLabel = null)
    {
        Title = title;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _rows = new StackPanel { Margin = new Avalonia.Thickness(12), Spacing = 6 };
        if (liveLabel != null)
        {
            _liveLabel = new TextBlock { Text = liveLabel, Foreground = Brushes.LightSkyBlue, Margin = new Avalonia.Thickness(0, 0, 0, 4) };
            _rows.Children.Add(_liveLabel);
        }

        var ok = new Button { Content = "OK", MinWidth = 72, IsDefault = true };
        ok.Click += (_, _) => { OnConfirm(); Confirmed = true; Close(true); };
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
        _rows.Children.Add(buttons);

        Content = _rows;
    }

    protected void SetLiveLabel(string text) { if (_liveLabel != null) _liveLabel.Text = text; }

    // Adds a labeled text box (inserted before the buttons row, which is always last).
    protected TextBox AddField(string label, string value)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*") };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        var box = new TextBox { Text = value };
        Grid.SetColumn(box, 1);
        grid.Children.Add(box);
        _rows.Children.Insert(_rows.Children.Count - 1, grid);
        return box;
    }

    protected static int ParseInt(TextBox box, int fallback)
        => int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    protected abstract void OnConfirm();
}

public sealed class ThingEditDialog : PropertyDialog
{
    private readonly GameConfiguration? _config;
    private readonly TextBox _type, _x, _y, _angle, _height, _tag, _action;

    public int ResultType, ResultAngle, ResultTag, ResultAction;
    public double ResultX, ResultY, ResultHeight;

    public ThingEditDialog(Thing t, GameConfiguration? config) : base("Edit Thing", " ")
    {
        _config = config;
        _type = AddField("Type", t.Type.ToString(CultureInfo.InvariantCulture));
        _x = AddField("X", t.Position.x.ToString("0.###", CultureInfo.InvariantCulture));
        _y = AddField("Y", t.Position.y.ToString("0.###", CultureInfo.InvariantCulture));
        _height = AddField("Height (Z)", t.Height.ToString("0.###", CultureInfo.InvariantCulture));
        _angle = AddField("Angle", t.Angle.ToString(CultureInfo.InvariantCulture));
        _tag = AddField("Tag (TID)", t.Tag.ToString(CultureInfo.InvariantCulture));
        _action = AddField("Action", t.Action.ToString(CultureInfo.InvariantCulture));

        _type.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) RefreshName(); };
        RefreshName();
    }

    private void RefreshName()
        => SetLiveLabel(_config?.ThingTitle(ParseInt(_type, -1)) ?? $"type {ParseInt(_type, -1)}");

    protected override void OnConfirm()
    {
        ResultType = ParseInt(_type, 0);
        ResultAngle = ParseInt(_angle, 0);
        ResultTag = ParseInt(_tag, 0);
        ResultAction = ParseInt(_action, 0);
        ResultX = double.TryParse(_x.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : 0;
        ResultY = double.TryParse(_y.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ? y : 0;
        ResultHeight = double.TryParse(_height.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var h) ? h : 0;
    }
}

public sealed class LinedefEditDialog : PropertyDialog
{
    private readonly GameConfiguration? _config;
    private readonly TextBox _action, _tag, _flags;

    public int ResultAction, ResultTag, ResultFlags;

    public LinedefEditDialog(Linedef l, GameConfiguration? config) : base("Edit Linedef", " ")
    {
        _config = config;
        _action = AddField("Action", l.Action.ToString(CultureInfo.InvariantCulture));
        _tag = AddField("Tag", l.Tag.ToString(CultureInfo.InvariantCulture));
        _flags = AddField("Flags (int)", l.Flags.ToString(CultureInfo.InvariantCulture));

        _action.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) RefreshName(); };
        RefreshName();
    }

    private void RefreshName()
        => SetLiveLabel(_config?.LinedefActionTitle(ParseInt(_action, 0)) ?? $"action {ParseInt(_action, 0)}");

    protected override void OnConfirm()
    {
        ResultAction = ParseInt(_action, 0);
        ResultTag = ParseInt(_tag, 0);
        ResultFlags = ParseInt(_flags, 0);
    }
}

public sealed class SectorEditDialog : PropertyDialog
{
    private readonly GameConfiguration? _config;
    private readonly TextBox _floor, _ceil, _floorTex, _ceilTex, _bright, _special, _tag;

    public int ResultFloor, ResultCeil, ResultBright, ResultSpecial, ResultTag;
    public string ResultFloorTex = "-", ResultCeilTex = "-";

    public SectorEditDialog(Sector s, GameConfiguration? config) : base("Edit Sector", " ")
    {
        _config = config;
        _floor = AddField("Floor height", s.FloorHeight.ToString(CultureInfo.InvariantCulture));
        _ceil = AddField("Ceiling height", s.CeilHeight.ToString(CultureInfo.InvariantCulture));
        _floorTex = AddField("Floor texture", s.FloorTexture);
        _ceilTex = AddField("Ceiling texture", s.CeilTexture);
        _bright = AddField("Brightness", s.Brightness.ToString(CultureInfo.InvariantCulture));
        _special = AddField("Effect", s.Special.ToString(CultureInfo.InvariantCulture));
        _tag = AddField("Tag", s.Tag.ToString(CultureInfo.InvariantCulture));

        _special.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) RefreshName(); };
        RefreshName();
    }

    private void RefreshName()
        => SetLiveLabel(_config?.SectorEffectTitle(ParseInt(_special, 0)) ?? $"effect {ParseInt(_special, 0)}");

    protected override void OnConfirm()
    {
        ResultFloor = ParseInt(_floor, 0);
        ResultCeil = ParseInt(_ceil, 0);
        ResultBright = ParseInt(_bright, 0);
        ResultSpecial = ParseInt(_special, 0);
        ResultTag = ParseInt(_tag, 0);
        ResultFloorTex = string.IsNullOrWhiteSpace(_floorTex.Text) ? "-" : _floorTex.Text!;
        ResultCeilTex = string.IsNullOrWhiteSpace(_ceilTex.Text) ? "-" : _ceilTex.Text!;
    }
}
