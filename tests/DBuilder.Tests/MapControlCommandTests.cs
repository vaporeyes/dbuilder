// ABOUTME: Tests MapControl command dispatch metadata without constructing the Avalonia control.
// ABOUTME: Keeps stable 2D and 3D command ids wired to map editing handlers.

using System.Reflection;
using DBuilder.Editor;

namespace DBuilder.Tests;

public sealed class MapControlCommandTests
{
    [Theory]
    [InlineData("STARTAN3", 1, "applied texture STARTAN3 to 1 surface")]
    [InlineData("BRICK1", 2, "applied texture BRICK1 to 2 surfaces")]
    public void TextureApplied3DStatusTextFormatsSingularAndPluralSurfaceCounts(string textureName, int surfaceCount, string expected)
        => Assert.Equal(expected, MapControl.TextureApplied3DStatusText(textureName, surfaceCount));

    [Theory]
    [InlineData(1, "1 surface selected")]
    [InlineData(2, "2 surfaces selected")]
    public void SurfaceSelection3DStatusTextFormatsSingularAndPluralSurfaceCounts(int surfaceCount, string expected)
        => Assert.Equal(expected, MapControl.SurfaceSelection3DStatusText(surfaceCount));

    [Theory]
    [InlineData("map2d.mode-automap", "ToggleAutomapMode")]
    [InlineData("map2d.split-linedefs", "SplitLinedefs")]
    [InlineData("map2d.fit-selected-textures", "FitSelectedTextures")]
    [InlineData("map2d.3dfloor.select-control-sector", "SelectThreeDFloorControlSectors")]
    [InlineData("map2d.3dfloor.relocate-control-sectors", "RelocateThreeDFloorControlSectors")]
    [InlineData("map2d.3dfloor.duplicate-geometry", "DuplicateThreeDFloorGeometry")]
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

    [Fact]
    public void RelocateThreeDFloorControlSectorsUsesInjectedAreaSettings()
    {
        string body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DBuilder.Editor/MapControl.cs"));

        Assert.Contains("ThreeDFloors.RelocateManagedControlSectors(_map, ThreeDFloorControlSectorAreaSettings)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ThreeDFloors.RelocateManagedControlSectors(_map, new ThreeDFloorControlSectorAreaSettings())", body, StringComparison.Ordinal);
    }
}
