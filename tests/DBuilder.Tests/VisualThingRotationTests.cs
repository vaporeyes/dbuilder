// ABOUTME: Tests UDB-style visual thing orientation helpers.
// ABOUTME: Covers Doom-angle snapping plus pitch and roll wrapping.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class VisualThingRotationTests
{
    [Fact]
    public void RotateAddsIncrementAndClamps()
    {
        var thing = new Thing(new Vector2D(0, 0), 1);
        thing.Rotate(358);

        int count = VisualThingRotation.Rotate(new[] { thing }, 5, snapToDoomAngles: false);

        Assert.Equal(1, count);
        Assert.Equal(3, thing.Angle);
    }

    [Fact]
    public void RotateSnapsToDoomAngleSteps()
    {
        var thing = new Thing(new Vector2D(0, 0), 1);
        thing.Rotate(10);

        VisualThingRotation.Rotate(new[] { thing }, 45, snapToDoomAngles: true);

        Assert.Equal(45, thing.Angle);
    }

    [Fact]
    public void RotateNegativeDoomStepMatchesUdbIntegerTruncation()
    {
        var thing = new Thing(new Vector2D(0, 0), 1);
        thing.Rotate(10);

        VisualThingRotation.Rotate(new[] { thing }, -45, snapToDoomAngles: true);

        Assert.Equal(0, thing.Angle);
    }

    [Fact]
    public void ChangePitchWrapsThroughThingSetter()
    {
        var thing = new Thing(new Vector2D(0, 0), 1);
        thing.SetPitch(358);

        VisualThingRotation.ChangePitch(new[] { thing }, 5);

        Assert.Equal(3, thing.Pitch);
    }

    [Fact]
    public void ChangeRollWrapsThroughThingSetter()
    {
        var thing = new Thing(new Vector2D(0, 0), 1);
        thing.SetRoll(2);

        VisualThingRotation.ChangeRoll(new[] { thing }, -5);

        Assert.Equal(357, thing.Roll);
    }

    [Fact]
    public void ApplyCameraRotationSetsDoomAngleFromYaw()
    {
        var thing = new Thing(new Vector2D(0, 0), 1);

        int count = VisualThingRotation.ApplyCameraRotation(
            new[] { thing },
            cameraYaw: Angle2D.DegToRad(90),
            cameraPitch: 0,
            applyPitch: false);

        Assert.Equal(1, count);
        Assert.Equal(180, thing.Angle);
    }

    [Fact]
    public void ApplyCameraRotationSetsPitchForUdmf()
    {
        var thing = new Thing(new Vector2D(0, 0), 1);

        VisualThingRotation.ApplyCameraRotation(
            new[] { thing },
            cameraYaw: 0,
            cameraPitch: Angle2D.DegToRad(350),
            applyPitch: true);

        Assert.Equal(170, thing.Pitch);
    }

    [Fact]
    public void ApplyCameraRotationLeavesPitchOutsideUdmf()
    {
        var thing = new Thing(new Vector2D(0, 0), 1);
        thing.SetPitch(45);

        VisualThingRotation.ApplyCameraRotation(
            new[] { thing },
            cameraYaw: 0,
            cameraPitch: Angle2D.DegToRad(350),
            applyPitch: false);

        Assert.Equal(45, thing.Pitch);
    }
}
