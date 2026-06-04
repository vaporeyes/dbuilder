// ABOUTME: Verifies UDB-style visual sidedef texture offset copy and paste rules.
// ABOUTME: Covers global sidedef offsets and local UDMF per-part offset fields.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class VisualSidedefTextureOffsetsTests
{
    [Fact]
    public void CopyUsesGlobalOffsetsWhenLocalOffsetsAreDisabled()
    {
        var side = Sidedef();
        side.OffsetX = 12;
        side.OffsetY = -4;
        side.SetFloatField("offsetx_top", 30.0);
        side.SetFloatField("offsety_top", 40.0);

        var offsets = VisualSidedefTextureOffsets.Copy(side, SidedefPart.Upper, useLocalOffsets: false);

        Assert.Equal((12, -4), offsets);
    }

    [Fact]
    public void CopyUsesPartOffsetsWhenLocalOffsetsAreEnabled()
    {
        var side = Sidedef();
        side.OffsetX = 12;
        side.OffsetY = -4;
        side.SetFloatField("offsetx_top", 30.0);
        side.SetFloatField("offsety_top", 40.0);

        var offsets = VisualSidedefTextureOffsets.Copy(side, SidedefPart.Upper, useLocalOffsets: true);

        Assert.Equal((30, 40), offsets);
    }

    [Fact]
    public void PasteWritesGlobalOffsetsWhenLocalOffsetsAreDisabled()
    {
        var side = Sidedef();

        VisualSidedefTextureOffsets.Paste(side, SidedefPart.Middle, (7, -3), useLocalOffsets: false);

        Assert.Equal(7, side.OffsetX);
        Assert.Equal(-3, side.OffsetY);
        Assert.False(side.Fields.ContainsKey("offsetx_mid"));
        Assert.False(side.Fields.ContainsKey("offsety_mid"));
    }

    [Fact]
    public void PasteWritesPartOffsetsWhenLocalOffsetsAreEnabled()
    {
        var side = Sidedef();

        VisualSidedefTextureOffsets.Paste(side, SidedefPart.Lower, (7, -3), useLocalOffsets: true);

        Assert.Equal(0, side.OffsetX);
        Assert.Equal(0, side.OffsetY);
        Assert.Equal(7.0, side.GetFloatField("offsetx_bottom", 0.0), 1e-9);
        Assert.Equal(-3.0, side.GetFloatField("offsety_bottom", 0.0), 1e-9);
    }

    [Fact]
    public void PasteKeepsZeroLocalOffsetsLikeUdb()
    {
        var side = Sidedef();

        VisualSidedefTextureOffsets.Paste(side, SidedefPart.Middle, (0, 0), useLocalOffsets: true);

        Assert.True(side.Fields.ContainsKey("offsetx_mid"));
        Assert.True(side.Fields.ContainsKey("offsety_mid"));
        Assert.Equal(0.0, side.GetFloatField("offsetx_mid", 1.0), 1e-9);
        Assert.Equal(0.0, side.GetFloatField("offsety_mid", 1.0), 1e-9);
    }

    private static Sidedef Sidedef()
    {
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(64, 0)));
        return new Sidedef(line, isFront: true);
    }
}
