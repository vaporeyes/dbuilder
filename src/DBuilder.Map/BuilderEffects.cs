// ABOUTME: Data-level BuilderEffects helpers for UDB-style jitter transforms.
// ABOUTME: Applies cached random factors to vertices, sectors, and things without UI dependencies.

using DBuilder.Geometry;

namespace DBuilder.Map;

public enum JitterOffsetMode
{
    RaiseAndLower,
    RaiseOnly,
    LowerOnly
}

public readonly record struct VertexJitter(Vertex Vertex, Vector2D InitialPosition, double AngleRadians, int SafeDistance = int.MaxValue);

public readonly record struct SectorHeightJitter(Sector Sector, int InitialFloorHeight, int InitialCeilingHeight, double FloorFactor, double CeilingFactor, int SafeDistance = int.MaxValue);

public readonly record struct ThingJitter(
    Thing Thing,
    Vector2D InitialPosition,
    int InitialAngle,
    int InitialPitch,
    int InitialRoll,
    double InitialHeight,
    double InitialScaleX,
    double InitialScaleY,
    double OffsetAngle,
    double RotationFactor,
    double PitchFactor,
    double RollFactor,
    double HeightFactor,
    double ScaleXFactor,
    double ScaleYFactor,
    int SafeDistance = int.MaxValue,
    int SectorHeight = int.MaxValue);

public static class BuilderEffects
{
    public static int ApplyVertexTranslation(IReadOnlyList<VertexJitter> vertices, int amount)
    {
        foreach (VertexJitter jitter in vertices)
        {
            int current = Math.Min(amount, jitter.SafeDistance);
            jitter.Vertex.Move(new Vector2D(
                jitter.InitialPosition.x + (int)(Math.Sin(jitter.AngleRadians) * current),
                jitter.InitialPosition.y + (int)(Math.Cos(jitter.AngleRadians) * current)));
        }

        return vertices.Count;
    }

    public static int ApplySectorFloorHeight(IReadOnlyList<SectorHeightJitter> sectors, int amount, JitterOffsetMode mode = JitterOffsetMode.RaiseAndLower)
    {
        foreach (SectorHeightJitter jitter in sectors)
        {
            int current = Math.Min(amount, jitter.SafeDistance);
            jitter.Sector.FloorHeight = jitter.InitialFloorHeight + (int)Math.Floor(current * ModifyByOffsetMode(jitter.FloorFactor, mode));
        }

        return sectors.Count;
    }

    public static int ApplySectorCeilingHeight(IReadOnlyList<SectorHeightJitter> sectors, int amount, JitterOffsetMode mode = JitterOffsetMode.RaiseAndLower)
    {
        foreach (SectorHeightJitter jitter in sectors)
        {
            int current = Math.Min(amount, jitter.SafeDistance);
            jitter.Sector.CeilHeight = jitter.InitialCeilingHeight - (int)Math.Floor(current * ModifyByOffsetMode(jitter.CeilingFactor, mode));
        }

        return sectors.Count;
    }

    public static int ApplyThingTranslation(IReadOnlyList<ThingJitter> things, int amount)
    {
        foreach (ThingJitter jitter in things)
        {
            int current = Math.Min(amount, jitter.SafeDistance);
            jitter.Thing.Move(new Vector2D(
                jitter.InitialPosition.x + (int)(Math.Sin(jitter.OffsetAngle) * current),
                jitter.InitialPosition.y + (int)(Math.Cos(jitter.OffsetAngle) * current)));
        }

        return things.Count;
    }

    public static int ApplyThingRotation(IReadOnlyList<ThingJitter> things, int amount, bool snapToDoomAngles = false)
    {
        foreach (ThingJitter jitter in things)
        {
            int angle = (int)Math.Round(jitter.InitialAngle + amount * jitter.RotationFactor);
            if (snapToDoomAngles) angle = angle / 45 * 45;
            jitter.Thing.Rotate(NormalizeAngle(angle));
        }

        return things.Count;
    }

    public static int ApplyThingPitch(IReadOnlyList<ThingJitter> things, int amount, bool relative)
    {
        foreach (ThingJitter jitter in things)
        {
            int pitch = relative
                ? (int)((jitter.InitialPitch + amount * jitter.PitchFactor) % 360)
                : (int)((amount * jitter.PitchFactor) % 360);
            jitter.Thing.SetPitch(pitch);
        }

        return things.Count;
    }

    public static int ApplyThingRoll(IReadOnlyList<ThingJitter> things, int amount, bool relative)
    {
        foreach (ThingJitter jitter in things)
        {
            int roll = relative
                ? (int)((jitter.InitialRoll + amount * jitter.RollFactor) % 360)
                : (int)((amount * jitter.RollFactor) % 360);
            jitter.Thing.SetRoll(roll);
        }

        return things.Count;
    }

    public static int ApplyThingHeight(IReadOnlyList<ThingJitter> things, int amount)
    {
        foreach (ThingJitter jitter in things)
        {
            if (jitter.SectorHeight == 0) continue;
            int current = Math.Min(jitter.SectorHeight, Math.Max(0, (int)jitter.InitialHeight + amount));
            jitter.Thing.Move(jitter.Thing.Position.x, jitter.Thing.Position.y, current * jitter.HeightFactor);
        }

        return things.Count;
    }

    public static int ApplyThingScale(IReadOnlyList<ThingJitter> things, double minX, double maxX, double minY, double maxY, bool relative = false, bool uniform = false)
    {
        if (uniform)
        {
            minY = minX;
            maxY = maxX;
        }

        if (minX > maxX) (minX, maxX) = (maxX, minX);
        if (minY > maxY) (minY, maxY) = (maxY, minY);

        double diffX = maxX - minX;
        double diffY = maxY - minY;

        foreach (ThingJitter jitter in things)
        {
            double factorX = jitter.ScaleXFactor;
            double factorY = uniform ? factorX : jitter.ScaleYFactor;
            double scaleX = minX + diffX * factorX;
            double scaleY = minY + diffY * factorY;

            if (relative)
            {
                scaleX += jitter.InitialScaleX;
                scaleY += jitter.InitialScaleY;
            }

            jitter.Thing.SetScale(scaleX, scaleY);
        }

        return things.Count;
    }

    public static double ModifyByOffsetMode(double value, JitterOffsetMode mode) => mode switch
    {
        JitterOffsetMode.RaiseAndLower => value,
        JitterOffsetMode.RaiseOnly => Math.Abs(value),
        JitterOffsetMode.LowerOnly => -Math.Abs(value),
        _ => value,
    };

    private static int NormalizeAngle(int angle)
    {
        int normalized = angle % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}
