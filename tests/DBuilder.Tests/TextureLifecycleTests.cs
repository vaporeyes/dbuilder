// ABOUTME: Verifies UDB-style texture metadata and allocation planning.
// ABOUTME: Keeps texture lifecycle parity testable without creating a live GL context.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class TextureLifecycleTests
{
    [Fact]
    public void TextureFormatMatchesUdbValues()
    {
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
    public void TextureExposesUdbMetadataProperties()
    {
        Assert.NotNull(typeof(Texture).GetProperty(nameof(Texture.Format)));
        Assert.NotNull(typeof(Texture).GetProperty(nameof(Texture.Tag)));
        Assert.NotNull(typeof(Texture).GetProperty(nameof(Texture.UserData)));
    }

    [Fact]
    public void CubeTextureExposesUdbMetadataProperties()
    {
        Assert.NotNull(typeof(CubeTexture).GetConstructor(new[] { typeof(Silk.NET.OpenGL.GL), typeof(int) }));
        Assert.NotNull(typeof(CubeTexture).GetProperty(nameof(CubeTexture.Size)));
        Assert.NotNull(typeof(CubeTexture).GetProperty(nameof(CubeTexture.Format)));
        Assert.NotNull(typeof(CubeTexture).GetProperty(nameof(CubeTexture.Tag)));
        Assert.NotNull(typeof(CubeTexture).GetProperty(nameof(CubeTexture.UserData)));
        Assert.NotNull(typeof(CubeTexture).GetProperty(nameof(CubeTexture.Disposed)));
        Assert.Contains(typeof(IDisposable), typeof(CubeTexture).GetInterfaces());
    }
}
