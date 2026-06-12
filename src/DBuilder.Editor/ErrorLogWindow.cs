// ABOUTME: Non-modal viewer for recent DBuilder error log and crash report files.
// ABOUTME: Gives users an in-editor way to inspect logged workflow failures.

using System.IO;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class ErrorLogWindow : Window
{
    public ErrorLogWindow(bool showErrorsWindow = true, Action<bool>? setShowErrorsWindow = null)
    {
        Title = "Error Log";
        Width = 780;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        string logText = ErrorLog.ReadRecentText();
        string reports = ReportText(ErrorLog.ListReportPaths());
        string text = string.IsNullOrWhiteSpace(logText)
            ? "No error log entries found."
            : logText;
        if (!string.IsNullOrWhiteSpace(reports))
            text += "\n\nRecent report files:\n" + reports;

        var box = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = FontFamily.Parse("Menlo,Consolas,monospace"),
            FontSize = 12,
        };

        var showErrors = new CheckBox
        {
            Content = "Show this window when errors occur",
            IsChecked = showErrorsWindow,
            Margin = new Avalonia.Thickness(0, 0, 0, 8),
        };
        showErrors.IsCheckedChanged += (_, _) => setShowErrorsWindow?.Invoke(showErrors.IsChecked == true);

        var close = new Button { Content = "Close", MinWidth = 80, IsDefault = true, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();

        var root = new DockPanel { Margin = new Avalonia.Thickness(10) };
        DockPanel.SetDock(showErrors, Dock.Top);
        DockPanel.SetDock(close, Dock.Bottom);
        root.Children.Add(showErrors);
        root.Children.Add(close);
        root.Children.Add(new ScrollViewer { Content = box });
        Content = root;
    }

    private static string ReportText(string[] paths)
    {
        if (paths.Length == 0) return "";
        var names = new string[paths.Length];
        for (int i = 0; i < paths.Length; i++) names[i] = Path.GetFileName(paths[i]);
        return string.Join("\n", names);
    }
}
