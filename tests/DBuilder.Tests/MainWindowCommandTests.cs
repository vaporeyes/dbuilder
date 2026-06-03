// ABOUTME: Tests MainWindow command dispatch metadata without constructing the Avalonia window.
// ABOUTME: Keeps stable window command ids wired to editor handlers as menu actions move into the catalog.

using System.Reflection;
using DBuilder.Editor;

namespace DBuilder.Tests;

public sealed class MainWindowCommandTests
{
    [Theory]
    [InlineData("window.shortcuts", "OnShortcuts")]
    [InlineData("window.about", "OnAbout")]
    [InlineData("window.select-all", "OnSelectAll")]
    [InlineData("window.invert-selection", "OnInvertSelection")]
    [InlineData("window.select-none", "OnSelectNone")]
    public void MenuCommandsAreRoutedThroughWindowCommandDispatch(string commandId, string handlerName)
    {
        Type type = typeof(MainWindow);
        MethodInfo? dispatcher = type.GetMethod("RunWindowCommand", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? handler = type.GetMethod(handlerName, BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(dispatcher);
        Assert.NotNull(handler);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MainWindow.axaml.cs"));
        Assert.Contains($"case \"{commandId}\"", body, StringComparison.Ordinal);
        Assert.Contains($"{handlerName}(this, new RoutedEventArgs())", body, StringComparison.Ordinal);
    }
}
