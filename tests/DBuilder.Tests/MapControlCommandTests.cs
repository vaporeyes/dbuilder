// ABOUTME: Tests MapControl command dispatch metadata without constructing the Avalonia control.
// ABOUTME: Keeps stable 2D and 3D command ids wired to map editing handlers.

using System.Reflection;
using DBuilder.Editor;

namespace DBuilder.Tests;

public sealed class MapControlCommandTests
{
    [Theory]
    [InlineData("map2d.mode-automap", "ToggleAutomapMode")]
    [InlineData("map2d.split-linedefs", "SplitLinedefs")]
    [InlineData("map2d.fit-selected-textures", "FitSelectedTextures")]
    public void MapCommandsAreRoutedThroughMapCommandDispatch(string commandId, string handlerName)
    {
        Type type = typeof(MapControl);
        MethodInfo? dispatcher = type.GetMethod("RunMapCommand", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? handler = type.GetMethod(
            handlerName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);

        Assert.NotNull(dispatcher);
        Assert.NotNull(handler);
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));
        Assert.Contains($"case \"{commandId}\"", body, StringComparison.Ordinal);
        Assert.Contains($"{handlerName}();", body, StringComparison.Ordinal);
    }
}
