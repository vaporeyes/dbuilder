// ABOUTME: Applies UDB-style floor and ceiling flat alignment from selected linedefs.
// ABOUTME: Writes UDMF rotation and panning fields without depending on editor resource APIs.

using DBuilder.Geometry;

namespace DBuilder.Map;

public readonly record struct SectorFlatAlignmentResult(bool Applied, int SectorCount, string Message);

public readonly record struct SectorFlatAlignmentTexture(int Width, int Height);

public static class SectorFlatAlignment
{
    public static SectorFlatAlignmentResult AlignToClosestLine(
        Sector sector,
        ICollection<Linedef> lines,
        Vector2D hitPosition,
        bool floors,
        bool alignX,
        bool alignY,
        SectorFlatAlignmentTexture? texture = null)
    {
        if (lines.Count == 0)
            return new SectorFlatAlignmentResult(false, 0, "This action requires sector linedefs!");

        Linedef? line = MapSet.NearestLinedef(lines, hitPosition);
        if (line == null)
            return new SectorFlatAlignmentResult(false, 0, "This action requires sector linedefs!");

        AlignSectorToClosestLine(line, sector, hitPosition, floors, alignX, alignY, texture);
        string target = floors ? "floor" : "ceiling";
        string axis = alignX && alignY ? "X and Y" : alignX ? "X" : "Y";
        return new SectorFlatAlignmentResult(true, 1, $"Aligned {target} texture on {axis}.");
    }

    public static SectorFlatAlignmentResult AlignToLinedefs(
        IReadOnlyList<Linedef> lines,
        bool floors,
        bool frontSide,
        Func<Sector, SectorFlatAlignmentTexture?>? flatSize = null)
    {
        if (lines.Count == 0)
            return new SectorFlatAlignmentResult(false, 0, "This action requires a selection!");

        int count = 0;
        foreach (var line in lines)
        {
            Sector? sector = frontSide ? line.Front?.Sector : line.Back?.Sector;
            if (sector == null) continue;

            AlignSector(line, sector, floors, frontSide, flatSize?.Invoke(sector));
            count++;
        }

        string target = (floors ? "Floors" : "Ceilings") + " to " + (frontSide ? "Front" : "Back") + " Side";
        return new SectorFlatAlignmentResult(count > 0, count, $"Aligned {count} {target}");
    }

    private static void AlignSector(
        Linedef line,
        Sector sector,
        bool floors,
        bool frontSide,
        SectorFlatAlignmentTexture? texture)
    {
        double sourceAngle = frontSide
            ? ClampAngle(-Angle2D.RadToDeg(line.Angle) + 90.0)
            : ClampAngle(ClampAngle(-Angle2D.RadToDeg(line.Angle) - 90.0) + 180.0);

        sector.SetFloatField(floors ? "rotationfloor" : "rotationceiling", Math.Round(sourceAngle, 1), 0.0);

        Vector2D sourcePoint = frontSide ? line.Start.Position : line.End.Position;
        Vector2D offset = sourcePoint.GetRotated(Angle2D.DegToRad(sourceAngle));
        if (texture is { Width: > 0, Height: > 0 })
        {
            double xScale = sector.GetFloatField(floors ? "xscalefloor" : "xscaleceiling", 1.0);
            double yScale = sector.GetFloatField(floors ? "yscalefloor" : "yscaleceiling", 1.0);
            double scaledWidth = texture.Value.Width / xScale;
            double scaledHeight = texture.Value.Height / yScale;
            if (scaledWidth != 0.0) offset.x %= scaledWidth;
            if (scaledHeight != 0.0) offset.y %= scaledHeight;
        }

        sector.SetFloatField(floors ? "xpanningfloor" : "xpanningceiling", Math.Round(-offset.x, 6), 0.0);
        sector.SetFloatField(floors ? "ypanningfloor" : "ypanningceiling", Math.Round(offset.y, 6), 0.0);
    }

    private static void AlignSectorToClosestLine(
        Linedef line,
        Sector sector,
        Vector2D hitPosition,
        bool floors,
        bool alignX,
        bool alignY,
        SectorFlatAlignmentTexture? texture)
    {
        bool isFront = line.SideOfLine(hitPosition) > 0.0;
        double sourceAngle = isFront
            ? ClampAngle(-Angle2D.RadToDeg(line.Angle) + 90.0)
            : ClampAngle(-Angle2D.RadToDeg(line.Angle) - 90.0);
        if (!isFront) sourceAngle = ClampAngle(sourceAngle + 180.0);

        sector.SetFloatField(floors ? "rotationfloor" : "rotationceiling", Math.Round(sourceAngle, 1), 0.0);

        double distToStart = Vector2D.Distance(hitPosition, line.Start.Position);
        double distToEnd = Vector2D.Distance(hitPosition, line.End.Position);
        Vector2D sourcePoint = distToStart < distToEnd ? line.Start.Position : line.End.Position;
        Vector2D offset = sourcePoint.GetRotated(Angle2D.DegToRad(sourceAngle));
        if (texture is { Width: > 0, Height: > 0 })
        {
            double xScale = sector.GetFloatField(floors ? "xscalefloor" : "xscaleceiling", 1.0);
            double yScale = sector.GetFloatField(floors ? "yscalefloor" : "yscaleceiling", 1.0);
            double scaledWidth = texture.Value.Width / xScale;
            double scaledHeight = texture.Value.Height / yScale;
            if (scaledWidth != 0.0) offset.x %= scaledWidth;
            if (scaledHeight != 0.0) offset.y %= scaledHeight;
        }

        if (alignX)
            sector.SetFloatField(floors ? "xpanningfloor" : "xpanningceiling", Math.Round(-offset.x, 6), 0.0);
        if (alignY)
            sector.SetFloatField(floors ? "ypanningfloor" : "ypanningceiling", Math.Round(offset.y, 6), 0.0);
    }

    private static double ClampAngle(double angle)
    {
        while (angle < 0.0) angle += 360.0;
        while (angle >= 360.0) angle -= 360.0;
        return angle;
    }
}
