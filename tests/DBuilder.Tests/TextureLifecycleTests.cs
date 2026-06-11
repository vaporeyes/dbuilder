// ABOUTME: Verifies UDB-style texture metadata and allocation planning.
// ABOUTME: Keeps texture lifecycle parity testable without creating a live GL context.

using DBuilder.Rendering;
using System.Reflection;

namespace DBuilder.Tests;

public sealed class TextureLifecycleTests
{
    [Fact]
    public void TextureFormatMatchesUdbValues()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(TextureFormat)));
        Assert.Equal(0, (int)TextureFormat.Rgba8);
        Assert.Equal(1, (int)TextureFormat.Bgra8);
        Assert.Equal(2, (int)TextureFormat.Rg16f);
        Assert.Equal(3, (int)TextureFormat.Rgba16f);
        Assert.Equal(4, (int)TextureFormat.R32f);
        Assert.Equal(5, (int)TextureFormat.Rg32f);
        Assert.Equal(6, (int)TextureFormat.Rgb32f);
        Assert.Equal(7, (int)TextureFormat.Rgba32f);
        Assert.Equal(8, (int)TextureFormat.D32f_S8);
        Assert.Equal(9, (int)TextureFormat.D24_S8);
    }

    [Fact]
    public void BaseTextureExposesUdbLifecycleProperties()
    {
        Assert.True(typeof(BaseTexture).IsAbstract);
        Assert.NotNull(typeof(BaseTexture).GetProperty(nameof(BaseTexture.Disposed)));
        Assert.NotNull(typeof(BaseTexture).GetProperty(nameof(BaseTexture.Tag)));
        Assert.NotNull(typeof(BaseTexture).GetProperty(nameof(BaseTexture.UserData)));
        Assert.Contains(typeof(IDisposable), typeof(BaseTexture).GetInterfaces());
        Assert.True(typeof(Texture).IsSubclassOf(typeof(BaseTexture)));
        Assert.True(typeof(CubeTexture).IsSubclassOf(typeof(BaseTexture)));

        MethodInfo? finalizer = typeof(BaseTexture).GetMethod("Finalize", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(finalizer);
        Assert.Equal(typeof(BaseTexture), finalizer.DeclaringType);
    }

    [Fact]
    public void Build2DAllocationPlanStoresDimensionsAndFormat()
    {
        TextureAllocationPlan plan = Texture.Build2DAllocationPlan(320, 200, TextureFormat.Bgra8);

        Assert.Equal(TextureAllocationKind.Texture2D, plan.Kind);
        Assert.Equal(320, plan.Width);
        Assert.Equal(200, plan.Height);
        Assert.Equal(TextureFormat.Bgra8, plan.Format);
    }

    [Fact]
    public void BuildCubeAllocationPlanUsesSquareBgraFacesLikeUdb()
    {
        TextureAllocationPlan plan = Texture.BuildCubeAllocationPlan(256);

        Assert.Equal(TextureAllocationKind.Cube, plan.Kind);
        Assert.Equal(256, plan.Width);
        Assert.Equal(256, plan.Height);
        Assert.Equal(TextureFormat.Bgra8, plan.Format);
    }

    [Fact]
    public void AllocationPlansRejectInvalidDimensionsAndFormats()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Texture.Build2DAllocationPlan(width: 0, height: 1, TextureFormat.Rgba8));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Texture.Build2DAllocationPlan(width: 1, height: 0, TextureFormat.Rgba8));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Texture.Build2DAllocationPlan(width: 1, height: 1, (TextureFormat)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Texture.BuildCubeAllocationPlan(size: 0));
    }

    [Fact]
    public void TextureConstructorsRejectNullGlAndDevice()
    {
        Assert.Throws<ArgumentNullException>(() => new Texture((Silk.NET.OpenGL.GL)null!));
        Assert.Throws<ArgumentNullException>(() => new Texture((RenderDevice)null!, 1, 1, TextureFormat.Rgba8));
        Assert.Throws<ArgumentNullException>(() => new CubeTexture((Silk.NET.OpenGL.GL)null!, 1));
        Assert.Throws<ArgumentNullException>(() => new CubeTexture((RenderDevice)null!, 1));
    }

    [Fact]
    public void Build2DPixelUploadPlanTracksRgbaByteCounts()
    {
        TexturePixelUploadPlan plan = Texture.BuildRgba8UploadPlan(
            width: 4,
            height: 3,
            pixelBufferByteCount: 52,
            generateMipmaps: false);

        Assert.Equal(new TexturePixelUploadPlan(
            TexturePixelUploadKind.Texture2D,
            Width: 4,
            Height: 3,
            TextureFormat.Rgba8,
            RequiredByteCount: 48,
            ProvidedByteCount: 52,
            GenerateMipmaps: false), plan);
    }

    [Fact]
    public void BuildCubePixelUploadPlanTracksFaceAndSquareRgbaByteCounts()
    {
        TexturePixelUploadPlan plan = CubeTexture.BuildRgba8UploadPlan(
            CubeMapFace.NegativeY,
            size: 8,
            pixelBufferByteCount: 256);

        Assert.Equal(TexturePixelUploadKind.CubeFace, plan.Kind);
        Assert.Equal(CubeMapFace.NegativeY, plan.CubeFace);
        Assert.Equal(8, plan.Width);
        Assert.Equal(8, plan.Height);
        Assert.Equal(TextureFormat.Rgba8, plan.Format);
        Assert.Equal(256, plan.RequiredByteCount);
        Assert.Equal(256, plan.ProvidedByteCount);
        Assert.True(plan.GenerateMipmaps);
    }

    [Fact]
    public void PixelUploadPlansRejectInvalidDimensionsAndShortBuffers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Texture.BuildRgba8UploadPlan(width: 0, height: 1, pixelBufferByteCount: 4));
        Assert.Throws<ArgumentException>(() =>
            Texture.BuildRgba8UploadPlan(width: 2, height: 2, pixelBufferByteCount: 15));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CubeTexture.BuildRgba8UploadPlan(CubeMapFace.PositiveX, size: 0, pixelBufferByteCount: 4));
        Assert.Throws<ArgumentException>(() =>
            CubeTexture.BuildRgba8UploadPlan(CubeMapFace.PositiveX, size: 2, pixelBufferByteCount: 15));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CubeTexture.BuildRgba8UploadPlan((CubeMapFace)99, size: 2, pixelBufferByteCount: 16));
    }

    [Fact]
    public void TextureExposesUdbMetadataProperties()
    {
        Assert.NotNull(typeof(Texture).GetConstructor(new[] { typeof(Silk.NET.OpenGL.GL), typeof(int), typeof(int), typeof(TextureFormat) }));
        Assert.NotNull(typeof(Texture).GetConstructor(new[] { typeof(RenderDevice), typeof(int), typeof(int), typeof(TextureFormat) }));
        Assert.NotNull(typeof(Texture).GetMethod(nameof(Texture.Allocate2D), new[] { typeof(int), typeof(int), typeof(TextureFormat) }));
        Assert.NotNull(typeof(Texture).GetProperty(nameof(Texture.Format)));
        Assert.NotNull(typeof(Texture).GetProperty(nameof(Texture.Tag)));
        Assert.NotNull(typeof(Texture).GetProperty(nameof(Texture.UserData)));
    }

    [Fact]
    public void CubeTextureExposesUdbMetadataProperties()
    {
        Assert.NotNull(typeof(CubeTexture).GetConstructor(new[] { typeof(Silk.NET.OpenGL.GL), typeof(int) }));
        Assert.NotNull(typeof(CubeTexture).GetConstructor(new[] { typeof(RenderDevice), typeof(int) }));
        Assert.NotNull(typeof(CubeTexture).GetProperty(nameof(CubeTexture.Size)));
        Assert.NotNull(typeof(CubeTexture).GetProperty(nameof(CubeTexture.Format)));
        Assert.NotNull(typeof(CubeTexture).GetProperty(nameof(CubeTexture.Tag)));
        Assert.NotNull(typeof(CubeTexture).GetProperty(nameof(CubeTexture.UserData)));
        Assert.NotNull(typeof(CubeTexture).GetProperty(nameof(CubeTexture.Disposed)));
    }
}
