// ABOUTME: Programmatic Avalonia property dialogs for editing a selected thing, linedef or sector.
// ABOUTME: Each dialog exposes its edited values via properties (read on OK) so the caller can snapshot for undo only when confirmed.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Editor;

/// <summary>A pickable catalog entry (number + display label) for config-driven dropdowns.</summary>
public sealed record CatalogItem(int Number, string Label)
{
    public override string ToString() => Label;
}

/// <summary>A pickable catalog entry (text value + display label) for config-driven dropdowns.</summary>
public sealed record CatalogTextItem(string Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>A set of flag checkboxes (bit -> CheckBox) that recombines into an int on demand.</summary>
public sealed class FlagChecks
{
    private readonly Dictionary<int, CheckBox> _boxes = new();
    public void Add(int bit, CheckBox box) => _boxes[bit] = box;
    public int Value
    {
        get { int v = 0; foreach (var (bit, cb) in _boxes) if (cb.IsChecked == true) v |= bit; return v; }
    }
}

public sealed class StringFlagChecks
{
    private readonly Dictionary<string, CheckBox> _boxes = new(StringComparer.OrdinalIgnoreCase);
    public void Add(string flag, CheckBox box) => _boxes[flag] = box;

    public HashSet<string> Value
    {
        get
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (flag, box) in _boxes)
                if (box.IsChecked == true) result.Add(flag);
            return result;
        }
    }
}

/// <summary>Up to 5 per-arg editors (combo for enum args, text box otherwise) that recombine into an int[5] on demand.</summary>
public sealed class ArgEditors
{
    private readonly ComboBox?[] _combos = new ComboBox?[5];
    private readonly TextBox?[] _boxes = new TextBox?[5];
    private readonly FlagChecks?[] _flags = new FlagChecks?[5];
    public void SetCombo(int i, ComboBox c) => _combos[i] = c;
    public void SetBox(int i, TextBox b) => _boxes[i] = b;
    public void SetFlags(int i, FlagChecks flags) => _flags[i] = flags;

    // Reads the edited args, falling back to the prior value for any arg without an editor.
    public int[] Read(int[] fallback)
    {
        var result = (int[])fallback.Clone();
        for (int i = 0; i < 5; i++)
        {
            if (_combos[i]?.SelectedItem is CatalogItem ci) result[i] = ci.Number;
            else if (_flags[i] is { } flags) result[i] = flags.Value;
            else if (_boxes[i] is { } box && int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) result[i] = v;
        }
        return result;
    }
}

public sealed class UniversalFieldEditors
{
    private readonly List<(UniversalFieldEditorValue Value, TextBox? Box, ComboBox? Combo, ComboBox? StringCombo, FlagChecks? Flags)> _editors = new();

    public void AddBox(UniversalFieldEditorValue value, TextBox box) => _editors.Add((value, box, null, null, null));

    public void AddCombo(UniversalFieldEditorValue value, ComboBox combo) => _editors.Add((value, null, combo, null, null));

    public void AddStringCombo(UniversalFieldEditorValue value, ComboBox combo) => _editors.Add((value, null, null, combo, null));

    public void AddFlags(UniversalFieldEditorValue value, FlagChecks flags) => _editors.Add((value, null, null, null, flags));

    public void Apply(Dictionary<string, object> fields)
    {
        foreach (var editor in _editors)
        {
            var handler = UniversalFieldEditorValues.CreateHandlerForInput(editor.Value);
            if (editor.Combo != null)
            {
                handler.SetValue(ComboNumber(editor.Combo, 0));
                fields[editor.Value.Field.Name] = handler.GetValue();
            }
            else if (editor.StringCombo != null)
            {
                handler.SetValue(ComboText(editor.StringCombo, ""));
                fields[editor.Value.Field.Name] = handler.GetValue();
            }
            else if (editor.Flags != null)
            {
                handler.SetValue(editor.Flags.Value);
                fields[editor.Value.Field.Name] = handler.GetValue();
            }
            else if (editor.Box != null)
            {
                handler.SetValue(editor.Box.Text);
                fields[editor.Value.Field.Name] = handler.GetValue();
            }
        }
    }

    private static int ComboNumber(ComboBox combo, int fallback)
        => combo.SelectedItem is CatalogItem ci ? ci.Number : fallback;

    private static string ComboText(ComboBox combo, string fallback)
        => combo.SelectedItem is CatalogTextItem ci ? ci.Value : fallback;
}

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

        Content = new ScrollViewer
        {
            Content = _rows,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 760,
        };
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

    protected TextBox AddFieldWithButton(
        string label,
        string value,
        string buttonText,
        Func<TextBox, System.Threading.Tasks.Task> action)
        => AddFieldWithButtons(label, value, [(buttonText, action)]);

    protected TextBox AddFieldWithButtons(
        string label,
        string value,
        IReadOnlyList<(string Text, Func<TextBox, System.Threading.Tasks.Task> Action)> buttons)
    {
        var columns = "130,*" + string.Concat(Enumerable.Repeat(",Auto", buttons.Count));
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(columns) };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        var box = new TextBox { Text = value };
        Grid.SetColumn(box, 1);
        grid.Children.Add(box);

        for (int i = 0; i < buttons.Count; i++)
        {
            var descriptor = buttons[i];
            var button = new Button { Content = descriptor.Text, MinWidth = 34, Margin = new Avalonia.Thickness(4, 0, 0, 0) };
            button.Click += async (_, _) => await descriptor.Action(box);
            Grid.SetColumn(button, i + 2);
            grid.Children.Add(button);
        }

        _rows.Children.Insert(_rows.Children.Count - 1, grid);
        return box;
    }

    // Adds a labeled text box with a texture/flat browser button.
    protected TextBox AddTextureField(string label, string value, ResourceManager resources, bool flats, string title)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*,Auto") };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        var box = new TextBox { Text = value };
        Grid.SetColumn(box, 1);
        grid.Children.Add(box);
        var browse = new Button { Content = "...", MinWidth = 34, Margin = new Avalonia.Thickness(4, 0, 0, 0) };
        browse.Click += async (_, _) =>
        {
            var dlg = new TextureBrowserDialog(resources, flats) { Title = title };
            if (await dlg.ShowDialog<bool>(this) && dlg.Selected is { } selected) box.Text = selected;
        };
        Grid.SetColumn(browse, 2);
        grid.Children.Add(browse);
        _rows.Children.Insert(_rows.Children.Count - 1, grid);
        return box;
    }

    // Adds a labeled dropdown of catalog items, sorted by number, preselecting the current value
    // (and preserving an unknown current value as a synthetic first entry).
    protected ComboBox AddCombo(string label, IEnumerable<CatalogItem> items, int current)
    {
        var list = items.OrderBy(i => i.Number).ToList();
        if (!list.Exists(i => i.Number == current))
            list.Insert(0, new CatalogItem(current, $"{current} - (current)"));

        var combo = new ComboBox { ItemsSource = list, HorizontalAlignment = HorizontalAlignment.Stretch };
        combo.SelectedItem = list.Find(i => i.Number == current);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*") };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(combo, 1);
        grid.Children.Add(combo);
        _rows.Children.Insert(_rows.Children.Count - 1, grid);
        return combo;
    }

    // Adds a labeled dropdown of text catalog items, preserving an unknown current value.
    protected ComboBox AddStringCombo(string label, IEnumerable<CatalogTextItem> items, string current)
    {
        var list = items.OrderBy(i => i.Value, StringComparer.OrdinalIgnoreCase).ToList();
        if (!string.IsNullOrWhiteSpace(current)
            && !list.Exists(i => string.Equals(i.Value, current, StringComparison.OrdinalIgnoreCase)))
            list.Insert(0, new CatalogTextItem(current, $"{current} - (current)"));

        var combo = new ComboBox { ItemsSource = list, HorizontalAlignment = HorizontalAlignment.Stretch };
        combo.SelectedItem = list.Find(i => string.Equals(i.Value, current, StringComparison.OrdinalIgnoreCase));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*") };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(combo, 1);
        grid.Children.Add(combo);
        _rows.Children.Insert(_rows.Children.Count - 1, grid);
        return combo;
    }

    // Like AddCombo, but with a "..." button opening the categorized browser; picking there updates the combo.
    protected ComboBox AddComboWithBrowse(string label, IEnumerable<CatalogItem> items, int current,
        string browseTitle, Func<List<BrowseEntry>> entriesFactory)
    {
        var list = items.OrderBy(i => i.Number).ToList();
        if (!list.Exists(i => i.Number == current))
            list.Insert(0, new CatalogItem(current, $"{current} - (current)"));

        var combo = new ComboBox { ItemsSource = list, HorizontalAlignment = HorizontalAlignment.Stretch };
        combo.SelectedItem = list.Find(i => i.Number == current);

        var browse = new Button { Content = "...", MinWidth = 34, Margin = new Avalonia.Thickness(4, 0, 0, 0) };
        browse.Click += async (_, _) =>
        {
            var dlg = new BrowserDialog(browseTitle, entriesFactory(), ComboNumber(combo, current));
            if (await dlg.ShowDialog<bool>(this) && dlg.SelectedNumber is int n)
                SetComboNumber(combo, list, n, dlg.SelectedTitle);
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*,Auto") };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(combo, 1);
        grid.Children.Add(combo);
        Grid.SetColumn(browse, 2);
        grid.Children.Add(browse);
        _rows.Children.Insert(_rows.Children.Count - 1, grid);
        return combo;
    }

    // Selects a number in a combo, inserting a synthetic entry (and refreshing the source) when it is not present.
    private static void SetComboNumber(ComboBox combo, List<CatalogItem> list, int number, string title)
    {
        if (!list.Exists(i => i.Number == number))
        {
            list.Add(new CatalogItem(number, $"{number} - {title}"));
            list.Sort((a, b) => a.Number.CompareTo(b.Number));
            combo.ItemsSource = list.ToList(); // reassign so the ComboBox observes the added entry
        }
        combo.SelectedItem = list.Find(i => i.Number == number);
    }

    // Adds a labeled column of checkboxes, one per flag bit, returning a handle that recombines them on OK.
    protected FlagChecks AddFlagChecks(string label, IReadOnlyDictionary<int, string> defs, int current)
    {
        var fc = new FlagChecks();
        var panel = new StackPanel { Spacing = 1 };
        panel.Children.Add(new TextBlock { Text = label, Margin = new Avalonia.Thickness(0, 2, 0, 2) });
        foreach (int bit in defs.Keys.OrderBy(b => b))
        {
            var cb = new CheckBox { Content = defs[bit], IsChecked = (current & bit) != 0, Padding = new Avalonia.Thickness(4, 0) };
            fc.Add(bit, cb);
            panel.Children.Add(cb);
        }
        _rows.Children.Insert(_rows.Children.Count - 1, panel);
        return fc;
    }

    protected StringFlagChecks AddStringFlagChecks(string label, IEnumerable<string> flags, IReadOnlySet<string> current)
    {
        var checks = new StringFlagChecks();
        var panel = new StackPanel { Spacing = 1 };
        panel.Children.Add(new TextBlock { Text = label, Margin = new Avalonia.Thickness(0, 2, 0, 2) });
        foreach (string flag in flags.Where(flag => !string.IsNullOrWhiteSpace(flag)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(flag => flag, StringComparer.OrdinalIgnoreCase))
        {
            var cb = new CheckBox { Content = flag, IsChecked = current.Contains(flag), Padding = new Avalonia.Thickness(4, 0) };
            checks.Add(flag, cb);
            panel.Children.Add(cb);
        }
        _rows.Children.Insert(_rows.Children.Count - 1, panel);
        return checks;
    }

    // Adds up to 5 labeled arg editors driven by action/thing argument metadata: a dropdown for
    // enum-typed args (value - title) and a text box otherwise. Returns null when no arg is used.
    protected ArgEditors? AddArgEditors(GameConfiguration? config, ArgInfo[] meta, int[] current)
    {
        if (meta.Length == 0 || !Array.Exists(meta, a => a.Used)) return null;
        var editors = new ArgEditors();
        for (int i = 0; i < meta.Length; i++)
        {
            var info = meta[i];
            if (!info.Used) continue;
            string label = $"Arg{i}: {info.Title}";
            var handler = config?.CreateArgumentHandler(info);
            var options = handler != null ? UniversalValueOptions.ForIntegerEditor(handler) : Array.Empty<UniversalValueOption>();
            if (options.Count > 0)
            {
                var items = options.Select(option => new CatalogItem(option.Value, $"{option.Value} - {option.Title}"));
                editors.SetCombo(i, AddCombo(label, items, current[i]));
            }
            else if (handler?.GetType() == typeof(AngleDegreesTypeHandler))
            {
                editors.SetCombo(i, AddCombo(label, AngleDegreeItems(), current[i]));
            }
            else if (handler is AngleByteTypeHandler)
            {
                editors.SetCombo(i, AddCombo(label, AngleByteItems(), current[i]));
            }
            else if (handler is EnumBitsTypeHandler bits && EnumBitDefinitions(bits.Values) is { Count: > 0 } bitDefs)
            {
                editors.SetFlags(i, AddFlagChecks(label, bitDefs, current[i]));
            }
            else if (config != null && handler is ThingTypeHandler)
            {
                editors.SetCombo(i, AddComboWithBrowse(
                    label,
                    config.Things.Values.Select(thing => new CatalogItem(thing.Index, $"{thing.Index} - {thing.Title}")),
                    current[i],
                    "Browse Things",
                    () => CatalogBrowse.Things(config)));
            }
            else if (config != null && handler is LinedefTypeHandler)
            {
                var items = config.LinedefActions.Values
                    .Select(action => new CatalogItem(action.Index, $"{action.Index} - {action.Title}"))
                    .Prepend(new CatalogItem(0, "0 - None"));
                editors.SetCombo(i, AddComboWithBrowse(
                    label,
                    items,
                    current[i],
                    "Browse Linedef Actions",
                    () => CatalogBrowse.LinedefActions(config)));
            }
            else if (config != null && handler is SectorEffectTypeHandler)
            {
                editors.SetCombo(i, AddComboWithBrowse(
                    label,
                    config.SectorEffects.Values.Select(effect => new CatalogItem(effect.Index, $"{effect.Index} - {effect.Title}")),
                    current[i],
                    "Browse Sector Effects",
                    () => CatalogBrowse.SectorEffects(config)));
            }
            else
            {
                editors.SetBox(i, AddField(label, current[i].ToString(CultureInfo.InvariantCulture)));
            }
        }
        return editors;
    }

    protected UniversalFieldEditors? AddUniversalFieldEditors(
        IEnumerable<UniversalFieldEditorValue> fields,
        out IReadOnlyList<UniversalFieldEditorValue> editorFields,
        GameConfiguration? config = null,
        ResourceManager? resources = null)
    {
        var list = fields.ToList();
        editorFields = list;
        if (list.Count == 0) return null;

        var editors = new UniversalFieldEditors();
        foreach (var item in list)
        {
            var handler = UniversalFieldEditorValues.CreateHandler(item);
            var options = UniversalValueOptions.ForIntegerEditor(handler);
            if (options.Count > 0)
            {
                var combo = AddCombo(
                    item.Field.Name,
                    options.Select(option => new CatalogItem(option.Value, $"{option.Value} - {option.Title}")),
                    handler.GetIntValue());
                editors.AddCombo(item, combo);
            }
            else if (handler.GetType() == typeof(AngleDegreesTypeHandler))
            {
                editors.AddCombo(item, AddCombo(item.Field.Name, AngleDegreeItems(), handler.GetIntValue()));
            }
            else if (handler is AngleByteTypeHandler)
            {
                editors.AddCombo(item, AddCombo(item.Field.Name, AngleByteItems(), handler.GetIntValue()));
            }
            else if (handler is EnumBitsTypeHandler bits && EnumBitDefinitions(bits.Values) is { Count: > 0 } bitDefs)
            {
                editors.AddFlags(item, AddFlagChecks(item.Field.Name, bitDefs, handler.GetIntValue()));
            }
            else if (config != null && handler is ThingTypeHandler)
            {
                var combo = AddComboWithBrowse(
                    item.Field.Name,
                    config.Things.Values.Select(thing => new CatalogItem(thing.Index, $"{thing.Index} - {thing.Title}")),
                    handler.GetIntValue(),
                    "Browse Things",
                    () => CatalogBrowse.Things(config));
                editors.AddCombo(item, combo);
            }
            else if (handler is ThingClassTypeHandler)
            {
                var classOptions = UniversalStringOptions.ForThingClassEditor(handler, config);
                if (classOptions.Count > 0)
                {
                    var combo = AddStringCombo(
                        item.Field.Name,
                        classOptions.Select(option => new CatalogTextItem(option.Value, $"{option.Value} - {option.Title}")),
                        handler.GetStringValue());
                    editors.AddStringCombo(item, combo);
                }
                else
                {
                    editors.AddBox(item, AddField(item.Field.Name, handler.GetStringValue()));
                }
            }
            else if (config != null && handler is LinedefTypeHandler)
            {
                var items = config.LinedefActions.Values
                    .Select(action => new CatalogItem(action.Index, $"{action.Index} - {action.Title}"))
                    .Prepend(new CatalogItem(0, "0 - None"));
                var combo = AddComboWithBrowse(
                    item.Field.Name,
                    items,
                    handler.GetIntValue(),
                    "Browse Linedef Actions",
                    () => CatalogBrowse.LinedefActions(config));
                editors.AddCombo(item, combo);
            }
            else if (config != null && handler is SectorEffectTypeHandler)
            {
                var combo = AddComboWithBrowse(
                    item.Field.Name,
                    config.SectorEffects.Values.Select(effect => new CatalogItem(effect.Index, $"{effect.Index} - {effect.Title}")),
                    handler.GetIntValue(),
                    "Browse Sector Effects",
                    () => CatalogBrowse.SectorEffects(config));
                editors.AddCombo(item, combo);
            }
            else if (resources != null && handler is ImageNameTypeHandler imageName)
            {
                var title = imageName.BrowseFlats ? "Browse Flats" : "Browse Textures";
                editors.AddBox(item, AddTextureField(item.Field.Name, handler.GetStringValue(), resources, imageName.BrowseFlats, title));
            }
            else
            {
                editors.AddBox(item, AddField(item.Field.Name, handler.GetStringValue()));
            }
        }

        return editors;
    }

    private static IEnumerable<CatalogItem> AngleDegreeItems()
        => new[]
        {
            new CatalogItem(0, "0 - East"),
            new CatalogItem(45, "45 - Northeast"),
            new CatalogItem(90, "90 - North"),
            new CatalogItem(135, "135 - Northwest"),
            new CatalogItem(180, "180 - West"),
            new CatalogItem(225, "225 - Southwest"),
            new CatalogItem(270, "270 - South"),
            new CatalogItem(315, "315 - Southeast"),
        };

    private static IEnumerable<CatalogItem> AngleByteItems()
        => new[]
        {
            new CatalogItem(0, "0 - East"),
            new CatalogItem(32, "32 - Northeast"),
            new CatalogItem(64, "64 - North"),
            new CatalogItem(96, "96 - Northwest"),
            new CatalogItem(128, "128 - West"),
            new CatalogItem(160, "160 - Southwest"),
            new CatalogItem(192, "192 - South"),
            new CatalogItem(224, "224 - Southeast"),
        };

    private static IReadOnlyDictionary<int, string> EnumBitDefinitions(EnumListInfo values)
    {
        var result = new SortedDictionary<int, string>();
        foreach (var item in values.Items)
            if (int.TryParse(item.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bit))
                result[bit] = item.Title;
        return result;
    }

    // Adds a labeled multi-line text area (e.g. for custom UDMF fields), inserted before the buttons row.
    protected TextBox AddTextArea(string label, string value)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock { Text = label, Margin = new Avalonia.Thickness(0, 4, 0, 0) });
        var box = new TextBox
        {
            Text = value, AcceptsReturn = true, MinHeight = 64, MaxHeight = 140,
            TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("monospace"),
        };
        panel.Children.Add(box);
        _rows.Children.Insert(_rows.Children.Count - 1, panel);
        return box;
    }

    // Adds a single labeled checkbox (inserted before the buttons row).
    protected CheckBox AddCheckBox(string label, bool initial)
    {
        var cb = new CheckBox { Content = label, IsChecked = initial };
        _rows.Children.Insert(_rows.Children.Count - 1, cb);
        return cb;
    }

    protected static int ParseInt(TextBox box, int fallback)
        => int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    protected static double ParseDouble(TextBox box, double fallback)
        => double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;

    protected static int ComboNumber(ComboBox combo, int fallback)
        => combo.SelectedItem is CatalogItem ci ? ci.Number : fallback;

    protected abstract void OnConfirm();
}

public sealed class ThingEditDialog : PropertyDialog
{
    private readonly ComboBox? _typeCombo;
    private readonly TextBox? _typeBox;
    private readonly TextBox _x, _y, _angle, _height, _tag, _action;
    private readonly FlagChecks? _flagChecks;
    private readonly ArgEditors? _args;
    private readonly UniversalFieldEditors? _fieldEditors;
    private readonly TextBox _custom;

    public int ResultType, ResultAngle, ResultTag, ResultAction, ResultFlags;
    public double ResultX, ResultY, ResultHeight;
    public int[] ResultArgs;
    public Dictionary<string, object> ResultFields = new();

    public ThingEditDialog(Thing t, GameConfiguration? config, ResourceManager? resources = null) : base("Edit Thing")
    {
        ResultArgs = (int[])t.Args.Clone();
        if (config != null && config.Things.Count > 0)
            _typeCombo = AddComboWithBrowse("Type",
                config.Things.Values.Select(x => new CatalogItem(x.Index, $"{x.Index} - {x.Title}")), t.Type,
                "Browse Things", () => CatalogBrowse.Things(config));
        else
            _typeBox = AddField("Type", t.Type.ToString(CultureInfo.InvariantCulture));

        _x = AddField("X", t.Position.x.ToString("0.###", CultureInfo.InvariantCulture));
        _y = AddField("Y", t.Position.y.ToString("0.###", CultureInfo.InvariantCulture));
        _height = AddField("Height (Z)", t.Height.ToString("0.###", CultureInfo.InvariantCulture));
        _angle = AddField("Angle", t.Angle.ToString(CultureInfo.InvariantCulture));
        _tag = AddField("Tag (TID)", t.Tag.ToString(CultureInfo.InvariantCulture));
        _action = AddField("Action", t.Action.ToString(CultureInfo.InvariantCulture));
        _args = AddArgEditors(config, config?.GetThing(t.Type)?.Args ?? Array.Empty<ArgInfo>(), t.Args);
        _flagChecks = (config != null && config.ThingFlags.Count > 0) ? AddFlagChecks("Flags", config.ThingFlags, t.Flags) : null;
        ResultFlags = t.Flags; // preserved when no flag config
        var configuredFields = UniversalFieldEditorValues.ForElement(
            config,
            "thing",
            t.Fields,
            config?.GetThing(t.Type)?.AddUniversalFields);
        _fieldEditors = AddUniversalFieldEditors(configuredFields, out var editorFields, config, resources);
        _custom = AddTextArea("Custom UDMF fields",
            UdmfFields.Format(UniversalFieldEditorValues.WithoutConfiguredFields(t.Fields, editorFields)));
    }

    protected override void OnConfirm()
    {
        ResultType = _typeCombo != null ? ComboNumber(_typeCombo, 0) : ParseInt(_typeBox!, 0);
        ResultAngle = ParseInt(_angle, 0);
        ResultTag = ParseInt(_tag, 0);
        ResultAction = ParseInt(_action, 0);
        if (_flagChecks != null) ResultFlags = _flagChecks.Value;
        if (_args != null) ResultArgs = _args.Read(ResultArgs);
        ResultX = double.TryParse(_x.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : 0;
        ResultY = double.TryParse(_y.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ? y : 0;
        ResultHeight = double.TryParse(_height.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var h) ? h : 0;
        ResultFields = UdmfFields.Parse(_custom.Text);
        _fieldEditors?.Apply(ResultFields);
    }
}

public sealed class VertexEditDialog : PropertyDialog
{
    private readonly TextBox _x, _y;
    private readonly double _originalX, _originalY;
    private readonly UniversalFieldEditors? _fieldEditors;
    private readonly TextBox _custom;

    public double ResultX, ResultY;
    public Dictionary<string, object> ResultFields = new();

    public VertexEditDialog(Vertex vertex, GameConfiguration? config)
        : base("Edit Vertex")
    {
        _originalX = vertex.Position.x;
        _originalY = vertex.Position.y;
        _x = AddField("X", vertex.Position.x.ToString("0.###", CultureInfo.InvariantCulture));
        _y = AddField("Y", vertex.Position.y.ToString("0.###", CultureInfo.InvariantCulture));
        var configuredFields = UniversalFieldEditorValues.ForElement(config, "vertex", vertex.Fields);
        _fieldEditors = AddUniversalFieldEditors(configuredFields, out var editorFields, config);
        _custom = AddTextArea("Custom UDMF fields",
            UdmfFields.Format(UniversalFieldEditorValues.WithoutConfiguredFields(vertex.Fields, editorFields)));
    }

    protected override void OnConfirm()
    {
        ResultX = double.TryParse(_x.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : _originalX;
        ResultY = double.TryParse(_y.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ? y : _originalY;
        ResultFields = UdmfFields.Parse(_custom.Text);
        _fieldEditors?.Apply(ResultFields);
    }
}

public sealed class SidedefEditDialog : PropertyDialog
{
    private readonly TextBox _high, _mid, _low, _offsetX, _offsetY;
    private readonly int _originalOffsetX, _originalOffsetY;
    private readonly StringFlagChecks? _sidedefFlags;
    private readonly UniversalFieldEditors? _fieldEditors;
    private readonly TextBox _custom;

    public string ResultHighTexture = "-", ResultMidTexture = "-", ResultLowTexture = "-";
    public int ResultOffsetX, ResultOffsetY;
    public HashSet<string>? ResultSidedefFlags;
    public Dictionary<string, object> ResultFields = new();

    public SidedefEditDialog(Sidedef side, GameConfiguration? config, ResourceManager? resources = null)
        : base("Edit Sidedef")
    {
        _high = AddSidedefTextureField("Upper texture", side.HighTexture, resources);
        _mid = AddSidedefTextureField("Middle texture", side.MidTexture, resources);
        _low = AddSidedefTextureField("Lower texture", side.LowTexture, resources);
        _originalOffsetX = side.OffsetX;
        _originalOffsetY = side.OffsetY;
        _offsetX = AddField("Offset X", side.OffsetX.ToString(CultureInfo.InvariantCulture));
        _offsetY = AddField("Offset Y", side.OffsetY.ToString(CultureInfo.InvariantCulture));

        var sideFlags = UdmfFlagChoices.KnownSidedefFlags(config, side);
        if (sideFlags.Count > 0)
            _sidedefFlags = AddStringFlagChecks("Sidedef flags", sideFlags, side.UdmfFlags);

        var configuredFields = UniversalFieldEditorValues.ForElement(config, "sidedef", side.Fields);
        _fieldEditors = AddUniversalFieldEditors(configuredFields, out var editorFields, config, resources);
        _custom = AddTextArea("Custom UDMF fields",
            UdmfFields.Format(UniversalFieldEditorValues.WithoutConfiguredFields(side.Fields, editorFields)));
    }

    private TextBox AddSidedefTextureField(string label, string value, ResourceManager? resources)
        => resources != null
            ? AddTextureField(label, value, resources, flats: false, "Browse Textures")
            : AddField(label, value);

    protected override void OnConfirm()
    {
        ResultHighTexture = TextureValue(_high);
        ResultMidTexture = TextureValue(_mid);
        ResultLowTexture = TextureValue(_low);
        ResultOffsetX = ParseInt(_offsetX, _originalOffsetX);
        ResultOffsetY = ParseInt(_offsetY, _originalOffsetY);
        ResultSidedefFlags = _sidedefFlags?.Value;
        ResultFields = UdmfFields.Parse(_custom.Text);
        _fieldEditors?.Apply(ResultFields);
    }

    private static string TextureValue(TextBox box)
        => string.IsNullOrWhiteSpace(box.Text) ? "-" : box.Text!;
}

public sealed class LinedefEditDialog : PropertyDialog
{
    private readonly ComboBox? _actionCombo;
    private readonly TextBox? _actionBox;
    private readonly TextBox _tag;
    private readonly TextBox? _frontHighTex, _frontMidTex, _frontLowTex;
    private readonly TextBox? _backHighTex, _backMidTex, _backLowTex;
    private readonly FlagChecks? _flagChecks;
    private readonly StringFlagChecks? _frontSidedefFlags, _backSidedefFlags;
    private readonly TextBox? _flagsBox;
    private readonly ArgEditors? _args;
    private readonly UniversalFieldEditors? _fieldEditors;
    private readonly TextBox _custom;

    public int ResultAction, ResultTag, ResultFlags;
    public int[] ResultArgs;
    public string? ResultFrontHighTex, ResultFrontMidTex, ResultFrontLowTex;
    public string? ResultBackHighTex, ResultBackMidTex, ResultBackLowTex;
    public HashSet<string>? ResultFrontSidedefFlags, ResultBackSidedefFlags;
    public Dictionary<string, object> ResultFields = new();

    public LinedefEditDialog(Linedef l, GameConfiguration? config, ResourceManager? resources = null) : base("Edit Linedef")
    {
        ResultArgs = (int[])l.Args.Clone();
        if (config != null && config.LinedefActions.Count > 0)
        {
            var items = config.LinedefActions.Values.Select(a => new CatalogItem(a.Index, $"{a.Index} - {a.Title}"))
                .Prepend(new CatalogItem(0, "0 - None"));
            _actionCombo = AddComboWithBrowse("Action", items, l.Action, "Browse Linedef Actions",
                () => CatalogBrowse.LinedefActions(config));
        }
        else _actionBox = AddField("Action", l.Action.ToString(CultureInfo.InvariantCulture));

        _tag = AddField("Tag", l.Tag.ToString(CultureInfo.InvariantCulture));
        if (l.Front is { } front)
        {
            _frontHighTex = AddLinedefTextureField("Front upper texture", front.HighTexture, resources);
            _frontMidTex = AddLinedefTextureField("Front middle texture", front.MidTexture, resources);
            _frontLowTex = AddLinedefTextureField("Front lower texture", front.LowTexture, resources);
            var frontFlags = UdmfFlagChoices.KnownSidedefFlags(config, front);
            if (frontFlags.Count > 0)
                _frontSidedefFlags = AddStringFlagChecks("Front sidedef flags", frontFlags, front.UdmfFlags);
        }
        if (l.Back is { } back)
        {
            _backHighTex = AddLinedefTextureField("Back upper texture", back.HighTexture, resources);
            _backMidTex = AddLinedefTextureField("Back middle texture", back.MidTexture, resources);
            _backLowTex = AddLinedefTextureField("Back lower texture", back.LowTexture, resources);
            var backFlags = UdmfFlagChoices.KnownSidedefFlags(config, back);
            if (backFlags.Count > 0)
                _backSidedefFlags = AddStringFlagChecks("Back sidedef flags", backFlags, back.UdmfFlags);
        }

        _args = AddArgEditors(config, config?.GetLinedefAction(l.Action)?.Args ?? Array.Empty<ArgInfo>(), l.Args);

        if (config != null && config.LinedefFlags.Count > 0)
            _flagChecks = AddFlagChecks("Flags", config.LinedefFlags, l.Flags);
        else
            _flagsBox = AddField("Flags (int)", l.Flags.ToString(CultureInfo.InvariantCulture));
        var configuredFields = UniversalFieldEditorValues.ForElement(config, "linedef", l.Fields);
        _fieldEditors = AddUniversalFieldEditors(configuredFields, out var editorFields, config, resources);
        _custom = AddTextArea("Custom UDMF fields",
            UdmfFields.Format(UniversalFieldEditorValues.WithoutConfiguredFields(l.Fields, editorFields)));
    }

    private TextBox AddLinedefTextureField(string label, string value, ResourceManager? resources)
        => resources != null
            ? AddTextureField(label, value, resources, flats: false, "Browse Textures")
            : AddField(label, value);

    protected override void OnConfirm()
    {
        ResultAction = _actionCombo != null ? ComboNumber(_actionCombo, 0) : ParseInt(_actionBox!, 0);
        ResultTag = ParseInt(_tag, 0);
        ResultFrontHighTex = TextureValue(_frontHighTex);
        ResultFrontMidTex = TextureValue(_frontMidTex);
        ResultFrontLowTex = TextureValue(_frontLowTex);
        ResultBackHighTex = TextureValue(_backHighTex);
        ResultBackMidTex = TextureValue(_backMidTex);
        ResultBackLowTex = TextureValue(_backLowTex);
        ResultFrontSidedefFlags = _frontSidedefFlags?.Value;
        ResultBackSidedefFlags = _backSidedefFlags?.Value;
        ResultFlags = _flagChecks != null ? _flagChecks.Value : ParseInt(_flagsBox!, 0);
        if (_args != null) ResultArgs = _args.Read(ResultArgs);
        ResultFields = UdmfFields.Parse(_custom.Text);
        _fieldEditors?.Apply(ResultFields);
    }

    private static string? TextureValue(TextBox? box)
        => box is null ? null : string.IsNullOrWhiteSpace(box.Text) ? "-" : box.Text!;
}

public sealed class SectorEditDialog : PropertyDialog
{
    private readonly TextBox _floor, _ceil, _floorTex, _ceilTex, _bright, _tag;
    private readonly ComboBox? _specialCombo;
    private readonly TextBox? _specialBox;
    private readonly UniversalFieldEditors? _fieldEditors;
    private readonly TextBox _custom;

    public int ResultFloor, ResultCeil, ResultBright, ResultSpecial, ResultTag;
    public string ResultFloorTex = "-", ResultCeilTex = "-";
    public Dictionary<string, object> ResultFields = new();

    public SectorEditDialog(Sector s, GameConfiguration? config, ResourceManager? resources = null) : base("Edit Sector")
    {
        _floor = AddField("Floor height", s.FloorHeight.ToString(CultureInfo.InvariantCulture));
        _ceil = AddField("Ceiling height", s.CeilHeight.ToString(CultureInfo.InvariantCulture));
        _floorTex = resources != null
            ? AddTextureField("Floor texture", s.FloorTexture, resources, flats: true, "Browse Floor Flat")
            : AddField("Floor texture", s.FloorTexture);
        _ceilTex = resources != null
            ? AddTextureField("Ceiling texture", s.CeilTexture, resources, flats: true, "Browse Ceiling Flat")
            : AddField("Ceiling texture", s.CeilTexture);
        _bright = AddField("Brightness", s.Brightness.ToString(CultureInfo.InvariantCulture));

        if (config != null && config.SectorEffects.Count > 0)
        {
            var items = config.SectorEffects.Values.Select(x => new CatalogItem(x.Index, $"{x.Index} - {x.Title}"));
            _specialCombo = AddComboWithBrowse("Effect", items, s.Special, "Browse Sector Effects",
                () => CatalogBrowse.SectorEffects(config));
        }
        else _specialBox = AddField("Effect", s.Special.ToString(CultureInfo.InvariantCulture));

        _tag = AddField("Tag", s.Tag.ToString(CultureInfo.InvariantCulture));
        var configuredFields = UniversalFieldEditorValues.ForElement(config, "sector", s.Fields);
        _fieldEditors = AddUniversalFieldEditors(configuredFields, out var editorFields, config, resources);
        _custom = AddTextArea("Custom UDMF fields",
            UdmfFields.Format(UniversalFieldEditorValues.WithoutConfiguredFields(s.Fields, editorFields)));
    }

    protected override void OnConfirm()
    {
        ResultFloor = ParseInt(_floor, 0);
        ResultCeil = ParseInt(_ceil, 0);
        ResultBright = ParseInt(_bright, 0);
        ResultSpecial = _specialCombo != null ? ComboNumber(_specialCombo, 0) : ParseInt(_specialBox!, 0);
        ResultTag = ParseInt(_tag, 0);
        ResultFloorTex = string.IsNullOrWhiteSpace(_floorTex.Text) ? "-" : _floorTex.Text!;
        ResultCeilTex = string.IsNullOrWhiteSpace(_ceilTex.Text) ? "-" : _ceilTex.Text!;
        ResultFields = UdmfFields.Parse(_custom.Text);
        _fieldEditors?.Apply(ResultFields);
    }
}

/// <summary>Picks a target map format for a format-converting save (defaults to the current format).</summary>
public sealed class FormatPickerDialog : PropertyDialog
{
    private readonly ComboBox _combo;
    public MapFormat ResultFormat;

    public FormatPickerDialog(MapFormat current)
        : base("Save As Format", $"Current format: {current}. Flags are translated via the loaded game config.")
    {
        ResultFormat = current;
        var items = new[]
        {
            new CatalogItem((int)MapFormat.Doom,  "Doom (binary)"),
            new CatalogItem((int)MapFormat.Hexen, "Hexen (binary)"),
            new CatalogItem((int)MapFormat.Udmf,  "UDMF (text)"),
        };
        _combo = AddCombo("Format", items, (int)current);
    }

    protected override void OnConfirm() => ResultFormat = (MapFormat)ComboNumber(_combo, (int)ResultFormat);
}
