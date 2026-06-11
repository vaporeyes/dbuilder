// ABOUTME: Verifies small UDB rendering metadata enums and prefix tables.
// ABOUTME: Keeps rendering API compatibility with TextAlignment.cs and CommentType.cs.

using DBuilder.Rendering;

namespace DBuilder.Tests;

public sealed class RenderingMetadataTests
{
    [Fact]
    public void TextAlignmentXValuesMatchUdbOrdering()
    {
        Assert.Equal(0, (int)TextAlignmentX.Left);
        Assert.Equal(1, (int)TextAlignmentX.Center);
        Assert.Equal(2, (int)TextAlignmentX.Right);
    }

    [Fact]
    public void TextAlignmentYValuesMatchUdbOrdering()
    {
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
