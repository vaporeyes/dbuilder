// ABOUTME: Plans UDB-style Renderer3D world-geometry collection lifecycle state.
// ABOUTME: Keeps StartGeometry render buckets explicit before live 3D rendering is complete.

using DBuilder.Geometry;

namespace DBuilder.Rendering;

public enum Renderer3DGeometryBucketKind
{
    SolidGeometry,
    MaskedGeometry,
    TranslucentGeometry,
    SkyGeometry,
    SolidThings,
    MaskedThings,
    TranslucentThings,
    MaskedModelThings,
    TranslucentModelThings,
    LightThings,
    AllThings,
    VisualVertices,
}

public enum Renderer3DGeometryCollectionKind
{
    ImageDictionary,
    ModelDictionary,
    List,
}

public sealed record Renderer3DGeometryBucketPlan(
    Renderer3DGeometryBucketKind Kind,
    Renderer3DGeometryCollectionKind CollectionKind,
    bool InitializedEmpty);

public sealed record Renderer3DStartGeometryPlan(IReadOnlyList<Renderer3DGeometryBucketPlan> Buckets)
{
    public bool InitializesAllBuckets => Buckets.All(bucket => bucket.InitializedEmpty);
}

public sealed record Renderer3DFinishGeometryCleanupPlan(
    bool UnbindTexture,
    IReadOnlyList<Renderer3DGeometryBucketKind> ClearedBuckets);

public sealed record Renderer3DFinishGeometryInitialStatePlan(
    Cull CullMode,
    bool DepthEnabled,
    bool DepthWriteEnabled,
    bool AlphaBlendEnabled,
    bool AlphaTestEnabled);

public enum Renderer3DGeometryPassOperationKind
{
    SetIdentityWorld,
    SetWorldUniform,
    RenderSky,
    RenderSinglePass,
    SetAlphaTest,
    SetAlphaBlend,
    SetZWrite,
    SetSourceBlend,
    SetCullMode,
    RenderModels,
    RenderTranslucentPass,
    RenderThingCages,
    RenderVertices,
    RenderSlopeHandles,
    RenderArrows,
}

public sealed record Renderer3DGeometryPassOperation(
    Renderer3DGeometryPassOperationKind Kind,
    Renderer3DGeometryBucketKind? GeometryBucket = null,
    Renderer3DGeometryBucketKind? ThingBucket = null,
    Renderer3DGeometryBucketKind? LightBucket = null,
    Renderer3DGeometryBucketKind? ModelBucket = null,
    bool? Enabled = null,
    Cull? CullMode = null,
    Blend? SourceBlend = null,
    bool? TranslucentModels = null);

public sealed record Renderer3DSkySolidPassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations);

public sealed record Renderer3DModelPassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations)
{
    public bool ShouldRender => Operations.Count > 0;
}

public sealed record Renderer3DMaskPassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations)
{
    public bool ShouldRender => Operations.Count > 0;
}

public sealed record Renderer3DTranslucentPassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations)
{
    public bool ShouldRender => Operations.Count > 0;
}

public sealed record Renderer3DThingCagePassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations)
{
    public bool ShouldRender => Operations.Count > 0;
}

public sealed record Renderer3DVisualVerticesPassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations);

public sealed record Renderer3DSlopeHandlesPassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations)
{
    public bool ShouldRender => Operations.Count > 0;
}

public sealed record Renderer3DEventLinesPassPlan(IReadOnlyList<Renderer3DGeometryPassOperation> Operations)
{
    public bool ShouldRender => Operations.Count > 0;
}

public enum Renderer3DFpsUpdateOperationKind
{
    IncrementFrameCounter,
    SetFpsLabelText,
    ResetFrameCounter,
    RestartStopwatch,
}

public sealed record Renderer3DFpsUpdateOperation(
    Renderer3DFpsUpdateOperationKind Kind,
    int? FrameCount = null,
    string? LabelText = null);

public sealed record Renderer3DFpsUpdatePlan(IReadOnlyList<Renderer3DFpsUpdateOperation> Operations)
{
    public bool ShouldUpdate => Operations.Count > 0;
}

public enum Renderer3DDynamicLightRenderStyle
{
    Normal,
    Vavoom,
    Attenuated,
    Additive,
    Subtractive,
    Lightmap,
}

public sealed record Renderer3DDynamicLightCandidate(
    int Id,
    double CameraDistance,
    bool Visible,
    Renderer3DDynamicLightRenderStyle RenderStyle);

public sealed record Renderer3DDynamicLightUpdatePlan(
    IReadOnlyList<int> SelectedLightIds,
    IReadOnlyList<int> LightOffsets);

public enum Renderer3DThingCageRenderOperationKind
{
    SetAlphaBlend,
    SetAlphaTest,
    SetZWrite,
    SetSourceBlend,
    SetDestinationBlend,
    SetShader,
}

public sealed record Renderer3DThingCageRenderOperation(
    Renderer3DThingCageRenderOperationKind Kind,
    bool? Enabled = null,
    Blend? Blend = null,
    ShaderName? Shader = null);

public sealed record Renderer3DThingCageCandidate(
    int Id,
    int CageColor,
    bool Selected,
    bool Highlighted,
    int CageLength);

public sealed record Renderer3DThingCageDrawPlan(
    int ThingId,
    int Color,
    float Alpha,
    PrimitiveType PrimitiveType,
    int StartIndex,
    int PrimitiveCount);

public sealed record Renderer3DThingCageRenderPlan(
    IReadOnlyList<Renderer3DThingCageRenderOperation> StateOperations,
    IReadOnlyList<Renderer3DThingCageDrawPlan> Draws);

public enum Renderer3DVisualVertexHandleKind
{
    Lower,
    Upper,
}

public sealed record Renderer3DVisualVertexCandidate(
    int Id,
    bool Selected,
    bool Highlighted,
    bool HaveHeightOffset,
    bool CeilingVertex);

public sealed record Renderer3DVisualVertexDrawPlan(
    int VertexId,
    int Color,
    float Alpha,
    Renderer3DVisualVertexHandleKind Handle,
    PrimitiveType PrimitiveType,
    int StartIndex,
    int PrimitiveCount);

public sealed record Renderer3DVisualVertexRenderPlan(
    IReadOnlyList<Renderer3DThingCageRenderOperation> StateOperations,
    IReadOnlyList<Renderer3DVisualVertexDrawPlan> Draws);

public enum Renderer3DSlopeHandleKind
{
    Line,
    Vertex,
}

public sealed record Renderer3DSlopeHandleCandidate(
    int Id,
    Renderer3DSlopeHandleKind Kind,
    bool Pivot,
    bool Selected,
    bool Highlighted,
    bool SmartPivot,
    double Length);

public sealed record Renderer3DSlopeHandleDrawPlan(
    int HandleId,
    int Color,
    Renderer3DSlopeHandleKind Geometry,
    float HandleLength,
    PrimitiveType PrimitiveType,
    int StartIndex,
    int PrimitiveCount);

public sealed record Renderer3DSlopeHandleRenderPlan(
    IReadOnlyList<Renderer3DThingCageRenderOperation> StateOperations,
    IReadOnlyList<Renderer3DSlopeHandleDrawPlan> Draws);

public sealed record Renderer3DEventLineRenderPlan(
    WorldVertex[] Vertices,
    IReadOnlyList<Renderer3DThingCageRenderOperation> StateOperations,
    bool SetIdentityWorld,
    PrimitiveType PrimitiveType,
    int StartIndex,
    int PrimitiveCount,
    bool DisposeVertexBuffer);

public static class Renderer3DGeometryLifecyclePlan
{
    public const float EventLineArrowheadLength = 20.0f;
    public const float EventLineArrowheadHalfAngle = 0.46f;

    public static Renderer3DStartGeometryPlan BuildStartGeometryPlan()
        => new(
            [
                new(Renderer3DGeometryBucketKind.SolidGeometry, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.MaskedGeometry, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.TranslucentGeometry, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.SkyGeometry, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.SolidThings, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.MaskedThings, Renderer3DGeometryCollectionKind.ImageDictionary, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.TranslucentThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.MaskedModelThings, Renderer3DGeometryCollectionKind.ModelDictionary, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.TranslucentModelThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.LightThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
                new(Renderer3DGeometryBucketKind.AllThings, Renderer3DGeometryCollectionKind.List, InitializedEmpty: true),
            ]);

    public static Renderer3DFinishGeometryInitialStatePlan BuildFinishGeometryInitialStatePlan()
        => new(
            Cull.Clockwise,
            DepthEnabled: true,
            DepthWriteEnabled: true,
            AlphaBlendEnabled: false,
            AlphaTestEnabled: false);

    public static Renderer3DSkySolidPassPlan BuildSkySolidPassPlan(int skyGeometryCount)
    {
        if (skyGeometryCount < 0) throw new ArgumentOutOfRangeException(nameof(skyGeometryCount));

        var operations = new List<Renderer3DGeometryPassOperation>();
        if (skyGeometryCount > 0)
        {
            operations.Add(new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetIdentityWorld));
            operations.Add(new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetWorldUniform));
            operations.Add(new Renderer3DGeometryPassOperation(
                Renderer3DGeometryPassOperationKind.RenderSky,
                GeometryBucket: Renderer3DGeometryBucketKind.SkyGeometry));
        }

        operations.Add(new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetIdentityWorld));
        operations.Add(new Renderer3DGeometryPassOperation(Renderer3DGeometryPassOperationKind.SetWorldUniform));
        operations.Add(new Renderer3DGeometryPassOperation(
            Renderer3DGeometryPassOperationKind.RenderSinglePass,
            GeometryBucket: Renderer3DGeometryBucketKind.SolidGeometry,
            ThingBucket: Renderer3DGeometryBucketKind.SolidThings,
            LightBucket: Renderer3DGeometryBucketKind.LightThings));

        return new Renderer3DSkySolidPassPlan(operations);
    }

    public static Renderer3DModelPassPlan BuildMaskedModelPassPlan(int maskedModelThingCount)
    {
        if (maskedModelThingCount < 0) throw new ArgumentOutOfRangeException(nameof(maskedModelThingCount));
        if (maskedModelThingCount == 0) return new Renderer3DModelPassPlan([]);

        return new Renderer3DModelPassPlan(
            [
                new(Renderer3DGeometryPassOperationKind.SetAlphaTest, Enabled: true),
                new(Renderer3DGeometryPassOperationKind.SetCullMode, CullMode: Cull.None),
                new(
                    Renderer3DGeometryPassOperationKind.RenderModels,
                    LightBucket: Renderer3DGeometryBucketKind.LightThings,
                    ModelBucket: Renderer3DGeometryBucketKind.MaskedModelThings,
                    TranslucentModels: false),
                new(Renderer3DGeometryPassOperationKind.SetCullMode, CullMode: Cull.Clockwise),
            ]);
    }

    public static Renderer3DMaskPassPlan BuildMaskPassPlan(int maskedGeometryCount, int maskedThingCount)
    {
        if (maskedGeometryCount < 0) throw new ArgumentOutOfRangeException(nameof(maskedGeometryCount));
        if (maskedThingCount < 0) throw new ArgumentOutOfRangeException(nameof(maskedThingCount));
        if (maskedGeometryCount == 0 && maskedThingCount == 0) return new Renderer3DMaskPassPlan([]);

        return new Renderer3DMaskPassPlan(
            [
                new(Renderer3DGeometryPassOperationKind.SetIdentityWorld),
                new(Renderer3DGeometryPassOperationKind.SetWorldUniform),
                new(Renderer3DGeometryPassOperationKind.SetAlphaTest, Enabled: true),
                new(
                    Renderer3DGeometryPassOperationKind.RenderSinglePass,
                    GeometryBucket: Renderer3DGeometryBucketKind.MaskedGeometry,
                    ThingBucket: Renderer3DGeometryBucketKind.MaskedThings,
                    LightBucket: Renderer3DGeometryBucketKind.LightThings),
            ]);
    }

    public static Renderer3DTranslucentPassPlan BuildTranslucentPassPlan(int translucentGeometryCount, int translucentThingCount)
    {
        if (translucentGeometryCount < 0) throw new ArgumentOutOfRangeException(nameof(translucentGeometryCount));
        if (translucentThingCount < 0) throw new ArgumentOutOfRangeException(nameof(translucentThingCount));
        if (translucentGeometryCount == 0 && translucentThingCount == 0) return new Renderer3DTranslucentPassPlan([]);

        return new Renderer3DTranslucentPassPlan(
            [
                new(Renderer3DGeometryPassOperationKind.SetIdentityWorld),
                new(Renderer3DGeometryPassOperationKind.SetWorldUniform),
                new(Renderer3DGeometryPassOperationKind.SetAlphaBlend, Enabled: true),
                new(Renderer3DGeometryPassOperationKind.SetAlphaTest, Enabled: false),
                new(Renderer3DGeometryPassOperationKind.SetZWrite, Enabled: false),
                new(Renderer3DGeometryPassOperationKind.SetSourceBlend, SourceBlend: Blend.SourceAlpha),
                new(
                    Renderer3DGeometryPassOperationKind.RenderTranslucentPass,
                    GeometryBucket: Renderer3DGeometryBucketKind.TranslucentGeometry,
                    ThingBucket: Renderer3DGeometryBucketKind.TranslucentThings,
                    LightBucket: Renderer3DGeometryBucketKind.LightThings),
            ]);
    }

    public static Renderer3DModelPassPlan BuildTranslucentModelPassPlan(int translucentModelThingCount)
    {
        if (translucentModelThingCount < 0) throw new ArgumentOutOfRangeException(nameof(translucentModelThingCount));
        if (translucentModelThingCount == 0) return new Renderer3DModelPassPlan([]);

        return new Renderer3DModelPassPlan(
            [
                new(Renderer3DGeometryPassOperationKind.SetAlphaBlend, Enabled: true),
                new(Renderer3DGeometryPassOperationKind.SetAlphaTest, Enabled: false),
                new(Renderer3DGeometryPassOperationKind.SetZWrite, Enabled: false),
                new(Renderer3DGeometryPassOperationKind.SetSourceBlend, SourceBlend: Blend.SourceAlpha),
                new(
                    Renderer3DGeometryPassOperationKind.RenderModels,
                    LightBucket: Renderer3DGeometryBucketKind.LightThings,
                    ModelBucket: Renderer3DGeometryBucketKind.TranslucentModelThings,
                    TranslucentModels: true),
            ]);
    }

    public static Renderer3DThingCagePassPlan BuildThingCagePassPlan(bool renderThingCages)
        => renderThingCages
            ? new Renderer3DThingCagePassPlan(
                [
                    new(Renderer3DGeometryPassOperationKind.SetIdentityWorld),
                    new(Renderer3DGeometryPassOperationKind.SetWorldUniform),
                    new(Renderer3DGeometryPassOperationKind.RenderThingCages),
                ])
            : new Renderer3DThingCagePassPlan([]);

    public static Renderer3DVisualVerticesPassPlan BuildVisualVerticesPassPlan()
        => new(
            [
                new(Renderer3DGeometryPassOperationKind.RenderVertices),
            ]);

    public static Renderer3DSlopeHandlesPassPlan BuildSlopeHandlesPassPlan(bool isUdmfMap)
        => isUdmfMap
            ? new Renderer3DSlopeHandlesPassPlan(
                [
                    new(Renderer3DGeometryPassOperationKind.RenderSlopeHandles),
                ])
            : new Renderer3DSlopeHandlesPassPlan([]);

    public static Renderer3DEventLinesPassPlan BuildEventLinesPassPlan(bool showGzEventLines)
        => showGzEventLines
            ? new Renderer3DEventLinesPassPlan(
                [
                    new(Renderer3DGeometryPassOperationKind.RenderArrows),
                ])
            : new Renderer3DEventLinesPassPlan([]);

    public static Renderer3DFpsUpdatePlan BuildFpsUpdatePlan(bool showFps, int currentFps, long elapsedMilliseconds)
    {
        if (currentFps < 0) throw new ArgumentOutOfRangeException(nameof(currentFps));
        if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
        if (!showFps) return new Renderer3DFpsUpdatePlan([]);

        int incrementedFps = currentFps + 1;
        var operations = new List<Renderer3DFpsUpdateOperation>
        {
            new(Renderer3DFpsUpdateOperationKind.IncrementFrameCounter, FrameCount: incrementedFps),
        };

        if (elapsedMilliseconds > 1000)
        {
            operations.Add(new Renderer3DFpsUpdateOperation(Renderer3DFpsUpdateOperationKind.SetFpsLabelText, LabelText: $"{incrementedFps} FPS"));
            operations.Add(new Renderer3DFpsUpdateOperation(Renderer3DFpsUpdateOperationKind.ResetFrameCounter, FrameCount: 0));
            operations.Add(new Renderer3DFpsUpdateOperation(Renderer3DFpsUpdateOperationKind.RestartStopwatch));
        }

        return new Renderer3DFpsUpdatePlan(operations);
    }

    public static Renderer3DDynamicLightUpdatePlan BuildDynamicLightUpdatePlan(
        IReadOnlyList<Renderer3DDynamicLightCandidate> lightCandidates,
        int maxDynamicLights)
    {
        ArgumentNullException.ThrowIfNull(lightCandidates);
        if (maxDynamicLights < 0) throw new ArgumentOutOfRangeException(nameof(maxDynamicLights));
        foreach (Renderer3DDynamicLightCandidate light in lightCandidates)
        {
            if (!double.IsFinite(light.CameraDistance)) throw new ArgumentOutOfRangeException(nameof(lightCandidates));
        }

        Renderer3DDynamicLightCandidate[] selected = lightCandidates
            .OrderBy(light => light.CameraDistance)
            .Where(light => light.Visible)
            .Take(maxDynamicLights)
            .OrderBy(light => DynamicLightRenderStyleSortValue(light.RenderStyle))
            .ToArray();

        int[] lightOffsets = new int[4];
        foreach (Renderer3DDynamicLightCandidate light in selected)
        {
            lightOffsets[DynamicLightOffsetIndex(light.RenderStyle)]++;
        }

        return new Renderer3DDynamicLightUpdatePlan(
            selected.Select(light => light.Id).ToArray(),
            lightOffsets);
    }

    private static int DynamicLightRenderStyleSortValue(Renderer3DDynamicLightRenderStyle renderStyle)
        => renderStyle switch
        {
            Renderer3DDynamicLightRenderStyle.Additive => 25,
            Renderer3DDynamicLightRenderStyle.Vavoom => 50,
            Renderer3DDynamicLightRenderStyle.Attenuated => 98,
            Renderer3DDynamicLightRenderStyle.Lightmap => 98,
            Renderer3DDynamicLightRenderStyle.Normal => 99,
            Renderer3DDynamicLightRenderStyle.Subtractive => 100,
            _ => throw new ArgumentOutOfRangeException(nameof(renderStyle)),
        };

    private static int DynamicLightOffsetIndex(Renderer3DDynamicLightRenderStyle renderStyle)
        => renderStyle switch
        {
            Renderer3DDynamicLightRenderStyle.Normal => 0,
            Renderer3DDynamicLightRenderStyle.Vavoom => 0,
            Renderer3DDynamicLightRenderStyle.Additive => 2,
            Renderer3DDynamicLightRenderStyle.Subtractive => 3,
            Renderer3DDynamicLightRenderStyle.Lightmap => 1,
            Renderer3DDynamicLightRenderStyle.Attenuated => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(renderStyle)),
        };

    public static Renderer3DThingCageRenderPlan BuildThingCageRenderPlan(
        IReadOnlyList<Renderer3DThingCageCandidate> things,
        bool showSelection,
        int selectionColor)
    {
        ArgumentNullException.ThrowIfNull(things);
        foreach (Renderer3DThingCageCandidate thing in things)
        {
            if (thing.CageLength < 0) throw new ArgumentOutOfRangeException(nameof(things));
        }

        return new Renderer3DThingCageRenderPlan(
            [
                new(Renderer3DThingCageRenderOperationKind.SetAlphaBlend, Enabled: true),
                new(Renderer3DThingCageRenderOperationKind.SetAlphaTest, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetZWrite, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetSourceBlend, Blend: Blend.SourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetDestinationBlend, Blend: Blend.SourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetShader, Shader: ShaderName.world3d_constant_color),
            ],
            things.Select(thing => new Renderer3DThingCageDrawPlan(
                thing.Id,
                thing.Selected && showSelection ? selectionColor : thing.CageColor,
                thing.Selected && showSelection || thing.Highlighted ? 1.0f : 0.6f,
                PrimitiveType.LineList,
                StartIndex: 0,
                PrimitiveCount: thing.CageLength)).ToArray());
    }

    public static Renderer3DVisualVertexRenderPlan BuildVisualVertexRenderPlan(
        IReadOnlyList<Renderer3DVisualVertexCandidate>? visualVertices,
        bool showSelection,
        int selectionColor,
        int infoLineColor,
        int verticesColor)
    {
        if (visualVertices == null)
        {
            return new Renderer3DVisualVertexRenderPlan([], []);
        }

        return new Renderer3DVisualVertexRenderPlan(
            [
                new(Renderer3DThingCageRenderOperationKind.SetAlphaBlend, Enabled: true),
                new(Renderer3DThingCageRenderOperationKind.SetAlphaTest, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetZWrite, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetSourceBlend, Blend: Blend.SourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetDestinationBlend, Blend: Blend.SourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetShader, Shader: ShaderName.world3d_constant_color),
            ],
            visualVertices.Select(vertex => new Renderer3DVisualVertexDrawPlan(
                vertex.Id,
                vertex.Selected && showSelection ? selectionColor : vertex.HaveHeightOffset ? infoLineColor : verticesColor,
                vertex.Selected && showSelection || vertex.Highlighted ? 1.0f : 0.6f,
                vertex.CeilingVertex ? Renderer3DVisualVertexHandleKind.Upper : Renderer3DVisualVertexHandleKind.Lower,
                PrimitiveType.LineList,
                StartIndex: 0,
                PrimitiveCount: 8)).ToArray());
    }

    public static Renderer3DSlopeHandleRenderPlan BuildSlopeHandleRenderPlan(
        IReadOnlyList<Renderer3DSlopeHandleCandidate>? slopeHandles,
        bool showSelection,
        int verticesColor,
        int guidelineColor,
        int selectionColor,
        int highlightColor)
    {
        if (slopeHandles == null || !showSelection)
        {
            return new Renderer3DSlopeHandleRenderPlan([], []);
        }

        return new Renderer3DSlopeHandleRenderPlan(
            [
                new(Renderer3DThingCageRenderOperationKind.SetAlphaBlend, Enabled: true),
                new(Renderer3DThingCageRenderOperationKind.SetAlphaTest, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetZWrite, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetSourceBlend, Blend: Blend.SourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetDestinationBlend, Blend: Blend.InverseSourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetShader, Shader: ShaderName.world3d_slope_handle),
            ],
            slopeHandles
                .Where(handle => handle.Kind is Renderer3DSlopeHandleKind.Line or Renderer3DSlopeHandleKind.Vertex)
                .Select(handle => new Renderer3DSlopeHandleDrawPlan(
                    handle.Id,
                    handle.Pivot ? guidelineColor : handle.Selected ? selectionColor : handle.Highlighted ? highlightColor : verticesColor,
                    handle.Kind,
                    handle.Kind == Renderer3DSlopeHandleKind.Line ? (float)handle.Length : 1.0f,
                    PrimitiveType.TriangleList,
                    StartIndex: 0,
                    PrimitiveCount: handle.Kind == Renderer3DSlopeHandleKind.Line ? 2 : 1))
                .ToArray());
    }

    public static Renderer3DEventLineRenderPlan BuildEventLineRenderPlan(IReadOnlyList<Line3D> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var vertices = new List<WorldVertex>(lines.Count * 6);
        foreach (Line3D line in lines)
        {
            if (!line.Start.IsFinite() || !line.End.IsFinite()) throw new ArgumentOutOfRangeException(nameof(lines));

            int color = unchecked((int)line.Color);
            WorldVertex end = Vertex(line.End, color);
            vertices.Add(Vertex(line.Start, color));
            vertices.Add(end);

            if (!line.RenderArrowhead) continue;

            double normalZ = line.GetDelta().GetNormal().z * EventLineArrowheadLength;
            double angle = line.GetAngle();
            Vector3D first = new(
                line.End.x - EventLineArrowheadLength * Math.Sin(angle - EventLineArrowheadHalfAngle),
                line.End.y + EventLineArrowheadLength * Math.Cos(angle - EventLineArrowheadHalfAngle),
                line.End.z - normalZ);
            Vector3D second = new(
                line.End.x - EventLineArrowheadLength * Math.Sin(angle + EventLineArrowheadHalfAngle),
                line.End.y + EventLineArrowheadLength * Math.Cos(angle + EventLineArrowheadHalfAngle),
                line.End.z - normalZ);

            vertices.Add(end);
            vertices.Add(Vertex(first, color));
            vertices.Add(end);
            vertices.Add(Vertex(second, color));
        }

        if (vertices.Count < 2)
        {
            return new Renderer3DEventLineRenderPlan([], [], SetIdentityWorld: false, PrimitiveType.LineList, StartIndex: 0, PrimitiveCount: 0, DisposeVertexBuffer: false);
        }

        return new Renderer3DEventLineRenderPlan(
            vertices.ToArray(),
            [
                new(Renderer3DThingCageRenderOperationKind.SetAlphaBlend, Enabled: true),
                new(Renderer3DThingCageRenderOperationKind.SetAlphaTest, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetZWrite, Enabled: false),
                new(Renderer3DThingCageRenderOperationKind.SetSourceBlend, Blend: Blend.SourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetDestinationBlend, Blend: Blend.SourceAlpha),
                new(Renderer3DThingCageRenderOperationKind.SetShader, Shader: ShaderName.world3d_vertex_color),
            ],
            SetIdentityWorld: true,
            PrimitiveType.LineList,
            StartIndex: 0,
            PrimitiveCount: vertices.Count / 2,
            DisposeVertexBuffer: true);
    }

    public static Renderer3DFinishGeometryCleanupPlan BuildFinishGeometryCleanupPlan()
        => new(
            UnbindTexture: true,
            [
                Renderer3DGeometryBucketKind.SolidGeometry,
                Renderer3DGeometryBucketKind.MaskedGeometry,
                Renderer3DGeometryBucketKind.TranslucentGeometry,
                Renderer3DGeometryBucketKind.SkyGeometry,
                Renderer3DGeometryBucketKind.SolidThings,
                Renderer3DGeometryBucketKind.MaskedThings,
                Renderer3DGeometryBucketKind.TranslucentThings,
                Renderer3DGeometryBucketKind.AllThings,
                Renderer3DGeometryBucketKind.LightThings,
                Renderer3DGeometryBucketKind.MaskedModelThings,
                Renderer3DGeometryBucketKind.TranslucentModelThings,
                Renderer3DGeometryBucketKind.VisualVertices,
            ]);

    private static WorldVertex Vertex(Vector3D position, int color)
        => new((float)position.x, (float)position.y, (float)position.z, color, u: 0.0f, v: 0.0f);
}
