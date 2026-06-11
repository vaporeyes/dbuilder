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
}
