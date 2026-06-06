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
            "Cull, depth, fill, blend, sampler, and texture state",
            "Flat and world vertex-buffer attribute binding",
            "Length-based vertex-buffer allocation",
            "Flat and world vertex-buffer subdata updates",
            "Index-buffer binding and primitive draw dispatch",
            "Owned indexed-triangle mesh wrapper",
            "Runtime shader compile, link, disposal, and uniform lookup caching",
        },
        RemainingGaps: new[]
        {
            "Full UDB render-pass graph",
            "Surface manager and surface-entry lifecycle",
            "Full mesh behavior",
            "Text font and label rendering",
            "Complete visual-mode rendering parity",
        });

    public static string Summary(RendererArchitectureReplacement replacement)
        => $"{replacement.DeviceApi}; shaders: {replacement.ShaderCompilerStrategy}; manager: {replacement.ShaderManagerReplacement}.";

    public static bool HasDocumentedShaderCompilerReplacement(RendererArchitectureReplacement replacement)
        => replacement.ShaderCompilerStrategy.Contains("Runtime GLSL", StringComparison.Ordinal)
            && replacement.ShaderCompilerStrategy.Contains(nameof(Shader), StringComparison.Ordinal)
            && replacement.ShaderManagerReplacement.Contains("GLShaderManager", StringComparison.Ordinal);
}
