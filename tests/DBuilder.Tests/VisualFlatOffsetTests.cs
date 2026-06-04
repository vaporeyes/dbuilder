// ABOUTME: Verifies UDB-style visual flat texture offset nudges for floor and ceiling targets.
// ABOUTME: Covers camera correction, scaled wrap spans, and UDMF panning fields.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public sealed class VisualFlatOffsetTests
{
    [Fact]
    public void NudgeWritesFloorPanningWithUdbSign()
    {
        var sector = new Sector();

        bool changed = VisualFlatOffset.Nudge(
            sector,
            ceiling: false,
            horizontal: 8,
            vertical: -8,
            scaledTextureWidth: 64,
            scaledTextureHeight: 64,
            cameraAngleXY: 0.0);

        Assert.True(changed);
        Assert.Equal(-8.0, sector.GetFloatField("xpanningfloor", 0.0), 1e-9);
        Assert.Equal(8.0, sector.GetFloatField("ypanningfloor", 0.0), 1e-9);
    }

    [Fact]
    public void NudgeWritesCeilingFields()
    {
        var sector = new Sector();

        bool changed = VisualFlatOffset.Nudge(
            sector,
            ceiling: true,
            horizontal: 8,
            vertical: -8,
            scaledTextureWidth: 64,
            scaledTextureHeight: 64,
            cameraAngleXY: 0.0);

        Assert.True(changed);
        Assert.Equal(-8.0, sector.GetFloatField("xpanningceiling", 0.0), 1e-9);
        Assert.Equal(8.0, sector.GetFloatField("ypanningceiling", 0.0), 1e-9);
    }

    [Fact]
    public void NudgeUsesScaledTextureSpanAndSurfaceScaleLikeUdb()
    {
        var sector = new Sector();
        sector.SetFloatField("xpanningfloor", 70.0, 0.0);
        sector.SetFloatField("xscalefloor", 2.0, 1.0);

        bool changed = VisualFlatOffset.Nudge(
            sector,
            ceiling: false,
            horizontal: 8,
            vertical: 0,
            scaledTextureWidth: 128,
            scaledTextureHeight: 64,
            cameraAngleXY: 0.0);

        Assert.True(changed);
        Assert.Equal(62.0, sector.GetFloatField("xpanningfloor", 0.0), 1e-9);
    }

    [Fact]
    public void NudgeCorrectsForCameraQuadrantsLikeUdb()
    {
        var sector = new Sector();

        bool changed = VisualFlatOffset.Nudge(
            sector,
            ceiling: false,
            horizontal: 8,
            vertical: 0,
            scaledTextureWidth: 64,
            scaledTextureHeight: 64,
            cameraAngleXY: Angle2D.DegToRad(90));

        Assert.True(changed);
        Assert.Equal(0.0, sector.GetFloatField("xpanningfloor", 0.0), 1e-9);
        Assert.Equal(8.0, sector.GetFloatField("ypanningfloor", 0.0), 1e-9);
    }

    [Fact]
    public void NudgeAddsSurfaceRotationBeforeCameraCorrectionLikeUdb()
    {
        var sector = new Sector();
        sector.SetFloatField("rotationfloor", 90.0, 0.0);

        bool changed = VisualFlatOffset.Nudge(
            sector,
            ceiling: false,
            horizontal: 8,
            vertical: 0,
            scaledTextureWidth: 64,
            scaledTextureHeight: 64,
            cameraAngleXY: 0.0);

        Assert.True(changed);
        Assert.Equal(0.0, sector.GetFloatField("xpanningfloor", 0.0), 1e-9);
        Assert.Equal(8.0, sector.GetFloatField("ypanningfloor", 0.0), 1e-9);
    }

    [Fact]
    public void NudgeRejectsZeroDimensions()
    {
        var sector = new Sector();

        bool changed = VisualFlatOffset.Nudge(
            sector,
            ceiling: false,
            horizontal: 8,
            vertical: 0,
            scaledTextureWidth: 0,
            scaledTextureHeight: 64,
            cameraAngleXY: 0.0);

        Assert.False(changed);
        Assert.False(sector.Fields.ContainsKey("xpanningfloor"));
    }
}
