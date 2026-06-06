// ABOUTME: Models UDB 3D Floor Mode slope vertex groups and their UDMF backing fields.
// ABOUTME: Applies stored slope groups to sector floor and ceiling plane equations.

using System.Text.RegularExpressions;
using DBuilder.Geometry;

namespace DBuilder.Map;

[Flags]
public enum ThreeDFloorSlopePlaneType
{
    Floor = 1,
    Ceiling = 2,
    Bottom = 4,
    Top = 8,
}

public enum ThreeDFloorSlopeDrawingMode
{
    Floor,
    Ceiling,
    FloorAndCeiling,
}

public sealed class ThreeDFloorSlopeVertex(Vector2D position, double z)
{
    public Vector2D Position { get; set; } = position;
    public double Z { get; set; } = z;
    public bool Selected { get; set; }
}

public sealed record ThreeDFloorSlopeVertexSelection(
    ThreeDFloorSlopeVertex Vertex,
    ThreeDFloorSlopeVertexGroup Group);

public sealed record ThreeDFloorSlopeDrawResult(IReadOnlyList<ThreeDFloorSlopeVertexGroup> CreatedGroups);

public sealed record ThreeDFloorSlopeVertexEdit(
    double? X = null,
    double? Y = null,
    double? Z = null,
    bool? Reposition = null,
    bool? Spline = null,
    IReadOnlyList<Sector>? SelectedSectors = null,
    bool AddSelectedSectorsToFloor = false,
    bool RemoveSelectedSectorsFromFloor = false,
    bool AddSelectedSectorsToCeiling = false,
    bool RemoveSelectedSectorsFromCeiling = false,
    IReadOnlyList<Sector>? SectorsToUnbind = null);

public sealed class ThreeDFloorSlopeVertexGroup
{
    public ThreeDFloorSlopeVertexGroup(int id, IEnumerable<ThreeDFloorSlopeVertex> vertices)
    {
        Id = id;
        Vertices = vertices.ToList();
        Height = ComputeHeight();
    }

    public int Id { get; }
    public List<ThreeDFloorSlopeVertex> Vertices { get; }
    public List<Sector> Sectors { get; } = new();
    public Dictionary<Sector, ThreeDFloorSlopePlaneType> SectorPlanes { get; } = new(ReferenceEqualityComparer.Instance);
    public List<Sector> TaggedSectors { get; } = new();
    public int Height { get; private set; }
    public bool Reposition { get; set; } = true;
    public bool Spline { get; set; }

    public static ThreeDFloorSlopeVertexGroup FromSector(Sector sector, int id)
    {
        var vertices = new List<ThreeDFloorSlopeVertex>
        {
            ReadVertex(sector, id, 0),
            ReadVertex(sector, id, 1),
        };

        if (sector.Fields.ContainsKey(VertexField(id, 2, "x")))
            vertices.Add(ReadVertex(sector, id, 2));

        var group = new ThreeDFloorSlopeVertexGroup(id, vertices)
        {
            Reposition = ReadBool(sector.Fields, $"user_svg{id}_reposition", true),
            Spline = ReadBool(sector.Fields, $"user_svg{id}_spline", false),
        };
        return group;
    }

    public void StoreInSector(Sector sector)
    {
        for (int i = 0; i < Vertices.Count; i++)
        {
            ThreeDFloorSlopeVertex vertex = Vertices[i];
            sector.Fields[VertexField(Id, i, "x")] = vertex.Position.x;
            sector.Fields[VertexField(Id, i, "y")] = vertex.Position.y;
            sector.Fields[VertexField(Id, i, "z")] = vertex.Z;
        }

        string repositionField = $"user_svg{Id}_reposition";
        if (Reposition) sector.Fields.Remove(repositionField);
        else sector.Fields[repositionField] = false;

        string splineField = $"user_svg{Id}_spline";
        if (Spline) sector.Fields[splineField] = true;
        else sector.Fields.Remove(splineField);
    }

    public void RemoveStoredFields(Sector sector)
    {
        string[] components = ["x", "y", "z"];
        for (int i = 0; i < 3; i++)
        {
            foreach (string component in components)
                sector.Fields.Remove(VertexField(Id, i, component));
        }

        sector.Fields.Remove($"user_svg{Id}_reposition");
        sector.Fields.Remove($"user_svg{Id}_spline");
    }

    public void FindSectors(MapSet map)
    {
        Sectors.Clear();
        SectorPlanes.Clear();
        TaggedSectors.Clear();

        foreach (Sector sector in map.Sectors)
        {
            bool onFloor = ReadInt(sector.Fields, ThreeDFloorSlopes.FloorPlaneIdField, -1) == Id;
            bool onCeiling = ReadInt(sector.Fields, ThreeDFloorSlopes.CeilingPlaneIdField, -1) == Id;
            if (!onFloor && !onCeiling) continue;

            ThreeDFloorSlopePlaneType plane = 0;
            if (onFloor) plane |= ThreeDFloorSlopePlaneType.Floor;
            if (onCeiling) plane |= ThreeDFloorSlopePlaneType.Ceiling;
            Sectors.Add(sector);
            SectorPlanes[sector] = plane;
            AddTaggedSectors(map, sector, plane);
        }
    }

    public void AddSector(MapSet map, Sector sector, ThreeDFloorSlopePlaneType plane)
    {
        if (SectorPlanes.TryGetValue(sector, out ThreeDFloorSlopePlaneType existing))
            plane |= existing;

        if (!Sectors.Contains(sector)) Sectors.Add(sector);
        SectorPlanes[sector] = plane;
        AddTaggedSectors(map, sector, plane);
        ApplyToSectors();
    }

    public void RemoveSector(Sector sector, ThreeDFloorSlopePlaneType plane)
    {
        if (SectorPlanes.TryGetValue(sector, out ThreeDFloorSlopePlaneType existing))
        {
            ThreeDFloorSlopePlaneType remaining = existing & ~plane;
            if (remaining == 0)
            {
                Sectors.Remove(sector);
                SectorPlanes.Remove(sector);
            }
            else SectorPlanes[sector] = remaining;
        }

        if ((plane & ThreeDFloorSlopePlaneType.Floor) == ThreeDFloorSlopePlaneType.Floor)
        {
            sector.FloorSlope = new Vector3D();
            sector.FloorSlopeOffset = 0;
            sector.Fields.Remove(ThreeDFloorSlopes.FloorPlaneIdField);
        }

        if ((plane & ThreeDFloorSlopePlaneType.Ceiling) == ThreeDFloorSlopePlaneType.Ceiling)
        {
            sector.CeilSlope = new Vector3D();
            sector.CeilSlopeOffset = 0;
            sector.Fields.Remove(ThreeDFloorSlopes.CeilingPlaneIdField);
        }
    }

    public void ApplyToSectors()
    {
        Height = ComputeHeight();

        foreach (Sector sector in Sectors.ToArray())
        {
            if (!SectorPlanes.TryGetValue(sector, out ThreeDFloorSlopePlaneType plane))
            {
                Sectors.Remove(sector);
                continue;
            }

            bool hasPlane = false;

            if ((plane & ThreeDFloorSlopePlaneType.Floor) == ThreeDFloorSlopePlaneType.Floor)
            {
                hasPlane = true;
                sector.Fields[ThreeDFloorSlopes.FloorPlaneIdField] = Id;
                ApplyToSector(sector, ThreeDFloorSlopePlaneType.Floor);
            }
            else if (ReadInt(sector.Fields, ThreeDFloorSlopes.FloorPlaneIdField, -1) == Id)
            {
                sector.Fields.Remove(ThreeDFloorSlopes.FloorPlaneIdField);
            }

            if ((plane & ThreeDFloorSlopePlaneType.Ceiling) == ThreeDFloorSlopePlaneType.Ceiling)
            {
                hasPlane = true;
                sector.Fields[ThreeDFloorSlopes.CeilingPlaneIdField] = Id;
                ApplyToSector(sector, ThreeDFloorSlopePlaneType.Ceiling);
            }
            else if (ReadInt(sector.Fields, ThreeDFloorSlopes.CeilingPlaneIdField, -1) == Id)
            {
                sector.Fields.Remove(ThreeDFloorSlopes.CeilingPlaneIdField);
            }

            if (!hasPlane)
            {
                Sectors.Remove(sector);
                SectorPlanes.Remove(sector);
            }
        }
    }

    public bool VerticesAreValid()
    {
        if (Vertices.Count < 2) return false;
        if (Vertices.Count == 2) return Vertices[0].Position != Vertices[1].Position;
        return Line2D.GetSideOfLine(Vertices[0].Position, Vertices[1].Position, Vertices[2].Position) != 0.0;
    }

    private void ApplyToSector(Sector sector, ThreeDFloorSlopePlaneType plane)
    {
        if (!VerticesAreValid()) return;

        List<Vector3D> points = GetPlanePoints();
        bool floor = plane == ThreeDFloorSlopePlaneType.Floor;
        Plane slope = new(points[0], points[1], points[2], floor);

        if (floor)
        {
            sector.FloorSlope = slope.Normal;
            sector.FloorSlopeOffset = slope.Offset;
            sector.FloorHeight = Height;
        }
        else
        {
            sector.CeilSlope = slope.Normal;
            sector.CeilSlopeOffset = slope.Offset;
            sector.CeilHeight = Height;
        }
    }

    private int ComputeHeight()
    {
        if (!VerticesAreValid()) return Vertices.Count == 0 ? 0 : Convert.ToInt32(Vertices[0].Z);

        List<Vector3D> points = GetPlanePoints();
        Plane slope = new(points[0], points[1], points[2], true);
        double height = slope.GetZ(GetCircumcenter(points));
        return double.IsNaN(height) ? Convert.ToInt32(points[0].z) : Convert.ToInt32(height);
    }

    private List<Vector3D> GetPlanePoints()
    {
        var points = Vertices
            .Select(vertex => new Vector3D(vertex.Position.x, vertex.Position.y, vertex.Z))
            .ToList();

        if (points.Count == 2)
        {
            double z = points[0].z;
            Line2D line = new(points[0], points[1]);
            Vector2D point = new Vector2D(points[0].x, points[0].y) + line.GetPerpendicular();
            points.Add(new Vector3D(point.x, point.y, z));
        }

        return points;
    }

    private static Vector2D GetCircumcenter(IReadOnlyList<Vector3D> points)
    {
        Line2D line1 = new(points[0], points[1]);
        Line2D line2 = new(points[2], points[0]);
        Line2D bisector1 = new(line1.GetCoordinatesAt(0.5), line1.GetCoordinatesAt(0.5) + line1.GetPerpendicular());
        Line2D bisector2 = new(line2.GetCoordinatesAt(0.5), line2.GetCoordinatesAt(0.5) + line2.GetPerpendicular());

        bisector1.GetIntersection(bisector2, out double uRay, bounded: false);
        return bisector1.GetCoordinatesAt(uRay);
    }

    private void AddTaggedSectors(MapSet map, Sector sector, ThreeDFloorSlopePlaneType plane)
    {
        foreach (Sidedef side in sector.Sidedefs)
        {
            if (side.Line.Action != ThreeDFloors.Sector3DFloorAction) continue;

            foreach (Sector tagged in GetSectorsByTag(map, side.Line.Args[0]))
            {
                if (!TaggedSectors.Contains(tagged)) TaggedSectors.Add(tagged);
                if (!SectorPlanes.ContainsKey(tagged)) SectorPlanes[tagged] = plane;
            }
        }
    }

    private static IEnumerable<Sector> GetSectorsByTag(MapSet map, int tag)
    {
        foreach (Sector sector in map.Sectors)
        {
            if (sector.Tags.Contains(tag)) yield return sector;
        }
    }

    private static ThreeDFloorSlopeVertex ReadVertex(Sector sector, int id, int vertexId)
        => new(
            new Vector2D(
                ReadDouble(sector.Fields, VertexField(id, vertexId, "x"), 0.0),
                ReadDouble(sector.Fields, VertexField(id, vertexId, "y"), 0.0)),
            ReadDouble(sector.Fields, VertexField(id, vertexId, "z"), 0.0));

    private static string VertexField(int id, int vertexId, string component)
        => $"user_svg{id}_v{vertexId}_{component}";

    private static bool ReadBool(IReadOnlyDictionary<string, object> fields, string key, bool fallback)
        => fields.TryGetValue(key, out object? value) && value is bool result ? result : fallback;

    private static int ReadInt(IReadOnlyDictionary<string, object> fields, string key, int fallback)
        => fields.TryGetValue(key, out object? value) ? Convert.ToInt32(value) : fallback;

    private static double ReadDouble(IReadOnlyDictionary<string, object> fields, string key, double fallback)
        => fields.TryGetValue(key, out object? value) ? Convert.ToDouble(value) : fallback;
}

public static class ThreeDFloorSlopes
{
    public const string SlopeDataSectorField = "user_slopedatasector";
    public const string SlopeDataSectorComment = "[!]DO NOT EDIT OR DELETE! This sector is used by the slope mode for undo/redo operations.";
    public const string FloorPlaneIdField = "user_floorplane_id";
    public const string CeilingPlaneIdField = "user_ceilingplane_id";

    private static readonly Regex SlopeVertexGroupRegex = new(@"^user_svg(\d+)_v0_x$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<ThreeDFloorSlopeVertexGroup> LoadGroupsFromSector(Sector? slopeDataSector)
    {
        var groups = new List<ThreeDFloorSlopeVertexGroup>();
        if (slopeDataSector == null || slopeDataSector.IsDisposed) return groups;

        foreach (string key in slopeDataSector.Fields.Keys)
        {
            Match match = SlopeVertexGroupRegex.Match(key);
            if (!match.Success) continue;

            int id = Convert.ToInt32(match.Groups[1].Value);
            groups.Add(ThreeDFloorSlopeVertexGroup.FromSector(slopeDataSector, id));
        }

        return groups.OrderBy(group => group.Id).ToArray();
    }

    public static Sector? GetSlopeDataSector(MapSet map)
    {
        foreach (Sector sector in map.Sectors)
        {
            if (sector.Fields.TryGetValue(SlopeDataSectorField, out object? value) && value is bool marker && marker)
                return sector;
        }

        return null;
    }

    public static void StoreGroupsInSector(Sector slopeDataSector, IEnumerable<ThreeDFloorSlopeVertexGroup> groups)
    {
        slopeDataSector.Fields[SlopeDataSectorField] = true;
        slopeDataSector.Fields["comment"] = SlopeDataSectorComment;

        foreach (ThreeDFloorSlopeVertexGroup group in groups)
            group.StoreInSector(slopeDataSector);
    }

    public static ThreeDFloorSlopeVertexGroup AddSlopeVertexGroup(ICollection<ThreeDFloorSlopeVertexGroup> groups, IEnumerable<ThreeDFloorSlopeVertex> vertices)
    {
        for (int id = 1; id < int.MaxValue; id++)
        {
            if (groups.Any(group => group.Id == id)) continue;

            var group = new ThreeDFloorSlopeVertexGroup(id, vertices);
            groups.Add(group);
            return group;
        }

        throw new InvalidOperationException("No free slope vertex group id is available.");
    }

    public static ThreeDFloorSlopeDrawResult FinishDraw(
        MapSet map,
        ICollection<ThreeDFloorSlopeVertexGroup> groups,
        IReadOnlyList<Vector2D> points,
        IReadOnlyList<Sector> selectedSectors,
        ThreeDFloorSlopeDrawingMode mode,
        Sector? slopeDataSector = null,
        bool flipHeights = false)
    {
        if (points.Count <= 1) return new ThreeDFloorSlopeDrawResult(Array.Empty<ThreeDFloorSlopeVertexGroup>());

        (List<ThreeDFloorSlopeVertex> floorVertices, List<ThreeDFloorSlopeVertex> ceilingVertices) =
            CreateSlopeVertices(map, points, selectedSectors);
        if (flipHeights)
        {
            FlipVertexHeights(floorVertices);
            FlipVertexHeights(ceilingVertices);
        }

        var created = new List<ThreeDFloorSlopeVertexGroup>();
        if (mode == ThreeDFloorSlopeDrawingMode.Floor || mode == ThreeDFloorSlopeDrawingMode.FloorAndCeiling)
        {
            ThreeDFloorSlopeVertexGroup group = AddSlopeVertexGroup(groups, floorVertices);
            foreach (Sector sector in selectedSectors)
            {
                sector.Fields.Remove(FloorPlaneIdField);
                sector.Fields[FloorPlaneIdField] = group.Id;
                group.AddSector(map, sector, ThreeDFloorSlopePlaneType.Floor);
            }

            created.Add(group);
        }

        if (mode == ThreeDFloorSlopeDrawingMode.Ceiling || mode == ThreeDFloorSlopeDrawingMode.FloorAndCeiling)
        {
            ThreeDFloorSlopeVertexGroup group = AddSlopeVertexGroup(groups, ceilingVertices);
            foreach (Sector sector in selectedSectors)
            {
                sector.Fields.Remove(CeilingPlaneIdField);
                sector.Fields[CeilingPlaneIdField] = group.Id;
                group.AddSector(map, sector, ThreeDFloorSlopePlaneType.Ceiling);
            }

            created.Add(group);
        }

        if (slopeDataSector != null) StoreGroupsInSector(slopeDataSector, groups);
        return new ThreeDFloorSlopeDrawResult(created);
    }

    public static void FlipVertexHeights(IReadOnlyList<ThreeDFloorSlopeVertex> vertices)
    {
        for (int i = 0, j = vertices.Count - 1; i < j; i++, j--)
            (vertices[i].Z, vertices[j].Z) = (vertices[j].Z, vertices[i].Z);
    }

    public static int ApplyGroups(MapSet map, IEnumerable<ThreeDFloorSlopeVertexGroup> groups)
    {
        int changed = 0;
        foreach (ThreeDFloorSlopeVertexGroup group in groups)
        {
            group.FindSectors(map);
            group.ApplyToSectors();
            changed += group.Sectors.Count;
        }

        return changed;
    }

    public static int ApplyVertexEdit(
        MapSet map,
        IEnumerable<ThreeDFloorSlopeVertexSelection> selections,
        ThreeDFloorSlopeVertexEdit edit)
    {
        var groups = new List<ThreeDFloorSlopeVertexGroup>();
        int changedVertices = 0;

        foreach (ThreeDFloorSlopeVertexSelection selection in selections)
        {
            ThreeDFloorSlopeVertex vertex = selection.Vertex;
            Vector2D oldPosition = vertex.Position;
            double oldZ = vertex.Z;
            double x = edit.X ?? vertex.Position.x;
            double y = edit.Y ?? vertex.Position.y;

            vertex.Position = new Vector2D(x, y);
            vertex.Z = edit.Z ?? vertex.Z;
            if (vertex.Position != oldPosition || vertex.Z != oldZ) changedVertices++;

            if (!groups.Contains(selection.Group)) groups.Add(selection.Group);
        }

        IReadOnlyList<Sector> selectedSectors = edit.SelectedSectors ?? Array.Empty<Sector>();
        IReadOnlyList<Sector> sectorsToUnbind = edit.SectorsToUnbind ?? Array.Empty<Sector>();

        foreach (ThreeDFloorSlopeVertexGroup group in groups)
        {
            if (edit.Reposition.HasValue) group.Reposition = edit.Reposition.Value;
            if (edit.Spline.HasValue && group.Vertices.Count == 3) group.Spline = edit.Spline.Value;

            foreach (Sector sector in selectedSectors)
            {
                if (edit.AddSelectedSectorsToCeiling) group.AddSector(map, sector, ThreeDFloorSlopePlaneType.Ceiling);
                if (edit.RemoveSelectedSectorsFromCeiling && group.Sectors.Contains(sector)) group.RemoveSector(sector, ThreeDFloorSlopePlaneType.Ceiling);
                if (edit.AddSelectedSectorsToFloor) group.AddSector(map, sector, ThreeDFloorSlopePlaneType.Floor);
                if (edit.RemoveSelectedSectorsFromFloor && group.Sectors.Contains(sector)) group.RemoveSector(sector, ThreeDFloorSlopePlaneType.Floor);
            }

            foreach (Sector sector in sectorsToUnbind)
            {
                if (!group.Sectors.Contains(sector)) continue;
                group.RemoveSector(sector, ThreeDFloorSlopePlaneType.Floor);
                group.RemoveSector(sector, ThreeDFloorSlopePlaneType.Ceiling);
            }

            group.ApplyToSectors();
        }

        return changedVertices;
    }

    public static int AssignSectorsToGroup(
        MapSet map,
        ThreeDFloorSlopeVertexGroup targetGroup,
        IEnumerable<ThreeDFloorSlopeVertexGroup> groups,
        IEnumerable<Sector> sectors,
        ThreeDFloorSlopePlaneType plane)
    {
        int changed = 0;
        foreach (Sector sector in sectors)
        {
            foreach (ThreeDFloorSlopeVertexGroup group in groups)
            {
                if (ReferenceEquals(group, targetGroup)) continue;
                if (!group.Sectors.Contains(sector)) continue;

                group.RemoveSector(sector, plane);
            }

            targetGroup.AddSector(map, sector, plane);
            changed++;
        }

        return changed;
    }

    private static (List<ThreeDFloorSlopeVertex> Floor, List<ThreeDFloorSlopeVertex> Ceiling) CreateSlopeVertices(
        MapSet map,
        IReadOnlyList<Vector2D> points,
        IReadOnlyList<Sector> selectedSectors)
    {
        var floorVertices = new List<ThreeDFloorSlopeVertex>(points.Count);
        var ceilingVertices = new List<ThreeDFloorSlopeVertex>(points.Count);

        if (selectedSectors.Count == 1 && IsControlSector(selectedSectors[0]))
        {
            Sector control = selectedSectors[0];
            foreach (Vector2D point in points)
            {
                floorVertices.Add(new ThreeDFloorSlopeVertex(point, control.FloorHeight));
                ceilingVertices.Add(new ThreeDFloorSlopeVertex(point, control.CeilHeight));
            }

            return (floorVertices, ceilingVertices);
        }

        foreach (Vector2D point in points)
        {
            (double floor, double ceiling) = GetDrawPointHeights(map, point, selectedSectors);
            floorVertices.Add(new ThreeDFloorSlopeVertex(point, floor));
            ceilingVertices.Add(new ThreeDFloorSlopeVertex(point, ceiling));
        }

        return (floorVertices, ceilingVertices);
    }

    private static bool IsControlSector(Sector sector)
    {
        foreach (Sidedef side in sector.Sidedefs)
        {
            if (side.Line.Action == ThreeDFloors.Sector3DFloorAction) return true;
        }

        return false;
    }

    private static (double Floor, double Ceiling) GetDrawPointHeights(MapSet map, Vector2D point, IReadOnlyList<Sector> selectedSectors)
    {
        Sector? sector = map.GetSectorAt(point);
        if (sector == null) return (0, 0);

        foreach (Sidedef side in sector.Sidedefs)
        {
            if (side.Line.Line.GetSideOfLine(point) != 0.0) continue;

            Sector? source = side.Line.Back?.Sector != null && !selectedSectors.Contains(side.Line.Back.Sector)
                ? side.Line.Back.Sector
                : side.Line.Front?.Sector;
            if (source != null) return (source.FloorHeight, source.CeilHeight);
        }

        return (0, 0);
    }
}
