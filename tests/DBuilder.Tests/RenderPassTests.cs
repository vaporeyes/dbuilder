// ABOUTME: Verifies UDB render pass enum names and ordering for visual render sorting.
// ABOUTME: Keeps the rendering enum surface source-compatible with Core/Rendering/RenderPasses.cs.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class RenderPassTests
{
    [Fact]
    public void RenderPassValuesMatchUdbOrdering()
    {
        Assert.Equal(0, (int)RenderPass.Solid);
        Assert.Equal(1, (int)RenderPass.Mask);
        Assert.Equal(2, (int)RenderPass.Alpha);
        Assert.Equal(3, (int)RenderPass.Additive);
    }

    [Fact]
    public void RenderPassNamesMatchUdbSurface()
    {
        Assert.Equal(
            new[] { "Solid", "Mask", "Alpha", "Additive" },
            Enum.GetNames<RenderPass>());
    }

    [Fact]
    public void RenderPassUsesUdbUnderlyingType()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(RenderPass)));
    }

    [Fact]
    public void RenderLayersValuesMatchUdbOrdering()
    {
        Assert.Equal(0, (int)RenderLayers.None);
        Assert.Equal(1, (int)RenderLayers.Background);
        Assert.Equal(2, (int)RenderLayers.Plotter);
        Assert.Equal(3, (int)RenderLayers.Things);
        Assert.Equal(4, (int)RenderLayers.Overlay);
        Assert.Equal(5, (int)RenderLayers.Surface);
    }

    [Fact]
    public void RenderLayersNamesMatchUdbSurface()
    {
        Assert.Equal(
            new[] { "None", "Background", "Plotter", "Things", "Overlay", "Surface" },
            Enum.GetNames<RenderLayers>());
    }
}
