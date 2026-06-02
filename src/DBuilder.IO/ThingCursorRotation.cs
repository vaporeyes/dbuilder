// ABOUTME: Rotates selected things toward or away from the 2D cursor using UDB ThingsMode rules.
// ABOUTME: Keeps fixed-rotation and Doom-angle snapping behavior testable outside the editor shell.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public static class ThingCursorRotation
{
    public static int PointThingsToCursor(
        IReadOnlyList<Thing> things,
        Vector2D cursor,
        GameConfiguration? config,
        bool awayFromCursor)
    {
        int changed = 0;
        foreach (Thing thing in things)
        {
            ThingTypeInfo? info = config?.GetThing(thing.Type);
            if (info == null || info.FixedRotation) continue;

            double angle = Vector2D.GetAngle(cursor, thing.Position);
            if (awayFromCursor) angle += Angle2D.PI;

            int newAngle = Angle2D.RealToDoom(angle);
            if (config?.DoomThingRotationAngles == true)
                newAngle = (newAngle + 22) / 45 * 45;

            thing.Rotate(ClampAngle(newAngle));
            changed++;
        }

        return changed;
    }

    private static int ClampAngle(int angle)
    {
        angle %= 360;
        if (angle < 0) angle += 360;
        return angle;
    }
}
