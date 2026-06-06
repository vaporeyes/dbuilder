// ABOUTME: Models UDB visual-mode camera movement helpers without depending on editor or renderer state.
// ABOUTME: Covers move-camera-to-cursor placement so commands can be tested outside the UI.

using DBuilder.Geometry;

namespace DBuilder.Map;

public static class VisualCameraMovement
{
    public const double MoveCameraToCursorDistance = 64.0;
    public const int DoomPlayerHeight = 41;
    public const int LowSectorMinimumCameraHeight = 16;
    public const int CeilingCameraClearance = 4;
    public const int CenterOnCoordinatesEyeHeight = 54;
    public const double StartThingCameraOffset = 41.0;
    public const double StartThingMoveThreshold = 1.0;
    public const double OrbitAngleFromMouse = 0.005;
    public const double MinOrbitPitch = -1.5;
    public const double MaxOrbitPitch = 1.5;
    public const int AimingCameraThingType = 9072;
    public const int MovingCameraThingType = 9073;
    public const int InterpolationPointThingType = 9070;
    public const int SecurityCameraThingType = 9025;
    public const string MissingCameraTargetMessagePrefix = "Camera target with Tag ";

    public static bool TryMoveCameraToCursor(Vector3D currentPosition, Vector3D hitPosition, out Vector3D nextPosition)
        => TryMoveCameraToCursor(currentPosition, hitPosition, MoveCameraToCursorDistance, out nextPosition);

    public static Vector3D PlanEngagePosition(Vector2D initialPosition, double currentZ, Sector? nearestSector)
    {
        double z = currentZ;
        if (nearestSector != null)
        {
            int sectorHeight = nearestSector.CeilHeight - nearestSector.FloorHeight;
            if (sectorHeight < DoomPlayerHeight)
                z = nearestSector.FloorHeight + Math.Max(LowSectorMinimumCameraHeight, sectorHeight / 2);
            else if (z < nearestSector.FloorHeight + DoomPlayerHeight)
                z = nearestSector.FloorHeight + DoomPlayerHeight;
            else if (z > nearestSector.CeilHeight)
                z = nearestSector.CeilHeight - CeilingCameraClearance;
        }

        return new Vector3D(initialPosition.x, initialPosition.y, z);
    }

    public static Vector3D PlanCenterOnCoordinatesPosition(Vector2D coordinates, Sector? sector)
        => sector == null
            ? new Vector3D(coordinates.x, coordinates.y, 0.0)
            : new Vector3D(coordinates.x, coordinates.y, sector.FloorHeight + CenterOnCoordinatesEyeHeight);

    public static bool TryPlanStartThingPose(
        IReadOnlyList<Thing> things,
        int startThingType,
        Vector3D currentPosition,
        out VisualCameraStartThingPlan plan)
    {
        plan = new VisualCameraStartThingPlan(new VisualCameraPose(currentPosition, 0.0, 0.0), PositionChanges: false);
        if (startThingType == 0) return false;

        Thing? start = things.FirstOrDefault(thing => thing.Type == startThingType);
        if (start == null) return false;

        double z = start.Height;
        if (start.Sector != null) z += start.Sector.FloorHeight;

        var position = new Vector3D(start.Position.x, start.Position.y, z + StartThingCameraOffset);
        plan = new VisualCameraStartThingPlan(
            new VisualCameraPose(position, YawFromThingAngle(start.Angle), 0.0),
            (currentPosition - position).GetLength() > StartThingMoveThreshold);
        return true;
    }

    public static bool TryMoveCameraToCursor(Vector3D currentPosition, Vector3D hitPosition, double distance, out Vector3D nextPosition)
    {
        nextPosition = currentPosition;
        if (!currentPosition.IsFinite() || !hitPosition.IsFinite() || distance < 0.0) return false;

        Vector3D delta = currentPosition - hitPosition;
        if (delta.GetLengthSq() <= 0.0000000001) return false;

        nextPosition = hitPosition + delta.GetFixedLength(distance);
        return true;
    }

    public static bool TryOrbit(
        Vector3D currentPosition,
        Vector3D orbitPoint,
        double deltaX,
        double deltaY,
        out VisualCameraPose nextPose)
    {
        nextPose = new VisualCameraPose(currentPosition, 0.0, 0.0);
        if (!currentPosition.IsFinite() || !orbitPoint.IsFinite()) return false;

        Vector3D offset = currentPosition - orbitPoint;
        double radius = offset.GetLength();
        if (radius <= 0.0000000001) return false;

        double azimuth = Math.Atan2(offset.y, offset.x) - deltaX * OrbitAngleFromMouse;
        double elevation = Math.Asin(Math.Clamp(offset.z / radius, -1.0, 1.0)) + deltaY * OrbitAngleFromMouse;
        elevation = Math.Clamp(elevation, MinOrbitPitch, MaxOrbitPitch);

        double flat = Math.Cos(elevation) * radius;
        Vector3D nextPosition = new Vector3D(
            orbitPoint.x + Math.Cos(azimuth) * flat,
            orbitPoint.y + Math.Sin(azimuth) * flat,
            orbitPoint.z + Math.Sin(elevation) * radius);

        Vector3D direction = orbitPoint - nextPosition;
        nextPose = new VisualCameraPose(nextPosition, YawFromDirection(direction), PitchFromDirection(direction));
        return true;
    }

    public static VisualCameraPose LookThroughThing(
        Thing thing,
        IReadOnlyList<Thing> things,
        Func<Thing, Vector3D> centerOfThing,
        bool useUdmfPitch)
    {
        Vector3D position = centerOfThing(thing);
        double yaw = YawFromThingAngle(thing.Angle);
        double pitch = 0.0;

        if ((thing.Type == AimingCameraThingType || thing.Type == MovingCameraThingType) && thing.Args[3] > 0)
        {
            if (thing.Type == AimingCameraThingType && (thing.Args[0] > 0 || thing.Args[1] > 0))
            {
                int interpolationPointTag = thing.Args[0] + (thing.Args[1] << 8);
                Thing? interpolationPoint = things.FirstOrDefault(candidate =>
                    candidate.Type == InterpolationPointThingType && candidate.Tag == interpolationPointTag);
                if (interpolationPoint != null)
                    position = centerOfThing(interpolationPoint);
            }

            Thing? target = things.FirstOrDefault(candidate => candidate.Tag == thing.Args[3]);
            if (target == null)
            {
                return new VisualCameraPose(
                    position,
                    yaw,
                    0.0,
                    MissingCameraTargetMessagePrefix + thing.Args[3] + " does not exist!");
            }

            Vector3D direction = centerOfThing(target) - position;
            yaw = YawFromDirection(direction);
            pitch = (thing.Args[2] & 4) != 0 ? PitchFromDirection(direction) : 0.0;
        }
        else if ((thing.Type == SecurityCameraThingType || thing.Type == MovingCameraThingType || thing.Type == InterpolationPointThingType) && thing.Args[0] != 0)
        {
            pitch = Angle2D.DegToRad(thing.Args[0]);
        }
        else if (useUdmfPitch)
        {
            pitch = Angle2D.DegToRad(thing.Pitch);
        }

        return new VisualCameraPose(position, yaw, pitch);
    }

    public static double YawFromThingAngle(int doomAngle)
        => Angle2D.Normalized(Angle2D.DoomToReal(doomAngle) + Angle2D.PI);

    private static double YawFromDirection(Vector3D direction)
        => Math.Atan2(direction.y, direction.x);

    private static double PitchFromDirection(Vector3D direction)
    {
        double flat = Math.Sqrt(direction.x * direction.x + direction.y * direction.y);
        return Math.Atan2(direction.z, flat);
    }
}

public sealed record VisualCameraPose(Vector3D Position, double Yaw, double Pitch, string? StatusMessage = null);

public sealed record VisualCameraStartThingPlan(VisualCameraPose Pose, bool PositionChanges);
