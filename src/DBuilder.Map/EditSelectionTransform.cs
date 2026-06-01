// ABOUTME: UI-independent helpers for UDB BuilderModes edit-selection transform behavior.
// ABOUTME: Keeps interactive transform math testable without depending on the editor window.

using DBuilder.Geometry;

namespace DBuilder.Map;

public static class EditSelectionTransform
{
    public const int RotationSnapSteps = 24;

    public static void AdjustSectorHeights(
        IEnumerable<Sector> sectors,
        EditSelectionHeightAdjustMode mode,
        int? oldFloorHeight,
        int? oldCeilingHeight,
        int? outsideFloorHeight,
        int? outsideCeilingHeight,
        bool udmf)
    {
        if (mode == EditSelectionHeightAdjustMode.None || oldFloorHeight == null || oldCeilingHeight == null) return;
        if (outsideFloorHeight == null && outsideCeilingHeight == null) return;

        int? floorOffset = outsideFloorHeight == null ? null : outsideFloorHeight.Value - oldFloorHeight.Value;
        int? ceilingOffset = outsideCeilingHeight == null ? null : outsideCeilingHeight.Value - oldCeilingHeight.Value;

        foreach (var sector in sectors)
        {
            switch (mode)
            {
                case EditSelectionHeightAdjustMode.AdjustFloors:
                    AdjustSectorHeight(sector, floorOffset, null, udmf);
                    break;
                case EditSelectionHeightAdjustMode.AdjustCeilings:
                    AdjustSectorHeight(sector, null, ceilingOffset, udmf);
                    break;
                case EditSelectionHeightAdjustMode.AdjustBoth:
                    AdjustSectorHeight(sector, floorOffset, ceilingOffset, udmf);
                    break;
            }
        }
    }

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

    private static void AdjustSectorHeight(Sector sector, int? floorOffset, int? ceilingOffset, bool udmf)
    {
        if (floorOffset != null)
        {
            sector.FloorHeight += floorOffset.Value;
            if (udmf) AdjustFloorSurface(sector, floorOffset.Value);
        }

        if (ceilingOffset != null)
        {
            sector.CeilHeight += ceilingOffset.Value;
            if (udmf) AdjustCeilingSurface(sector, ceilingOffset.Value);
        }
    }

    private static void AdjustFloorSurface(Sector sector, int offset)
    {
        if (HasAdjustableSlope(sector.FloorSlope, sector.FloorSlopeOffset))
            sector.FloorSlopeOffset -= offset * Math.Sin(sector.FloorSlope.GetAngleZ());
        else
            AdjustTriangleVertexHeights(sector, floor: true, offset);
    }

    private static void AdjustCeilingSurface(Sector sector, int offset)
    {
        if (HasAdjustableSlope(sector.CeilSlope, sector.CeilSlopeOffset))
            sector.CeilSlopeOffset -= offset * Math.Sin(sector.CeilSlope.GetAngleZ());
        else
            AdjustTriangleVertexHeights(sector, floor: false, offset);
    }

    private static bool HasAdjustableSlope(Vector3D slope, double offset)
        => slope.GetLengthSq() > 0 && !double.IsNaN(offset / slope.z);

    private static void AdjustTriangleVertexHeights(Sector sector, bool floor, int offset)
    {
        if (sector.Sidedefs.Count != 3) return;

        var vertices = new HashSet<Vertex>();
        foreach (var side in sector.Sidedefs)
        {
            vertices.Add(side.Line.Start);
            vertices.Add(side.Line.End);
        }

        foreach (var vertex in vertices)
        {
            if (floor)
            {
                if (!double.IsNaN(vertex.ZFloor)) vertex.ZFloor += offset;
            }
            else
            {
                if (!double.IsNaN(vertex.ZCeiling)) vertex.ZCeiling += offset;
            }
        }
    }
}
