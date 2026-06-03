// ABOUTME: Non-modal undo/redo history panel backed by the UDB-style timeline model.
// ABOUTME: Lets the host refresh history rows and execute multi-level undo or redo operations.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class UndoRedoPanelWindow : Window
{
    private readonly ListBox _list = new();
    private readonly TextBlock _header = new();
    private bool _syncing;

    public event Action<UndoRedoPanelOperation>? OperationRequested;

    public UndoRedoPanelWindow(UndoRedoPanelState state)
    {
        Title = "Undo / Redo";
        Width = 360;
        Height = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _header.Margin = new Avalonia.Thickness(8, 8, 8, 6);
        _header.Foreground = Brushes.LightSkyBlue;
        _header.TextWrapping = TextWrapping.Wrap;

        _list.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            if (_list.SelectedIndex < 0) return;
            UndoRedoPanelState state = CurrentState;
            UndoRedoPanelOperation operation = state.OperationForSelection(_list.SelectedIndex);
            if (operation.Kind != UndoRedoPanelOperationKind.None)
                OperationRequested?.Invoke(operation);
        };

        var root = new DockPanel();
        DockPanel.SetDock(_header, Dock.Top);
        root.Children.Add(_header);
        root.Children.Add(new ScrollViewer { Content = _list });
        Content = root;

        SetState(state);
    }

    public UndoRedoPanelState CurrentState { get; private set; } = new(Array.Empty<UndoRedoPanelItem>(), -1, 0, 0);

    public void SetState(UndoRedoPanelState state)
    {
        CurrentState = state;
        _header.Text = state.HeaderText;

        var rows = new List<ListBoxItem>();
        foreach (UndoRedoPanelItem item in state.Items)
            rows.Add(new ListBoxItem { Content = FormatItem(item), IsEnabled = item.Kind != UndoRedoPanelItemKind.Elided });

        _syncing = true;
        _list.ItemsSource = rows;
        _list.SelectedIndex = state.CurrentSelection;
        _syncing = false;
    }

    private static TextBlock FormatItem(UndoRedoPanelItem item)
    {
        string marker = item.Kind switch
        {
            UndoRedoPanelItemKind.Begin => "Begin",
            UndoRedoPanelItemKind.Undo => "Undo",
            UndoRedoPanelItemKind.Current => "Current",
            UndoRedoPanelItemKind.Redo => "Redo",
            _ => "",
        };
        return new TextBlock
        {
            Text = string.IsNullOrEmpty(marker) ? item.Description : $"{marker}: {item.Description}",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = item.Kind == UndoRedoPanelItemKind.Current ? FontWeight.SemiBold : FontWeight.Normal,
        };
    }
}
