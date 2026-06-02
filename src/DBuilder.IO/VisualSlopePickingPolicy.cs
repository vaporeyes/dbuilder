// ABOUTME: Encapsulates UDB visual slope picking availability checks for map format and game configuration.
// ABOUTME: Keeps editor UI command guards testable without depending on Avalonia controls.

using DBuilder.Map;

namespace DBuilder.IO;

public static class VisualSlopePickingPolicy
{
    public const string UdmfRequiredMessage = "Visual sloping is supported in UDMF only.";
    public const string PlaneEquationRequiredMessage = "Visual sloping is not supported in this game configuration.";

    public static bool CanUse(MapFormat mapFormat, GameConfiguration? configuration, out string warning)
    {
        if (mapFormat != MapFormat.Udmf)
        {
            warning = UdmfRequiredMessage;
            return false;
        }

        if (configuration?.PlaneEquationSupport != true)
        {
            warning = PlaneEquationRequiredMessage;
            return false;
        }

        warning = "";
        return true;
    }
}
