// ABOUTME: Applies UDB-style visual flat rotation increments to selected floor and ceiling surfaces.
// ABOUTME: Keeps UDMF rotation field updates isolated from editor command dispatch.

namespace DBuilder.Map;

public static class VisualFlatRotation
{
    public static int Rotate(IEnumerable<VisualHit> hits, double angleIncrement, bool isUdmf)
    {
        if (!isUdmf) return 0;

        int changed = 0;
        var seen = new HashSet<(Sector Sector, bool Ceiling)>();
        foreach (VisualHit hit in hits)
        {
            if (hit.Kind is not (VisualHitKind.Floor or VisualHitKind.Ceiling) || hit.Sector == null) continue;

            bool ceiling = hit.Kind == VisualHitKind.Ceiling;
            if (!seen.Add((hit.Sector, ceiling))) continue;

            string field = ceiling ? "rotationceiling" : "rotationfloor";
            double angle = ClampAngle(hit.Sector.GetFloatField(field, 0.0) + angleIncrement);
            hit.Sector.SetFloatField(field, angle, 0.0);
            changed++;
        }

        return changed;
    }

    private static double ClampAngle(double angle)
    {
        while (angle < 0.0) angle += 360.0;
        while (angle >= 360.0) angle -= 360.0;
        return angle;
    }
}
