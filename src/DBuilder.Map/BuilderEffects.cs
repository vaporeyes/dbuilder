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

public readonly record struct SectorVertexHeightJitter(Vertex Vertex, double InitialFloorHeight, double InitialCeilingHeight, double FloorFactor, double CeilingFactor);

public readonly record struct SectorHeightJitter(
    Sector Sector,
    int InitialFloorHeight,
    int InitialCeilingHeight,
    double FloorFactor,
    double CeilingFactor,
    int SafeDistance = int.MaxValue,
    IReadOnlyList<SectorVertexHeightJitter>? VertexHeights = null);

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

public readonly record struct DirectionalShadingOptions(
    int SunAngleDegrees = 45,
    int LightAmount = 64,
    int LightColor = 0xFDEBD7,
    int ShadeAmount = 16,
    int ShadeColor = 0xABC8EB);

public readonly record struct DirectionalShadingSector(Sector Sector, Vector3D Normal, int InitialBrightness, int InitialLightColor);

public readonly record struct DirectionalShadingSide(Sidedef Sidedef, Vector3D Normal, int InitialBrightness);

public static class BuilderEffects
{
    public const int WhiteNoAlpha = 0x00FFFFFF;

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

    public static int ApplySectorFloorHeight(
        IReadOnlyList<SectorHeightJitter> sectors,
        int amount,
        JitterOffsetMode mode = JitterOffsetMode.RaiseAndLower,
        bool useVertexHeights = false)
    {
        foreach (SectorHeightJitter jitter in sectors)
        {
            int current = Math.Min(amount, jitter.SafeDistance);
            if (useVertexHeights && jitter.VertexHeights is { Count: > 0 } vertexHeights)
            {
                foreach (SectorVertexHeightJitter vertexHeight in vertexHeights)
                    vertexHeight.Vertex.ZFloor = vertexHeight.InitialFloorHeight + Math.Floor(current * ModifyByOffsetMode(vertexHeight.FloorFactor, mode));
            }
            else
            {
                jitter.Sector.FloorHeight = jitter.InitialFloorHeight + (int)Math.Floor(current * ModifyByOffsetMode(jitter.FloorFactor, mode));
            }
        }

        return sectors.Count;
    }

    public static int ApplySectorCeilingHeight(
        IReadOnlyList<SectorHeightJitter> sectors,
        int amount,
        JitterOffsetMode mode = JitterOffsetMode.RaiseAndLower,
        bool useVertexHeights = false)
    {
        foreach (SectorHeightJitter jitter in sectors)
        {
            int current = Math.Min(amount, jitter.SafeDistance);
            if (useVertexHeights && jitter.VertexHeights is { Count: > 0 } vertexHeights)
            {
                foreach (SectorVertexHeightJitter vertexHeight in vertexHeights)
                    vertexHeight.Vertex.ZCeiling = vertexHeight.InitialCeilingHeight - Math.Floor(current * ModifyByOffsetMode(vertexHeight.CeilingFactor, mode));
            }
            else
            {
                jitter.Sector.CeilHeight = jitter.InitialCeilingHeight - (int)Math.Floor(current * ModifyByOffsetMode(jitter.CeilingFactor, mode));
            }
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

    public static DirectionalShadingSector CaptureDirectionalShadingSector(Sector sector)
    {
        int brightness = IsAbsoluteLight(sector, "lightfloorabsolute")
            ? sector.GetIntegerField("lightfloor")
            : sector.Brightness;
        int color = sector.GetIntegerField("lightcolor", WhiteNoAlpha);
        Vector3D normal = sector.HasFloorSlope ? sector.FloorSlope.GetNormal() : new Vector3D(0, 0, 1);

        return new DirectionalShadingSector(sector, normal, brightness, color);
    }

    public static DirectionalShadingSide CaptureDirectionalShadingSide(Sidedef sidedef)
    {
        Vector3D normal = Vector3D.FromAngleXY(sidedef.Line.Angle + Angle2D.PIHALF);
        if (sidedef.IsFront) normal = -normal;

        int brightness = IsAbsoluteLight(sidedef, "lightabsolute")
            ? sidedef.GetIntegerField("light")
            : sidedef.Sector?.Brightness ?? 0;

        return new DirectionalShadingSide(sidedef, normal, brightness);
    }

    public static int ApplyDirectionalShading(
        IReadOnlyList<DirectionalShadingSector> sectors,
        IReadOnlyList<DirectionalShadingSide> sides,
        DirectionalShadingOptions options)
    {
        Vector3D sunvector = Vector3D.FromAngleXYZ(Angle2D.DegToRad(options.SunAngleDegrees + 90), Angle2D.DegToRad(45));

        foreach (DirectionalShadingSector sector in sectors)
        {
            DirectionalShadingResult result = CalculateDirectionalShading(sector.Normal, sunvector, options);
            if (IsAbsoluteLight(sector.Sector, "lightfloorabsolute"))
                sector.Sector.SetIntegerField("lightfloor", Clamp(result.Light + sector.InitialBrightness, 0, 255), 0);
            else
                sector.Sector.SetIntegerField("lightfloor", Clamp(result.Light, -255, 255), 0);

            int color = result.Color & WhiteNoAlpha;
            if (color == WhiteNoAlpha) color = sector.InitialLightColor;
            sector.Sector.SetIntegerField("lightcolor", color, WhiteNoAlpha);
        }

        foreach (DirectionalShadingSide side in sides)
        {
            DirectionalShadingResult result = CalculateDirectionalShading(side.Normal, sunvector, options);
            if (IsAbsoluteLight(side.Sidedef, "lightabsolute"))
                side.Sidedef.SetIntegerField("light", Clamp(result.Light + side.InitialBrightness, 0, 255), 0);
            else
                side.Sidedef.SetIntegerField("light", Clamp(result.Light, -255, 255), 0);
        }

        return sectors.Count + sides.Count;
    }

    private static int NormalizeAngle(int angle)
    {
        int normalized = angle % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static DirectionalShadingResult CalculateDirectionalShading(
        Vector3D normal,
        Vector3D sunvector,
        DirectionalShadingOptions options)
    {
        double anglediff = Vector3D.DotProduct(normal.GetNormal(), sunvector);
        int light;
        if (anglediff >= 0.5f)
        {
            double lightmul = (anglediff - 0.5f) * 2.0f;
            light = (int)Math.Round(options.LightAmount * lightmul);
        }
        else
        {
            double lightmul = (0.5f - anglediff) * -2.0f;
            light = (int)Math.Round(options.ShadeAmount * lightmul);
        }

        uint color = InterpolationTools.InterpolateColor((uint)options.ShadeColor, (uint)options.LightColor, anglediff);
        return new DirectionalShadingResult(light, (int)(color & WhiteNoAlpha));
    }

    private static bool IsAbsoluteLight(IFielded element, string flagName)
    {
        if (element.Fields.TryGetValue(flagName, out object? raw) && raw is bool fieldValue) return fieldValue;
        return element switch
        {
            Sector sector => sector.UdmfFlags.Contains(flagName),
            Sidedef sidedef => sidedef.UdmfFlags.Contains(flagName),
            _ => false,
        };
    }

    private static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));

    private readonly record struct DirectionalShadingResult(int Light, int Color);
}
