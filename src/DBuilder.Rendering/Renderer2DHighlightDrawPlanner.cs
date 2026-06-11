// ABOUTME: Plans UDB-style Renderer2D.RenderHighlight triangle-list draws and state.
// ABOUTME: Keeps fill-color uniform, world transform use, and short input behavior testable.

namespace DBuilder.Rendering;

public readonly record struct Renderer2DHighlightDrawPlan(
    FlatVertex[] Vertices,
    bool ShouldDraw,
    int PrimitiveCount,
    Cull CullMode,
    bool DepthEnabled,
    bool AlphaBlendEnabled,
    bool AlphaTestEnabled,
    bool UseWorldTransformation,
    UniformName FillColorUniform,
    Color4 FillColor,
    float ThingScale,
    ShaderName Shader,
    PrimitiveType PrimitiveType);

public static class Renderer2DHighlightDrawPlanner
{
    public static Renderer2DHighlightDrawPlan BuildDrawPlan(FlatVertex[] vertices, int color)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        bool shouldDraw = vertices.Length >= 3;
        return new Renderer2DHighlightDrawPlan(
            vertices,
            shouldDraw,
            PrimitiveCount: shouldDraw ? vertices.Length / 3 : 0,
            CullMode: Cull.None,
            DepthEnabled: false,
            AlphaBlendEnabled: false,
            AlphaTestEnabled: false,
            UseWorldTransformation: true,
            FillColorUniform: UniformName.FillColor,
            FillColor: new Color4(color),
            ThingScale: 1.0f,
            Shader: ShaderName.things2d_fill,
            PrimitiveType: PrimitiveType.TriangleList);
    }
}
