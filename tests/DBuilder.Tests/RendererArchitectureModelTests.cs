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
        Assert.Contains("Index-buffer binding and primitive draw dispatch", replacement.CoveredResponsibilities);
        Assert.Contains("Length-based vertex-buffer allocation", replacement.CoveredResponsibilities);
        Assert.Contains("Flat and world vertex-buffer subdata updates", replacement.CoveredResponsibilities);
    }

    [Fact]
    public void CurrentContractDocumentsShaderCompilerReplacement()
    {
        RendererArchitectureReplacement replacement = RendererArchitectureModel.Current;

        Assert.True(RendererArchitectureModel.HasDocumentedShaderCompilerReplacement(replacement));
        Assert.Contains("uniform-location caching", replacement.ShaderManagerReplacement);
        Assert.Contains("Runtime shader compile, link, disposal, and uniform lookup caching", replacement.CoveredResponsibilities);
    }

    [Fact]
    public void CurrentContractKeepsFullRenderParityGapsExplicit()
    {
        RendererArchitectureReplacement replacement = RendererArchitectureModel.Current;

        Assert.Contains("Full UDB render-pass graph", replacement.RemainingGaps);
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
}
