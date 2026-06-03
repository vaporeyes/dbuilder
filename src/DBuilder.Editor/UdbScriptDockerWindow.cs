// ABOUTME: Non-modal Avalonia window for browsing discovered UDBScript scripts.
// ABOUTME: Presents the modeled script docker tree, description, options, and action events.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class UdbScriptDockerWindow : Window
{
    private readonly UdbScriptDirectory _rootDirectory;
    private readonly IReadOnlyDictionary<int, string> _hotkeys;
    private IReadOnlyDictionary<int, UdbScriptInfo?> _slotAssignments;
    private readonly TextBox _filter = new();
    private readonly TreeView _tree = new();
    private readonly TextBox _description = new();
    private readonly ListBox _options = new();
    private readonly Button _run = new();
    private readonly Button _optionsButton = new();
    private readonly Button _reset = new();
    private readonly Button _edit = new();

    public event Action<UdbScriptInfo>? RunRequested;
    public event Action<UdbScriptInfo>? EditRequested;
    public event Action<UdbScriptInfo>? OptionsRequested;
    public event Action<UdbScriptInfo>? ResetOptionsRequested;
    public event Action<UdbScriptInfo, int>? SlotAssignmentRequested;
    public event Action<UdbScriptInfo>? SlotClearedRequested;

    public IReadOnlyList<UdbScriptDockerNode> Nodes { get; private set; } = Array.Empty<UdbScriptDockerNode>();
    public UdbScriptDockerSelection CurrentSelection { get; private set; } = UdbScriptDockerModel.Selection(null);

    public UdbScriptDockerWindow(
        UdbScriptDirectory rootDirectory,
        IReadOnlyDictionary<int, UdbScriptInfo?>? slotAssignments = null,
        IReadOnlyDictionary<int, string>? hotkeys = null)
    {
        _rootDirectory = rootDirectory;
        _slotAssignments = slotAssignments ?? new Dictionary<int, UdbScriptInfo?>();
        _hotkeys = hotkeys ?? new Dictionary<int, string>();

        Title = UdbScriptDockerModel.DockerTitle;
        Width = 760;
        Height = 560;
        MinWidth = 520;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _filter.Watermark = UdbScriptDockerModel.FilterLabel;
        _filter.TextChanged += (_, _) => RebuildTree();
        _tree.SelectionChanged += (_, _) => ApplySelectedNode(_tree.SelectedItem as TreeViewItem);
        _description.IsReadOnly = true;
        _description.AcceptsReturn = true;
        _description.TextWrapping = Avalonia.Media.TextWrapping.Wrap;

        _run.Content = UdbScriptDockerModel.RunButtonText;
        _run.MinWidth = 82;
        _run.Click += (_, _) => InvokeForCurrent(RunRequested);
        _edit.Content = UdbScriptDockerModel.EditMenuText;
        _edit.MinWidth = 82;
        _edit.Click += (_, _) => InvokeForCurrent(EditRequested);
        _optionsButton.Content = UdbScriptDockerModel.OptionsButtonText;
        _optionsButton.MinWidth = 82;
        _optionsButton.Click += (_, _) => InvokeForCurrent(OptionsRequested);
        _reset.Content = UdbScriptDockerModel.ResetButtonText;
        _reset.MinWidth = 82;
        _reset.Click += (_, _) => InvokeForCurrent(ResetOptionsRequested);

        Content = BuildContent();
        RebuildTree();
        ApplySelection(UdbScriptDockerModel.Selection(null));
    }

    public void ApplyCurrentScript(UdbScriptInfo script)
    {
        if (CurrentSelection.CurrentScript is null)
            return;

        ApplySelection(new UdbScriptDockerSelection(script, script.Description, script.Options));
    }

    public void ApplySlotAssignments(IReadOnlyDictionary<int, UdbScriptInfo?> slotAssignments)
    {
        UdbScriptInfo? selected = CurrentSelection.CurrentScript;
        _slotAssignments = slotAssignments;
        RebuildTree();
        if (selected is not null)
            ApplySelection(new UdbScriptDockerSelection(selected, selected.Description, selected.Options));
    }

    private Control BuildContent()
    {
        var left = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 6,
        };
        left.Children.Add(_filter);
        var treeScroll = new ScrollViewer { Content = _tree };
        Grid.SetRow(treeScroll, 1);
        left.Children.Add(treeScroll);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttons.Children.Add(_edit);
        buttons.Children.Add(_optionsButton);
        buttons.Children.Add(_reset);
        buttons.Children.Add(_run);

        var right = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto,*,Auto"),
            RowSpacing = 6,
        };
        right.Children.Add(new TextBlock { Text = UdbScriptDockerModel.DescriptionLabel });
        var descriptionScroll = new ScrollViewer
        {
            Content = _description,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Grid.SetRow(descriptionScroll, 1);
        right.Children.Add(descriptionScroll);
        var optionsLabel = new TextBlock { Text = UdbScriptDockerModel.OptionsLabel };
        Grid.SetRow(optionsLabel, 2);
        right.Children.Add(optionsLabel);
        Grid.SetRow(_options, 3);
        right.Children.Add(_options);
        Grid.SetRow(buttons, 4);
        right.Children.Add(buttons);

        var root = new Grid
        {
            Margin = new Avalonia.Thickness(10),
            ColumnDefinitions = new ColumnDefinitions("*,2*"),
            ColumnSpacing = 10,
        };
        root.Children.Add(left);
        Grid.SetColumn(right, 1);
        root.Children.Add(right);
        return root;
    }

    private void RebuildTree()
    {
        Nodes = UdbScriptDockerModel.BuildTree(_rootDirectory, _filter.Text ?? "", _slotAssignments, _hotkeys);
        _tree.ItemsSource = Nodes.Select(TreeItem).ToArray();
        ApplySelection(UdbScriptDockerModel.Selection(null));
    }

    private TreeViewItem TreeItem(UdbScriptDockerNode node)
    {
        var item = new TreeViewItem
        {
            Header = node.Text,
            IsExpanded = node.Expanded,
            Tag = node,
        };
        item.ItemsSource = node.Children.Select(TreeItem).ToArray();
        if (node.Script is not null)
            item.ContextMenu = BuildScriptContextMenu(node.Script);
        return item;
    }

    private ContextMenu BuildScriptContextMenu(UdbScriptInfo script)
    {
        var items = new List<object>();
        foreach (UdbScriptDockerMenuItem model in UdbScriptDockerModel.FileContextMenuItems())
        {
            if (model.Kind == UdbScriptDockerMenuItemKind.Command && model.Text == UdbScriptDockerModel.EditMenuText)
            {
                var edit = new MenuItem { Header = model.Text };
                edit.Click += (_, _) => EditRequested?.Invoke(script);
                items.Add(edit);
            }
            else if (model.Kind == UdbScriptDockerMenuItemKind.Submenu)
            {
                var slotMenu = new MenuItem { Header = model.Text };
                slotMenu.ItemsSource = model.Children.Select(child => SlotMenuItem(script, child)).ToArray();
                items.Add(slotMenu);
            }
        }

        return new ContextMenu { ItemsSource = items };
    }

    private object SlotMenuItem(UdbScriptInfo script, UdbScriptDockerMenuItem model)
    {
        if (model.Kind == UdbScriptDockerMenuItemKind.Separator)
            return new Separator();

        var item = new MenuItem { Header = model.Text };
        if (model.Kind == UdbScriptDockerMenuItemKind.Command && model.Text == UdbScriptDockerModel.ClearSlotMenuText)
            item.Click += (_, _) => SlotClearedRequested?.Invoke(script);
        else if (model.Kind == UdbScriptDockerMenuItemKind.Slot)
            item.Click += (_, _) => SlotAssignmentRequested?.Invoke(script, model.Slot);
        return item;
    }

    private void ApplySelectedNode(TreeViewItem? item)
    {
        if (item?.Tag is not UdbScriptDockerNode node)
        {
            ApplySelection(UdbScriptDockerModel.Selection(null));
            return;
        }

        ApplySelection(UdbScriptDockerModel.ApplySelection(CurrentSelection, node));
    }

    private void ApplySelection(UdbScriptDockerSelection selection)
    {
        CurrentSelection = selection;
        _description.Text = selection.Description;
        _options.ItemsSource = selection.Options.Select(OptionText).ToArray();
        bool hasScript = selection.CurrentScript is not null;
        _run.IsEnabled = hasScript;
        _edit.IsEnabled = hasScript;
        _optionsButton.IsEnabled = hasScript && selection.Options.Count > 0;
        _reset.IsEnabled = hasScript && selection.Options.Count > 0;
    }

    private static string OptionText(UdbScriptOption option)
        => option.Name + ": " + (option.Value?.ToString() ?? "");

    private void InvokeForCurrent(Action<UdbScriptInfo>? handler)
    {
        if (handler is not null && CurrentSelection.CurrentScript is { } script)
            handler(script);
    }
}
