// ABOUTME: Resolves GZDoom 3D floors - Sector_3DFloor (special 160) on a control sector's line inserts a slab
// ABOUTME: into every sector tagged with arg0. The control sector's floor/ceiling become the slab's bottom/top.

using System;
using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

/// <summary>One resolved 3D floor inserted into a target sector by a control sector.</summary>
public sealed record ThreeDFloor(Sector Control, double Bottom, double Top, int Alpha, int TypeBits, int Flags, string SideTexture, int TargetTag = 0)
{
    /// <summary>Top flat (the surface walked on) - the control sector's ceiling flat.</summary>
    public string TopFlat => Control.CeilTexture;
    /// <summary>Bottom flat (the underside) - the control sector's floor flat.</summary>
    public string BottomFlat => Control.FloorTexture;
    public int Brightness => Control.Brightness;
}

public sealed record ThreeDFloorControlEdit(
    int BottomHeight,
    int TopHeight,
    string BottomFlat,
    string TopFlat,
    string SideTexture,
    int Type,
    int Flags,
    int Alpha,
    int Brightness,
    Vector3D FloorSlope,
    double FloorSlopeOffset,
    Vector3D CeilingSlope,
    double CeilingSlopeOffset,
    IReadOnlyList<int> Tags);

public readonly record struct ThreeDFloorCleanupResult(int ClearedLines, bool ControlSectorDeleted);

public sealed record ThreeDFloorModeDescriptor(
    string DisplayName,
    string SwitchAction,
    string ButtonImage,
    int ButtonOrder,
    string ButtonGroup,
    bool UseByDefault,
    bool SafeStartMode,
    bool Volatile,
    bool AllowCopyPaste,
    bool Deprecated,
    string DeprecationMessage,
    IReadOnlyList<string> SupportedMapFormats,
    IReadOnlyList<string> RequiredMapFeatures);

public sealed record ThreeDFloorActionDescriptor(
    string Id,
    string Title,
    string Category,
    string Description,
    bool AllowKeys = true,
    bool AllowMouse = true,
    bool AllowScroll = false,
    bool DisregardShift = false,
    bool DisregardControl = false,
    int? DefaultInput = null);

public static class ThreeDFloors
{
    /// <summary>The Hexen/ZDoom Sector_3DFloor linedef special number.</summary>
    public const int Sector3DFloorAction = 160;
    public const string ActionCategory = "threedfloorplugin";
    public const string ActionCategoryTitle = "3D Floor Plugin";

    public static ThreeDFloorModeDescriptor ModeDescriptor { get; } = new(
        "3D Floor Mode",
        "threedfloorhelpermode",
        "ThreeDFloorIcon.png",
        int.MinValue + 501,
        "000_editing",
        UseByDefault: true,
        SafeStartMode: false,
        Volatile: false,
        AllowCopyPaste: true,
        Deprecated: false,
        DeprecationMessage: "",
        SupportedMapFormats: new[] { "HexenMapSetIO", "UniversalMapSetIO" },
        RequiredMapFeatures: new[] { "Effect3DFloorSupport" });

    public static ThreeDFloorModeDescriptor SlopeModeDescriptor { get; } = new(
        "Slope Mode",
        "threedslopemode",
        "SlopeModeIcon.png",
        int.MinValue + 501,
        "000_editing",
        UseByDefault: true,
        SafeStartMode: false,
        Volatile: false,
        AllowCopyPaste: true,
        Deprecated: true,
        DeprecationMessage: "Please use the visual sloping functionality instead.",
        SupportedMapFormats: new[] { "UniversalMapSetIO" },
        RequiredMapFeatures: new[] { "PlaneEquationSupport" });

    public static ThreeDFloorModeDescriptor DrawSlopesModeDescriptor { get; } = new(
        "Draw Slopes Mode",
        "drawslopesmode",
        "DrawSlopeModeIcon.png",
        int.MinValue + 501,
        "000_editing",
        UseByDefault: true,
        SafeStartMode: false,
        Volatile: true,
        AllowCopyPaste: false,
        Deprecated: true,
        DeprecationMessage: "Please use the visual sloping functionality instead.",
        SupportedMapFormats: new[] { "UniversalMapSetIO" },
        RequiredMapFeatures: new[] { "PlaneEquationSupport" });

    public static IReadOnlyList<ThreeDFloorActionDescriptor> ActionDescriptors { get; } =
    [
        new("threedfloorhelpermode", "3D floor editing mode", ActionCategory, "Edits 3D floors", AllowScroll: true),
        new("threedslopemode", "Slope mode", ActionCategory, "Edits slope vertex groups", AllowScroll: true),
        new("drawslopesmode", "Draw slope mode", ActionCategory, "Draws a slope vertex group", AllowScroll: true),
        new("drawslopepoint", "Draw slope vertex", ActionCategory, "Draws a slope vertex at the mousecursor position.", AllowScroll: true, DisregardShift: true, DisregardControl: true, DefaultInput: 1),
        new("drawfloorslope", "Draw Floor Slope", ActionCategory, "The drawn slope will be applied to the floor", AllowScroll: true),
        new("drawceilingslope", "Draw Ceiling Slope", ActionCategory, "The drawn slope will be applied to the ceiling", AllowScroll: true),
        new("drawfloorandceilingslope", "Draw Floor and Ceiling Slope", ActionCategory, "The drawn slope will be applied to the floor and ceiling", AllowScroll: true),
        new("finishslopedraw", "Finish Slope Drawing", ActionCategory, "Finishes the slope drawing.", AllowScroll: true, DefaultInput: 2),
        new("threedflipslope", "Flip 3D slope", ActionCategory, ""),
        new("cyclehighlighted3dfloorup", "Cycle highlighted 3D floor up", ActionCategory, "Cycles up through the 3D floors of the currently highlighted sector", AllowScroll: true, DefaultInput: 131066),
        new("cyclehighlighted3dfloordown", "Cycle highlighted 3D floor down", ActionCategory, "Cycles down through the 3D floors of the currently highlighted sector", AllowScroll: true, DefaultInput: 131067),
        new("relocate3dfloorcontrolsectors", "Relocate 3D floor control sectors", ActionCategory, "Relocates the managed 3D floor control sectors to the current position of the control sector area", AllowScroll: true),
        new("select3dfloorcontrolsector", "Select 3D floor control sector", ActionCategory, "Selects the control sector of the currently highlighted 3D floor. Removes all other selections", AllowScroll: true),
        new("duplicate3dfloorgeometry", "Duplicate and paste geometry", ActionCategory, "Duplicates and pastes selected geometry and its 3D floors", AllowScroll: true),
    ];

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
                fl.Add(new ThreeDFloor(control, bottom, top, alpha, line.Args[1], line.Args[2], side, tag));
            }
        }
        return result;
    }

    public static ThreeDFloorControlEdit CreateControlEdit(ThreeDFloor floor)
        => CreateControlEdit(floor.Control, floor.SideTexture, floor.TypeBits, floor.Flags, floor.Alpha);

    public static ThreeDFloorControlEdit CreateControlEdit(Sector control, string sideTexture, int type, int flags, int alpha)
        => new(
            control.FloorHeight,
            control.CeilHeight,
            control.FloorTexture,
            control.CeilTexture,
            NormalizeTextureName(sideTexture),
            type,
            flags,
            Math.Clamp(alpha, 0, 255),
            control.Brightness,
            control.FloorSlope,
            control.FloorSlopeOffset,
            control.CeilSlope,
            control.CeilSlopeOffset,
            control.Tags.ToArray());

    public static int ApplyControlEdit(Sector control, ThreeDFloorControlEdit edit)
    {
        control.FloorHeight = edit.BottomHeight;
        control.CeilHeight = edit.TopHeight;
        control.SetFloorTexture(edit.BottomFlat);
        control.SetCeilTexture(edit.TopFlat);
        control.Brightness = edit.Brightness;
        control.FloorSlope = edit.FloorSlope;
        control.FloorSlopeOffset = edit.FloorSlopeOffset;
        control.CeilSlope = edit.CeilingSlope;
        control.CeilSlopeOffset = edit.CeilingSlopeOffset;
        control.Tags.Clear();
        control.Tags.AddRange(edit.Tags);

        int updatedLines = 0;
        foreach (Sidedef side in control.Sidedefs)
        {
            side.SetTextureMid(edit.SideTexture);
            if (side.Line.Action != Sector3DFloorAction) continue;

            side.Line.Args[1] = edit.Type;
            side.Line.Args[2] = edit.Flags;
            side.Line.Args[3] = Math.Clamp(edit.Alpha, 0, 255);
            updatedLines++;
        }

        return updatedLines;
    }

    public static ThreeDFloorCleanupResult CleanupControlSector(MapSet map, Sector control)
    {
        int cleared = 0;
        foreach (Sidedef side in control.Sidedefs)
        {
            Linedef line = side.Line;
            if (line.Action != Sector3DFloorAction) continue;
            if (HasTargetSector(map, line.Args[0], control)) continue;

            line.Action = 0;
            Array.Clear(line.Args);
            cleared++;
        }

        foreach (Sidedef side in control.Sidedefs)
        {
            if (side.Line.Action != 0)
                return new ThreeDFloorCleanupResult(cleared, false);
        }

        DeleteControlSector(map, control);
        return new ThreeDFloorCleanupResult(cleared, true);
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

    private static bool HasTargetSector(MapSet map, int tag, Sector control)
    {
        if (tag == 0) return false;

        foreach (Sector sector in map.Sectors)
        {
            if (ReferenceEquals(sector, control)) continue;
            if (sector.Tags.Contains(tag)) return true;
        }

        return false;
    }

    private static void DeleteControlSector(MapSet map, Sector control)
    {
        var lines = new List<Linedef>();
        foreach (Sidedef side in control.Sidedefs)
            if (!lines.Contains(side.Line))
                lines.Add(side.Line);

        var sides = new List<Sidedef>(control.Sidedefs);
        foreach (Sidedef side in sides)
            map.RemoveSidedef(side);

        map.RemoveSector(control);

        foreach (Linedef line in lines)
            if (line.Front == null && line.Back == null)
                map.RemoveLinedef(line);

        map.RemoveUnusedVertices();
        map.BuildIndexes();
    }

    private static string NormalizeTextureName(string? name)
        => string.IsNullOrEmpty(name) ? "-" : name;
}
