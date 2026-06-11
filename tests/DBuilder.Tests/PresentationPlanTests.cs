// ABOUTME: Verifies UDB-style 2D renderer presentation layer stacks.
// ABOUTME: Covers Standard, Things, custom layer addition, and hidden-sector skip state.

using System.Globalization;
using System.Text.RegularExpressions;
using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class PresentationPlanTests
{
    private static string? FindUdbRoot()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "."));
        string sibling = Path.GetFullPath(Path.Combine(repositoryRoot, "..", "UltimateDoomBuilder"));
        if (File.Exists(Path.Combine(sibling, "Source", "Core", "Rendering", "Renderer2D.cs"))) return sibling;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string root = Path.Combine(home, "dev", "repos", "UltimateDoomBuilder");
        return File.Exists(Path.Combine(root, "Source", "Core", "Rendering", "Renderer2D.cs")) ? root : null;
    }

    private static double Renderer2DConstant(string source, string type, string name)
    {
        Match match = Regex.Match(source, @"(?:private|internal)\s+const\s+" + Regex.Escape(type) + @"\s+" + Regex.Escape(name) + @"\s*=\s*(?<value>[0-9.]+)f?");
        Assert.True(match.Success, "Expected UDB Renderer2D constant " + name + ".");
        return double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
    }

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
    public void RenderLayerMasksUseUdbUnderlyingType()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(PresentationRenderLayerMask)));
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
        Assert.False(commands[0].BlendFactorsApplied);
        Assert.False(commands[1].AlphaBlendEnabled);
        Assert.True(commands[1].AlphaTestEnabled);
        Assert.False(commands[1].BlendFactorsApplied);
        Assert.True(commands[2].AlphaBlendEnabled);
        Assert.False(commands[2].AlphaTestEnabled);
        Assert.True(commands[2].BlendFactorsApplied);
        Assert.Equal(Blend.InverseSourceAlpha, commands[2].DestinationBlend);
        Assert.True(commands[3].AlphaBlendEnabled);
        Assert.True(commands[3].BlendFactorsApplied);
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
    public void RenderSessionStartRejectsBusyRendererLikeUdbDebugBuild()
    {
        PresentationRenderSessionStartPlan plan = PresentationRenderTargetPlan.BuildRenderSessionStartPlan(
            PresentationRenderSessionStartKind.Things,
            currentLayer: RenderLayers.Plotter,
            targetAvailable: true,
            clear: true);

        Assert.False(plan.CanStart);
        Assert.True(plan.ThrowsWhenBusy);
        Assert.Equal(RenderLayers.Plotter, plan.RenderLayerAfter);
        Assert.Equal("Renderer starting called before finished previous layer. Call Finish() first!", plan.FailureReason);
        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void PlotterStartPlanClearsGridAndUpdatesTransformationsWhenRequested()
    {
        PresentationRenderSessionStartPlan plan = PresentationRenderTargetPlan.BuildRenderSessionStartPlan(
            PresentationRenderSessionStartKind.Plotter,
            currentLayer: RenderLayers.None,
            targetAvailable: true,
            clear: true);

        Assert.True(plan.CanStart);
        Assert.False(plan.ThrowsWhenBusy);
        Assert.Equal(RenderLayers.Plotter, plan.RenderLayerAfter);
        Assert.Equal(new[]
        {
            new PresentationRenderSessionStartStep(PresentationRenderSessionStartStepKind.SetRenderLayer, "Plotter"),
            new PresentationRenderSessionStartStep(PresentationRenderSessionStartStepKind.ClearPlotter, "plotter"),
            new PresentationRenderSessionStartStep(PresentationRenderSessionStartStepKind.RenderBackgroundGrid),
            new PresentationRenderSessionStartStep(PresentationRenderSessionStartStepKind.SetupBackground),
            new PresentationRenderSessionStartStep(PresentationRenderSessionStartStepKind.UpdateTransformations),
        }, plan.Steps);
    }

    [Fact]
    public void ThingsAndOverlayStartPlansBindTextureTargetsLikeUdb()
    {
        PresentationRenderSessionStartPlan things = PresentationRenderTargetPlan.BuildRenderSessionStartPlan(
            PresentationRenderSessionStartKind.Things,
            currentLayer: RenderLayers.None,
            targetAvailable: true,
            clear: true);
        PresentationRenderSessionStartPlan overlay = PresentationRenderTargetPlan.BuildRenderSessionStartPlan(
            PresentationRenderSessionStartKind.Overlay,
            currentLayer: RenderLayers.None,
            targetAvailable: true,
            clear: false,
            overlayLayerNumber: 2,
            overlayTextureCount: 3);

        Assert.True(things.CanStart);
        Assert.Equal(RenderLayers.Things, things.RenderLayerAfter);
        Assert.Equal(new[]
        {
            new PresentationRenderSessionStartStep(PresentationRenderSessionStartStepKind.SetRenderLayer, "Things"),
            new PresentationRenderSessionStartStep(PresentationRenderSessionStartStepKind.StartRenderingToTexture, "things"),
            new PresentationRenderSessionStartStep(PresentationRenderSessionStartStepKind.UpdateTransformations),
        }, things.Steps);
        Assert.True(overlay.CanStart);
        Assert.Equal(RenderLayers.Overlay, overlay.RenderLayerAfter);
        Assert.Equal(new[]
        {
            new PresentationRenderSessionStartStep(PresentationRenderSessionStartStepKind.SetRenderLayer, "Overlay"),
            new PresentationRenderSessionStartStep(PresentationRenderSessionStartStepKind.StartRenderingToTexture, "overlay2"),
            new PresentationRenderSessionStartStep(PresentationRenderSessionStartStepKind.UpdateTransformations),
        }, overlay.Steps);
    }

    [Fact]
    public void MissingTargetsFinishAndLeaveNoActiveRenderLayer()
    {
        PresentationRenderSessionStartPlan missingThings = PresentationRenderTargetPlan.BuildRenderSessionStartPlan(
            PresentationRenderSessionStartKind.Things,
            currentLayer: RenderLayers.None,
            targetAvailable: false,
            clear: true);
        PresentationRenderSessionStartPlan missingOverlay = PresentationRenderTargetPlan.BuildRenderSessionStartPlan(
            PresentationRenderSessionStartKind.Overlay,
            currentLayer: RenderLayers.None,
            targetAvailable: true,
            clear: true,
            overlayLayerNumber: 2,
            overlayTextureCount: 2);

        Assert.False(missingThings.CanStart);
        Assert.False(missingThings.ThrowsWhenBusy);
        Assert.Equal(RenderLayers.None, missingThings.RenderLayerAfter);
        Assert.Equal("Render target unavailable", missingThings.FailureReason);
        Assert.Equal(new[]
        {
            new PresentationRenderSessionStartStep(PresentationRenderSessionStartStepKind.SetRenderLayer, "Things"),
            new PresentationRenderSessionStartStep(PresentationRenderSessionStartStepKind.Finish),
        }, missingThings.Steps);
        Assert.False(missingOverlay.CanStart);
        Assert.Equal("Overlay layer unavailable", missingOverlay.FailureReason);
        Assert.Equal(RenderLayers.None, missingOverlay.RenderLayerAfter);
    }

    [Fact]
    public void FinishPlanDrawsPlotterOrStopsTextureRenderingThenClearsLayer()
    {
        PresentationRenderSessionFinishPlan plotter = PresentationRenderTargetPlan.BuildRenderSessionFinishPlan(RenderLayers.Plotter);
        PresentationRenderSessionFinishPlan things = PresentationRenderTargetPlan.BuildRenderSessionFinishPlan(RenderLayers.Things);
        PresentationRenderSessionFinishPlan none = PresentationRenderTargetPlan.BuildRenderSessionFinishPlan(RenderLayers.None);

        Assert.Equal(RenderLayers.None, plotter.RenderLayerAfter);
        Assert.Equal(new[]
        {
            new PresentationRenderSessionFinishStep(PresentationRenderSessionFinishStepKind.DrawPlotterContents),
            new PresentationRenderSessionFinishStep(PresentationRenderSessionFinishStepKind.SetRenderLayerNone),
        }, plotter.Steps);
        Assert.Equal(new[]
        {
            new PresentationRenderSessionFinishStep(PresentationRenderSessionFinishStepKind.FinishRendering),
            new PresentationRenderSessionFinishStep(PresentationRenderSessionFinishStepKind.SetRenderLayerNone),
        }, things.Steps);
        Assert.Equal(new[]
        {
            new PresentationRenderSessionFinishStep(PresentationRenderSessionFinishStepKind.SetRenderLayerNone),
        }, none.Steps);
    }

    [Fact]
    public void SurfaceRedrawReturnsWithoutWorkWhenRendererIsBusy()
    {
        PresentationSurfaceRedrawPlan plan = PresentationRenderTargetPlan.BuildSurfaceRedrawPlan(
            currentLayer: RenderLayers.Overlay,
            surfaceTargetAvailable: true,
            windowSizeChanged: true,
            viewMode: ViewMode.Brightness,
            skipHiddenSectors: true);

        Assert.False(plan.CanRedraw);
        Assert.False(plan.RecreateRenderTargets);
        Assert.Equal(RenderLayers.Overlay, plan.RenderLayerAfter);
        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void SurfaceRedrawRecreatesTargetsAndRendersBrightnessLikeUdb()
    {
        PresentationSurfaceRedrawPlan plan = PresentationRenderTargetPlan.BuildSurfaceRedrawPlan(
            currentLayer: RenderLayers.None,
            surfaceTargetAvailable: true,
            windowSizeChanged: true,
            viewMode: ViewMode.Brightness,
            skipHiddenSectors: true);

        Assert.True(plan.CanRedraw);
        Assert.True(plan.RecreateRenderTargets);
        Assert.Equal(RenderLayers.None, plan.RenderLayerAfter);
        Assert.Equal(ViewMode.Brightness, plan.ViewMode);
        Assert.True(plan.SkipHiddenSectors);
        Assert.Equal(new[]
        {
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.RecreateRenderTargets),
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.SetRenderLayerSurface),
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.StartRenderingToSurface, "surface"),
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.UpdateTransformations),
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.SetCullNone),
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.DisableDepth),
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.DisableAlphaBlend),
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.DisableAlphaTest),
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.ClearDesaturation),
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.SetWorldTransformation),
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.ApplyDisplaySettings),
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.RenderSectorBrightness, "True"),
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.RenderSectorSurfaces),
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.Finish),
        }, plan.Steps);
    }

    [Theory]
    [InlineData(ViewMode.FloorTextures, PresentationSurfaceRedrawStepKind.RenderSectorFloors)]
    [InlineData(ViewMode.CeilingTextures, PresentationSurfaceRedrawStepKind.RenderSectorCeilings)]
    public void SurfaceRedrawUsesViewModeSpecificSurfacePreparation(
        ViewMode viewMode,
        PresentationSurfaceRedrawStepKind expected)
    {
        PresentationSurfaceRedrawPlan plan = PresentationRenderTargetPlan.BuildSurfaceRedrawPlan(
            currentLayer: RenderLayers.None,
            surfaceTargetAvailable: true,
            windowSizeChanged: false,
            viewMode: viewMode,
            skipHiddenSectors: false);

        Assert.Contains(new PresentationSurfaceRedrawStep(expected, "False"), plan.Steps);
        Assert.Contains(new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.RenderSectorSurfaces), plan.Steps);
        Assert.DoesNotContain(plan.Steps, step => step.Kind == PresentationSurfaceRedrawStepKind.RecreateRenderTargets);
    }

    [Fact]
    public void SurfaceRedrawNormalModeOnlyClearsSurfaceTargetAndFinishes()
    {
        PresentationSurfaceRedrawPlan plan = PresentationRenderTargetPlan.BuildSurfaceRedrawPlan(
            currentLayer: RenderLayers.None,
            surfaceTargetAvailable: true,
            windowSizeChanged: false,
            viewMode: ViewMode.Normal,
            skipHiddenSectors: false);

        Assert.DoesNotContain(plan.Steps, step => step.Kind == PresentationSurfaceRedrawStepKind.RenderSectorBrightness);
        Assert.DoesNotContain(plan.Steps, step => step.Kind == PresentationSurfaceRedrawStepKind.RenderSectorFloors);
        Assert.DoesNotContain(plan.Steps, step => step.Kind == PresentationSurfaceRedrawStepKind.RenderSectorCeilings);
        Assert.DoesNotContain(plan.Steps, step => step.Kind == PresentationSurfaceRedrawStepKind.RenderSectorSurfaces);
        Assert.Equal(PresentationSurfaceRedrawStepKind.Finish, plan.Steps[^1].Kind);
    }

    [Fact]
    public void SurfaceRedrawFinishesWhenSurfaceTargetIsUnavailable()
    {
        PresentationSurfaceRedrawPlan plan = PresentationRenderTargetPlan.BuildSurfaceRedrawPlan(
            currentLayer: RenderLayers.None,
            surfaceTargetAvailable: false,
            windowSizeChanged: false,
            viewMode: ViewMode.FloorTextures,
            skipHiddenSectors: true);

        Assert.True(plan.CanRedraw);
        Assert.Equal(RenderLayers.None, plan.RenderLayerAfter);
        Assert.Equal(new[]
        {
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.SetRenderLayerSurface),
            new PresentationSurfaceRedrawStep(PresentationSurfaceRedrawStepKind.Finish),
        }, plan.Steps);
    }

    [Fact]
    public void SurfaceRedrawRejectsInvalidViewMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PresentationRenderTargetPlan.BuildSurfaceRedrawPlan(
            currentLayer: RenderLayers.None,
            surfaceTargetAvailable: true,
            windowSizeChanged: false,
            viewMode: (ViewMode)99,
            skipHiddenSectors: false));
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
        Assert.True(plan.ResetGridOrigin);
        Assert.False(plan.RedrawExistingMap);
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
    public void RenderTargetPlanRedrawsExistingMapOnlyWhenMapConfigurationExists()
    {
        PresentationRenderTargetPlan noMap = PresentationRenderTargetPlan.Create(
            320,
            200,
            PresentationPlan.Standard(0.4f, 0.25f));
        PresentationRenderTargetPlan withMap = PresentationRenderTargetPlan.Create(
            320,
            200,
            PresentationPlan.Standard(0.4f, 0.25f),
            hasMapConfiguration: true);

        Assert.False(noMap.RedrawExistingMap);
        Assert.True(withMap.RedrawExistingMap);
        Assert.True(withMap.ResetGridScale);
        Assert.True(withMap.ResetGridSize);
        Assert.True(withMap.ResetGridOrigin);
    }

    [Fact]
    public void RenderTargetPlanRejectsInvalidTargetDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PresentationRenderTargetPlan.Create(0, 200, PresentationPlan.Standard(0.4f, 0.25f)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PresentationRenderTargetPlan.Create(320, 0, PresentationPlan.Standard(0.4f, 0.25f)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PresentationRenderTargetPlan.BuildCreateSequencePlan(0, 200, PresentationPlan.Standard(0.4f, 0.25f)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PresentationRenderTargetPlan.BuildCreateSequencePlan(320, 0, PresentationPlan.Standard(0.4f, 0.25f)));
    }

    [Fact]
    public void SetPresentationPlanAddsAndClearsOneOverlayTextureWhenUdbNeedsMore()
    {
        var presentation = new PresentationPlan(new[]
        {
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Alpha),
            new PresentationLayer(PresentationRendererLayer.Grid, PresentationBlendingMode.Mask),
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Additive),
        });

        PresentationSetPlan plan = PresentationRenderTargetPlan.BuildSetPresentationPlan(
            presentation,
            existingOverlayTextureCount: 1);

        Assert.Equal(new PresentationSetPlan(
            ExistingOverlayTextureCount: 1,
            RequestedOverlayLayerCount: 2,
            CopyPresentation: true,
            AddOverlayTexture: true,
            ClearAddedOverlayTexture: true,
            OverlayTextureCountAfter: 2), plan);
    }

    [Fact]
    public void SetPresentationPlanKeepsOverlayTexturesWhenEnoughAlreadyExist()
    {
        PresentationSetPlan plan = PresentationRenderTargetPlan.BuildSetPresentationPlan(
            PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f),
            existingOverlayTextureCount: 2);

        Assert.Equal(new PresentationSetPlan(
            ExistingOverlayTextureCount: 2,
            RequestedOverlayLayerCount: 1,
            CopyPresentation: true,
            AddOverlayTexture: false,
            ClearAddedOverlayTexture: false,
            OverlayTextureCountAfter: 2), plan);
    }

    [Fact]
    public void SetPresentationPlanRejectsNegativeOverlayTextureCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PresentationRenderTargetPlan.BuildSetPresentationPlan(
                PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f),
                existingOverlayTextureCount: -1));
    }

    [Fact]
    public void PresentationPlannersRejectNullInputs()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);
        PresentationDisplaySettings setting = targets.BuildDisplaySettings(presentation, qualityDisplay: false)[0];
        PresentationLayerDrawPlan drawPlan = targets.BuildLayerDrawPlans(
            presentation,
            qualityDisplay: false,
            hasBackgroundVertices: true,
            hasBackgroundTexture: true)[0];

        Assert.Throws<ArgumentNullException>(() =>
            PresentationRenderTargetPlan.BuildSetPresentationPlan(null!, existingOverlayTextureCount: 0));
        Assert.Throws<ArgumentNullException>(() =>
            targets.BuildDisplaySettings(null!, qualityDisplay: false));
        Assert.Throws<ArgumentNullException>(() =>
            targets.BuildDisplaySettingSteps(null!));
        Assert.Throws<ArgumentNullException>(() =>
            PresentationRenderTargetPlan.BuildRenderSettingsVector(null!));
        Assert.Throws<ArgumentNullException>(() =>
            PresentationRenderTargetPlan.BuildLayerDrawSteps(null!));
        Assert.Throws<ArgumentNullException>(() =>
            targets.BuildLayerDrawPlans(null!, qualityDisplay: false, hasBackgroundVertices: true, hasBackgroundTexture: true));
        Assert.Throws<ArgumentNullException>(() =>
            targets.BuildFramePlan(null!, qualityDisplay: false));

        Assert.NotNull(setting);
        Assert.NotNull(drawPlan);
    }

    [Fact]
    public void LifecyclePlanConstructsSurfaceManagerTargetsAndSuppressesFinalizeLikeUdb()
    {
        PresentationRenderTargetLifecyclePlan plan = PresentationRenderTargetPlan.BuildLifecyclePlan(
            PresentationRenderTargetLifecycleOperation.Construct);

        Assert.Equal(PresentationRenderTargetLifecycleOperation.Construct, plan.Operation);
        Assert.True(plan.CreateSurfaceManager);
        Assert.True(plan.CreateRenderTargets);
        Assert.True(plan.SuppressFinalize);
        Assert.False(plan.DestroyRenderTargets);
        Assert.False(plan.DisposeSurfaceManager);
    }

    [Fact]
    public void LifecyclePlanDisposesTargetsSurfaceManagerThingBufferAndGridCacheLikeUdb()
    {
        PresentationRenderTargetLifecyclePlan plan = PresentationRenderTargetPlan.BuildLifecyclePlan(
            PresentationRenderTargetLifecycleOperation.Dispose);

        Assert.Equal(PresentationRenderTargetLifecycleOperation.Dispose, plan.Operation);
        Assert.True(plan.DestroyRenderTargets);
        Assert.True(plan.DisposeSurfaceManager);
        Assert.True(plan.DisposeThingBatchBuffer);
        Assert.True(plan.ResetGridCache);
        Assert.False(plan.CreateRenderTargets);
    }

    [Fact]
    public void LifecyclePlanMatchesUdbUnloadReloadAndCreateDestroyResourcePaths()
    {
        PresentationRenderTargetLifecyclePlan unload = PresentationRenderTargetPlan.BuildLifecyclePlan(
            PresentationRenderTargetLifecycleOperation.UnloadResource);
        PresentationRenderTargetLifecyclePlan reload = PresentationRenderTargetPlan.BuildLifecyclePlan(
            PresentationRenderTargetLifecycleOperation.ReloadResource);
        PresentationRenderTargetLifecyclePlan destroy = PresentationRenderTargetPlan.BuildLifecyclePlan(
            PresentationRenderTargetLifecycleOperation.DestroyRenderTargets);
        PresentationRenderTargetLifecyclePlan create = PresentationRenderTargetPlan.BuildLifecyclePlan(
            PresentationRenderTargetLifecycleOperation.CreateRenderTargets);

        Assert.True(unload.DestroyRenderTargets);
        Assert.False(unload.CreateRenderTargets);
        Assert.True(unload.DisposeThingBatchBuffer);
        Assert.True(unload.ResetGridCache);

        Assert.False(reload.DestroyRenderTargets);
        Assert.True(reload.CreateRenderTargets);
        Assert.False(reload.DisposeThingBatchBuffer);
        Assert.False(reload.ResetGridCache);

        Assert.True(destroy.DestroyRenderTargets);
        Assert.False(destroy.CreateRenderTargets);
        Assert.True(destroy.DisposeThingBatchBuffer);
        Assert.True(destroy.ResetGridCache);

        Assert.True(create.DestroyRenderTargets);
        Assert.True(create.CreateRenderTargets);
        Assert.True(create.DisposeThingBatchBuffer);
        Assert.True(create.ResetGridCache);
    }

    [Fact]
    public void DestroyPlanDisposesTargetsInUdbOrder()
    {
        PresentationRenderTargetDestroyPlan plan = PresentationRenderTargetPlan.BuildDestroyPlan(overlayTextureCount: 2);

        Assert.Equal(new[]
        {
            PresentationRenderTargetDestroyStepKind.DisposeResource,
            PresentationRenderTargetDestroyStepKind.DisposeResource,
            PresentationRenderTargetDestroyStepKind.DisposeResource,
            PresentationRenderTargetDestroyStepKind.ClearReference,
            PresentationRenderTargetDestroyStepKind.DisposeResource,
            PresentationRenderTargetDestroyStepKind.ClearReference,
            PresentationRenderTargetDestroyStepKind.DisposeResource,
            PresentationRenderTargetDestroyStepKind.DisposeResource,
            PresentationRenderTargetDestroyStepKind.DisposeResource,
        }, plan.Steps.Take(9).Select(step => step.Kind));
        Assert.Equal(new[]
        {
            "plotter",
            "things",
            "overlay0",
            "overlay0",
            "overlay1",
            "overlay1",
            "surface",
            "gridplotter",
            "screenverts",
        }, plan.Steps.Take(9).Select(step => step.TargetName));
    }

    [Fact]
    public void DestroyPlanClearsUdbReferencesAndGridCache()
    {
        PresentationRenderTargetDestroyPlan plan = PresentationRenderTargetPlan.BuildDestroyPlan(overlayTextureCount: 1);

        Assert.Equal(1, plan.OverlayTextureCount);
        Assert.Equal(new[]
        {
            "things",
            "gridplotter",
            "screenverts",
            "overlaytex",
            "surface",
            "thingsvertices",
        }, plan.Steps
            .Where(step => step.Kind == PresentationRenderTargetDestroyStepKind.ClearReference)
            .Skip(1)
            .Select(step => step.TargetName));
        Assert.Equal(PresentationRenderTargetDestroyStepKind.ResetGridCache, plan.Steps[^1].Kind);
        Assert.Null(plan.Steps[^1].TargetName);
        Assert.Equal(-1.0f, plan.LastGridScaleAfter);
        Assert.Equal(0.0, plan.LastGridSizeAfter);
    }

    [Fact]
    public void DestroyPlanRejectsNegativeOverlayTextureCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PresentationRenderTargetPlan.BuildDestroyPlan(overlayTextureCount: -1));
    }

    [Fact]
    public void CreateSequencePlanAllocatesResourcesAndClearsTargetsInUdbOrder()
    {
        var presentation = new PresentationPlan(new[]
        {
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Alpha),
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Additive),
        });

        PresentationRenderTargetCreateSequencePlan sequence = PresentationRenderTargetPlan.BuildCreateSequencePlan(
            320,
            200,
            presentation);

        Assert.Equal(new[]
        {
            PresentationRenderTargetCreateStepKind.DestroyExistingTargets,
            PresentationRenderTargetCreateStepKind.ReadRenderTargetSize,
            PresentationRenderTargetCreateStepKind.AllocateResource,
            PresentationRenderTargetCreateStepKind.AllocateResource,
            PresentationRenderTargetCreateStepKind.AllocateResource,
            PresentationRenderTargetCreateStepKind.AllocateResource,
            PresentationRenderTargetCreateStepKind.AllocateResource,
            PresentationRenderTargetCreateStepKind.AllocateResource,
            PresentationRenderTargetCreateStepKind.ClearTarget,
            PresentationRenderTargetCreateStepKind.ClearTarget,
            PresentationRenderTargetCreateStepKind.ClearTarget,
        }, sequence.Steps.Take(11).Select(step => step.Kind));
        Assert.Equal(new[]
        {
            null,
            null,
            "plotter",
            "gridplotter",
            "things",
            "surface",
            "overlay0",
            "overlay1",
            "things",
            "overlay0",
            "overlay1",
        }, sequence.Steps.Take(11).Select(step => step.TargetName));
    }

    [Fact]
    public void CreateSequencePlanUploadsVertexBuffersAndUpdatesTransformationsLikeUdb()
    {
        PresentationRenderTargetCreateSequencePlan sequence = PresentationRenderTargetPlan.BuildCreateSequencePlan(
            320,
            200,
            PresentationPlan.Standard(0.4f, 0.25f));

        Assert.Equal(new[]
        {
            PresentationRenderTargetCreateStepKind.CreateVertexBuffer,
            PresentationRenderTargetCreateStepKind.CreateVertexBuffer,
            PresentationRenderTargetCreateStepKind.UploadThingsVertexBuffer,
            PresentationRenderTargetCreateStepKind.UploadScreenVertexBuffer,
            PresentationRenderTargetCreateStepKind.ResetGridCache,
            PresentationRenderTargetCreateStepKind.UpdateTransformations,
        }, sequence.Steps.TakeLast(6).Select(step => step.Kind));
        Assert.Equal(new[]
        {
            "screenverts",
            "thingsvertices",
            "thingsvertices",
            "screenverts",
            null,
            null,
        }, sequence.Steps.TakeLast(6).Select(step => step.TargetName));
        Assert.True(sequence.TargetPlan.ResetGridScale);
        Assert.True(sequence.TargetPlan.ResetGridSize);
        Assert.True(sequence.TargetPlan.ResetGridOrigin);
    }

    [Fact]
    public void CreateSequencePlanRedrawsExistingMapOnlyWithMapConfiguration()
    {
        PresentationRenderTargetCreateSequencePlan noMap = PresentationRenderTargetPlan.BuildCreateSequencePlan(
            320,
            200,
            PresentationPlan.Standard(0.4f, 0.25f));
        PresentationRenderTargetCreateSequencePlan withMap = PresentationRenderTargetPlan.BuildCreateSequencePlan(
            320,
            200,
            PresentationPlan.Standard(0.4f, 0.25f),
            hasMapConfiguration: true);

        Assert.DoesNotContain(noMap.Steps, step => step.Kind == PresentationRenderTargetCreateStepKind.RedrawExistingMap);
        Assert.Equal(PresentationRenderTargetCreateStepKind.RedrawExistingMap, withMap.Steps[^1].Kind);
        Assert.False(noMap.TargetPlan.RedrawExistingMap);
        Assert.True(withMap.TargetPlan.RedrawExistingMap);
    }

    [Fact]
    public void RenderTargetPlanMatchesUdbThingVertexBufferCapacity()
    {
        PresentationRenderTargetPlan plan = PresentationRenderTargetPlan.Create(320, 200, PresentationPlan.Standard(0.4f, 0.25f));

        Assert.Equal(100, PresentationRenderTargetPlan.ThingBufferSize);
        Assert.Equal(1200, plan.ThingVertexCapacity);
    }

    [Fact]
    public void RenderTargetConstantsMatchUdbRenderer2DWhenCloneIsAvailable()
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        string source = File.ReadAllText(Path.Combine(udbRoot, "Source", "Core", "Rendering", "Renderer2D.cs"));

        Assert.Equal(PresentationRenderTargetPlan.ThingBufferSize, Renderer2DConstant(source, "int", "THING_BUFFER_SIZE"));
        Assert.Equal(PresentationRenderTargetPlan.MapCenterSize, Renderer2DConstant(source, "int", "MAP_CENTER_SIZE"));
        Assert.Equal(PresentationRenderTargetPlan.FsaaFactor, (float)Renderer2DConstant(source, "float", "FSAA_FACTOR"));
    }

    [Fact]
    public void MapCenterLinesMatchUdbRenderer2DCrosshair()
    {
        IReadOnlyList<PresentationMapCenterLine> lines = PresentationRenderTargetPlan.BuildMapCenterLines(
            drawMapCenter: true,
            translateX: 10,
            translateY: -4,
            scale: 2);

        Assert.Equal(new[]
        {
            new PresentationMapCenterLine(20, 24, 20, -8, "Highlight"),
            new PresentationMapCenterLine(4, 8, 36, 8, "Highlight"),
        }, lines);
    }

    [Fact]
    public void MapCenterLinesAreSkippedWhenModeDisablesMapCenter()
    {
        Assert.Empty(PresentationRenderTargetPlan.BuildMapCenterLines(
            drawMapCenter: false,
            translateX: 10,
            translateY: -4,
            scale: 2));
    }

    [Fact]
    public void MapCenterLineExpressionsMatchUdbRenderer2DWhenCloneIsAvailable()
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        string source = File.ReadAllText(Path.Combine(udbRoot, "Source", "Core", "Rendering", "Renderer2D.cs"));

        Assert.Contains("gridplotter.DrawLineSolid(cx, cy + MAP_CENTER_SIZE, cx, cy - MAP_CENTER_SIZE", source, StringComparison.Ordinal);
        Assert.Contains("gridplotter.DrawLineSolid(cx - MAP_CENTER_SIZE, cy, cx + MAP_CENTER_SIZE, cy", source, StringComparison.Ordinal);
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
    public void DisplaySettingsMatchUdbProjectionTransformBehavior()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        IReadOnlyList<PresentationDisplaySettings> settings = targets.BuildDisplaySettings(presentation, qualityDisplay: false);

        Assert.Equal(PresentationProjectionTransformKind.WorldViewFlipY, settings[0].ProjectionTransform);
        Assert.Equal(PresentationProjectionTransformKind.WorldViewFlipY, settings[1].ProjectionTransform);
        Assert.Equal(PresentationProjectionTransformKind.WorldViewFlipY, settings[2].ProjectionTransform);
        Assert.Equal(PresentationProjectionTransformKind.WorldViewFlipY, settings[3].ProjectionTransform);
        Assert.Equal(PresentationProjectionTransformKind.WorldView, settings[4].ProjectionTransform);
        Assert.Equal(PresentationProjectionTransformKind.WorldViewFlipY, settings[5].ProjectionTransform);
    }

    [Fact]
    public void RenderSettingsVectorMatchesUdbDisplayUniformPayload()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);
        PresentationDisplaySettings setting = targets.BuildDisplaySettings(presentation, qualityDisplay: false)[0];

        PresentationRenderSettingsVector vector = PresentationRenderTargetPlan.BuildRenderSettingsVector(setting);

        Assert.Equal(1.0f / 320, vector.TexelX);
        Assert.Equal(1.0f / 200, vector.TexelY);
        Assert.Equal(PresentationRenderTargetPlan.FsaaFactor, vector.FsaaFactor);
        Assert.Equal(0.4f, vector.Alpha);
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
    public void DisplaySettingStepsMatchUdbUniformAndSamplerOrder()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);
        PresentationDisplaySettings setting = targets.BuildDisplaySettings(presentation, qualityDisplay: false)[0];

        IReadOnlyList<PresentationDisplaySettingStep> steps = targets.BuildDisplaySettingSteps(setting);

        Assert.Equal(new[]
        {
            PresentationDisplaySettingStepKind.SetRenderSettingsUniform,
            PresentationDisplaySettingStepKind.SetProjectionUniform,
            PresentationDisplaySettingStepKind.SetSamplerFilter,
        }, steps.Select(step => step.Kind));
        Assert.Equal(new[] { "rendersettings", "projection", "Nearest" }, steps.Select(step => step.TargetName));
    }

    [Fact]
    public void DisplaySettingStepsPreserveBilinearSamplerFilter()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);
        PresentationDisplaySettings setting = targets.BuildDisplaySettings(presentation, qualityDisplay: true, bilinear: true)[0];

        IReadOnlyList<PresentationDisplaySettingStep> steps = targets.BuildDisplaySettingSteps(setting);

        Assert.Equal(PresentationDisplaySettingStepKind.SetSamplerFilter, steps[^1].Kind);
        Assert.Equal("Linear", steps[^1].TargetName);
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
    public void LayerDrawPlansSkipOnlyBackgroundWhenBackgroundVerticesAreMissingLikeUdb()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        IReadOnlyList<PresentationLayerDrawPlan> plans = targets.BuildLayerDrawPlans(
            presentation,
            qualityDisplay: false,
            hasBackgroundVertices: false,
            hasBackgroundTexture: true);

        Assert.False(plans[0].Draws);
        Assert.Equal(PresentationRendererLayer.Background, plans[0].Layer);
        Assert.Equal("Missing background vertices or texture", plans[0].SkipReason);
        Assert.All(plans.Skip(1), plan =>
        {
            Assert.True(plan.Draws);
            Assert.Null(plan.SkipReason);
        });
    }

    [Fact]
    public void LayerDrawPlansSkipOnlyBackgroundWhenBackgroundTextureIsMissingLikeUdb()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        IReadOnlyList<PresentationLayerDrawPlan> plans = targets.BuildLayerDrawPlans(
            presentation,
            qualityDisplay: false,
            hasBackgroundVertices: true,
            hasBackgroundTexture: false);

        Assert.False(plans[0].Draws);
        Assert.Equal("background", plans[0].SourceTargetName);
        Assert.Equal("Missing background vertices or texture", plans[0].SkipReason);
        Assert.Equal(new[]
        {
            PresentationRendererLayer.Surface,
            PresentationRendererLayer.Things,
            PresentationRendererLayer.Grid,
            PresentationRendererLayer.Geometry,
            PresentationRendererLayer.Overlay,
        }, plans.Skip(1).Select(plan => plan.Layer));
        Assert.All(plans.Skip(1), plan => Assert.True(plan.Draws));
    }

    [Fact]
    public void LayerDrawPlansDrawEveryLayerWhenBackgroundImageInputsExist()
    {
        var presentation = new PresentationPlan(new[]
        {
            new PresentationLayer(PresentationRendererLayer.Background, PresentationBlendingMode.Mask),
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Alpha),
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Additive),
        });
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        IReadOnlyList<PresentationLayerDrawPlan> plans = targets.BuildLayerDrawPlans(
            presentation,
            qualityDisplay: true,
            hasBackgroundVertices: true,
            hasBackgroundTexture: true);

        Assert.All(plans, plan =>
        {
            Assert.True(plan.Draws);
            Assert.Null(plan.SkipReason);
        });
        Assert.Equal(new int?[] { null, 0, 1 }, plans.Select(plan => plan.OverlayIndex));
    }

    [Fact]
    public void LayerDrawPlansUseBackgroundVerticesAndRestoreScreenBufferLikeUdb()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        IReadOnlyList<PresentationLayerDrawPlan> plans = targets.BuildLayerDrawPlans(
            presentation,
            qualityDisplay: false,
            hasBackgroundVertices: true,
            hasBackgroundTexture: true);

        Assert.Equal("backimageverts", plans[0].VertexSourceName);
        Assert.True(plans[0].RestoreScreenVertexBufferAfterDraw);
        Assert.All(plans.Skip(1), plan =>
        {
            Assert.Equal("screenverts", plan.VertexSourceName);
            Assert.False(plan.RestoreScreenVertexBufferAfterDraw);
        });
    }

    [Fact]
    public void LayerDrawPlansDoNotRestoreScreenBufferWhenBackgroundDrawIsSkipped()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        IReadOnlyList<PresentationLayerDrawPlan> plans = targets.BuildLayerDrawPlans(
            presentation,
            qualityDisplay: false,
            hasBackgroundVertices: false,
            hasBackgroundTexture: true);

        Assert.False(plans[0].Draws);
        Assert.Equal("backimageverts", plans[0].VertexSourceName);
        Assert.False(plans[0].RestoreScreenVertexBufferAfterDraw);
    }

    [Fact]
    public void LayerDrawPlansBindMapBackgroundTextureOnlyForBackgroundLayerLikeUdb()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        IReadOnlyList<PresentationLayerDrawPlan> plans = targets.BuildLayerDrawPlans(
            presentation,
            qualityDisplay: false,
            hasBackgroundVertices: true,
            hasBackgroundTexture: true);

        Assert.Equal("map-grid-background", plans[0].TextureBindingName);
        Assert.False(plans[0].UsesRenderTargetTexture);
        Assert.Equal(new[] { "surface", "things", "gridplotter", "plotter", "overlay0" }, plans.Skip(1).Select(plan => plan.TextureBindingName));
        Assert.All(plans.Skip(1), plan => Assert.True(plan.UsesRenderTargetTexture));
    }

    [Fact]
    public void LayerDrawPlansKeepBackgroundTextureBindingWhenBackgroundDrawIsSkipped()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        IReadOnlyList<PresentationLayerDrawPlan> plans = targets.BuildLayerDrawPlans(
            presentation,
            qualityDisplay: false,
            hasBackgroundVertices: true,
            hasBackgroundTexture: false);

        Assert.False(plans[0].Draws);
        Assert.Equal("map-grid-background", plans[0].TextureBindingName);
        Assert.False(plans[0].UsesRenderTargetTexture);
    }

    [Fact]
    public void LayerDrawPlansUseUdbTriangleStripDrawArgumentsForEveryLayer()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        IReadOnlyList<PresentationLayerDrawPlan> plans = targets.BuildLayerDrawPlans(
            presentation,
            qualityDisplay: false,
            hasBackgroundVertices: true,
            hasBackgroundTexture: true);

        Assert.All(plans, plan =>
        {
            Assert.Equal(PrimitiveType.TriangleStrip, plan.PrimitiveType);
            Assert.Equal(0, plan.StartVertex);
            Assert.Equal(2, plan.PrimitiveCount);
        });
    }

    [Fact]
    public void LayerDrawPlansKeepUdbDrawArgumentsWhenBackgroundDrawIsSkipped()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);

        IReadOnlyList<PresentationLayerDrawPlan> plans = targets.BuildLayerDrawPlans(
            presentation,
            qualityDisplay: false,
            hasBackgroundVertices: false,
            hasBackgroundTexture: true);

        Assert.False(plans[0].Draws);
        Assert.Equal(PrimitiveType.TriangleStrip, plans[0].PrimitiveType);
        Assert.Equal(0, plans[0].StartVertex);
        Assert.Equal(2, plans[0].PrimitiveCount);
    }

    [Fact]
    public void LayerDrawStepsMatchUdbBackgroundOperationOrder()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);
        PresentationLayerDrawPlan background = targets.BuildLayerDrawPlans(
            presentation,
            qualityDisplay: false,
            hasBackgroundVertices: true,
            hasBackgroundTexture: true)[0];

        IReadOnlyList<PresentationLayerDrawStep> steps = PresentationRenderTargetPlan.BuildLayerDrawSteps(background);

        Assert.Equal(new[]
        {
            PresentationLayerDrawStepKind.SetShader,
            PresentationLayerDrawStepKind.SetTexture,
            PresentationLayerDrawStepKind.SetSamplerState,
            PresentationLayerDrawStepKind.ApplyDisplaySettings,
            PresentationLayerDrawStepKind.Draw,
            PresentationLayerDrawStepKind.RestoreScreenVertexBuffer,
        }, steps.Select(step => step.Kind));
        Assert.Equal("map-grid-background", steps[1].TargetName);
        Assert.Equal("backimageverts", steps[4].TargetName);
        Assert.Equal("screenverts", steps[5].TargetName);
    }

    [Fact]
    public void LayerDrawStepsMatchUdbOverlayOperationOrder()
    {
        var presentation = new PresentationPlan(new[]
        {
            new PresentationLayer(PresentationRendererLayer.Overlay, PresentationBlendingMode.Alpha),
        });
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);
        PresentationLayerDrawPlan overlay = targets.BuildLayerDrawPlans(
            presentation,
            qualityDisplay: false,
            hasBackgroundVertices: false,
            hasBackgroundTexture: false)[0];

        IReadOnlyList<PresentationLayerDrawStep> steps = PresentationRenderTargetPlan.BuildLayerDrawSteps(overlay);

        Assert.Equal(new[]
        {
            PresentationLayerDrawStepKind.SetShader,
            PresentationLayerDrawStepKind.SetTexture,
            PresentationLayerDrawStepKind.SetSamplerState,
            PresentationLayerDrawStepKind.ApplyDisplaySettings,
            PresentationLayerDrawStepKind.Draw,
            PresentationLayerDrawStepKind.AdvanceOverlayLayer,
        }, steps.Select(step => step.Kind));
        Assert.Equal("overlay0", steps[1].TargetName);
        Assert.Equal("screenverts", steps[4].TargetName);
        Assert.Null(steps[5].TargetName);
    }

    [Fact]
    public void LayerDrawStepsAreEmptyWhenBackgroundDrawIsSkipped()
    {
        PresentationPlan presentation = PresentationPlan.Standard(backgroundAlpha: 0.4f, inactiveThingsAlpha: 0.25f);
        PresentationRenderTargetPlan targets = PresentationRenderTargetPlan.Create(320, 200, presentation);
        PresentationLayerDrawPlan background = targets.BuildLayerDrawPlans(
            presentation,
            qualityDisplay: false,
            hasBackgroundVertices: false,
            hasBackgroundTexture: true)[0];

        Assert.Empty(PresentationRenderTargetPlan.BuildLayerDrawSteps(background));
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
    public void FrameStartPlanMatchesUdbPresentSetupState()
    {
        PresentationFrameStartPlan plan = PresentationRenderTargetPlan.BuildFrameStartPlan();

        Assert.True(plan.ClearTarget);
        Assert.Equal("background", plan.ClearColorName);
        Assert.Equal("none", plan.CullMode);
        Assert.False(plan.DepthEnabled);
        Assert.Equal("screenverts", plan.VertexBufferName);
        Assert.Equal("identity", plan.WorldMatrixName);
    }

    [Fact]
    public void FrameReleasePlanClearsTextureAndVertexBufferLikeUdb()
    {
        PresentationFrameReleasePlan plan = PresentationRenderTargetPlan.BuildFrameReleasePlan();

        Assert.Equal("null", plan.TextureBindingAfter);
        Assert.Equal("null", plan.VertexBufferBindingAfter);
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
