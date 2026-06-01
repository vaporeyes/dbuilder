// ABOUTME: UI-independent helpers for UDB BuilderModes edit-selection transform behavior.
// ABOUTME: Keeps interactive transform math testable without depending on the editor window.

using DBuilder.Geometry;

namespace DBuilder.Map;

public static class EditSelectionTransform
{
    public const int RotationSnapSteps = 24;

    public static double SnapRotationToUdbGrid(double rotation)
    {
        double snapped = 0.0;
        double closestDistance = double.MaxValue;
        Vector2D rotationVector = Vector2D.FromAngle(rotation);

        for (int i = 0; i < RotationSnapSteps; i++)
        {
            double angle = i * Angle2D.PI * 0.08333333333;
            Vector2D gridVector = Vector2D.FromAngle(angle);
            double distance = 2.0 - Vector2D.DotProduct(gridVector, rotationVector);
            if (distance < closestDistance)
            {
                snapped = angle;
                closestDistance = distance;
            }
        }

        return Angle2D.Normalized(snapped);
    }
}
