// ABOUTME: Verifies small UDB rendering metadata enums and prefix tables.
// ABOUTME: Keeps rendering API compatibility with TextAlignment.cs and CommentType.cs.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class RenderingMetadataTests
{
    [Fact]
    public void RenderDeviceEnumValuesMatchUdbNativeSurface()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(VertexFormat)));
        Assert.Equal(0, (int)VertexFormat.Flat);
        Assert.Equal(1, (int)VertexFormat.World);

        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Cull)));
        Assert.Equal(0, (int)Cull.None);
        Assert.Equal(1, (int)Cull.Clockwise);

        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Blend)));
        Assert.Equal(0, (int)Blend.InverseSourceAlpha);
        Assert.Equal(1, (int)Blend.SourceAlpha);
        Assert.Equal(2, (int)Blend.One);

        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(BlendOperation)));
        Assert.Equal(0, (int)BlendOperation.Add);
        Assert.Equal(1, (int)BlendOperation.ReverseSubtract);

        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(FillMode)));
        Assert.Equal(0, (int)FillMode.Solid);
        Assert.Equal(1, (int)FillMode.Wireframe);

        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(TextureAddress)));
        Assert.Equal(0, (int)TextureAddress.Wrap);
        Assert.Equal(1, (int)TextureAddress.Clamp);

        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(PrimitiveType)));
        Assert.Equal(0, (int)PrimitiveType.LineList);
        Assert.Equal(1, (int)PrimitiveType.TriangleList);
        Assert.Equal(2, (int)PrimitiveType.TriangleStrip);

        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(TextureFilter)));
        Assert.Equal(0, (int)TextureFilter.Nearest);
        Assert.Equal(1, (int)TextureFilter.Linear);

        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(MipmapFilter)));
        Assert.Equal(0, (int)MipmapFilter.None);
        Assert.Equal(1, (int)MipmapFilter.Nearest);
        Assert.Equal(2, (int)MipmapFilter.Linear);
    }

    [Fact]
    public void BufferDataPlansUseUdbVertexAndIndexByteSizes()
    {
        RenderBufferOperationPlan flat = RenderDevice.BuildSetBufferDataPlan(new FlatVertex[3]);
        RenderBufferOperationPlan world = RenderDevice.BuildSetBufferDataPlan(new WorldVertex[2]);
        RenderBufferOperationPlan index = RenderDevice.BuildSetBufferDataPlan(new int[5]);

        Assert.Equal(new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetFlatVertexData,
            VertexFormat.Flat,
            ElementCount: 3,
            ElementOffset: 0,
            ByteOffset: 0,
            ByteCount: 3 * FlatVertex.Stride), flat);
        Assert.Equal(new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetWorldVertexData,
            VertexFormat.World,
            ElementCount: 2,
            ElementOffset: 0,
            ByteOffset: 0,
            ByteCount: 2 * WorldVertex.Stride), world);
        Assert.Equal(new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetIndexData,
            VertexFormat: null,
            ElementCount: 5,
            ElementOffset: 0,
            ByteOffset: 0,
            ByteCount: 5 * sizeof(int)), index);
    }

    [Fact]
    public void BufferDataPlansRejectNullArrays()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RenderDevice.BuildSetBufferDataPlan((FlatVertex[])null!));
        Assert.Throws<ArgumentNullException>(() =>
            RenderDevice.BuildSetBufferDataPlan((WorldVertex[])null!));
        Assert.Throws<ArgumentNullException>(() =>
            RenderDevice.BuildSetBufferDataPlan((int[])null!));
    }

    [Fact]
    public void BufferConstructorsRejectNullGl()
    {
        Assert.Throws<ArgumentNullException>(() => new VertexBuffer(null!));
        Assert.Throws<ArgumentNullException>(() => new IndexBuffer(null!));
    }

    [Fact]
    public void BufferLengthPlanUsesRequestedVertexFormatStride()
    {
        RenderBufferOperationPlan flat = RenderDevice.BuildSetBufferDataPlan(8, VertexFormat.Flat);
        RenderBufferOperationPlan world = RenderDevice.BuildSetBufferDataPlan(8, VertexFormat.World);
        RenderBufferOperationPlan index = RenderDevice.BuildSetIndexBufferDataPlan(8);

        Assert.Equal(8 * FlatVertex.Stride, flat.ByteCount);
        Assert.Equal(8 * WorldVertex.Stride, world.ByteCount);
        Assert.Equal(8 * sizeof(int), index.ByteCount);
        Assert.Equal(VertexFormat.Flat, flat.VertexFormat);
        Assert.Equal(VertexFormat.World, world.VertexFormat);
        Assert.Null(index.VertexFormat);
        Assert.Equal(RenderBufferOperationKind.SetIndexLength, index.Kind);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RenderDevice.BuildSetBufferDataPlan(-1, VertexFormat.Flat));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RenderDevice.BuildSetIndexBufferDataPlan(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RenderDevice.BuildSetBufferDataPlan(1, (VertexFormat)99));
    }

    [Fact]
    public void BufferSubdataPlansConvertElementOffsetsToByteOffsets()
    {
        RenderBufferOperationPlan flat = RenderDevice.BuildSetBufferSubdataPlan(4, new FlatVertex[3]);
        RenderBufferOperationPlan world = RenderDevice.BuildSetBufferSubdataPlan(2, new WorldVertex[5]);
        RenderBufferOperationPlan flatPrefix = RenderDevice.BuildSetBufferSubdataPlan(new FlatVertex[6], size: 4);
        RenderBufferOperationPlan index = RenderDevice.BuildSetIndexBufferSubdataPlan(7, new int[6]);

        Assert.Equal(new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetFlatVertexSubdata,
            VertexFormat.Flat,
            ElementCount: 3,
            ElementOffset: 4,
            ByteOffset: 4 * FlatVertex.Stride,
            ByteCount: 3 * FlatVertex.Stride), flat);
        Assert.Equal(new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetWorldVertexSubdata,
            VertexFormat.World,
            ElementCount: 5,
            ElementOffset: 2,
            ByteOffset: 2 * WorldVertex.Stride,
            ByteCount: 5 * WorldVertex.Stride), world);
        Assert.Equal(new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetFlatVertexSubdata,
            VertexFormat.Flat,
            ElementCount: 4,
            ElementOffset: 0,
            ByteOffset: 0,
            ByteCount: 4 * FlatVertex.Stride), flatPrefix);
        Assert.Equal(new RenderBufferOperationPlan(
            RenderBufferOperationKind.SetIndexSubdata,
            VertexFormat: null,
            ElementCount: 6,
            ElementOffset: 7,
            ByteOffset: 7 * sizeof(int),
            ByteCount: 6 * sizeof(int)), index);
    }

    [Fact]
    public void BufferSubdataPlansRejectInvalidRanges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RenderDevice.BuildSetBufferSubdataPlan(-1, Array.Empty<FlatVertex>()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RenderDevice.BuildSetBufferSubdataPlan(-1, Array.Empty<WorldVertex>()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RenderDevice.BuildSetBufferSubdataPlan(new FlatVertex[1], size: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RenderDevice.BuildSetIndexBufferSubdataPlan(-1, Array.Empty<int>()));
    }

    [Fact]
    public void BufferSubdataPlansRejectNullArrays()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RenderDevice.BuildSetBufferSubdataPlan(0, (FlatVertex[])null!));
        Assert.Throws<ArgumentNullException>(() =>
            RenderDevice.BuildSetBufferSubdataPlan(0, (WorldVertex[])null!));
        Assert.Throws<ArgumentNullException>(() =>
            RenderDevice.BuildSetBufferSubdataPlan((FlatVertex[])null!, size: 0));
        Assert.Throws<ArgumentNullException>(() =>
            RenderDevice.BuildSetIndexBufferSubdataPlan(0, (int[])null!));
    }

    [Fact]
    public void BufferBindingPlansTrackBindAndReleaseRequests()
    {
        RenderBufferBindingPlan flat = RenderDevice.BuildSetVertexBufferPlan(VertexFormat.Flat, vertexCount: 8);
        RenderBufferBindingPlan world = RenderDevice.BuildSetVertexBufferPlan(VertexFormat.World, vertexCount: 4);
        RenderBufferBindingPlan vertexRelease = RenderDevice.BuildReleaseVertexBufferPlan();
        RenderBufferBindingPlan index = RenderDevice.BuildSetIndexBufferPlan(indexCount: 12);
        RenderBufferBindingPlan indexRelease = RenderDevice.BuildReleaseIndexBufferPlan();

        Assert.Equal(new RenderBufferBindingPlan(
            RenderBufferBindingKind.SetVertexBuffer,
            HasBuffer: true,
            VertexFormat.Flat,
            ElementCount: 8), flat);
        Assert.Equal(VertexFormat.World, world.VertexFormat);
        Assert.Equal(4, world.ElementCount);
        Assert.Equal(new RenderBufferBindingPlan(
            RenderBufferBindingKind.SetVertexBuffer,
            HasBuffer: false,
            VertexFormat: null,
            ElementCount: 0), vertexRelease);
        Assert.Equal(new RenderBufferBindingPlan(
            RenderBufferBindingKind.SetIndexBuffer,
            HasBuffer: true,
            VertexFormat: null,
            ElementCount: 12), index);
        Assert.Equal(new RenderBufferBindingPlan(
            RenderBufferBindingKind.SetIndexBuffer,
            HasBuffer: false,
            VertexFormat: null,
            ElementCount: 0), indexRelease);
    }

    [Fact]
    public void BufferBindingPlansRejectInvalidMetadata()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RenderDevice.BuildSetVertexBufferPlan(VertexFormat.Flat, vertexCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RenderDevice.BuildSetVertexBufferPlan((VertexFormat)99, vertexCount: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RenderDevice.BuildSetIndexBufferPlan(indexCount: -1));
    }

    [Fact]
    public void RenderDeviceExposesCurrentBufferBindings()
    {
        var vertex = typeof(RenderDevice).GetProperty(nameof(RenderDevice.BoundVertexBuffer));
        var index = typeof(RenderDevice).GetProperty(nameof(RenderDevice.BoundIndexBuffer));

        Assert.NotNull(vertex);
        Assert.NotNull(index);
        Assert.Equal(typeof(VertexBuffer), vertex!.PropertyType);
        Assert.Equal(typeof(IndexBuffer), index!.PropertyType);
        Assert.Null(vertex.SetMethod);
        Assert.Null(index.SetMethod);
    }

    [Fact]
    public void UniformTypeValuesMatchUdbOrdering()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(UniformType)));
        Assert.Equal(0, (int)UniformType.Vec4f);
        Assert.Equal(1, (int)UniformType.Vec3f);
        Assert.Equal(2, (int)UniformType.Vec2f);
        Assert.Equal(3, (int)UniformType.Float);
        Assert.Equal(4, (int)UniformType.Mat4);
        Assert.Equal(5, (int)UniformType.Vec4i);
        Assert.Equal(6, (int)UniformType.Vec3i);
        Assert.Equal(7, (int)UniformType.Vec2i);
        Assert.Equal(8, (int)UniformType.Int);
        Assert.Equal(9, (int)UniformType.Vec4fArray);
        Assert.Equal(10, (int)UniformType.Vec3fArray);
        Assert.Equal(11, (int)UniformType.Vec2fArray);
    }

    [Fact]
    public void ShaderNameValuesMatchUdbOrdering()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(ShaderName)));
        Assert.Equal(
            new[]
            {
                "display2d_fsaa",
                "display2d_normal",
                "display2d_fullbright",
                "things2d_thing",
                "things2d_sprite",
                "things2d_fill",
                "world3d_main",
                "world3d_fullbright",
                "world3d_main_highlight",
                "world3d_fullbright_highlight",
                "world3d_main_vertexcolor",
                "world3d_skybox",
                "world3d_main_highlight_vertexcolor",
                "world3d_p7",
                "world3d_main_fog",
                "world3d_p9",
                "world3d_main_highlight_fog",
                "world3d_p11",
                "world3d_main_fog_vertexcolor",
                "world3d_p13",
                "world3d_main_highlight_fog_vertexcolor",
                "world3d_vertex_color",
                "world3d_constant_color",
                "world3d_slope_handle",
                "world3d_classic",
                "world3d_p19",
                "world3d_classic_highlight",
            },
            Enum.GetNames<ShaderName>());
    }

    [Fact]
    public void UniformNameValuesMatchUdbOrdering()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(UniformName)));
        Assert.Equal(
            new[]
            {
                "rendersettings",
                "projection",
                "desaturation",
                "highlightcolor",
                "view",
                "world",
                "modelnormal",
                "FillColor",
                "vertexColor",
                "stencilColor",
                "lightPosAndRadius",
                "lightOrientation",
                "light2Radius",
                "lightColor",
                "ignoreNormals",
                "spotLight",
                "campos",
                "fogsettings",
                "fogcolor",
                "sectorfogcolor",
                "lightsEnabled",
                "slopeHandleLength",
                "drawPaletted",
                "colormapSize",
                "sectorLightLevel",
                "doomlightlevels",
                "skew",
                "lightStrengthAndLinearity",
                "useLightStrength",
            },
            Enum.GetNames<UniformName>());
    }

    [Fact]
    public void TextAlignmentXValuesMatchUdbOrdering()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(TextAlignmentX)));
        Assert.Equal(0, (int)TextAlignmentX.Left);
        Assert.Equal(1, (int)TextAlignmentX.Center);
        Assert.Equal(2, (int)TextAlignmentX.Right);
    }

    [Fact]
    public void TextAlignmentYValuesMatchUdbOrdering()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(TextAlignmentY)));
        Assert.Equal(0, (int)TextAlignmentY.Top);
        Assert.Equal(1, (int)TextAlignmentY.Middle);
        Assert.Equal(2, (int)TextAlignmentY.Bottom);
    }

    [Fact]
    public void CommentTypePrefixesMatchUdbOrdering()
    {
        Assert.Equal(new[] { "", "[i]", "[?]", "[!]", "[:]" }, CommentType.Types);
    }
}
