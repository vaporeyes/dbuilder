// ABOUTME: Models UDB visual-mode thing angle, pitch, and roll commands.
// ABOUTME: Keeps selected thing orientation changes testable without editor or renderer dependencies.

using DBuilder.Geometry;

namespace DBuilder.Map;

public static class VisualThingRotation
{
    public static int Rotate(IReadOnlyList<Thing> things, int angleIncrement, bool snapToDoomAngles)
    {
        foreach (Thing thing in things)
        {
            int angle = thing.Angle + angleIncrement;
            if (snapToDoomAngles) angle = angle / 45 * 45;
            thing.Rotate(ClampAngle(angle));
        }

        return things.Count;
    }

    public static int ChangePitch(IReadOnlyList<Thing> things, int increment)
    {
        foreach (Thing thing in things)
            thing.SetPitch(thing.Pitch + increment);

        return things.Count;
    }

    public static int ChangeRoll(IReadOnlyList<Thing> things, int increment)
    {
        foreach (Thing thing in things)
            thing.SetRoll(thing.Roll + increment);

        return things.Count;
    }

    public static int ApplyCameraRotation(IReadOnlyList<Thing> things, double cameraYaw, double cameraPitch, bool applyPitch)
    {
        foreach (Thing thing in things)
        {
            thing.Rotate(cameraYaw - Angle2D.PI);
            if (applyPitch)
                thing.SetPitch((int)Angle2D.RadToDeg(cameraPitch - Angle2D.PI));
        }

        return things.Count;
    }

    private static int ClampAngle(int angle)
    {
        angle %= 360;
        if (angle < 0) angle += 360;
        return angle;
    }
}
