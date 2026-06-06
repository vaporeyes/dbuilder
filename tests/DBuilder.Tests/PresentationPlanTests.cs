// ABOUTME: Verifies UDB-style 2D renderer presentation layer stacks.
// ABOUTME: Covers Standard, Things, custom layer addition, and hidden-sector skip state.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class PresentationPlanTests
{
    [Fact]
    public void StandardPresentationMatchesUdbLayerOrder()
    {
        PresentationPlan plan = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);

        Assert.False(plan.SkipHiddenSectors);
        Assert.Equal(6, plan.Layers.Count);
        Assert.Equal(new PresentationLayer(PresentationRendererLayer.Background, PresentationBlendingMode.Mask, 0.4f), plan.Layers[0]);
        Assert.Equal(new PresentationLayer(PresentationRendererLayer.Surface, PresentationBlendingMode.Mask), plan.Layers[1]);
        Assert.Equal(new PresentationLayer(PresentationRendererLayer.Things, PresentationBlendingMode.Alpha, 0.25f), plan.Layers[2]);
        Assert.Equal(new PresentationLayer(PresentationRendererLayer.Grid, PresentationBlendingMode.Mask), plan.Layers[3]);
        Assert.Equal(new PresentationLayer(PresentationRendererLayer.Geometry, PresentationBlendingMode.Alpha, 1.0f, Antialiasing: true), plan.Layers[4]);
        Assert.Equal(new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Alpha, 1.0f, Antialiasing: true), plan.Layers[5]);
    }

    [Fact]
    public void ThingsPresentationMatchesUdbLayerOrder()
    {
        PresentationPlan plan = PresentationPlan.Things(backgroundAlpha: 0.4f);

        Assert.Equal(7, plan.Layers.Count);
        Assert.Equal(new PresentationLayer(PresentationRendererLayer.Background, PresentationBlendingMode.Mask, 0.4f), plan.Layers[0]);
        Assert.Equal(new PresentationLayer(PresentationRendererLayer.Surface, PresentationBlendingMode.Mask), plan.Layers[1]);
        Assert.Equal(new PresentationLayer(PresentationRendererLayer.Things, PresentationBlendingMode.Alpha, 1.0f), plan.Layers[2]);
        Assert.Equal(new PresentationLayer(PresentationRendererLayer.Grid, PresentationBlendingMode.Mask), plan.Layers[3]);
        Assert.Equal(new PresentationLayer(PresentationRendererLayer.Geometry, PresentationBlendingMode.Alpha, 1.0f, Antialiasing: true), plan.Layers[4]);
        Assert.Equal(new PresentationLayer(PresentationRendererLayer.Things, PresentationBlendingMode.Alpha, 0.5f), plan.Layers[5]);
        Assert.Equal(new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Alpha, 1.0f, Antialiasing: true), plan.Layers[6]);
    }

    [Fact]
    public void PresentationConstantsMatchUdbThingAlphaValues()
    {
        Assert.Equal(0.3f, PresentationPlan.ThingsBackAlpha);
        Assert.Equal(0.66f, PresentationPlan.ThingsHiddenAlpha);
        Assert.Equal(1.0f, PresentationPlan.ThingsAlpha);
    }

    [Fact]
    public void CustomPresentationAddsLayersWithoutMutatingOriginal()
    {
        PresentationPlan standard = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationPlan custom = standard.AddLayer(new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Additive, 0.75f));

        Assert.Equal(6, standard.Layers.Count);
        Assert.Equal(7, custom.Layers.Count);
        Assert.Equal(new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Additive, 0.75f), custom.Layers[^1]);
    }

    [Fact]
    public void SkipHiddenSectorsCanBeCopiedLikeUdbPresentation()
    {
        PresentationPlan plan = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f)
            .WithSkipHiddenSectors(true);

        Assert.True(plan.SkipHiddenSectors);
        Assert.Equal(6, plan.Layers.Count);
    }

    [Fact]
    public void BuildDrawCommandsMapsBlendStateLikeUdbPresent()
    {
        var plan = new PresentationPlan(new[]
        {
            new PresentationLayer(PresentationRendererLayer.Grid, PresentationBlendingMode.None),
            new PresentationLayer(PresentationRendererLayer.Surface, PresentationBlendingMode.Mask),
            new PresentationLayer(PresentationRendererLayer.Geometry, PresentationBlendingMode.Alpha),
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Additive),
        });

        IReadOnlyList<PresentationDrawCommand> commands = plan.BuildDrawCommands(qualityDisplay: false);

        Assert.False(commands[0].AlphaBlendEnabled);
        Assert.False(commands[0].AlphaTestEnabled);
        Assert.False(commands[1].AlphaBlendEnabled);
        Assert.True(commands[1].AlphaTestEnabled);
        Assert.True(commands[2].AlphaBlendEnabled);
        Assert.False(commands[2].AlphaTestEnabled);
        Assert.Equal(Blend.InverseSourceAlpha, commands[2].DestinationBlend);
        Assert.True(commands[3].AlphaBlendEnabled);
        Assert.Equal(Blend.One, commands[3].DestinationBlend);
    }

    [Fact]
    public void BuildDrawCommandsUsesFsaaOnlyForAntialiasedLayersWhenQualityDisplayIsEnabled()
    {
        PresentationPlan plan = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);

        IReadOnlyList<PresentationDrawCommand> normal = plan.BuildDrawCommands(qualityDisplay: false);
        IReadOnlyList<PresentationDrawCommand> quality = plan.BuildDrawCommands(qualityDisplay: true);

        Assert.All(normal, command => Assert.Equal(PresentationPlan.Display2DNormalShaderName, command.ShaderName));
        Assert.Equal(PresentationPlan.Display2DNormalShaderName, quality[0].ShaderName);
        Assert.Equal(PresentationPlan.Display2DNormalShaderName, quality[1].ShaderName);
        Assert.Equal(PresentationPlan.Display2DNormalShaderName, quality[2].ShaderName);
        Assert.Equal(PresentationPlan.Display2DNormalShaderName, quality[3].ShaderName);
        Assert.Equal(PresentationPlan.Display2DFsaaShaderName, quality[4].ShaderName);
        Assert.Equal(PresentationPlan.Display2DFsaaShaderName, quality[5].ShaderName);
    }

    [Fact]
    public void BuildDrawCommandsUsesClampOnlyForThingsLayerLikeUdbPresent()
    {
        PresentationPlan plan = PresentationPlan.Things(backgroundAlpha: 0.4f);

        IReadOnlyList<PresentationDrawCommand> commands = plan.BuildDrawCommands(qualityDisplay: false);

        Assert.Equal(TextureAddress.Wrap, commands[0].SamplerAddress);
        Assert.Equal(TextureAddress.Wrap, commands[1].SamplerAddress);
        Assert.Equal(TextureAddress.Clamp, commands[2].SamplerAddress);
        Assert.Equal(TextureAddress.Wrap, commands[3].SamplerAddress);
        Assert.Equal(TextureAddress.Wrap, commands[4].SamplerAddress);
        Assert.Equal(TextureAddress.Clamp, commands[5].SamplerAddress);
        Assert.Equal(TextureAddress.Wrap, commands[6].SamplerAddress);
    }

    [Fact]
    public void BuildDrawCommandsAssignsOverlayTextureIndexesInLayerOrder()
    {
        var plan = new PresentationPlan(new[]
        {
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Alpha),
            new PresentationLayer(PresentationRendererLayer.Geometry, PresentationBlendingMode.Alpha),
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Additive),
        });

        IReadOnlyList<PresentationDrawCommand> commands = plan.BuildDrawCommands(qualityDisplay: false);

        Assert.Equal(0, commands[0].OverlayIndex);
        Assert.Null(commands[1].OverlayIndex);
        Assert.Equal(1, commands[2].OverlayIndex);
    }
}
