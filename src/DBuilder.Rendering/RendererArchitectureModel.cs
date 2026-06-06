// ABOUTME: Documents the DBuilder renderer replacement contract for UDB native OpenGL pieces.
// ABOUTME: Keeps renderer architecture status testable while full render parity is still in progress.

namespace DBuilder.Rendering;

public sealed record RendererArchitectureReplacement(
    string DeviceApi,
    string ShaderCompilerStrategy,
    string ShaderManagerReplacement,
    string MinimumGlProfile,
    IReadOnlyList<string> CoveredResponsibilities,
    IReadOnlyList<string> RemainingGaps);

public static class RendererArchitectureModel
{
    public static RendererArchitectureReplacement Current { get; } = new(
        DeviceApi: "Silk.NET OpenGL through Avalonia GL contexts and standalone Silk window hosts",
        ShaderCompilerStrategy: "Runtime GLSL compilation through DBuilder.Rendering.Shader",
        ShaderManagerReplacement: "Direct Shader instances with uniform-location caching replace UDB GLShader and GLShaderManager for the current renderer subset",
        MinimumGlProfile: "Desktop OpenGL 3.3 core profile",
        CoveredResponsibilities: new[]
        {
            "Viewport and clear state",
            "Render-device disposed-state reporting",
            "2D presentation layer stack planning",
            "2D presentation draw-command planning",
            "2D presentation blend-factor application planning",
            "2D presentation render-target lifecycle planning",
            "2D presentation plotter and texture target allocation planning",
            "2D presentation display shader settings planning",
            "2D presentation display uniform application planning",
            "2D presentation frame operation sequence planning",
            "2D presentation render-layer mask planning",
            "2D presentation set-presentation overlay lifecycle planning",
            "2D presentation render-target transform reset and redraw planning",
            "2D presentation renderer lifecycle operation planning",
            "2D presentation render-target destroy sequence planning",
            "2D presentation render-target create sequence planning",
            "2D presentation background draw availability planning",
            "2D presentation background vertex-buffer restore planning",
            "2D presentation layer draw dispatch argument planning",
            "2D presentation layer texture binding planning",
            "Render-device alpha-test compatibility state planning",
            "Render-device multisample antialias compatibility state planning",
            "Render-device sampler-filter overload planning",
            "Render-device setup settings planning",
            "Render-device setup settings state application",
            "Render-device resource registration lifecycle planning",
            "Render-device target start-rendering planning",
            "Render-device inline vertex draw planning and overload",
            "Render-device finish and present frame handoff planning",
            "Cull, depth, fill, blend, sampler, and texture state",
            "Flat and world vertex-buffer attribute binding",
            "Length-based vertex-buffer allocation",
            "Flat and world vertex-buffer subdata updates",
            "Vertex and index buffer disposed-state reporting",
            "Index-buffer binding and primitive draw dispatch",
            "Texture disposed-state reporting",
            "Base texture lifecycle and render-device binding surface",
            "Texture format metadata and 2D/cube allocation planning",
            "Texture 2D format allocation application",
            "Render-device texture operation planning",
            "Render-device 2D texture clear and pixel upload application",
            "Render-device unsafe plotter pixel upload surface",
            "Cube texture resource and render-device cube operation surface",
            "Owned indexed-triangle mesh wrapper",
            "Mesh disposal and finalizer lifecycle",
            "Surface-entry chunk metadata and bounds model",
            "Surface manager vertex chunk and buffer allocation planning",
            "Surface buffer hole allocation and free-entry planning",
            "Surface update entry application and chunk reuse planning",
            "Surface render pass visibility filtering and texture grouping",
            "Surface render texture fallback resolution planning",
            "Surface render draw-command and vertex-buffer binding planning",
            "Surface render shader and sampler state planning",
            "Surface buffer resource reload upload planning",
            "Surface buffer reset invalidation planning",
            "Surface buffer unload and reload resource-state planning",
            "Surface locked-buffer unlock lifecycle planning",
            "Surface manager resource registration and lifecycle planning",
            "Text font byte-indexed glyph table planning",
            "Text font configuration metric normalization",
            "Text font glyph metrics and vertex planning",
            "Text label font selection and legacy scale planning",
            "Text label size, alignment, transform, culling, and quad planning",
            "Text label plain and background image drawing planning",
            "Text label render dispatch planning",
            "Text label render-state planning",
            "Text label dirty-state and resource invalidation planning",
            "Text label texture and vertex-buffer update planning",
            "Text label viewport inclusion planning",
            "Runtime shader compile, link, disposal, and uniform lookup caching",
            "Shader disposed-state reporting",
        },
        RemainingGaps: new[]
        {
            "Full UDB render-pass graph",
            "Surface manager and surface-entry lifecycle",
            "Full mesh behavior",
            "Live text font texture generation and GL execution",
            "Complete visual-mode rendering parity",
        });

    public static string Summary(RendererArchitectureReplacement replacement)
        => $"{replacement.DeviceApi}; shaders: {replacement.ShaderCompilerStrategy}; manager: {replacement.ShaderManagerReplacement}.";

    public static bool HasDocumentedShaderCompilerReplacement(RendererArchitectureReplacement replacement)
        => replacement.ShaderCompilerStrategy.Contains("Runtime GLSL", StringComparison.Ordinal)
            && replacement.ShaderCompilerStrategy.Contains(nameof(Shader), StringComparison.Ordinal)
            && replacement.ShaderManagerReplacement.Contains("GLShaderManager", StringComparison.Ordinal);
}
