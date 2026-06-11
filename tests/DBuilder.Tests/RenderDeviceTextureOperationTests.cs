// ABOUTME: Verifies UDB-style render-device texture operation planning.
// ABOUTME: Covers texture binding, clear, cube copy, pixel upload, and PBO operation surfaces.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class RenderDeviceTextureOperationTests
{
    [Fact]
    public void CubeMapFaceValuesMatchUdbTextureSurface()
    {
        Assert.Equal(0, (int)CubeMapFace.PositiveX);
        Assert.Equal(1, (int)CubeMapFace.PositiveY);
        Assert.Equal(2, (int)CubeMapFace.PositiveZ);
        Assert.Equal(3, (int)CubeMapFace.NegativeX);
        Assert.Equal(4, (int)CubeMapFace.NegativeY);
        Assert.Equal(5, (int)CubeMapFace.NegativeZ);
    }

    [Fact]
    public void RenderDeviceExposesUdbTextureOperationSurface()
    {
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.SetTexture),
            new[] { typeof(Texture), typeof(int) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.SetTexture),
            new[] { typeof(BaseTexture), typeof(int) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.SetTexture),
            new[] { typeof(int), typeof(BaseTexture) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.ClearTexture),
            new[] { typeof(uint), typeof(Texture) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.ClearTexture),
            new[] { typeof(Color4), typeof(Texture) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.SetPixels),
            new[] { typeof(Texture), typeof(int), typeof(int), typeof(byte[]), typeof(bool) }));
        Assert.Contains(typeof(RenderDevice).GetMethods(), method =>
            method.Name == nameof(RenderDevice.SetPixels)
            && method.GetParameters() is { Length: 2 } parameters
            && parameters[0].ParameterType == typeof(Texture)
            && parameters[1].ParameterType.IsPointer
            && parameters[1].ParameterType.GetElementType() == typeof(uint));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.CopyTexture),
            new[] { typeof(CubeTexture), typeof(CubeMapFace) }));
        Assert.NotNull(typeof(RenderDevice).GetMethod(
            nameof(RenderDevice.SetPixels),
            new[] { typeof(CubeTexture), typeof(CubeMapFace), typeof(byte[]), typeof(bool) }));
    }

    [Fact]
    public void BuildSetTexturePlanTracksUnitAndNullTexture()
    {
        TextureOperationPlan plan = RenderDevice.BuildSetTexturePlan(texture: null, unit: 3);

        Assert.Equal(TextureOperationKind.Bind, plan.Kind);
        Assert.Equal(3, plan.Unit);
        Assert.False(plan.HasTexture);
        Assert.Null(plan.CubeFace);
        Assert.Null(plan.ColorArgb);
    }

    [Fact]
    public void BuildClearTexturePlanTracksColorAndTarget()
    {
        TextureOperationPlan plan = RenderDevice.BuildClearTexturePlan(0xff112233, texture: null);
        TextureOperationPlan colorPlan = RenderDevice.BuildClearTexturePlan(
            new Color4(unchecked((int)0xff112233)),
            texture: null);

        Assert.Equal(TextureOperationKind.Clear, plan.Kind);
        Assert.Equal(0xff112233u, plan.ColorArgb);
        Assert.False(plan.HasTexture);
        Assert.Equal(plan, colorPlan);
    }

    [Fact]
    public void BuildCopyTexturePlanTracksCubeFace()
    {
        TextureOperationPlan plan = RenderDevice.BuildCopyTexturePlan(CubeMapFace.NegativeZ, texture: null);

        Assert.Equal(TextureOperationKind.CopyCubeFace, plan.Kind);
        Assert.Equal(CubeMapFace.NegativeZ, plan.CubeFace);
        Assert.False(plan.HasTexture);
    }

    [Fact]
    public void BuildPixelUploadPlansSeparate2DAndCubeUploads()
    {
        TextureOperationPlan twoD = RenderDevice.BuildSetPixelsPlan(texture: null);
        TextureOperationPlan cube = RenderDevice.BuildSetCubePixelsPlan(texture: null, CubeMapFace.PositiveX);

        Assert.Equal(TextureOperationKind.SetPixels2D, twoD.Kind);
        Assert.Null(twoD.CubeFace);
        Assert.Equal(TextureOperationKind.SetPixelsCubeFace, cube.Kind);
        Assert.Equal(CubeMapFace.PositiveX, cube.CubeFace);
    }

    [Fact]
    public void BuildPboPlansTrackMapAndUnmapOperations()
    {
        TextureOperationPlan map = RenderDevice.BuildMapPboPlan(texture: null);
        TextureOperationPlan unmap = RenderDevice.BuildUnmapPboPlan(texture: null);

        Assert.Equal(TextureOperationKind.MapPbo, map.Kind);
        Assert.Equal(TextureOperationKind.UnmapPbo, unmap.Kind);
        Assert.False(map.HasTexture);
        Assert.False(unmap.HasTexture);
    }
}
