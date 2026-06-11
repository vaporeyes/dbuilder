// ABOUTME: Verifies UDB render mode enum names and ordering for rendering namespace compatibility.
// ABOUTME: Covers ModelRenderMode, LightRenderMode, and ThingRenderMode from Core/Rendering/RenderModeEnums.cs.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class RenderModeEnumTests
{
    [Fact]
    public void ModelRenderModeValuesMatchUdbOrdering()
    {
        Assert.Equal(0, (int)ModelRenderMode.NONE);
        Assert.Equal(1, (int)ModelRenderMode.SELECTION);
        Assert.Equal(2, (int)ModelRenderMode.ACTIVE_THINGS_FILTER);
        Assert.Equal(3, (int)ModelRenderMode.ALL);
    }

    [Fact]
    public void ModelRenderModeNamesMatchUdbSurface()
    {
        Assert.Equal(
            new[] { "NONE", "SELECTION", "ACTIVE_THINGS_FILTER", "ALL" },
            Enum.GetNames<ModelRenderMode>());
    }

    [Fact]
    public void LightRenderModeValuesMatchUdbOrdering()
    {
        Assert.Equal(0, (int)LightRenderMode.NONE);
        Assert.Equal(1, (int)LightRenderMode.ALL);
        Assert.Equal(2, (int)LightRenderMode.ALL_ANIMATED);
    }

    [Fact]
    public void LightRenderModeNamesMatchUdbSurface()
    {
        Assert.Equal(
            new[] { "NONE", "ALL", "ALL_ANIMATED" },
            Enum.GetNames<LightRenderMode>());
    }

    [Fact]
    public void ThingRenderModeValuesMatchUdbOrdering()
    {
        Assert.Equal(0, (int)ThingRenderMode.NORMAL);
        Assert.Equal(1, (int)ThingRenderMode.MODEL);
        Assert.Equal(2, (int)ThingRenderMode.VOXEL);
        Assert.Equal(3, (int)ThingRenderMode.WALLSPRITE);
        Assert.Equal(4, (int)ThingRenderMode.FLATSPRITE);
    }

    [Fact]
    public void ThingRenderModeNamesMatchUdbSurface()
    {
        Assert.Equal(
            new[] { "NORMAL", "MODEL", "VOXEL", "WALLSPRITE", "FLATSPRITE" },
            Enum.GetNames<ThingRenderMode>());
    }

    [Fact]
    public void ViewModeValuesMatchUdbOrdering()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(ViewMode)));
        Assert.Equal(0, (int)ViewMode.Normal);
        Assert.Equal(1, (int)ViewMode.Brightness);
        Assert.Equal(2, (int)ViewMode.FloorTextures);
        Assert.Equal(3, (int)ViewMode.CeilingTextures);
    }

    [Fact]
    public void ViewModeNamesMatchUdbSurface()
    {
        Assert.Equal(
            new[] { "Normal", "Brightness", "FloorTextures", "CeilingTextures" },
            Enum.GetNames<ViewMode>());
    }
}
