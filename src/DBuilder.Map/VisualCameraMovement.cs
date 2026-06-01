// ABOUTME: Models UDB visual-mode camera movement helpers without depending on editor or renderer state.
// ABOUTME: Covers move-camera-to-cursor placement so commands can be tested outside the UI.

using DBuilder.Geometry;

namespace DBuilder.Map;

public static class VisualCameraMovement
{
    public const double MoveCameraToCursorDistance = 64.0;

    public static bool TryMoveCameraToCursor(Vector3D currentPosition, Vector3D hitPosition, out Vector3D nextPosition)
        => TryMoveCameraToCursor(currentPosition, hitPosition, MoveCameraToCursorDistance, out nextPosition);

    public static bool TryMoveCameraToCursor(Vector3D currentPosition, Vector3D hitPosition, double distance, out Vector3D nextPosition)
    {
        nextPosition = currentPosition;
        if (!currentPosition.IsFinite() || !hitPosition.IsFinite() || distance < 0.0) return false;

        Vector3D delta = currentPosition - hitPosition;
        if (delta.GetLengthSq() <= 0.0000000001) return false;

        nextPosition = hitPosition + delta.GetFixedLength(distance);
        return true;
    }
}
