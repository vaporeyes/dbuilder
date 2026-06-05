// ABOUTME: Modal editor for UDB-style linedef color presets stored in DBuilder settings.
// ABOUTME: Supports adding, removing, enabling, and editing preset match criteria without map mutation.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.Map;

namespace DBuilder.Editor;

public sealed class LinedefColorPresetsDialog : Window
{
    private readonly ListBox _list = new() { MinWidth = 190, MinHeight = 320 };
    private readonly TextBox _name = new();
    private readonly TextBox _color = new();
    private readonly TextBox _action = new();
    private readonly TextBox _activation = new();
    private readonly TextBox _flags = new();
    private readonly TextBox _restrictedFlags = new();
    private readonly CheckBox _enabled = new() { Content = "Enabled" };
    private readonly TextBlock _warning = new() { Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly List<LinedefColorPreset> _presets;
    private readonly bool _isUdmf;
    private int _selectedIndex = -1;
    private bool _syncing;

    public IReadOnlyList<LinedefColorPreset> ResultPresets { get; private set; }

    public LinedefColorPresetsDialog(IReadOnlyList<LinedefColorPreset> presets, bool isUdmf)
    {
        _presets = LinedefColorPresetModel.NormalizedPresets(presets).Select(preset => preset with { }).ToList();
        _isUdmf = isUdmf;
        ResultPresets = _presets;

        Title = LinedefColorPresetModel.DialogTitle;
        Width = 720;
        Height = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _list.SelectionChanged += (_, _) => SelectPreset(_list.SelectedIndex);
        _name.TextChanged += (_, _) => UpdateCurrentFromFields();
        _color.TextChanged += (_, _) => UpdateCurrentFromFields();
        _action.TextChanged += (_, _) => UpdateCurrentFromFields();
        _activation.TextChanged += (_, _) => UpdateCurrentFromFields();
        _flags.TextChanged += (_, _) => UpdateCurrentFromFields();
        _restrictedFlags.TextChanged += (_, _) => UpdateCurrentFromFields();
        _enabled.IsCheckedChanged += (_, _) => UpdateCurrentFromFields();

        Content = BuildLayout();
        RefreshList();
        _list.SelectedIndex = _presets.Count > 0 ? 0 : -1;
    }

    private Control BuildLayout()
    {
        var root = new Grid
        {
            Margin = new Avalonia.Thickness(12),
            ColumnDefinitions = new ColumnDefinitions("220,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            ColumnSpacing = 12,
            RowSpacing = 10,
        };

        var left = new DockPanel();
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Avalonia.Thickness(0, 8, 0, 0),
        };
        var add = new Button { Content = "Add", MinWidth = 62 };
        add.Click += (_, _) => AddPreset();
        var remove = new Button { Content = "Delete", MinWidth = 72 };
        remove.Click += (_, _) => RemovePreset();
        var up = new Button { Content = "Up", MinWidth = 54 };
        up.Click += (_, _) => MovePreset(-1);
        var down = new Button { Content = "Down", MinWidth = 62 };
        down.Click += (_, _) => MovePreset(1);
        buttons.Children.Add(add);
        buttons.Children.Add(remove);
        buttons.Children.Add(up);
        buttons.Children.Add(down);
        DockPanel.SetDock(buttons, Dock.Bottom);
        left.Children.Add(buttons);
        left.Children.Add(_list);
        root.Children.Add(left);

        var editor = new StackPanel { Spacing = 8 };
        editor.Children.Add(new TextBlock
        {
            Text = "Use action -1 for any nonzero action. Use activation -1 for any nonzero classic activation. Flags are separated with ^.",
            Foreground = Brushes.SlateGray,
            TextWrapping = TextWrapping.Wrap,
        });
        editor.Children.Add(_enabled);
        editor.Children.Add(Row("Name", _name));
        editor.Children.Add(Row("Color ARGB", _color));
        editor.Children.Add(Row("Action", _action));
        editor.Children.Add(Row("Activation", _activation));
        editor.Children.Add(Row("Required flags", _flags));
        editor.Children.Add(Row("Restricted flags", _restrictedFlags));
        editor.Children.Add(_warning);
        Grid.SetColumn(editor, 1);
        root.Children.Add(editor);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        var reset = new Button { Content = "Reset Defaults", MinWidth = 108 };
        reset.Click += (_, _) => ResetDefaults();
        var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true };
        ok.Click += (_, _) => { StoreCurrentFields(); ResultPresets = _presets.ToList(); Close(true); };
        var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
        cancel.Click += (_, _) => Close(false);
        footer.Children.Add(reset);
        footer.Children.Add(ok);
        footer.Children.Add(cancel);
        Grid.SetRow(footer, 1);
        Grid.SetColumnSpan(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private static Control Row(string label, TextBox box)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*") };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(box, 1);
        grid.Children.Add(box);
        return grid;
    }

    private void AddPreset()
    {
        StoreCurrentFields();
        int index = Math.Max(0, _list.SelectedIndex);
        _presets.Insert(index, new LinedefColorPreset(LinedefColorPresetModel.NewPresetName, unchecked((int)0xffffffff), Action: 0, Activation: 0));
        RefreshList();
        _list.SelectedIndex = index;
    }

    private void RemovePreset()
    {
        int index = _list.SelectedIndex;
        if (index < 0 || index >= _presets.Count) return;
        _presets.RemoveAt(index);
        RefreshList();
        _list.SelectedIndex = Math.Min(index, _presets.Count - 1);
    }

    private void MovePreset(int offset)
    {
        StoreCurrentFields();
        int index = _list.SelectedIndex;
        var moved = LinedefColorPresetModel.MovePreset(_presets, index, offset);
        if (ReferenceEquals(moved, _presets)) return;

        _presets.Clear();
        _presets.AddRange(moved);
        RefreshList();
        _list.SelectedIndex = index + offset;
    }

    private void ResetDefaults()
    {
        _presets.Clear();
        _presets.AddRange(LinedefColorPresetModel.DefaultPresets);
        RefreshList();
        _list.SelectedIndex = 0;
    }

    private void SelectPreset(int index)
    {
        if (_syncing) return;
        StoreCurrentFields();
        _selectedIndex = index;
        LoadFields(index);
    }

    private void StoreCurrentFields()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _presets.Count) return;
        _presets[_selectedIndex] = ReadFields(_presets[_selectedIndex]);
        RefreshList(preserveSelection: true);
    }

    private void UpdateCurrentFromFields()
    {
        if (_syncing || _selectedIndex < 0 || _selectedIndex >= _presets.Count) return;
        _presets[_selectedIndex] = ReadFields(_presets[_selectedIndex]);
        RefreshWarning(_selectedIndex);
        RefreshList(preserveSelection: true);
    }

    private LinedefColorPreset ReadFields(LinedefColorPreset fallback)
    {
        return new LinedefColorPreset(
            string.IsNullOrWhiteSpace(_name.Text) ? fallback.Name : _name.Text.Trim(),
            LinedefColorPresetModel.ParseColor(_color.Text, fallback.Color),
            ParseInt(_action.Text, fallback.Action),
            ParseInt(_activation.Text, fallback.Activation),
            LinedefColorPresetModel.ParseFlags(_flags.Text),
            LinedefColorPresetModel.ParseFlags(_restrictedFlags.Text),
            _enabled.IsChecked == true);
    }

    private void LoadFields(int index)
    {
        _syncing = true;
        try
        {
            if (index < 0 || index >= _presets.Count)
            {
                _name.Text = "";
                _color.Text = "";
                _action.Text = "";
                _activation.Text = "";
                _flags.Text = "";
                _restrictedFlags.Text = "";
                _enabled.IsChecked = false;
                RefreshWarning(-1);
                return;
            }

            LinedefColorPreset preset = _presets[index];
            _name.Text = preset.Name;
            _color.Text = LinedefColorPresetModel.FormatColor(preset.Color);
            _action.Text = preset.Action.ToString(CultureInfo.InvariantCulture);
            _activation.Text = preset.Activation.ToString(CultureInfo.InvariantCulture);
            _flags.Text = LinedefColorPresetModel.FormatFlags(preset.RequiredFlags);
            _restrictedFlags.Text = LinedefColorPresetModel.FormatFlags(preset.DisallowedFlags);
            _enabled.IsChecked = preset.Enabled;
            RefreshWarning(index);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void RefreshList(bool preserveSelection = false)
    {
        int index = preserveSelection ? _list.SelectedIndex : -1;
        _syncing = true;
        try
        {
            _list.ItemsSource = _presets.Select(PresetLabel).ToArray();
            if (preserveSelection) _list.SelectedIndex = index;
        }
        finally
        {
            _syncing = false;
        }
    }

    private static string PresetLabel(LinedefColorPreset preset)
        => (preset.Enabled ? "" : "[off] ") + preset.Name;

    private void RefreshWarning(int index)
    {
        string? warning = LinedefColorPresetModel.ValidationWarning(_presets, index, _isUdmf);
        _warning.Text = warning ?? "";
        _warning.IsVisible = warning != null;
    }

    private static int ParseInt(string? text, int fallback)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
}
