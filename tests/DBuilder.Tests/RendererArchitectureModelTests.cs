// ABOUTME: Verifies the documented renderer replacement contract for UDB native OpenGL behavior.
// ABOUTME: Covers the Silk.NET device path, runtime shader compiler strategy, and remaining gaps.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public class RendererArchitectureModelTests
{
    [Fact]
    public void CurrentContractDocumentsSilkNetOpenGlReplacement()
    {
        RendererArchitectureReplacement replacement = RendererArchitectureModel.Current;

        Assert.Contains("Silk.NET OpenGL", replacement.DeviceApi);
        Assert.Equal("Desktop OpenGL 3.3 core profile", replacement.MinimumGlProfile);
        Assert.Contains("Viewport and clear state", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device disposed-state reporting", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation layer stack planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation draw-command planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation render-target lifecycle planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation plotter and texture target allocation planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation display shader settings planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation frame operation sequence planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device alpha-test compatibility state planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device multisample antialias compatibility state planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device sampler-filter overload planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device target start-rendering planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device inline vertex draw planning and overload", replacement.CoveredResponsibilities);
        Assert.Contains("Index-buffer binding and primitive draw dispatch", replacement.CoveredResponsibilities);
        Assert.Contains("Length-based vertex-buffer allocation", replacement.CoveredResponsibilities);
        Assert.Contains("Flat and world vertex-buffer subdata updates", replacement.CoveredResponsibilities);
        Assert.Contains("Vertex and index buffer disposed-state reporting", replacement.CoveredResponsibilities);
        Assert.Contains("Texture disposed-state reporting", replacement.CoveredResponsibilities);
        Assert.Contains("Texture format metadata and 2D/cube allocation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device texture operation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface-entry chunk metadata and bounds model", replacement.CoveredResponsibilities);
        Assert.Contains("Surface manager vertex chunk and buffer allocation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface buffer hole allocation and free-entry planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface update entry application and chunk reuse planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface render pass visibility filtering and texture grouping", replacement.CoveredResponsibilities);
        Assert.Contains("Surface render draw-command and vertex-buffer binding planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface buffer resource reload upload planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface buffer reset invalidation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface buffer unload and reload resource-state planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label font selection and legacy scale planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label size, alignment, transform, culling, and quad planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label plain and background image drawing planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label render dispatch planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label render-state planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label dirty-state and resource invalidation planning", replacement.CoveredResponsibilities);
    }

    [Fact]
    public void CurrentContractDocumentsShaderCompilerReplacement()
    {
        RendererArchitectureReplacement replacement = RendererArchitectureModel.Current;

        Assert.True(RendererArchitectureModel.HasDocumentedShaderCompilerReplacement(replacement));
        Assert.Contains("uniform-location caching", replacement.ShaderManagerReplacement);
        Assert.Contains("Runtime shader compile, link, disposal, and uniform lookup caching", replacement.CoveredResponsibilities);
        Assert.Contains("Shader disposed-state reporting", replacement.CoveredResponsibilities);
    }

    [Fact]
    public void CurrentContractKeepsFullRenderParityGapsExplicit()
    {
        RendererArchitectureReplacement replacement = RendererArchitectureModel.Current;

        Assert.Contains("Full UDB render-pass graph", replacement.RemainingGaps);
        Assert.Contains("Live text font texture generation and GL execution", replacement.RemainingGaps);
        Assert.Contains("Complete visual-mode rendering parity", replacement.RemainingGaps);
    }

    [Fact]
    public void SummaryNamesDeviceShaderAndManagerStrategies()
    {
        string summary = RendererArchitectureModel.Summary(RendererArchitectureModel.Current);

        Assert.Contains("Silk.NET OpenGL", summary);
        Assert.Contains("Runtime GLSL", summary);
        Assert.Contains("GLShaderManager", summary);
    }

    [Fact]
    public void RenderDeviceExposesUdbLengthFormatVertexBufferUpload()
    {
        var overload = typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.SetBufferData),
            new[] { typeof(VertexBuffer), typeof(int), typeof(VertexFormat) });

        Assert.NotNull(overload);
    }

    [Fact]
    public void RenderDeviceExposesUdbVertexBufferSubdataUploads()
    {
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.SetBufferSubdata),
            new[] { typeof(VertexBuffer), typeof(long), typeof(FlatVertex[]) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.SetBufferSubdata),
            new[] { typeof(VertexBuffer), typeof(long), typeof(WorldVertex[]) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.SetBufferSubdata),
            new[] { typeof(VertexBuffer), typeof(FlatVertex[]), typeof(long) }));
    }

    [Fact]
    public void MeshExposesUdbIndexedTriangleWrapperSurface()
    {
        Assert.NotNull(typeof(Mesh).GetConstructor(new[] { typeof(RenderDevice), typeof(WorldVertex[]), typeof(int[]) }));
        Assert.NotNull(typeof(Mesh).GetMethod(nameof(Mesh.Draw), new[] { typeof(RenderDevice) }));
        Assert.NotNull(typeof(Mesh).GetProperty(nameof(Mesh.PrimitivesCount)));
        Assert.Contains(typeof(IDisposable), typeof(Mesh).GetInterfaces());
    }

    [Fact]
    public void BufferWrappersExposeUdbDisposedState()
    {
        Assert.NotNull(typeof(VertexBuffer).GetProperty(nameof(VertexBuffer.Disposed)));
        Assert.NotNull(typeof(IndexBuffer).GetProperty(nameof(IndexBuffer.Disposed)));
    }

    [Fact]
    public void TextureWrapperExposesUdbDisposedState()
    {
        Assert.NotNull(typeof(Texture).GetProperty(nameof(Texture.Disposed)));
    }

    [Fact]
    public void RenderDeviceExposesUdbDisposedState()
    {
        Assert.NotNull(typeof(RenderDevice).GetProperty(nameof(RenderDevice.Disposed)));
    }

    [Fact]
    public void RenderDeviceExposesUdbAlphaTestCompatibilityState()
    {
        Assert.NotNull(typeof(RenderDevice).GetProperty(nameof(RenderDevice.AlphaTestEnabled)));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetAlphaTestEnable), new[] { typeof(bool) }));

        RenderStateTogglePlan plan = RenderDevice.BuildAlphaTestPlan(enabled: true);

        Assert.Equal(RenderStateToggleKind.AlphaTest, plan.Kind);
        Assert.True(plan.Enabled);
    }

    [Fact]
    public void RenderDeviceExposesUdbMultisampleAntialiasCompatibilityState()
    {
        Assert.NotNull(typeof(RenderDevice).GetProperty(nameof(RenderDevice.MultisampleAntialiasEnabled)));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetMultisampleAntialias), new[] { typeof(bool) }));

        RenderStateTogglePlan plan = RenderDevice.BuildMultisampleAntialiasPlan(enabled: true);

        Assert.Equal(RenderStateToggleKind.MultisampleAntialias, plan.Kind);
        Assert.True(plan.Enabled);
    }

    [Fact]
    public void RenderDeviceExposesUdbSamplerFilterOverloads()
    {
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.SetSamplerFilter),
            new[] { typeof(TextureFilter), typeof(int) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.SetSamplerFilter),
            new[] { typeof(TextureFilter), typeof(TextureFilter), typeof(MipmapFilter), typeof(float), typeof(int) }));

        SamplerFilterPlan single = RenderDevice.BuildSamplerFilterPlan(TextureFilter.Linear, unit: 2);
        SamplerFilterPlan detailed = RenderDevice.BuildSamplerFilterPlan(
            TextureFilter.Nearest,
            TextureFilter.Linear,
            MipmapFilter.Nearest,
            maxAnisotropy: 4.0f,
            unit: 3);

        Assert.Equal(TextureFilter.Linear, single.MinFilter);
        Assert.Equal(TextureFilter.Linear, single.MagFilter);
        Assert.Equal(MipmapFilter.None, single.MipFilter);
        Assert.Equal(0.0f, single.MaxAnisotropy);
        Assert.Equal(2, single.Unit);
        Assert.Equal(TextureFilter.Nearest, detailed.MinFilter);
        Assert.Equal(TextureFilter.Linear, detailed.MagFilter);
        Assert.Equal(MipmapFilter.Nearest, detailed.MipFilter);
        Assert.Equal(4.0f, detailed.MaxAnisotropy);
        Assert.Equal(3, detailed.Unit);
    }

    [Fact]
    public void RenderDeviceBuildsUdbStartRenderingPlans()
    {
        RenderStartPlan backbuffer = RenderDevice.BuildStartRenderingPlan(clear: true, clearColorArgb: 0xff112233);
        RenderStartPlan target = RenderDevice.BuildStartRenderingPlan(clear: false, clearColorArgb: 0xff445566, target: null, useDepthBuffer: false);

        Assert.True(backbuffer.Clear);
        Assert.Equal(0xff112233u, backbuffer.ClearColorArgb);
        Assert.False(backbuffer.HasTarget);
        Assert.True(backbuffer.UseDepthBuffer);
        Assert.False(target.Clear);
        Assert.Equal(0xff445566u, target.ClearColorArgb);
        Assert.False(target.HasTarget);
        Assert.False(target.UseDepthBuffer);
    }

    [Fact]
    public void RenderDeviceBuildsUdbDrawOperationPlans()
    {
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.Draw),
            new[] { typeof(PrimitiveType), typeof(int), typeof(int) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.Draw),
            new[] { typeof(PrimitiveType), typeof(int), typeof(int), typeof(FlatVertex[]) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.DrawIndexed),
            new[] { typeof(PrimitiveType), typeof(int), typeof(int) }));

        DrawOperationPlan draw = RenderDevice.BuildDrawPlan(PrimitiveType.TriangleStrip, startIndex: 2, primitiveCount: 4);
        DrawOperationPlan indexed = RenderDevice.BuildDrawIndexedPlan(PrimitiveType.TriangleList, startIndex: 6, primitiveCount: 8);
        DrawOperationPlan data = RenderDevice.BuildDrawDataPlan(
            PrimitiveType.LineList,
            startIndex: 1,
            primitiveCount: 3,
            new[] { new FlatVertex(), new FlatVertex(), new FlatVertex() });

        Assert.Equal(DrawOperationKind.Draw, draw.Kind);
        Assert.Equal(PrimitiveType.TriangleStrip, draw.PrimitiveType);
        Assert.Equal(2, draw.StartIndex);
        Assert.Equal(4, draw.PrimitiveCount);
        Assert.Equal(DrawOperationKind.DrawIndexed, indexed.Kind);
        Assert.Equal(DrawOperationKind.DrawData, data.Kind);
        Assert.Equal(3, data.InlineVertexCount);
    }

    [Fact]
    public void ShaderWrapperExposesDisposedState()
    {
        Assert.NotNull(typeof(Shader).GetProperty(nameof(Shader.Disposed)));
    }
}
