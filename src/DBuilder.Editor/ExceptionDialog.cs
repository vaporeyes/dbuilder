// ABOUTME: Modal dialog that presents unexpected editor exceptions and the saved crash report path.
// ABOUTME: Gives users readable failure details when Avalonia reports an unhandled UI-thread exception.

using System;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class ExceptionDialog : Window
{
    public ExceptionDialog(Exception exception)
    {
        Title = "Unexpected Error";
        Width = 720;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        string? reportPath = WriteReport(exception);
        var root = new DockPanel { Margin = new Avalonia.Thickness(14) };

        var header = new StackPanel { Spacing = 6 };
        header.Children.Add(new TextBlock
        {
            Text = "DBuilder ran into an unexpected error.",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.Salmon,
        });
        header.Children.Add(new TextBlock
        {
            Text = reportPath == null ? "The crash report could not be written." : $"Crash report: {reportPath}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Khaki,
        });
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var close = new Button
        {
            Content = "Close",
            MinWidth = 80,
            IsCancel = true,
            IsDefault = true,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 10, 0, 0),
        };
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Bottom);
        root.Children.Add(close);

        root.Children.Add(new TextBox
        {
            Text = exception.ToString(),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("monospace"),
            Margin = new Avalonia.Thickness(0, 10, 0, 0),
        });

        Content = root;
    }

    private static string? WriteReport(Exception exception)
    {
        try
        {
            string settingsDir = Path.GetDirectoryName(Settings.DefaultPath) ?? Environment.CurrentDirectory;
            Directory.CreateDirectory(settingsDir);
            string path = Path.Combine(settingsDir, "DBuilderCrash.txt");
            File.WriteAllText(path, ReportText(exception));
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static string ReportText(Exception exception)
    {
        var text = new StringBuilder();
        text.AppendLine("***********SYSTEM INFO***********");
        text.AppendLine($"OS: {Environment.OSVersion}");
        text.AppendLine($"Runtime: {Environment.Version}");
        text.AppendLine($"Process: {(Environment.Is64BitProcess ? "x64" : "x86")}");
        text.AppendLine();
        text.AppendLine("********EXCEPTION DETAILS********");
        text.AppendLine(exception.ToString());
        return text.ToString();
    }
}
