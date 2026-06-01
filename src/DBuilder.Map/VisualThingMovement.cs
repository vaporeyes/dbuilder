// ABOUTME: Models UDB visual-mode thing movement helpers for relative and cursor placement commands.
// ABOUTME: Keeps selected thing translation testable without editor or renderer dependencies.

using DBuilder.Geometry;

namespace DBuilder.Map;

public static class VisualThingMovement
{
    public static IReadOnlyList<Vector3D> TranslateRelative(
        IReadOnlyList<Vector3D> coordinates,
        Vector2D direction,
        double cameraAngleXY)
    {
        if (coordinates.Count == 0) return Array.Empty<Vector3D>();

        int cameraDegrees = (int)Math.Round(Angle2D.RadToDeg(cameraAngleXY));
        int sector = ClampAngle(cameraDegrees - 45) / 90;
        Vector2D rotated = direction.GetRotated(sector * Angle2D.PIHALF);
        rotated = new Vector2D(Math.Round(rotated.x, 6), Math.Round(rotated.y, 6));

        var translated = new Vector3D[coordinates.Count];
        for (int i = 0; i < coordinates.Count; i++)
            translated[i] = coordinates[i] + new Vector3D(rotated);

        return translated;
    }

    public static IReadOnlyList<Vector3D> TranslateToCursor(IReadOnlyList<Vector3D> coordinates, Vector2D cursor)
    {
        if (coordinates.Count == 0) return Array.Empty<Vector3D>();

        if (coordinates.Count == 1)
            return new[] { new Vector3D(cursor.x, cursor.y, coordinates[0].z) };

        double minX = coordinates[0].x;
        double maxX = minX;
        double minY = coordinates[0].y;
        double maxY = minY;

        for (int i = 1; i < coordinates.Count; i++)
        {
            Vector3D coordinate = coordinates[i];
            if (coordinate.x < minX) minX = coordinate.x;
            else if (coordinate.x > maxX) maxX = coordinate.x;

            if (coordinate.y < minY) minY = coordinate.y;
            else if (coordinate.y > maxY) maxY = coordinate.y;
        }

        var selectionCenter = new Vector2D(minX + (maxX - minX) * 0.5, minY + (maxY - minY) * 0.5);
        var translated = new Vector3D[coordinates.Count];
        for (int i = 0; i < coordinates.Count; i++)
        {
            Vector3D coordinate = coordinates[i];
            translated[i] = new Vector3D(
                Math.Round(cursor.x - (selectionCenter.x - coordinate.x)),
                Math.Round(cursor.y - (selectionCenter.y - coordinate.y)),
                Math.Round(coordinate.z));
        }

        return translated;
    }

    private static int ClampAngle(int angle)
    {
        angle %= 360;
        if (angle < 0) angle += 360;
        return angle;
    }
}
