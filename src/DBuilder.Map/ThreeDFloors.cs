// ABOUTME: Resolves GZDoom 3D floors - Sector_3DFloor (special 160) on a control sector's line inserts a slab
// ABOUTME: into every sector tagged with arg0. The control sector's floor/ceiling become the slab's bottom/top.

using System;
using System.Collections.Generic;

namespace DBuilder.Map;

/// <summary>One resolved 3D floor inserted into a target sector by a control sector.</summary>
public sealed record ThreeDFloor(Sector Control, double Bottom, double Top, int Alpha, int TypeBits, string SideTexture, int TargetTag = 0)
{
    /// <summary>Top flat (the surface walked on) - the control sector's ceiling flat.</summary>
    public string TopFlat => Control.CeilTexture;
    /// <summary>Bottom flat (the underside) - the control sector's floor flat.</summary>
    public string BottomFlat => Control.FloorTexture;
    public int Brightness => Control.Brightness;
}

public static class ThreeDFloors
{
    /// <summary>The Hexen/ZDoom Sector_3DFloor linedef special number.</summary>
    public const int Sector3DFloorAction = 160;

    /// <summary>
    /// Maps each target sector to the 3D floors inserted into it. The slab bottom/top come from the control
    /// sector's floor/ceiling heights; the side texture is the control line's middle texture.
    /// </summary>
    public static Dictionary<Sector, List<ThreeDFloor>> Resolve(MapSet map, bool udmf = false, bool requireManagedControlSector = false)
    {
        var byTag = new Dictionary<int, List<Sector>>();
        foreach (var s in map.Sectors)
        {
            foreach (int tag in s.Tags)
            {
                if (tag == 0) continue;
                if (!byTag.TryGetValue(tag, out var lst)) { lst = new List<Sector>(); byTag[tag] = lst; }
                lst.Add(s);
            }
        }

        var result = new Dictionary<Sector, List<ThreeDFloor>>(ReferenceEqualityComparer.Instance);
        foreach (var line in map.Linedefs)
        {
            if (line.Action != Sector3DFloorAction) continue;
            var control = line.Front?.Sector;
            if (control == null) continue;
            if (requireManagedControlSector && !IsManagedControlSector(control, udmf)) continue;
            int tag = line.Args[0];
            if (tag == 0 || !byTag.TryGetValue(tag, out var targets)) continue;

            double bottom = control.FloorHeight, top = control.CeilHeight;
            int alpha = Math.Clamp(line.Args[3], 0, 255);
            string side = line.Front?.MidTexture ?? "-";

            foreach (var t in targets)
            {
                if (ReferenceEquals(t, control)) continue;
                if (!result.TryGetValue(t, out var fl)) { fl = new List<ThreeDFloor>(); result[t] = fl; }
                fl.Add(new ThreeDFloor(control, bottom, top, alpha, line.Args[1], side, tag));
            }
        }
        return result;
    }

    public static List<ThreeDFloor> GetThreeDFloors(MapSet map, IReadOnlyList<Sector> sectors, bool sharedOnly = false, bool udmf = false, bool requireManagedControlSector = false)
    {
        var floorsBySector = Resolve(map, udmf, requireManagedControlSector);
        var floors = new List<ThreeDFloor>();

        foreach (Sector sector in sectors)
        {
            if (!floorsBySector.TryGetValue(sector, out List<ThreeDFloor>? sectorFloors)) continue;

            foreach (ThreeDFloor floor in sectorFloors)
            {
                if (sharedOnly && !IsSharedByAllSelectedSectors(floor, sectors, floorsBySector)) continue;
                if (!floors.Exists(existing => ReferenceEquals(existing.Control, floor.Control))) floors.Add(floor);
            }
        }

        return floors;
    }

    private static bool IsSharedByAllSelectedSectors(ThreeDFloor floor, IReadOnlyList<Sector> sectors, Dictionary<Sector, List<ThreeDFloor>> floorsBySector)
    {
        foreach (Sector sector in sectors)
        {
            if (!floorsBySector.TryGetValue(sector, out List<ThreeDFloor>? sectorFloors)) return false;
            if (!sectorFloors.Exists(candidate => ReferenceEquals(candidate.Control, floor.Control))) return false;
        }

        return true;
    }

    private static bool IsManagedControlSector(Sector control, bool udmf)
    {
        if (!udmf) return true;
        return control.Fields.TryGetValue("user_managed_3d_floor", out object? value)
            && value is bool managed
            && managed;
    }
}
