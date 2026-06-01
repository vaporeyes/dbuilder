// ABOUTME: UDB-style thing geometry helpers that need game-configuration thing metadata.
// ABOUTME: Keeps metadata-dependent calculations out of DBuilder.Map to avoid a reverse project dependency.

using DBuilder.Map;

namespace DBuilder.IO;

public static class ThingGeometryTools
{
    public static int GetThingAbsoluteZ(Thing thing, ThingTypeInfo info)
    {
        if (info.AbsoluteZ) return (int)thing.Height;

        if (thing.Sector != null)
        {
            if (info.Hangs) return (int)(thing.Sector.CeilHeight - thing.Height - info.Height);

            return (int)(thing.Sector.FloorHeight + thing.Height);
        }

        return (int)thing.Height;
    }
}
