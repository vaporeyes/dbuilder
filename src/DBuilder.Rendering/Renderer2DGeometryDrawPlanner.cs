// ABOUTME: Plans UDB-style Renderer2D.RenderGeometry triangle-list draws and state.
// ABOUTME: Keeps texture choice, world transform use, and empty input behavior testable.

namespace DBuilder.Rendering;

public readonly record struct Renderer2DGeometryDrawPlan(
    FlatVertex[] Vertices,
    bool ShouldDraw,
    int PrimitiveCount,
    Cull CullMode,
    bool DepthEnabled,
    bool AlphaBlendEnabled,
    bool AlphaTestEnabled,
    bool ResetWorldTransformation,
    bool UseWorldTransformation,
    ShaderName Shader,
    bool BindWhiteTexture,
    bool BindProvidedTexture,
    PrimitiveType PrimitiveType,
    bool UseClassicBilinear);

public static class Renderer2DGeometryDrawPlanner
{
    public static Renderer2DGeometryDrawPlan BuildDrawPlan(
        FlatVertex[] vertices,
        bool hasTexture,
        bool transformCoordinates)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        bool shouldDraw = vertices.Length > 0;
        return new Renderer2DGeometryDrawPlan(
            vertices,
            shouldDraw,
            PrimitiveCount: shouldDraw ? vertices.Length / 3 : 0,
            CullMode: Cull.None,
            DepthEnabled: false,
            AlphaBlendEnabled: false,
            AlphaTestEnabled: false,
            ResetWorldTransformation: false,
            UseWorldTransformation: transformCoordinates,
            Shader: ShaderName.display2d_normal,
            BindWhiteTexture: !hasTexture,
            BindProvidedTexture: hasTexture,
            PrimitiveType: PrimitiveType.TriangleList,
            UseClassicBilinear: true);
    }
}
