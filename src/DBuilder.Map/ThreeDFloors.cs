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

public sealed record ThreeDFloorControlSectorMaterializationResult(Sector Sector, Linedef ActionLine, int TargetTag);

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

public sealed record ThreeDFloorControlSectorAreaSettings(
    double OuterLeft = -512.0,
    double OuterRight = 0.0,
    double OuterTop = 512.0,
    double OuterBottom = 0.0,
    double GridSize = 64.0,
    double SectorSize = 56.0,
    bool UseCustomTagRange = false,
    int FirstTag = 0,
    int LastTag = 0)
{
    public const string PluginName = "controlsectorarea";
    public const string UseCustomTagRangeKey = "usecustomtagrange";
    public const string FirstTagKey = "firsttag";
    public const string LastTagKey = "lasttag";
    public const string OuterLeftKey = "outerleft";
    public const string OuterRightKey = "outerright";
    public const string OuterTopKey = "outertop";
    public const string OuterBottomKey = "outerbottom";
    public const string ManagedControlSectorField = "user_managed_3d_floor";

    public static ThreeDFloorControlSectorAreaSettings FromDictionary(IReadOnlyDictionary<string, object?> settings)
    {
        var defaults = new ThreeDFloorControlSectorAreaSettings();
        return defaults with
        {
            UseCustomTagRange = ReadBool(settings, UseCustomTagRangeKey, defaults.UseCustomTagRange),
            FirstTag = ReadInt(settings, FirstTagKey, defaults.FirstTag),
            LastTag = ReadInt(settings, LastTagKey, defaults.LastTag),
            OuterLeft = ReadDouble(settings, OuterLeftKey, defaults.OuterLeft),
            OuterRight = ReadDouble(settings, OuterRightKey, defaults.OuterRight),
            OuterTop = ReadDouble(settings, OuterTopKey, defaults.OuterTop),
            OuterBottom = ReadDouble(settings, OuterBottomKey, defaults.OuterBottom),
        };
    }

    public void WriteTo(IDictionary<string, object?> settings)
    {
        settings[UseCustomTagRangeKey] = UseCustomTagRange;

        if (UseCustomTagRange)
        {
            settings[FirstTagKey] = FirstTag;
            settings[LastTagKey] = LastTag;
        }
        else
        {
            settings.Remove(FirstTagKey);
            settings.Remove(LastTagKey);
        }

        settings[OuterLeftKey] = OuterLeft;
        settings[OuterRightKey] = OuterRight;
        settings[OuterTopKey] = OuterTop;
        settings[OuterBottomKey] = OuterBottom;
    }

    public int GetNewSectorTag(MapSet map, IEnumerable<int>? tagBlacklist = null)
    {
        if (!UseCustomTagRange) return map.GetNewTag(tagBlacklist ?? Array.Empty<int>());

        var blacklist = new HashSet<int>(tagBlacklist ?? Array.Empty<int>());
        for (int tag = FirstTag; tag <= LastTag; tag++)
        {
            if (blacklist.Contains(tag)) continue;
            if (!SectorTagExists(map, tag)) return tag;
        }

        throw new InvalidOperationException($"No free tags in the custom range between {FirstTag} and {LastTag}.");
    }

    public IReadOnlyList<IReadOnlyList<Vector2D>> GetNewControlSectorVertexLoops(MapSet map, int sectorCount)
    {
        var loops = new List<IReadOnlyList<Vector2D>>();
        foreach (Vector2D origin in FindAvailableOrigins(map, sectorCount, ignoreActiveManagedControls: false, activeControlSectors: null))
        {
            loops.Add(new[]
            {
                origin,
                new Vector2D(origin.x + SectorSize, origin.y),
                new Vector2D(origin.x + SectorSize, origin.y - SectorSize),
                new Vector2D(origin.x, origin.y - SectorSize),
                origin,
            });
        }

        return loops;
    }

    public IReadOnlyList<Vector2D> GetRelocatePositions(MapSet map, int sectorCount, IReadOnlySet<Sector> activeControlSectors)
        => FindAvailableOrigins(map, sectorCount, ignoreActiveManagedControls: true, activeControlSectors);

    private static bool SectorTagExists(MapSet map, int tag)
    {
        foreach (Sector sector in map.Sectors)
        {
            if (sector.Tags.Contains(tag)) return true;
        }

        return false;
    }

    private IReadOnlyList<Vector2D> FindAvailableOrigins(
        MapSet map,
        int sectorCount,
        bool ignoreActiveManagedControls,
        IReadOnlySet<Sector>? activeControlSectors)
    {
        if (sectorCount <= 0) return Array.Empty<Vector2D>();

        double margin = Math.Truncate((GridSize - SectorSize) / 2.0);
        var origins = new List<Vector2D>(sectorCount);

        for (int x = (int)OuterLeft; x < (int)OuterRight; x += (int)GridSize)
        {
            for (int y = (int)OuterTop; y > (int)OuterBottom; y -= (int)GridSize)
            {
                if (CandidateIntersectsMap(map, x, y, margin, ignoreActiveManagedControls, activeControlSectors)) continue;

                origins.Add(new Vector2D(x + margin, y - margin));
                if (origins.Count == sectorCount) return origins;
            }
        }

        throw new InvalidOperationException(ignoreActiveManagedControls
            ? "Not enough space for control sector relocation."
            : "No space left for control sectors.");
    }

    private bool CandidateIntersectsMap(
        MapSet map,
        int x,
        int y,
        double margin,
        bool ignoreActiveManagedControls,
        IReadOnlySet<Sector>? activeControlSectors)
    {
        double left = x + margin;
        double right = left + SectorSize;
        double top = y - margin;
        double bottom = top - SectorSize;
        var corners = new[]
        {
            new Vector2D(left, top),
            new Vector2D(right, top),
            new Vector2D(right, bottom),
            new Vector2D(left, bottom),
        };
        var candidateLines = new[]
        {
            new Line2D(corners[0], corners[1]),
            new Line2D(corners[1], corners[2]),
            new Line2D(corners[2], corners[3]),
            new Line2D(corners[3], corners[0]),
        };

        foreach (Sector sector in map.Sectors)
        {
            if (ShouldIgnoreSector(sector, ignoreActiveManagedControls, activeControlSectors)) continue;
            if (SectorIntersectsCandidate(sector, left, right, top, bottom, corners, candidateLines)) return true;
        }

        return false;
    }

    private static bool ShouldIgnoreSector(
        Sector sector,
        bool ignoreActiveManagedControls,
        IReadOnlySet<Sector>? activeControlSectors)
    {
        if (!ignoreActiveManagedControls) return false;
        return activeControlSectors?.Contains(sector) == true
            && sector.Fields.TryGetValue(ManagedControlSectorField, out object? value)
            && value is bool managed
            && managed;
    }

    private static bool SectorIntersectsCandidate(
        Sector sector,
        double left,
        double right,
        double top,
        double bottom,
        IReadOnlyList<Vector2D> corners,
        IReadOnlyList<Line2D> candidateLines)
    {
        var sectorVertices = new HashSet<Vertex>();
        foreach (Sidedef side in sector.Sidedefs)
        {
            sectorVertices.Add(side.Line.Start);
            sectorVertices.Add(side.Line.End);
        }

        foreach (Vertex vertex in sectorVertices)
        {
            Vector2D position = vertex.Position;
            if (position.x >= left && position.x <= right && position.y <= top && position.y >= bottom) return true;
        }

        var polygon = new List<Vector2D>();
        foreach (Sidedef side in sector.Sidedefs)
            polygon.Add(side.Line.Start.Position);
        if (polygon.Count >= 3)
        {
            foreach (Vector2D corner in corners)
                if (Tools.PointInPolygon(polygon, corner))
                    return true;
        }

        foreach (Sidedef side in sector.Sidedefs)
        {
            foreach (Line2D candidateLine in candidateLines)
                if (Line2D.GetIntersection(side.Line.Line, candidateLine))
                    return true;
        }

        return false;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> settings, string key, bool fallback)
        => settings.TryGetValue(key, out object? value) && value is bool result ? result : fallback;

    private static int ReadInt(IReadOnlyDictionary<string, object?> settings, string key, int fallback)
        => settings.TryGetValue(key, out object? value) && value is int result ? result : fallback;

    private static double ReadDouble(IReadOnlyDictionary<string, object?> settings, string key, double fallback)
        => settings.TryGetValue(key, out object? value) && value is double result ? result : value is float single ? single : value is int integer ? integer : fallback;
}

public static class ThreeDFloors
{
    /// <summary>The Hexen/ZDoom Sector_3DFloor linedef special number.</summary>
    public const int Sector3DFloorAction = 160;
    public const string ActionCategory = "threedfloorplugin";
    public const string ActionCategoryTitle = "3D Floor Plugin";
    public const string ManagedControlSectorComment = "[!]DO NOT DELETE! This sector is managed by the 3D floor plugin.";

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
            Sidedef? controlSide = line.Front?.Sector != null ? line.Front : line.Back?.Sector != null ? line.Back : null;
            var control = controlSide?.Sector;
            if (control == null) continue;
            if (requireManagedControlSector && !IsManagedControlSector(control, udmf)) continue;
            int tag = line.Args[0];
            if (tag == 0 || !byTag.TryGetValue(tag, out var targets)) continue;

            double bottom = control.FloorHeight, top = control.CeilHeight;
            int alpha = Math.Clamp(line.Args[3], 0, 255);
            string side = controlSide?.MidTexture ?? "-";

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

    public static ThreeDFloorControlSectorMaterializationResult MaterializeControlSector(
        MapSet map,
        IReadOnlyList<Vector2D> loop,
        ThreeDFloorControlEdit edit,
        int targetTag,
        bool managed = true)
    {
        if (loop.Count < 4) throw new ArgumentException("A control sector loop must contain at least 4 points.", nameof(loop));

        IReadOnlyList<Vector2D> points = RemoveClosingPoint(loop);
        var vertices = new List<Vertex>(points.Count);
        foreach (Vector2D point in points)
            vertices.Add(map.AddVertex(point));

        Sector control = SectorBuilder.CreateSector(map, vertices)
            ?? throw new InvalidOperationException("A control sector requires at least 3 unique vertices.");

        map.BuildIndexes();

        if (managed)
        {
            control.Fields[ThreeDFloorControlSectorAreaSettings.ManagedControlSectorField] = true;
            control.Fields["comment"] = ManagedControlSectorComment;
        }

        ApplyControlEdit(control, edit);
        Linedef actionLine = BindControlSectorTag(map, control, targetTag, edit);
        map.BuildIndexes();
        return new ThreeDFloorControlSectorMaterializationResult(control, actionLine, targetTag);
    }

    public static Linedef BindControlSectorTag(MapSet map, Sector control, int targetTag, ThreeDFloorControlEdit edit)
    {
        if (targetTag <= 0) throw new ArgumentOutOfRangeException(nameof(targetTag), "3D floor target tags must be positive.");

        foreach (Sidedef side in control.Sidedefs)
        {
            if (side.Line.Action == Sector3DFloorAction && side.Line.Args[0] == targetTag)
            {
                ApplyActionLine(side.Line, targetTag, edit);
                return side.Line;
            }
        }

        foreach (Sidedef side in control.Sidedefs)
        {
            if (side.Line.Action != 0 || side.Line.Tag != 0) continue;

            ApplyActionLine(side.Line, targetTag, edit);
            return side.Line;
        }

        Linedef splitLine = SplitLongestControlLine(map, control);
        ApplyActionLine(splitLine, targetTag, edit);
        map.BuildIndexes();
        return splitLine;
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

    public static int SelectControlSectors(MapSet map, IReadOnlyList<Sector> sectors, bool sharedOnly = false, bool udmf = false, bool requireManagedControlSector = false)
    {
        List<ThreeDFloor> floors = GetThreeDFloors(map, sectors, sharedOnly, udmf, requireManagedControlSector);
        map.ClearAllSelected();

        foreach (ThreeDFloor floor in floors)
            floor.Control.Selected = true;

        return floors.Count;
    }

    public static int RelocateManagedControlSectors(MapSet map, ThreeDFloorControlSectorAreaSettings settings)
    {
        List<Sector> controls = map.Sectors
            .Where(IsManagedControlSectorField)
            .ToList();
        if (controls.Count == 0) return 0;

        IReadOnlyList<Vector2D> origins = settings.GetRelocatePositions(
            map,
            controls.Count,
            new HashSet<Sector>(controls, ReferenceEqualityComparer.Instance));

        for (int i = 0; i < controls.Count; i++)
            MoveSectorTopLeftTo(controls[i], origins[i]);

        map.BuildIndexes();
        return controls.Count;
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

    private static void MoveSectorTopLeftTo(Sector sector, Vector2D origin)
    {
        HashSet<Vertex> vertices = SectorVertices(sector);
        if (vertices.Count == 0) return;

        double left = vertices.Min(vertex => vertex.Position.x);
        double top = vertices.Max(vertex => vertex.Position.y);
        Vector2D delta = origin - new Vector2D(left, top);

        foreach (Vertex vertex in vertices)
            vertex.Position += delta;
    }

    private static HashSet<Vertex> SectorVertices(Sector sector)
    {
        var vertices = new HashSet<Vertex>(ReferenceEqualityComparer.Instance);
        foreach (Sidedef side in sector.Sidedefs)
        {
            vertices.Add(side.Line.Start);
            vertices.Add(side.Line.End);
        }

        return vertices;
    }

    private static IReadOnlyList<Vector2D> RemoveClosingPoint(IReadOnlyList<Vector2D> loop)
    {
        if (loop.Count > 1 && loop[0].Equals(loop[^1]))
        {
            var points = new Vector2D[loop.Count - 1];
            for (int i = 0; i < points.Length; i++) points[i] = loop[i];
            return points;
        }

        return loop;
    }

    private static void ApplyActionLine(Linedef line, int targetTag, ThreeDFloorControlEdit edit)
    {
        line.Action = Sector3DFloorAction;
        line.Args[0] = targetTag;
        line.Args[1] = edit.Type;
        line.Args[2] = edit.Flags;
        line.Args[3] = Math.Clamp(edit.Alpha, 0, 255);
    }

    private static Linedef SplitLongestControlLine(MapSet map, Sector control)
    {
        Linedef? longest = null;
        foreach (Sidedef side in control.Sidedefs)
        {
            if (longest == null || side.Line.LengthSq > longest.LengthSq)
                longest = side.Line;
        }

        if (longest == null) throw new InvalidOperationException("Control sector has no lines to bind.");

        Vertex vertex = map.AddVertex(longest.GetCenterPoint());
        return map.SplitLinedefAt(longest, vertex);
    }

    private static bool IsManagedControlSector(Sector control, bool udmf)
    {
        if (!udmf) return true;
        return IsManagedControlSectorField(control);
    }

    private static bool IsManagedControlSectorField(Sector control)
    {
        return control.Fields.TryGetValue(ThreeDFloorControlSectorAreaSettings.ManagedControlSectorField, out object? value)
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
