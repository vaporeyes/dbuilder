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
