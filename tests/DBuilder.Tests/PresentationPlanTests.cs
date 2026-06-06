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
    public void RenderLayerMasksMatchUdbNumericValues()
    {
        Assert.Equal(0, (int)PresentationRenderLayerMask.None);
        Assert.Equal(1, (int)PresentationRenderLayerMask.Background);
        Assert.Equal(2, (int)PresentationRenderLayerMask.Plotter);
        Assert.Equal(3, (int)PresentationRenderLayerMask.Things);
        Assert.Equal(4, (int)PresentationRenderLayerMask.Overlay);
        Assert.Equal(5, (int)PresentationRenderLayerMask.Surface);
    }

    [Fact]
    public void RenderLayerMaskForMapsGridAndGeometryToUdbPlotterLayer()
    {
        Assert.Equal(PresentationRenderLayerMask.Background, PresentationPlan.RenderLayerMaskFor(PresentationRendererLayer.Background));
        Assert.Equal(PresentationRenderLayerMask.Surface, PresentationPlan.RenderLayerMaskFor(PresentationRendererLayer.Surface));
        Assert.Equal(PresentationRenderLayerMask.Things, PresentationPlan.RenderLayerMaskFor(PresentationRendererLayer.Things));
        Assert.Equal(PresentationRenderLayerMask.Plotter, PresentationPlan.RenderLayerMaskFor(PresentationRendererLayer.Grid));
        Assert.Equal(PresentationRenderLayerMask.Plotter, PresentationPlan.RenderLayerMaskFor(PresentationRendererLayer.Geometry));
        Assert.Equal(PresentationRenderLayerMask.Overlay, PresentationPlan.RenderLayerMaskFor(PresentationRendererLayer.Overlay));
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

    [Fact]
    public void RenderTargetPlanCreatesDefaultOverlayTextureWithoutPresentation()
    {
        PresentationRenderTargetPlan plan = PresentationRenderTargetPlan.Create(320, 200, presentation: null);

        Assert.Equal(1, plan.OverlayTextureCount);
        Assert.Equal(4, plan.ScreenVertexCapacity);
        Assert.Equal(new[] { "things", "overlay0" }, plan.ClearTargets);
        Assert.True(plan.ResetGridScale);
        Assert.True(plan.ResetGridSize);
    }

    [Fact]
    public void RenderTargetPlanAllocatesUdbPlotterAndTextureTargets()
    {
        PresentationRenderTargetPlan plan = PresentationRenderTargetPlan.Create(320, 200, presentation: null);

        Assert.Equal(new[]
        {
            new PresentationRenderTargetResource("plotter", PresentationRenderTargetKind.Plotter, 320, 200),
            new PresentationRenderTargetResource("gridplotter", PresentationRenderTargetKind.Plotter, 320, 200),
            new PresentationRenderTargetResource("things", PresentationRenderTargetKind.Texture, 320, 200, TextureFormat.Rgba8),
            new PresentationRenderTargetResource("surface", PresentationRenderTargetKind.Texture, 320, 200, TextureFormat.Rgba8),
            new PresentationRenderTargetResource("overlay0", PresentationRenderTargetKind.Texture, 320, 200, TextureFormat.Rgba8),
        }, plan.Resources);
    }

    [Fact]
    public void RenderTargetPlanCountsOverlayLayersFromPresentation()
    {
        var presentation = new PresentationPlan(new[]
        {
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Alpha),
            new PresentationLayer(PresentationRendererLayer.Grid, PresentationBlendingMode.Mask),
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Additive),
        });

        PresentationRenderTargetPlan plan = PresentationRenderTargetPlan.Create(320, 200, presentation);

        Assert.Equal(2, plan.OverlayTextureCount);
        Assert.Equal(new[] { "things", "overlay0", "overlay1" }, plan.ClearTargets);
        Assert.Equal(new[] { "plotter", "gridplotter", "things", "surface", "overlay0", "overlay1" }, plan.Resources.Select(resource => resource.Name));
    }

    [Fact]
    public void RenderTargetPlanMatchesUdbThingVertexBufferCapacity()
    {
        PresentationRenderTargetPlan plan = PresentationRenderTargetPlan.Create(320, 200, PresentationPlan.Standard(0.4f, 0.25f));

        Assert.Equal(100, PresentationRenderTargetPlan.ThingBufferSize);
        Assert.Equal(1200, plan.ThingVertexCapacity);
    }

    [Fact]
    public void RenderTargetPlanCreatesScreenVerticesLikeUdb()
    {
        PresentationRenderTargetPlan plan = PresentationRenderTargetPlan.Create(320, 200, PresentationPlan.Standard(0.4f, 0.25f));

        Assert.Equal(4, plan.ScreenVertices.Length);
        AssertScreenVertex(plan.ScreenVertices[0], 0, 0, 0, 0);
        AssertScreenVertex(plan.ScreenVertices[1], 320, 0, 1, 0);
        AssertScreenVertex(plan.ScreenVertices[2], 0, 200, 0, 1);
        AssertScreenVertex(plan.ScreenVertices[3], 320, 200, 1, 1);
    }

    [Fact]
    public void DisplaySettingsUseUdbSourceTargetsAndTexelSizes()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        IReadOnlyList<PresentationDisplaySettings> settings = targets.BuildDisplaySettings(presentation, qualityDisplay: false);

        Assert.Equal(new[] { "background", "surface", "things", "gridplotter", "plotter", "overlay0" }, settings.Select(setting => setting.SourceTargetName));
        Assert.All(settings, setting =>
        {
            Assert.Equal(1.0f / 320, setting.TexelX);
            Assert.Equal(1.0f / 200, setting.TexelY);
            Assert.Equal(PresentationRenderTargetPlan.FsaaFactor, setting.FsaaFactor);
        });
    }

    [Fact]
    public void DisplaySettingsMatchUdbFlipYAndAlphaBehavior()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        IReadOnlyList<PresentationDisplaySettings> settings = targets.BuildDisplaySettings(presentation, qualityDisplay: false);

        Assert.True(settings[0].FlipY);
        Assert.True(settings[1].FlipY);
        Assert.True(settings[2].FlipY);
        Assert.True(settings[3].FlipY);
        Assert.False(settings[4].FlipY);
        Assert.True(settings[5].FlipY);
        Assert.Equal(0.4f, settings[0].Alpha);
        Assert.Equal(0.25f, settings[2].Alpha);
        Assert.Equal(1.0f, settings[5].Alpha);
    }

    [Fact]
    public void DisplaySettingsTrackQualityShaderAndSamplerFilter()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        IReadOnlyList<PresentationDisplaySettings> normal = targets.BuildDisplaySettings(presentation, qualityDisplay: false);
        IReadOnlyList<PresentationDisplaySettings> quality = targets.BuildDisplaySettings(presentation, qualityDisplay: true, bilinear: true);

        Assert.All(normal, setting =>
        {
            Assert.Equal(PresentationPlan.Display2DNormalShaderName, setting.ShaderName);
            Assert.Equal(TextureFilter.Nearest, setting.SamplerFilter);
        });
        Assert.Equal(PresentationPlan.Display2DFsaaShaderName, quality[4].ShaderName);
        Assert.Equal(PresentationPlan.Display2DFsaaShaderName, quality[5].ShaderName);
        Assert.All(quality, setting => Assert.Equal(TextureFilter.Linear, setting.SamplerFilter));
    }

    [Fact]
    public void DisplaySettingsAssignOverlayIndexesInLayerOrder()
    {
        var presentation = new PresentationPlan(new[]
        {
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Alpha),
            new PresentationLayer(PresentationRendererLayer.Geometry, PresentationBlendingMode.Alpha),
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Additive),
        });
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        IReadOnlyList<PresentationDisplaySettings> settings = targets.BuildDisplaySettings(presentation, qualityDisplay: false);

        Assert.Equal(0, settings[0].OverlayIndex);
        Assert.Equal("overlay0", settings[0].SourceTargetName);
        Assert.Null(settings[1].OverlayIndex);
        Assert.Equal(1, settings[2].OverlayIndex);
        Assert.Equal("overlay1", settings[2].SourceTargetName);
    }

    [Fact]
    public void FramePlanMatchesUdbPresentOperationEnvelope()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        PresentationFramePlan frame = targets.BuildFramePlan(presentation, qualityDisplay: false);

        Assert.Equal(new[]
        {
            PresentationFrameOperationKind.PluginPresentBegin,
            PresentationFrameOperationKind.StartRendering,
            PresentationFrameOperationKind.SetCullNone,
            PresentationFrameOperationKind.DisableDepth,
            PresentationFrameOperationKind.BindScreenVertexBuffer,
            PresentationFrameOperationKind.ResetWorldMatrix,
            PresentationFrameOperationKind.DrawLayer,
            PresentationFrameOperationKind.DrawLayer,
            PresentationFrameOperationKind.DrawLayer,
            PresentationFrameOperationKind.DrawLayer,
            PresentationFrameOperationKind.DrawLayer,
            PresentationFrameOperationKind.DrawLayer,
            PresentationFrameOperationKind.FinishRendering,
            PresentationFrameOperationKind.PresentSwapChain,
            PresentationFrameOperationKind.ReleaseTexture,
            PresentationFrameOperationKind.ReleaseVertexBuffer,
        }, frame.Operations.Select(operation => operation.Kind));
    }

    [Fact]
    public void FramePlanDrawLayerOperationsFollowPresentationOrder()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        PresentationFramePlan frame = targets.BuildFramePlan(presentation, qualityDisplay: true);
        PresentationFrameOperation[] drawOperations = frame.Operations
            .Where(operation => operation.Kind == PresentationFrameOperationKind.DrawLayer)
            .ToArray();

        Assert.Equal(frame.DrawCommands.Select(command => command.Layer), drawOperations.Select(operation => operation.Layer!.Value));
        Assert.Equal(frame.DisplaySettings.Select(setting => setting.SourceTargetName), drawOperations.Select(operation => operation.SourceTargetName));
        Assert.Equal(PresentationRendererLayer.Background, drawOperations[0].Layer);
        Assert.Equal(PresentationRendererLayer.Surface, drawOperations[1].Layer);
        Assert.Equal(PresentationRendererLayer.Things, drawOperations[2].Layer);
        Assert.Equal(PresentationRendererLayer.Grid, drawOperations[3].Layer);
        Assert.Equal(PresentationRendererLayer.Geometry, drawOperations[4].Layer);
        Assert.Equal(PresentationRendererLayer.Overlay, drawOperations[5].Layer);
    }

    [Fact]
    public void FramePlanPreservesOverlayIndexesAcrossDrawCommandsAndSettings()
    {
        var presentation = new PresentationPlan(new[]
        {
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Alpha),
            new PresentationLayer(PresentationRendererLayer.Geometry, PresentationBlendingMode.Alpha),
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Additive),
        });
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        PresentationFramePlan frame = targets.BuildFramePlan(presentation, qualityDisplay: false);
        PresentationFrameOperation[] drawOperations = frame.Operations
            .Where(operation => operation.Kind == PresentationFrameOperationKind.DrawLayer)
            .ToArray();

        Assert.Equal(new int?[] { 0, null, 1 }, frame.DrawCommands.Select(command => command.OverlayIndex));
        Assert.Equal(new int?[] { 0, null, 1 }, frame.DisplaySettings.Select(setting => setting.OverlayIndex));
        Assert.Equal(new int?[] { 0, null, 1 }, drawOperations.Select(operation => operation.OverlayIndex));
    }

    private static void AssertScreenVertex(FlatVertex vertex, float x, float y, float u, float v)
    {
        Assert.Equal(x, vertex.x);
        Assert.Equal(y, vertex.y);
        Assert.Equal(0.0f, vertex.z);
        Assert.Equal(1.0f, vertex.w);
        Assert.Equal(-1, vertex.c);
        Assert.Equal(u, vertex.u);
        Assert.Equal(v, vertex.v);
    }
}
