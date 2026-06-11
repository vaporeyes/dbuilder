// ABOUTME: Verifies the documented renderer replacement contract for UDB native OpenGL behavior.
// ABOUTME: Covers the Silk.NET device path, runtime shader compiler strategy, and remaining gaps.

using DBuilder.Rendering;
using System.Reflection;

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
        Assert.Contains("2D presentation blend-factor application planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation render-target lifecycle planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation plotter and texture target allocation planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation display shader settings planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation display uniform application planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation display render-settings vector planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation projection transform planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation frame operation sequence planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation frame setup and release binding planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation render-layer mask planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation set-presentation overlay lifecycle planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation render-target transform reset and redraw planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation renderer lifecycle operation planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation render-target destroy sequence planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation render-target create sequence planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation background draw availability planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation background vertex-buffer restore planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation layer draw dispatch argument planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation layer texture binding planning", replacement.CoveredResponsibilities);
        Assert.Contains("2D presentation per-layer draw operation sequence planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device alpha-test compatibility state planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device multisample antialias compatibility state planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device sampler-filter overload planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device setup settings planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device setup settings state application", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device resource registration lifecycle planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device target start-rendering planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device Color4 start-rendering overload planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device inline vertex draw planning and overload", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device finish and present frame handoff planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device named shader and uniform operation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device named shader and uniform source-compatible method surface", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device named uniform payload conversion planning", replacement.CoveredResponsibilities);
        Assert.Contains("Index-buffer binding and primitive draw dispatch", replacement.CoveredResponsibilities);
        Assert.Contains("Length-based vertex-buffer allocation", replacement.CoveredResponsibilities);
        Assert.Contains("Flat and world vertex-buffer subdata updates", replacement.CoveredResponsibilities);
        Assert.Contains("Vertex and index buffer upload byte-size planning", replacement.CoveredResponsibilities);
        Assert.Contains("Vertex and index buffer disposed-state reporting", replacement.CoveredResponsibilities);
        Assert.Contains("Texture disposed-state reporting", replacement.CoveredResponsibilities);
        Assert.Contains("Base texture lifecycle and render-device binding surface", replacement.CoveredResponsibilities);
        Assert.Contains("Texture format metadata and 2D/cube allocation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Texture allocation validation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Texture 2D format allocation application", replacement.CoveredResponsibilities);
        Assert.Contains("Texture pixel upload byte-size planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device texture operation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device Color4 texture clear overload planning", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device 2D texture clear and pixel upload application", replacement.CoveredResponsibilities);
        Assert.Contains("Render-device unsafe plotter pixel upload surface", replacement.CoveredResponsibilities);
        Assert.Contains("Cube texture resource and render-device cube operation surface", replacement.CoveredResponsibilities);
        Assert.Contains("Mesh construction, draw, and dispose operation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Mesh disposal and finalizer lifecycle", replacement.CoveredResponsibilities);
        Assert.Contains("Surface-entry chunk metadata and bounds model", replacement.CoveredResponsibilities);
        Assert.Contains("Surface manager vertex chunk and buffer allocation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface buffer hole allocation and free-entry planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface update entry application and chunk reuse planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface render pass visibility filtering and texture grouping", replacement.CoveredResponsibilities);
        Assert.Contains("Surface render texture fallback resolution planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface render draw-command and vertex-buffer binding planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface render shader and sampler state planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface buffer resource reload upload planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface buffer reset invalidation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface buffer unload and reload resource-state planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface locked-buffer unlock lifecycle planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface manager resource registration and lifecycle planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface manager map-analysis allocation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface manager update buffer upload planning", replacement.CoveredResponsibilities);
        Assert.Contains("Surface manager update and free surface-entry lifecycle planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text font byte-indexed glyph table planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text font Font.cfg resource discovery and glyph-source parsing", replacement.CoveredResponsibilities);
        Assert.Contains("Text font configuration metric normalization", replacement.CoveredResponsibilities);
        Assert.Contains("Text font glyph metrics and vertex planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label constructor and dispose lifecycle planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label font selection and legacy scale planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label compatibility property mutation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label UDB setter invalidation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label size, alignment, transform, culling, and quad planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label plain and background image drawing planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label render dispatch planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label render-state planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label dirty-state and resource invalidation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label skipped-resource invalidation planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label texture and vertex-buffer update planning", replacement.CoveredResponsibilities);
        Assert.Contains("Text label viewport inclusion planning", replacement.CoveredResponsibilities);
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
        Assert.Contains("Live surface manager GL execution", replacement.RemainingGaps);
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
        MethodInfo? finalizer = typeof(Mesh).GetMethod("Finalize", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(finalizer);
        Assert.Equal(typeof(Mesh), finalizer.DeclaringType);
    }

    [Fact]
    public void BufferWrappersExposeUdbDisposedState()
    {
        Assert.NotNull(typeof(VertexBuffer).GetProperty(nameof(VertexBuffer.Disposed)));
        Assert.NotNull(typeof(IndexBuffer).GetProperty(nameof(IndexBuffer.Disposed)));

        MethodInfo? vertexFinalizer = typeof(VertexBuffer).GetMethod("Finalize", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo? indexFinalizer = typeof(IndexBuffer).GetMethod("Finalize", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(vertexFinalizer);
        Assert.NotNull(indexFinalizer);
        Assert.Equal(typeof(VertexBuffer), vertexFinalizer.DeclaringType);
        Assert.Equal(typeof(IndexBuffer), indexFinalizer.DeclaringType);
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
    public void RenderDeviceBuildsUdbSetupSettingsPlan()
    {
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.SetupSettings),
            new[] { typeof(bool), typeof(bool), typeof(float) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.SetupSettings),
            new[] { typeof(RenderDeviceSetupSettingsPlan) }));

        RenderDeviceSetupSettingsPlan plan = RenderDevice.BuildSetupSettingsPlan(
            visualBilinear: true,
            antialiasingEnabled: true,
            filterAnisotropy: 8.0f);

        Assert.Equal(RenderStateToggleKind.AlphaBlend, plan.AlphaBlend.Kind);
        Assert.False(plan.AlphaBlend.Enabled);
        Assert.Equal(RenderStateToggleKind.AlphaTest, plan.AlphaTest.Kind);
        Assert.False(plan.AlphaTest.Enabled);
        Assert.Equal(Cull.None, plan.CullMode);
        Assert.Equal(Blend.InverseSourceAlpha, plan.DestinationBlend);
        Assert.Equal(FillMode.Solid, plan.FillMode);
        Assert.Equal(RenderStateToggleKind.MultisampleAntialias, plan.MultisampleAntialias.Kind);
        Assert.True(plan.MultisampleAntialias.Enabled);
        Assert.Equal(Blend.SourceAlpha, plan.SourceBlend);
        Assert.Equal(RenderStateToggleKind.Depth, plan.Depth.Kind);
        Assert.False(plan.Depth.Enabled);
        Assert.Equal(RenderStateToggleKind.DepthWrite, plan.DepthWrite.Kind);
        Assert.False(plan.DepthWrite.Enabled);
        Assert.Equal(TextureAddress.Wrap, plan.SamplerAddress);
        Assert.True(plan.InitializePresentation);
    }

    [Fact]
    public void RenderDeviceSetupSettingsPlanUsesUdbVisualBilinearFilters()
    {
        RenderDeviceSetupSettingsPlan nearest = RenderDevice.BuildSetupSettingsPlan(
            visualBilinear: false,
            antialiasingEnabled: false,
            filterAnisotropy: 2.0f);
        RenderDeviceSetupSettingsPlan linear = RenderDevice.BuildSetupSettingsPlan(
            visualBilinear: true,
            antialiasingEnabled: false,
            filterAnisotropy: 4.0f);

        Assert.Equal(TextureFilter.Nearest, nearest.SamplerFilter.MinFilter);
        Assert.Equal(TextureFilter.Nearest, nearest.SamplerFilter.MagFilter);
        Assert.Equal(MipmapFilter.Nearest, nearest.SamplerFilter.MipFilter);
        Assert.Equal(2.0f, nearest.SamplerFilter.MaxAnisotropy);
        Assert.False(nearest.MultisampleAntialias.Enabled);
        Assert.Equal(TextureFilter.Linear, linear.SamplerFilter.MinFilter);
        Assert.Equal(TextureFilter.Linear, linear.SamplerFilter.MagFilter);
        Assert.Equal(MipmapFilter.Linear, linear.SamplerFilter.MipFilter);
        Assert.Equal(4.0f, linear.SamplerFilter.MaxAnisotropy);
    }

    [Fact]
    public void RenderDeviceBuildsUdbStartRenderingPlans()
    {
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.StartRendering),
            new[] { typeof(bool), typeof(Color4) }));

        RenderStartPlan backbuffer = RenderDevice.BuildStartRenderingPlan(clear: true, clearColorArgb: 0xff112233);
        RenderStartPlan target = RenderDevice.BuildStartRenderingPlan(clear: false, clearColorArgb: 0xff445566, target: null, useDepthBuffer: false);
        RenderStartPlan colorBackbuffer = RenderDevice.BuildStartRenderingPlan(clear: true, new Color4(unchecked((int)0xff112233)));
        RenderStartPlan colorTarget = RenderDevice.BuildStartRenderingPlan(clear: false, new Color4(unchecked((int)0xff445566)), target: null, useDepthBuffer: false);

        Assert.True(backbuffer.Clear);
        Assert.Equal(0xff112233u, backbuffer.ClearColorArgb);
        Assert.False(backbuffer.HasTarget);
        Assert.True(backbuffer.UseDepthBuffer);
        Assert.False(target.Clear);
        Assert.Equal(0xff445566u, target.ClearColorArgb);
        Assert.False(target.HasTarget);
        Assert.False(target.UseDepthBuffer);
        Assert.Equal(backbuffer, colorBackbuffer);
        Assert.Equal(target, colorTarget);
    }

    [Fact]
    public void RenderDeviceExposesUdbResourceRegistrationLifecycle()
    {
        Assert.NotNull(typeof(RenderDevice).GetProperty(nameof(RenderDevice.RegisteredResourceCount)));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.RegisterResource), new[] { typeof(IRenderResource) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.UnregisterResource), new[] { typeof(IRenderResource) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.UnloadRegisteredResources), Type.EmptyTypes));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.ReloadRegisteredResources), Type.EmptyTypes));
        Assert.NotNull(typeof(IRenderResource).GetMethod(nameof(IRenderResource.UnloadResource), Type.EmptyTypes));
        Assert.NotNull(typeof(IRenderResource).GetMethod(nameof(IRenderResource.ReloadResource), Type.EmptyTypes));

        RenderResourceRegistrationPlan register = RenderDevice.BuildResourceRegistrationPlan(
            RenderResourceRegistrationKind.Register,
            wasRegistered: false);
        RenderResourceRegistrationPlan duplicate = RenderDevice.BuildResourceRegistrationPlan(
            RenderResourceRegistrationKind.Register,
            wasRegistered: true);
        RenderResourceRegistrationPlan unregister = RenderDevice.BuildResourceRegistrationPlan(
            RenderResourceRegistrationKind.Unregister,
            wasRegistered: true);

        Assert.Equal(RenderResourceRegistrationKind.Register, register.Kind);
        Assert.True(register.WillBeRegistered);
        Assert.True(register.ChangesRegistry);
        Assert.False(duplicate.ChangesRegistry);
        Assert.Equal(RenderResourceRegistrationKind.Unregister, unregister.Kind);
        Assert.False(unregister.WillBeRegistered);
        Assert.True(unregister.ChangesRegistry);
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
    public void RenderDeviceExposesUdbFinishAndPresentFrameOperations()
    {
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.FinishRendering), Type.EmptyTypes));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.Present), Type.EmptyTypes));

        RenderFrameOperationPlan finish = RenderDevice.BuildFinishRenderingPlan();
        RenderFrameOperationPlan present = RenderDevice.BuildPresentPlan();

        Assert.Equal(RenderFrameOperationKind.FinishRendering, finish.Kind);
        Assert.False(finish.FlushCommands);
        Assert.Equal(RenderFrameOperationKind.Present, present.Kind);
        Assert.True(present.FlushCommands);
    }

    [Fact]
    public void RenderDeviceBuildsUdbNamedShaderAndUniformOperationPlans()
    {
        RenderShaderOperationPlan uniformDeclaration = RenderDevice.BuildDeclareUniformPlan(
            UniformName.projection,
            "projection",
            UniformType.Mat4);
        RenderShaderOperationPlan shaderDeclaration = RenderDevice.BuildDeclareShaderPlan(
            ShaderName.display2d_normal,
            "display2d.vert",
            "display2d.frag");
        RenderShaderOperationPlan shaderCompilation = RenderDevice.BuildCompileShaderPlan(
            ShaderName.world3d_main,
            "world3d.shader",
            "world3d_main");
        RenderShaderOperationPlan shaderBinding = RenderDevice.BuildSetShaderPlan(ShaderName.world3d_skybox);
        RenderShaderOperationPlan scalarUniform = RenderDevice.BuildSetUniformPlan(UniformName.desaturation, UniformType.Float);
        RenderShaderOperationPlan arrayUniform = RenderDevice.BuildSetUniformPlan(
            UniformName.lightPosAndRadius,
            UniformType.Vec4fArray,
            valueCount: 3);

        Assert.Equal(RenderShaderOperationKind.DeclareUniform, uniformDeclaration.Kind);
        Assert.Equal(UniformName.projection, uniformDeclaration.UniformName);
        Assert.Equal(UniformType.Mat4, uniformDeclaration.UniformType);
        Assert.Equal("projection", uniformDeclaration.UniformVariableName);
        Assert.Equal(RenderShaderOperationKind.DeclareShader, shaderDeclaration.Kind);
        Assert.Equal(ShaderName.display2d_normal, shaderDeclaration.ShaderName);
        Assert.Equal("display2d.vert", shaderDeclaration.VertexResourceName);
        Assert.Equal("display2d.frag", shaderDeclaration.FragmentResourceName);
        Assert.Equal(RenderShaderOperationKind.CompileShader, shaderCompilation.Kind);
        Assert.Equal("world3d.shader", shaderCompilation.ShaderGroupName);
        Assert.Equal("world3d_main", shaderCompilation.ShaderEntryName);
        Assert.Equal(RenderShaderOperationKind.SetShader, shaderBinding.Kind);
        Assert.Equal(ShaderName.world3d_skybox, shaderBinding.ShaderName);
        Assert.Equal(RenderShaderOperationKind.SetUniform, scalarUniform.Kind);
        Assert.Equal(UniformName.desaturation, scalarUniform.UniformName);
        Assert.Equal(4, scalarUniform.ValueByteSize);
        Assert.Equal(1, scalarUniform.ValueCount);
        Assert.Equal(3, arrayUniform.ValueCount);
        Assert.Equal(48, arrayUniform.ValueByteSize);
    }

    [Fact]
    public void RenderDeviceExposesUdbNamedShaderAndUniformMethods()
    {
        Assert.NotNull(typeof(RenderDevice).GetProperty(nameof(RenderDevice.LastShaderOperation)));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.DeclareUniform),
            new[] { typeof(UniformName), typeof(string), typeof(UniformType) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.DeclareShader),
            new[] { typeof(ShaderName), typeof(string), typeof(string) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.CompileShader),
            new[] { typeof(ShaderName), typeof(string), typeof(string) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetShader), new[] { typeof(ShaderName) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(bool) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(float) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(Vector2f) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(Vector3f) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(Vector4f) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(Color4) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(Matrix) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(Matrix).MakeByRefType() }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(int) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(Vector2i) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(Vector3i) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(Vector4i) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(Vector2f[]) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(Vector3f[]) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(nameof(RenderDevice.SetUniform), new[] { typeof(UniformName), typeof(Vector4f[]) }));
    }

    [Fact]
    public void RenderDeviceBuildsUdbNamedUniformPayloadPlans()
    {
        Matrix matrix = Matrix.Identity;
        matrix.M12 = 2.0f;
        matrix.M43 = 3.0f;

        RenderShaderOperationPlan boolPlan = RenderDevice.BuildSetUniformPlan(UniformName.lightsEnabled, true);
        RenderShaderOperationPlan colorPlan = RenderDevice.BuildSetUniformPlan(UniformName.vertexColor, new Color4(0.1f, 0.2f, 0.3f, 0.4f));
        RenderShaderOperationPlan matrixPlan = RenderDevice.BuildSetUniformPlan(UniformName.world, matrix);
        RenderShaderOperationPlan intVectorPlan = RenderDevice.BuildSetUniformPlan(UniformName.colormapSize, new Vector2i(320, 200));
        RenderShaderOperationPlan arrayPlan = RenderDevice.BuildSetUniformPlan(
            UniformName.lightPosAndRadius,
            new[] { new Vector4f(1.0f, 2.0f, 3.0f, 4.0f), new Vector4f(5.0f, 6.0f, 7.0f, 8.0f) });
        RenderShaderOperationPlan emptyArrayPlan = RenderDevice.BuildSetUniformPlan(
            UniformName.light2Radius,
            Array.Empty<Vector2f>());

        Assert.Equal(UniformType.Float, boolPlan.UniformType);
        Assert.Equal(new[] { 1.0f }, boolPlan.FloatValues);
        Assert.Null(boolPlan.IntValues);
        Assert.Equal(4, boolPlan.ValueByteSize);
        Assert.Equal(UniformType.Vec4f, colorPlan.UniformType);
        Assert.Equal(new[] { 0.1f, 0.2f, 0.3f, 0.4f }, colorPlan.FloatValues);
        Assert.Equal(UniformType.Mat4, matrixPlan.UniformType);
        Assert.Equal(new[] { 1.0f, 2.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 3.0f, 1.0f }, matrixPlan.FloatValues);
        Assert.Equal(64, matrixPlan.ValueByteSize);
        Assert.Equal(UniformType.Vec2i, intVectorPlan.UniformType);
        Assert.Equal(new[] { 320, 200 }, intVectorPlan.IntValues);
        Assert.Null(intVectorPlan.FloatValues);
        Assert.Equal(8, intVectorPlan.ValueByteSize);
        Assert.Equal(UniformType.Vec4fArray, arrayPlan.UniformType);
        Assert.Equal(2, arrayPlan.ValueCount);
        Assert.Equal(32, arrayPlan.ValueByteSize);
        Assert.Equal(new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f }, arrayPlan.FloatValues);
        Assert.Equal(0, emptyArrayPlan.ValueCount);
        Assert.Equal(0, emptyArrayPlan.ValueByteSize);
        Assert.Empty(emptyArrayPlan.FloatValues!);
    }

    [Fact]
    public void ShaderWrapperExposesDisposedState()
    {
        Assert.NotNull(typeof(Shader).GetProperty(nameof(Shader.Disposed)));
    }
}
