// ABOUTME: Extends selection duplication so selected sectors carry their resolved 3D floor controls with them.
// ABOUTME: Keeps the 3D floor duplicate action testable while reusing the existing clipboard paste pipeline.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public static class ThreeDFloorSelectionClipboard
{
    public static PasteResult? DuplicateSelectionWithThreeDFloors(
        MapSet map,
        Vector2D offset,
        PasteOptions options,
        GameConfiguration? config,
        Action? beforePaste = null)
    {
        Sector[] selectedSectors = map.Sectors.Where(sector => sector.Selected).ToArray();
        if (selectedSectors.Length == 0)
            return SelectionClipboard.DuplicateSelection(map, offset, options, config, beforePaste);

        var controls = new HashSet<Sector>(ReferenceEqualityComparer.Instance);
        foreach (ThreeDFloor floor in ThreeDFloors.GetThreeDFloors(map, selectedSectors))
            if (!floor.Control.Selected)
                controls.Add(floor.Control);
        Sector[] controlSectors = controls.ToArray();

        foreach (Sector control in controlSectors)
            control.Selected = true;

        byte[]? data;
        try
        {
            data = SelectionClipboard.CopySelection(map);
        }
        finally
        {
            foreach (Sector control in controlSectors)
                control.Selected = false;
        }

        if (data is null) return null;
        beforePaste?.Invoke();
        return SelectionClipboard.Paste(map, data, offset, options, config);
    }
}
