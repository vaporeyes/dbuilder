// ABOUTME: Resolves GZDoom 3D floors - Sector_3DFloor (special 160) on a control sector's line inserts a slab
// ABOUTME: into every sector tagged with arg0. The control sector's floor/ceiling become the slab's bottom/top.

using System;
using System.Collections.Generic;

namespace DBuilder.Map;

/// <summary>One resolved 3D floor inserted into a target sector by a control sector.</summary>
public sealed record ThreeDFloor(Sector Control, double Bottom, double Top, int Alpha, int TypeBits, string SideTexture)
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
    public static Dictionary<Sector, List<ThreeDFloor>> Resolve(MapSet map)
    {
        var byTag = new Dictionary<int, List<Sector>>();
        foreach (var s in map.Sectors)
        {
            if (s.Tag == 0) continue;
            if (!byTag.TryGetValue(s.Tag, out var lst)) { lst = new List<Sector>(); byTag[s.Tag] = lst; }
            lst.Add(s);
        }

        var result = new Dictionary<Sector, List<ThreeDFloor>>(ReferenceEqualityComparer.Instance);
        foreach (var line in map.Linedefs)
        {
            if (line.Action != Sector3DFloorAction) continue;
            var control = line.Front?.Sector;
            if (control == null) continue;
            int tag = line.Args[0];
            if (tag == 0 || !byTag.TryGetValue(tag, out var targets)) continue;

            double bottom = control.FloorHeight, top = control.CeilHeight;
            int alpha = Math.Clamp(line.Args[3], 0, 255);
            string side = line.Front?.MidTexture ?? "-";

            foreach (var t in targets)
            {
                if (ReferenceEquals(t, control)) continue;
                if (!result.TryGetValue(t, out var fl)) { fl = new List<ThreeDFloor>(); result[t] = fl; }
                fl.Add(new ThreeDFloor(control, bottom, top, alpha, line.Args[1], side));
            }
        }
        return result;
    }
}
