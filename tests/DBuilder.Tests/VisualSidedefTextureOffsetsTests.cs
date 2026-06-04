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

    [Fact]
    public void NudgeGlobalOffsetsUsesUdbSignAndTextureWrap()
    {
        var side = Sidedef();
        side.OffsetX = 62;
        side.OffsetY = 62;

        bool changed = VisualSidedefTextureOffsets.Nudge(
            side,
            SidedefPart.Upper,
            horizontal: -8,
            vertical: -8,
            useLocalOffsets: false,
            textureWidth: 64,
            textureHeight: 64);

        Assert.True(changed);
        Assert.Equal(6, side.OffsetX);
        Assert.Equal(6, side.OffsetY);
    }

    [Fact]
    public void NudgeGlobalMiddleOffsetsDoesNotWrapVerticalLikeUdb()
    {
        var side = Sidedef();
        side.OffsetY = 62;

        bool changed = VisualSidedefTextureOffsets.Nudge(
            side,
            SidedefPart.Middle,
            horizontal: 0,
            vertical: -8,
            useLocalOffsets: false,
            textureWidth: 64,
            textureHeight: 64);

        Assert.True(changed);
        Assert.Equal(70, side.OffsetY);
    }

    [Fact]
    public void NudgeLocalOffsetsUsesPartFieldsAndUdbSign()
    {
        var side = Sidedef();
        side.SetFloatField("offsetx_top", 2.0);
        side.SetFloatField("offsety_top", 3.0);

        bool changed = VisualSidedefTextureOffsets.Nudge(
            side,
            SidedefPart.Upper,
            horizontal: -8,
            vertical: 8,
            useLocalOffsets: true,
            textureWidth: 64,
            textureHeight: 64);

        Assert.True(changed);
        Assert.Equal(10.0, side.GetFloatField("offsetx_top", 0.0), 1e-9);
        Assert.Equal(-5.0, side.GetFloatField("offsety_top", 0.0), 1e-9);
        Assert.Equal(0, side.OffsetX);
        Assert.Equal(0, side.OffsetY);
    }

    [Fact]
    public void NudgeLocalOffsetsKeepsValueWhenOffsetIsFullTextureSizeLikeUdb()
    {
        var side = Sidedef();
        side.SetFloatField("offsetx_mid", 9.0);

        bool changed = VisualSidedefTextureOffsets.Nudge(
            side,
            SidedefPart.Middle,
            horizontal: -64,
            vertical: 0,
            useLocalOffsets: true,
            textureWidth: 64,
            textureHeight: 64);

        Assert.True(changed);
        Assert.Equal(9.0, side.GetFloatField("offsetx_mid", 0.0), 1e-9);
    }

    private static Sidedef Sidedef()
    {
        var line = new Linedef(new Vertex(new Vector2D(0, 0)), new Vertex(new Vector2D(64, 0)));
        return new Sidedef(line, isFront: true);
    }
}
