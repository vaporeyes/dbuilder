// ABOUTME: Applies UDB-style visual scale increments to wall texture parts and things.
// ABOUTME: Keeps the per-pixel scale math isolated from editor command dispatch.

namespace DBuilder.Map;

public static class VisualScaleAdjustment
{
    public static bool AdjustThing(Thing thing, int incrementX, int incrementY, int spriteWidth, int spriteHeight)
    {
        if (spriteWidth <= 0 || spriteHeight <= 0) return false;

        double scaleX = thing.ScaleX == 0.0 ? 1.0 : thing.ScaleX;
        double scaleY = thing.ScaleY == 0.0 ? 1.0 : thing.ScaleY;

        if (incrementX != 0)
            scaleX = AdjustScaleByPixels(scaleX, spriteWidth, incrementX, add: true);

        if (incrementY != 0)
            scaleY = AdjustScaleByPixels(scaleY, spriteHeight, incrementY, add: true);

        if (scaleX == thing.ScaleX && scaleY == thing.ScaleY) return false;
        thing.SetScale(scaleX, scaleY);
        return true;
    }

    public static bool AdjustWall(Sidedef side, SidedefPart part, int incrementX, int incrementY, int textureWidth, int textureHeight)
    {
        if (part == SidedefPart.None || textureWidth <= 0 || textureHeight <= 0) return false;

        string xField = ScaleXField(part);
        string yField = ScaleYField(part);
        double scaleX = side.GetFloatField(xField, 1.0);
        double scaleY = side.GetFloatField(yField, 1.0);
        double nextX = scaleX;
        double nextY = scaleY;

        if (incrementX != 0)
            nextX = AdjustScaleByPixels(scaleX, textureWidth, incrementX, add: false);

        if (incrementY != 0)
            nextY = AdjustScaleByPixels(scaleY, textureHeight, incrementY, add: false);

        bool changed = false;
        if (nextX != scaleX)
        {
            side.SetFloatField(xField, nextX, 1.0);
            changed = true;
        }

        if (nextY != scaleY)
        {
            side.SetFloatField(yField, nextY, 1.0);
            changed = true;
        }

        return changed;
    }

    private static double AdjustScaleByPixels(double scale, int dimension, int increment, bool add)
    {
        double pixels = (int)Math.Round(dimension * scale) + (add ? increment : -increment);
        double adjusted = Math.Round(pixels / dimension, 3);
        return adjusted == 0.0 ? scale * -1.0 : adjusted;
    }

    private static string ScaleXField(SidedefPart part) => "scalex_" + PartName(part);
    private static string ScaleYField(SidedefPart part) => "scaley_" + PartName(part);

    private static string PartName(SidedefPart part) => part switch
    {
        SidedefPart.Upper => "top",
        SidedefPart.Middle => "mid",
        SidedefPart.Lower => "bottom",
        _ => "mid",
    };
}
