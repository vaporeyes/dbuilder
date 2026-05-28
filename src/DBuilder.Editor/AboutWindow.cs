// ABOUTME: Modal About window showing app identity, version and runtime details.
// ABOUTME: Provides the Help menu dialog equivalent for DBuilder's Avalonia editor shell.

using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DBuilder.Editor;

public sealed class AboutWindow : Window
{
    public AboutWindow()
    {
        Title = "About DBuilder";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var stack = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = "DBuilder",
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.LightSkyBlue,
        });
        stack.Children.Add(new TextBlock
        {
            Text = "A cross-platform Doom map editor built with Avalonia and Silk.NET.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0xd0, 0xd8, 0xe0)),
        });

        stack.Children.Add(Field("Version", VersionText()));
        stack.Children.Add(Field("Runtime", Environment.Version.ToString()));
        stack.Children.Add(Field("Platform", OperatingSystem.IsMacOS() ? "macOS" : Environment.OSVersion.Platform.ToString()));

        var close = new Button
        {
            Content = "Close",
            MinWidth = 80,
            IsCancel = true,
            IsDefault = true,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 8, 0, 0),
        };
        close.Click += (_, _) => Close();
        stack.Children.Add(close);

        Content = stack;
    }

    private static Grid Field(string label, string value)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("90,*") };
        grid.Children.Add(new TextBlock { Text = label, Foreground = Brushes.Khaki });
        var text = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private static string VersionText()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "(unknown)";
    }
}
