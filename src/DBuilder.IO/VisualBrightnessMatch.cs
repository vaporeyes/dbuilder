// ABOUTME: Applies UDB visual-mode Match Brightness semantics for UDMF surface light fields.
// ABOUTME: Keeps absolute and relative sector or sidedef light handling testable outside editor UI.

using DBuilder.Map;

namespace DBuilder.IO;

public sealed record VisualBrightnessMatchResult(int ChangedSurfaces, string Message);

public static class VisualBrightnessMatch
{
    public const string InvalidTargetMessage = "Highlight a surface, to which you want to match the brightness.";

    public static bool TryReadTargetBrightness(VisualHit hit, out int brightness, out string message)
    {
        brightness = 0;
        message = "";

        switch (hit.Kind)
        {
            case VisualHitKind.Floor when hit.Sector != null:
                brightness = SurfaceLightValue(hit.Sector, "lightfloor", "lightfloorabsolute");
                break;
            case VisualHitKind.Ceiling when hit.Sector != null:
                brightness = SurfaceLightValue(hit.Sector, "lightceiling", "lightceilingabsolute");
                break;
            case VisualHitKind.Wall:
                Sidedef? side = HitSidedef(hit);
                if (side?.Sector == null)
                {
                    message = InvalidTargetMessage;
                    return false;
                }

                brightness = SidedefLightValue(side);
                break;
            default:
                message = InvalidTargetMessage;
                return false;
        }

        brightness = Math.Clamp(brightness, 0, 255);
        return true;
    }

    public static VisualBrightnessMatchResult Apply(
        int targetBrightness,
        IEnumerable<VisualHit> selection,
        VisualHit highlighted,
        GameConfiguration? config)
    {
        VisualHit[] selected = selection.ToArray();
        int brightness = Math.Clamp(targetBrightness, 0, 255);
        int changed = 0;

        foreach (VisualHit hit in selected)
        {
            if (SameSurface(hit, highlighted)) continue;

            switch (hit.Kind)
            {
                case VisualHitKind.Floor when hit.Sector != null:
                    WriteSurfaceLightValue(hit.Sector, "lightfloor", "lightfloorabsolute", brightness);
                    changed++;
                    break;
                case VisualHitKind.Ceiling when hit.Sector != null:
                    WriteSurfaceLightValue(hit.Sector, "lightceiling", "lightceilingabsolute", brightness);
                    changed++;
                    break;
                case VisualHitKind.Wall:
                    Sidedef? side = HitSidedef(hit);
                    if (side?.Sector == null) break;
                    WriteSidedefLightValue(side, brightness);
                    SidedefFogTools.UpdateLightFogFlag(side, mapInfo: null, config);
                    changed++;
                    break;
            }
        }

        return new VisualBrightnessMatchResult(changed, $"Matched brightness for {selected.Length} surfaces.");
    }

    private static int SurfaceLightValue(Sector sector, string lightKey, string absoluteKey)
        => sector.GetField(absoluteKey, false)
            ? sector.GetIntegerField(lightKey)
            : Math.Clamp(sector.Brightness + sector.GetIntegerField(lightKey), 0, 255);

    private static void WriteSurfaceLightValue(Sector sector, string lightKey, string absoluteKey, int brightness)
    {
        if (sector.GetField(absoluteKey, false))
            sector.SetIntegerField(lightKey, brightness, 0);
        else
            sector.SetIntegerField(lightKey, brightness - sector.Brightness, 0);
    }

    private static int SidedefLightValue(Sidedef side)
        => side.GetField("lightabsolute", false)
            ? side.GetIntegerField("light")
            : Math.Clamp(side.Sector!.Brightness + side.GetIntegerField("light"), 0, 255);

    private static void WriteSidedefLightValue(Sidedef side, int brightness)
    {
        if (side.GetField("lightabsolute", false))
            side.SetIntegerField("light", brightness, 0);
        else
            side.SetIntegerField("light", brightness - side.Sector!.Brightness, 0);
    }

    private static Sidedef? HitSidedef(VisualHit hit)
        => hit.Front ? hit.Line?.Front : hit.Line?.Back;

    private static bool SameSurface(VisualHit a, VisualHit b)
    {
        if (a.Kind != b.Kind) return false;
        return a.Kind switch
        {
            VisualHitKind.Floor or VisualHitKind.Ceiling => ReferenceEquals(a.Sector, b.Sector),
            VisualHitKind.Wall => ReferenceEquals(a.Line, b.Line) && a.Front == b.Front && a.Part == b.Part,
            VisualHitKind.Thing => ReferenceEquals(a.Thing, b.Thing),
            _ => false,
        };
    }
}
