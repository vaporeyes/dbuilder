// ABOUTME: Geometry tools ported from UDB Source/Core/Geometry/Tools.cs - boundary tracing for sector detection.
// ABOUTME: FindClosestPath walks the tightest-turn loop of linedef sides from a starting line+side back to itself.

/*
 * A focused port of UDB's sector-tracing core. FindClosestPath follows the planar subdivision from a starting
 * (line, side) by, at each vertex, choosing the angle-sorted next line (the tightest turn keeping the traced
 * face on the chosen side), until it returns to the start. This is the building block UDB layers outer/inner
 * loop classification on top of; that classification (raycasting for holes) is deferred.
 *
 * Requires MapSet.BuildIndexes() to have populated Vertex.Linedefs.
 */

using System;
using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public static class Tools
{
    private readonly record struct SidedefFillJob(Sidedef Sidedef, bool Forward);

    public sealed record SectorCreationOptions
    {
        public int DefaultFloorHeight { get; init; }
        public int DefaultCeilingHeight { get; init; } = 128;
        public int DefaultBrightness { get; init; } = 192;
        public string DefaultFloorTexture { get; init; } = "-";
        public string DefaultCeilingTexture { get; init; } = "-";
        public string DefaultHighTexture { get; init; } = "-";
        public string DefaultMiddleTexture { get; init; } = "-";
        public string DefaultLowTexture { get; init; } = "-";
        public bool OverrideFloorTexture { get; init; }
        public bool OverrideCeilingTexture { get; init; }
        public bool OverrideFloorHeight { get; init; }
        public bool OverrideCeilingHeight { get; init; }
        public bool OverrideBrightness { get; init; }
        public int CustomFloorHeight { get; init; }
        public int CustomCeilingHeight { get; init; } = 128;
        public int CustomBrightness { get; init; } = 192;
    }

    /// <summary>
    /// Performs Hermite spline interpolation for the position between p1 using tangent t1 and p2 using tangent t2.
    /// </summary>
    public static Vector2D HermiteSpline(Vector2D p1, Vector2D t1, Vector2D p2, Vector2D t2, double u)
    {
        double u2 = u * u;
        double u3 = u2 * u;
        double h1 = 2 * u3 - 3 * u2 + 1;
        double h2 = -2 * u3 + 3 * u2;
        double h3 = u3 - 2 * u2 + u;
        double h4 = u3 - u2;
        return p1 * h1 + p2 * h2 + t1 * h3 + t2 * h4;
    }

    /// <summary>
    /// Performs Hermite spline interpolation for the position between p1 using tangent t1 and p2 using tangent t2.
    /// </summary>
    public static Vector3D HermiteSpline(Vector3D p1, Vector3D t1, Vector3D p2, Vector3D t2, double u)
    {
        double u2 = u * u;
        double u3 = u2 * u;
        double h1 = 2 * u3 - 3 * u2 + 1;
        double h2 = -2 * u3 + 3 * u2;
        double h3 = u3 - 2 * u2 + u;
        double h4 = u3 - u2;
        return p1 * h1 + p2 * h2 + t1 * h3 + t2 * h4;
    }

    /// <summary>Finds UDB-style sector label positions from the sector triangulation.</summary>
    public static List<LabelPositionInfo> FindLabelPositions(Sector sector)
    {
        var positions = new List<LabelPositionInfo>(2);
        int islandoffset = 0;
        Triangulation triangles = Triangulation.Create(sector);

        for (int i = 0; i < triangles.IslandVertices.Count; i++)
        {
            var sides = new Dictionary<Sidedef, Linedef>(triangles.IslandVertices[i] >> 1);
            var candidatepositions = new List<Vector2D>(triangles.IslandVertices[i] >> 1);
            double founddistance = double.MinValue;
            Vector2D foundposition = new Vector2D();
            double minx = double.MaxValue;
            double miny = double.MaxValue;
            double maxx = double.MinValue;
            double maxy = double.MinValue;

            for (int t = 0; t < triangles.IslandVertices[i]; t += 3)
            {
                int triangleoffset = islandoffset + t;
                Vector2D v1 = triangles.Vertices[triangleoffset + 2];
                Sidedef? sd = triangles.Sidedefs[triangleoffset + 2];
                for (int v = 0; v < 3; v++)
                {
                    Vector2D v2 = triangles.Vertices[triangleoffset + v];

                    if (sd == null)
                    {
                        candidatepositions.Add(v1 + (v2 - v1) * 0.5);
                    }
                    else
                    {
                        sides[sd] = sd.Line;
                    }

                    minx = Math.Min(minx, v1.x);
                    miny = Math.Min(miny, v1.y);
                    maxx = Math.Max(maxx, v1.x);
                    maxy = Math.Max(maxy, v1.y);

                    sd = triangles.Sidedefs[triangleoffset + v];
                    v1 = v2;
                }
            }

            if (candidatepositions.Count > 0)
            {
                foreach (Vector2D candidatepos in candidatepositions)
                {
                    double smallestdist = int.MaxValue;
                    foreach (KeyValuePair<Sidedef, Linedef> sd in sides)
                    {
                        double distance = sd.Value.DistanceToSq(candidatepos, true);
                        smallestdist = Math.Min(smallestdist, distance);
                    }

                    if (smallestdist > founddistance)
                    {
                        foundposition = candidatepos;
                        founddistance = smallestdist;
                    }
                }

                positions.Add(new LabelPositionInfo(foundposition, Math.Sqrt(founddistance)));
            }
            else if (triangles.IslandVertices[i] == 3)
            {
                Vector2D v = (triangles.Vertices[islandoffset] + triangles.Vertices[islandoffset + 1] + triangles.Vertices[islandoffset + 2]) / 3.0;
                double d = Line2D.GetDistanceToLineSq(triangles.Vertices[islandoffset], triangles.Vertices[islandoffset + 1], v, false);
                d = Math.Min(d, Line2D.GetDistanceToLineSq(triangles.Vertices[islandoffset + 1], triangles.Vertices[islandoffset + 2], v, false));
                d = Math.Min(d, Line2D.GetDistanceToLineSq(triangles.Vertices[islandoffset + 2], triangles.Vertices[islandoffset], v, false));
                positions.Add(new LabelPositionInfo(v, Math.Sqrt(d)));
            }
            else
            {
                double d = Math.Min((maxx - minx) * 0.5, (maxy - miny) * 0.5);
                positions.Add(new LabelPositionInfo(new Vector2D(minx + (maxx - minx) * 0.5, miny + (maxy - miny) * 0.5), d));
            }

            islandoffset += triangles.IslandVertices[i];
        }

        return positions;
    }

    /// <summary>Removes actions and action arguments from marked linedefs and things, matching UDB Tools.RemoveMarkedActions.</summary>
    public static void RemoveMarkedActions(MapSet map)
    {
        foreach (Thing thing in map.Things)
        {
            if (!thing.Marked) continue;
            thing.Action = 0;
            Array.Clear(thing.Args);
        }

        foreach (Linedef line in map.Linedefs)
        {
            if (!line.Marked) continue;
            line.Action = 0;
            Array.Clear(line.Args);
        }
    }

    /// <summary>Flips sector linedefs so they all face either inward or outward, matching UDB Tools.FlipSectorLinedefs.</summary>
    public static void FlipSectorLinedefs(ICollection<Sector> sectors, bool selectedLinesOnly)
    {
        var processed = new HashSet<Linedef>();

        foreach (Sector sector in sectors)
        {
            var frontLines = new List<Linedef>();
            var backLines = new List<Linedef>();
            int unselectedFrontLines = 0;
            int unselectedBackLines = 0;

            foreach (Sidedef side in sector.Sidedefs)
            {
                if (processed.Contains(side.Line)) continue;

                if (selectedLinesOnly && !side.Line.Selected)
                {
                    if (ReferenceEquals(side, side.Line.Front)) unselectedFrontLines++;
                    else unselectedBackLines++;
                    continue;
                }

                if (ReferenceEquals(side, side.Line.Front)) frontLines.Add(side.Line);
                else backLines.Add(side.Line);

                processed.Add(side.Line);
            }

            if (frontLines.Count == 0 || (frontLines.Count + unselectedFrontLines > backLines.Count + unselectedBackLines && backLines.Count > 0))
            {
                foreach (Linedef line in backLines)
                {
                    line.FlipVertices();
                    line.FlipSidedefs();
                }
            }
            else
            {
                foreach (Linedef line in frontLines)
                {
                    if (line.Back == null) continue;

                    line.FlipVertices();
                    line.FlipSidedefs();
                }
            }
        }
    }

    /// <summary>Assigns traced linedef sides to an existing sector, matching UDB Tools.JoinSector.</summary>
    public static Sector? JoinSector(
        MapSet map,
        IReadOnlyList<LinedefSide> allLines,
        Sidedef original,
        string defaultHighTexture = "-",
        string defaultMiddleTexture = "-",
        string defaultLowTexture = "-",
        bool autoClearSidedefTextures = true)
    {
        Sector? sector = original.Sector;
        if (sector == null || sector.IsDisposed) return sector;

        SidedefTextureDefaults defaults = SidedefTextureDefaults.From(original, defaultHighTexture, defaultMiddleTexture, defaultLowTexture);

        foreach (LinedefSide side in allLines)
        {
            Sidedef? target = side.Front ? side.Line.Front : side.Line.Back;
            if (target == null)
            {
                target = map.AddSidedef(side.Line, side.Front, sector);
                LinkOppositeSidedef(target);
                ApplyDefaultsToSidedef(target, defaults);

                Sidedef? other = target.Other;
                if (other != null)
                    other.RemoveUnneededTextures(removeMiddle: true, force: true, shiftMiddle: true, autoClearSidedefTextures);
            }
            else
            {
                target.SetSector(sector);
            }
        }

        return sector;
    }

    /// <summary>Creates a sector from traced linedef sides, matching UDB Tools.MakeSector without editor globals.</summary>
    public static Sector? MakeSector(
        MapSet map,
        IReadOnlyList<LinedefSide> allLines,
        IReadOnlyList<Linedef>? nearbyLines = null,
        bool useOverrides = false,
        SectorCreationOptions? options = null,
        bool autoClearSidedefTextures = true)
    {
        if (allLines.Count == 0) return null;

        options ??= new SectorCreationOptions();
        Sector? sourceSector = null;
        Sector? nearestSector = null;
        SidedefTextureDefaults sourceSide = new(null, null, null, null, null, null);
        bool foundSideDefaults = false;

        foreach (LinedefSide lineSide in allLines)
        {
            Sidedef? side = lineSide.Front ? lineSide.Line.Front : lineSide.Line.Back;
            if (side == null) continue;

            sourceSector ??= side.Sector;
            sourceSide = TakeSidedefSettings(sourceSide, side);
            foundSideDefaults = true;
            break;
        }

        foreach (LinedefSide lineSide in allLines)
        {
            Sidedef? side = lineSide.Front ? lineSide.Line.Back : lineSide.Line.Front;
            if (side == null) continue;

            sourceSector ??= side.Sector;
            sourceSide = TakeSidedefSettings(sourceSide, side);
            foundSideDefaults = true;
            break;
        }

        if (nearbyLines != null && allLines.Count > 0 && (!foundSideDefaults || sourceSector == null))
        {
            Vector2D testPoint = allLines[0].Line.GetSidePoint(allLines[0].Front);
            Linedef? nearest = MapSet.NearestLinedef(new List<Linedef>(nearbyLines), testPoint);
            if (nearest != null)
            {
                double sideOfLine = nearest.SideOfLine(testPoint);
                Sidedef? defaultSide = sideOfLine < 0.0 ? nearest.Front : nearest.Back;

                if (defaultSide != null)
                {
                    sourceSector ??= defaultSide.Sector;
                    sourceSide = TakeSidedefSettings(sourceSide, defaultSide);
                }
                else
                {
                    defaultSide = sideOfLine < 0.0 ? nearest.Back : nearest.Front;
                    if (defaultSide != null)
                    {
                        sourceSide = TakeSidedefSettings(sourceSide, defaultSide);
                        nearestSector = defaultSide.Sector;
                    }
                }
            }
        }

        sourceSide = TakeSidedefDefaults(sourceSide, options);

        Sector newSector = map.AddSector();
        if (sourceSector != null)
        {
            sourceSector.CopyPropertiesTo(newSector);
        }
        else if (nearestSector != null)
        {
            newSector.SetFloorTexture(nearestSector.FloorTexture);
            if (newSector.FloorTexture != "-") newSector.LongFloorTexture = nearestSector.LongFloorTexture;
            newSector.SetCeilTexture(nearestSector.CeilTexture);
            if (newSector.CeilTexture != "-") newSector.LongCeilTexture = nearestSector.LongCeilTexture;
            newSector.FloorHeight = nearestSector.FloorHeight;
            newSector.CeilHeight = nearestSector.CeilHeight;
            newSector.Brightness = nearestSector.Brightness;
        }
        else
        {
            newSector.SetFloorTexture(options.DefaultFloorTexture);
            newSector.SetCeilTexture(options.DefaultCeilingTexture);
            newSector.FloorHeight = options.DefaultFloorHeight;
            newSector.CeilHeight = options.DefaultCeilingHeight;
            newSector.Brightness = options.DefaultBrightness;
        }

        if (useOverrides)
        {
            if (options.OverrideCeilingTexture) newSector.SetCeilTexture(options.DefaultCeilingTexture);
            if (options.OverrideFloorTexture) newSector.SetFloorTexture(options.DefaultFloorTexture);
            if (options.OverrideCeilingHeight) newSector.CeilHeight = options.CustomCeilingHeight;
            if (options.OverrideFloorHeight) newSector.FloorHeight = options.CustomFloorHeight;
            if (options.OverrideBrightness) newSector.Brightness = options.CustomBrightness;
        }
        else if (newSector.CeilHeight < newSector.FloorHeight)
        {
            newSector.CeilHeight = newSector.FloorHeight;
        }

        foreach (LinedefSide lineSide in allLines)
        {
            bool wasSingleSided = lineSide.Line.Back == null || lineSide.Line.Front == null;
            Sidedef? target = lineSide.Front ? lineSide.Line.Front : lineSide.Line.Back;
            if (target == null)
            {
                target = map.AddSidedef(lineSide.Line, lineSide.Front, newSector);
                LinkOppositeSidedef(target);
            }
            else if (!ReferenceEquals(target.Sector, newSector))
            {
                target.SetSector(newSector);
            }

            ApplyDefaultsToSidedef(target, sourceSide);

            lineSide.Line.Front?.RemoveUnneededTextures(wasSingleSided, force: false, shiftMiddle: wasSingleSided, autoClearSidedefTextures);
            lineSide.Line.Back?.RemoveUnneededTextures(wasSingleSided, force: false, shiftMiddle: wasSingleSided, autoClearSidedefTextures);
        }

        return newSector;
    }

    /// <summary>Rebuilds invalid one or two sided sectors from surrounding geometry, matching UDB Tools.MergeInvalidSectors.</summary>
    public static void MergeInvalidSectors(
        MapSet map,
        IReadOnlyDictionary<Sector, Vector2D> toMerge,
        SectorCreationOptions? options = null,
        bool autoClearSidedefTextures = true)
    {
        map.BuildIndexes();

        foreach (var group in toMerge)
        {
            Sector sector = group.Key;
            if (sector.IsDisposed || sector.Sidedefs.Count == 0 || sector.Sidedefs.Count >= 3) continue;

            foreach (Sidedef side in sector.Sidedefs.ToArray())
                map.RemoveSidedef(side);
            map.RemoveSector(sector);
            map.BuildIndexes();

            List<LinedefSide>? sides = FindPotentialSectorAt(map, group.Value);
            if (sides == null) continue;

            var sideLines = sides.Select(side => side.Line).ToHashSet(ReferenceEqualityComparer.Instance);
            var nearbyLines = map.Linedefs.Where(line => !sideLines.Contains(line)).ToList();
            Sector? rebuilt = MakeSector(
                map,
                sides,
                nearbyLines,
                useOverrides: false,
                options,
                autoClearSidedefTextures);
            if (rebuilt == null) continue;

            map.BuildIndexes();
            FlipBackOnlyLinedefs(rebuilt);
            map.BuildIndexes();
        }
    }

    /// <summary>Splits drawn islands out of multipart outer sectors, matching UDB Tools.SplitOuterSectors.</summary>
    public static int SplitOuterSectors(
        MapSet map,
        IEnumerable<Linedef> drawnLines,
        SectorCreationOptions? options = null,
        bool autoClearSidedefTextures = true)
    {
        map.BuildIndexes();
        var sectorSides = new Dictionary<Sector, HashSet<Sidedef>>(ReferenceEqualityComparer.Instance);
        var drawnSides = new HashSet<Sidedef>(ReferenceEqualityComparer.Instance);

        foreach (Linedef line in drawnLines)
        {
            AddDrawnSide(line.Front);
            AddDrawnSide(line.Back);
        }

        int splitCount = 0;
        foreach (var group in sectorSides)
        {
            map.BuildIndexes();
            if (group.Key.Sidedefs.Count == group.Value.Count)
            {
                group.Key.Marked = true;
                continue;
            }

            foreach (Sidedef side in group.Value)
            {
                if (!ReferenceEquals(side.Sector, group.Key)) continue;

                List<LinedefSide>? lineSides = FindPotentialSectorAt(map, side.Line, side.IsFront);
                if (lineSides == null || lineSides.Count == 0 || lineSides.Count >= group.Key.Sidedefs.Count) continue;

                var newSectorSides = new HashSet<Sidedef>(ReferenceEqualityComparer.Instance);
                foreach (LinedefSide lineSide in lineSides)
                {
                    Sidedef? found = lineSide.Front ? lineSide.Line.Front : lineSide.Line.Back;
                    if (found != null) newSectorSides.Add(found);
                }

                bool shouldSplit = group.Key.Sidedefs.Any(sideInOriginal =>
                    !newSectorSides.Contains(sideInOriginal) && drawnSides.Contains(sideInOriginal));
                if (!shouldSplit) continue;

                Sector? newSector = MakeSector(
                    map,
                    lineSides,
                    nearbyLines: null,
                    useOverrides: false,
                    options,
                    autoClearSidedefTextures);
                if (newSector == null) continue;

                splitCount++;
                map.BuildIndexes();
                SectorWasInvalid(map, group.Key);
                break;
            }
        }

        return splitCount;

        void AddDrawnSide(Sidedef? side)
        {
            if (side?.Sector == null || SectorWasInvalid(map, side.Sector)) return;

            if (Triangulation.Create(side.Sector).IslandVertices.Count > 1)
            {
                if (!sectorSides.TryGetValue(side.Sector, out var sides))
                    sectorSides[side.Sector] = sides = new HashSet<Sidedef>(ReferenceEqualityComparer.Instance);
                sides.Add(side);
            }

            drawnSides.Add(side);
        }
    }

    private static bool SectorWasInvalid(MapSet map, Sector sector)
    {
        if (sector.IsDisposed) return true;
        if (sector.Sidedefs.Count >= 3 && Triangulation.Create(sector).Vertices.Count >= 3) return false;

        var changedLines = new HashSet<Linedef>(ReferenceEqualityComparer.Instance);
        foreach (Sidedef side in sector.Sidedefs)
            if (side.Line != null) changedLines.Add(side.Line);
        foreach (Sidedef side in sector.Sidedefs.ToArray())
            map.RemoveSidedef(side);
        map.RemoveSector(sector);

        foreach (Linedef line in changedLines)
        {
            if (line.Front != null || line.Back == null) continue;

            line.FlipVertices();
            line.FlipSidedefs();
        }

        map.BuildIndexes();
        return true;
    }

    /// <summary>Creates or reuses loop linedefs, then materializes a sector through UDB-style traced-side creation.</summary>
    public static Sector? MakeSectorFromLoop(
        MapSet map,
        IReadOnlyList<Vertex> loop,
        IReadOnlyList<Linedef>? nearbyLines = null,
        bool useOverrides = false,
        SectorCreationOptions? options = null,
        bool autoClearSidedefTextures = true)
    {
        if (loop.Count < 3) return null;

        var vertices = new List<Vertex>(loop);
        if (SignedArea(vertices) > 0) vertices.Reverse();

        var sides = new List<LinedefSide>(vertices.Count);
        for (int i = 0; i < vertices.Count; i++)
        {
            Vertex start = vertices[i];
            Vertex end = vertices[(i + 1) % vertices.Count];
            if (ReferenceEquals(start, end)) continue;

            Linedef line = FindLinedef(map, start, end) ?? map.AddLinedef(start, end);
            bool useFront = ReferenceEquals(line.Start, start);
            sides.Add(new LinedefSide(line, useFront));
        }

        Sector? sector = sides.Count < 3
            ? null
            : MakeSector(map, sides, nearbyLines, useOverrides, options, autoClearSidedefTextures);
        if (sector != null) FlipBackOnlyLinedefs(sides.Select(side => side.Line));

        return sector;
    }

    /// <summary>Flips one-sided sector lines that only have a back side, matching UDB MakeSectorMode cleanup.</summary>
    public static int FlipBackOnlyLinedefs(Sector sector)
        => FlipBackOnlyLinedefs(sector.Sidedefs.Select(side => side.Line));

    /// <summary>Flips one-sided lines that only have a back side, even before sector indexes are rebuilt.</summary>
    public static int FlipBackOnlyLinedefs(IEnumerable<Linedef> lines)
    {
        int flipped = 0;
        var seen = new HashSet<Linedef>(ReferenceEqualityComparer.Instance);
        foreach (Linedef line in lines)
        {
            if (!seen.Add(line)) continue;
            if (line.Front != null || line.Back == null) continue;

            line.FlipVertices();
            line.FlipSidedefs();
            flipped++;
        }

        return flipped;
    }

    private readonly record struct SidedefTextureDefaults(
        string? High,
        string? Middle,
        string? Low,
        long? LongHigh,
        long? LongMiddle,
        long? LongLow)
    {
        public static SidedefTextureDefaults From(Sidedef side, string defaultHigh, string defaultMiddle, string defaultLow)
            => new(
                IsBlankTexture(side.HighTexture) ? defaultHigh : side.HighTexture,
                IsBlankTexture(side.MidTexture) ? defaultMiddle : side.MidTexture,
                IsBlankTexture(side.LowTexture) ? defaultLow : side.LowTexture,
                IsBlankTexture(side.HighTexture) ? null : side.LongHighTexture,
                IsBlankTexture(side.MidTexture) ? null : side.LongMiddleTexture,
                IsBlankTexture(side.LowTexture) ? null : side.LongLowTexture);
    }

    private static SidedefTextureDefaults TakeSidedefDefaults(SidedefTextureDefaults settings, SectorCreationOptions options)
        => new(
            settings.High ?? options.DefaultHighTexture,
            settings.Middle ?? options.DefaultMiddleTexture,
            settings.Low ?? options.DefaultLowTexture,
            settings.LongHigh,
            settings.LongMiddle,
            settings.LongLow);

    private static SidedefTextureDefaults TakeSidedefSettings(SidedefTextureDefaults settings, Sidedef side)
        => new(
            settings.High ?? (IsBlankTexture(side.HighTexture) ? null : side.HighTexture),
            settings.Middle ?? (IsBlankTexture(side.MidTexture) ? null : side.MidTexture),
            settings.Low ?? (IsBlankTexture(side.LowTexture) ? null : side.LowTexture),
            settings.LongHigh ?? (IsBlankTexture(side.HighTexture) ? null : side.LongHighTexture),
            settings.LongMiddle ?? (IsBlankTexture(side.MidTexture) ? null : side.LongMiddleTexture),
            settings.LongLow ?? (IsBlankTexture(side.LowTexture) ? null : side.LongLowTexture));

    private static void LinkOppositeSidedef(Sidedef side)
    {
        Sidedef? other = side.IsFront ? side.Line.Back : side.Line.Front;
        side.Other = other;
        if (other != null) other.Other = side;
    }

    private static void ApplyDefaultsToSidedef(Sidedef side, SidedefTextureDefaults defaults)
    {
        if (side.HighRequired() && IsBlankTexture(side.HighTexture))
        {
            side.SetTextureHigh(defaults.High);
            if (defaults.LongHigh.HasValue && side.HighTexture != "-") side.LongHighTexture = defaults.LongHigh.Value;
        }
        if (side.MiddleRequired() && IsBlankTexture(side.MidTexture))
        {
            side.SetTextureMid(defaults.Middle);
            if (defaults.LongMiddle.HasValue && side.MidTexture != "-") side.LongMiddleTexture = defaults.LongMiddle.Value;
        }
        if (side.LowRequired() && IsBlankTexture(side.LowTexture))
        {
            side.SetTextureLow(defaults.Low);
            if (defaults.LongLow.HasValue && side.LowTexture != "-") side.LongLowTexture = defaults.LongLow.Value;
        }
    }

    private static bool IsBlankTexture(string? texture)
        => string.IsNullOrWhiteSpace(texture) || texture == "-";

    private static double SignedArea(IReadOnlyList<Vertex> vertices)
    {
        double sum = 0;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector2D a = vertices[i].Position;
            Vector2D b = vertices[(i + 1) % vertices.Count].Position;
            sum += a.x * b.y - b.x * a.y;
        }
        return sum * 0.5;
    }

    private static Linedef? FindLinedef(MapSet map, Vertex a, Vertex b)
    {
        foreach (Linedef line in map.Linedefs)
        {
            if ((ReferenceEquals(line.Start, a) && ReferenceEquals(line.End, b)) ||
                (ReferenceEquals(line.Start, b) && ReferenceEquals(line.End, a)))
                return line;
        }
        return null;
    }

    /// <summary>Flood-fills matching floor or ceiling flats through adjacent sectors, matching UDB Tools.FloodfillFlats.</summary>
    public static void FloodfillFlats(MapSet map, Sector start, bool fillCeilings, ISet<string> originalFlats, string fillFlat, bool resetSectorMarks)
    {
        var todo = new Stack<Sector>();

        if (resetSectorMarks) map.ClearMarkedSectors(false);

        if (TextureMatches(start, fillCeilings, originalFlats))
            todo.Push(start);

        while (todo.Count > 0)
        {
            Sector sector = todo.Pop();

            if (fillCeilings) sector.SetCeilTexture(fillFlat);
            else sector.SetFloorTexture(fillFlat);
            sector.Marked = true;

            foreach (Sidedef side in sector.Sidedefs)
            {
                Sector? other = side.Other?.Sector;
                if (other == null || other.Marked) continue;
                if (TextureMatches(other, fillCeilings, originalFlats))
                    todo.Push(other);
            }
        }
    }

    private static bool TextureMatches(Sector sector, bool ceiling, ISet<string> textures)
        => textures.Contains(ceiling ? sector.CeilTexture : sector.FloorTexture);

    /// <summary>Flood-fills matching wall textures through vertex-connected sidedefs, matching UDB Tools.FloodfillTextures.</summary>
    public static void FloodfillTextures(MapSet map, Sidedef start, ISet<string> originalTextures, string fillTexture, bool resetSideMarks)
    {
        var todo = new Stack<SidedefFillJob>();

        if (resetSideMarks) map.ClearMarkedSidedefs(false);

        if (SidedefTextureMatch(start, originalTextures))
            todo.Push(new SidedefFillJob(start, Forward: true));

        while (todo.Count > 0)
        {
            SidedefFillJob job = todo.Pop();
            Sidedef sidedef = job.Sidedef;

            if (sidedef.HighRequired() && originalTextures.Contains(sidedef.HighTexture))
                sidedef.SetTextureHigh(fillTexture);
            if ((sidedef.MidTexture != "-" || sidedef.MiddleRequired()) && originalTextures.Contains(sidedef.MidTexture))
                sidedef.SetTextureMid(fillTexture);
            if (sidedef.LowRequired() && originalTextures.Contains(sidedef.LowTexture))
                sidedef.SetTextureLow(fillTexture);

            sidedef.Marked = true;

            if (job.Forward)
            {
                AddSidedefsForFloodfill(todo, sidedef.IsFront ? sidedef.Line.End : sidedef.Line.Start, forward: true, originalTextures);
                AddSidedefsForFloodfill(todo, sidedef.IsFront ? sidedef.Line.Start : sidedef.Line.End, forward: false, originalTextures);
            }
            else
            {
                AddSidedefsForFloodfill(todo, sidedef.IsFront ? sidedef.Line.Start : sidedef.Line.End, forward: false, originalTextures);
                AddSidedefsForFloodfill(todo, sidedef.IsFront ? sidedef.Line.End : sidedef.Line.Start, forward: true, originalTextures);
            }
        }
    }

    private static void AddSidedefsForFloodfill(Stack<SidedefFillJob> stack, Vertex vertex, bool forward, ISet<string> textureNames)
    {
        foreach (Linedef line in vertex.Linedefs)
        {
            Sidedef? side1 = forward ? line.Front : line.Back;
            Sidedef? side2 = forward ? line.Back : line.Front;

            if ((side1 != null && side1.Marked) || (side2 != null && side2.Marked))
                continue;

            if (line.Start == vertex && side1 != null && !side1.Marked)
            {
                if (SidedefTextureMatch(side1, textureNames))
                    stack.Push(new SidedefFillJob(side1, forward));
            }
            else if (line.End == vertex && side2 != null && !side2.Marked)
            {
                if (SidedefTextureMatch(side2, textureNames))
                    stack.Push(new SidedefFillJob(side2, forward));
            }
        }
    }

    /// <summary>Returns true when a required or non-empty sidedef texture slot matches one of the texture names.</summary>
    public static bool SidedefTextureMatch(Sidedef sidedef, ISet<string> textureNames)
    {
        return (textureNames.Contains(sidedef.HighTexture) && sidedef.HighRequired()) ||
               (textureNames.Contains(sidedef.LowTexture) && sidedef.LowRequired()) ||
               (textureNames.Contains(sidedef.MidTexture) && (sidedef.MiddleRequired() || sidedef.MidTexture != "-"));
    }

    /// <summary>Returns true when a point is inside a polygon using UDB's crossing rule.</summary>
    public static bool PointInPolygon(ICollection<Vector2D> polygon, Vector2D point)
    {
        if (polygon.Count == 0) return false;

        Vector2D previous = default;
        bool foundPrevious = false;
        foreach (var vertex in polygon)
        {
            previous = vertex;
            foundPrevious = true;
        }
        if (!foundPrevious) return false;

        uint crossings = 0;
        foreach (var current in polygon)
        {
            double minY = System.Math.Min(previous.y, current.y);
            double maxY = System.Math.Max(previous.y, current.y);
            double maxX = System.Math.Max(previous.x, current.x);

            if (point.y > minY && point.y <= maxY && point.x <= maxX && previous.y != current.y)
            {
                double xIntersection = (point.y - previous.y) * (current.x - previous.x) / (current.y - previous.y) + previous.x;
                if (previous.x == current.x || point.x <= xIntersection) crossings++;
            }

            previous = current;
        }

        return (crossings & 1U) != 0;
    }

    /// <summary>
    /// Finds the linedef sides bounding the sector that would contain <paramref name="pos"/>: traces the loop
    /// of the nearest linedef on the side facing the point (retracing outward if that loop is itself a hole),
    /// then detects inner hole loops. Returns the combined outer+inner sides, or null if no enclosing loop.
    /// </summary>
    public static List<LinedefSide>? FindPotentialSectorAt(MapSet map, Vector2D pos)
    {
        var line = map.NearestLinedef(pos);
        if (line == null) return null;
        bool front = Line2D.GetSideOfLine(line.Start.Position, line.End.Position, pos) <= 0;
        return FindPotentialSectorAt(map, line, front);
    }

    // A scanline shot to the right from a hole's rightmost vertex extends to here looking for the enclosing loop.
    private const double RightBoundary = 1e7;

    /// <summary>Finds the outer loop enclosing the line+side (retracing outward if it lands on a hole), then adds inner hole loops.</summary>
    public static List<LinedefSide>? FindPotentialSectorAt(MapSet map, Linedef line, bool front)
    {
        var all = new List<LinedefSide>();
        var poly = FindOuterLines(map, line, front, all);
        if (poly == null || all.Count < 3) return null;

        FindInnerLines(map, poly, all);
        return all;
    }

    // Traces the outermost loop enclosing the start line+side (UDB FindOuterLines). If the trace lands on an
    // inner (hole) loop, casts a scanline to the right from the loop's rightmost vertex to find the next
    // outward linedef and retraces from there, until the traced side faces inside its own loop (the true outer).
    private static EarClipPolygon? FindOuterLines(MapSet map, Linedef line, bool front, List<LinedefSide> all)
    {
        Linedef? scanline = line;
        bool scanfront = front;
        int guard = 0;

        while (scanline != null && ++guard < 1000)
        {
            var path = FindClosestPath(scanline, scanfront, turnatends: true);
            if (path == null || path.Count < 3) return null;

            var poly = new LinedefTracePath(path).MakePolygon(true);

            // The traced side faces into its own loop: this is the outer boundary we want.
            if (poly.Intersect(GetSidePoint(scanline, scanfront)))
            {
                all.AddRange(path);
                return poly;
            }

            // Otherwise this is a hole boundary. Cast a ray right from the loop's rightmost vertex.
            Vector2D rightmost = poly.First!.Value.Position;
            foreach (var ecv in poly)
                if (ecv.Position.x > rightmost.x) rightmost = ecv.Position;

            Linedef? foundline = FindRightwardScanLine(map, rightmost);
            if (foundline == null) return null;

            // Continue tracing the found line on the side facing back toward our region (the rightmost vertex).
            scanfront = Line2D.GetSideOfLine(foundline.Start.Position, foundline.End.Position, rightmost) <= 0;
            scanline = foundline;
        }
        return null;
    }

    private static Linedef? FindRightwardScanLine(MapSet map, Vector2D pos)
    {
        var scan = new Line2D(pos, new Vector2D(RightBoundary, pos.y));
        Linedef? foundline = null;
        double foundu = double.MaxValue;
        double foundDistance = double.MaxValue;
        foreach (var line in map.Linedefs)
        {
            if (!ScanCandidateCrossesRightwardRay(line, pos)) continue;
            if (!scan.GetIntersection(line.Start.Position.x, line.Start.Position.y, line.End.Position.x, line.End.Position.y, out double _, out double u_ray)) continue;
            if (u_ray * (RightBoundary - pos.x) <= 0.00001) continue;

            if (u_ray < foundu)
            {
                foundu = u_ray;
                foundline = line;
                foundDistance = line.DistanceTo(pos, true);
            }
            else if (foundline != null && Math.Round(u_ray, 4) == Math.Round(foundu, 4))
            {
                double lineDistance = line.DistanceTo(pos, true);
                if (GetRelativeAngle(line, pos, out double lineAngle) &&
                    GetRelativeAngle(foundline, pos, out double foundAngle) &&
                    lineAngle < foundAngle &&
                    lineDistance < foundDistance)
                {
                    foundline = line;
                    foundDistance = lineDistance;
                }
            }
        }

        return foundline;
    }

    private static bool ScanCandidateCrossesRightwardRay(Linedef line, Vector2D pos)
    {
        return (line.Start.Position.x > pos.x || line.End.Position.x > pos.x) &&
            ((line.Start.Position.y >= pos.y && line.End.Position.y <= pos.y) ||
                (line.Start.Position.y <= pos.y && line.End.Position.y >= pos.y));
    }

    private static bool GetRelativeAngle(Linedef line, Vector2D pos, out double result)
    {
        if (line.Start.Position.y == pos.y)
        {
            result = Angle2D.GetAngle(pos, line.Start.Position, line.End.Position);
            return true;
        }

        if (line.End.Position.y == pos.y)
        {
            result = Angle2D.GetAngle(pos, line.End.Position, line.Start.Position);
            return true;
        }

        result = double.MaxValue;
        return false;
    }

    // Finds hole loops fully inside the outer polygon and appends their sides to alllines (UDB FindInnerLines).
    private static void FindInnerLines(MapSet map, EarClipPolygon p, List<LinedefSide> all)
    {
        var bbox = p.CreateBBox();
        bool findmore;
        do
        {
            findmore = false;

            // Right-most vertex strictly inside the polygon that isn't part of the boundary we've collected.
            Vertex? foundv = null;
            foreach (var v in map.Vertices)
            {
                if (v.Position.x < bbox.Left || v.Position.x > bbox.Right || v.Position.y < bbox.Top || v.Position.y > bbox.Bottom) continue;
                if (foundv != null && v.Position.x < foundv.Position.x) continue;
                if (v.Linedefs.Count == 0 || !p.Intersect(v.Position)) continue;

                bool partOfBoundary = false;
                foreach (var ls in all)
                    if (ReferenceEquals(ls.Line.Start, v) || ReferenceEquals(ls.Line.End, v)) { partOfBoundary = true; break; }
                if (!partOfBoundary) foundv = v;
            }
            if (foundv == null) continue;

            // From this right-most interior vertex, the attached line closest to pointing "up" (toward +90 deg).
            const double target = Angle2D.PIHALF;
            Linedef? foundline = null;
            double foundangle = 0;
            foreach (var l in foundv.Linedefs)
            {
                double lineangle = l.Angle;
                if (ReferenceEquals(l.End, foundv)) lineangle += Angle2D.PI;
                double delta = Angle2D.Difference(target, lineangle);
                if (foundline == null || delta < foundangle) { foundline = l; foundangle = delta; }
            }
            if (foundline == null) continue;

            // Start tracing on the side facing right of the interior vertex.
            bool flFront = Line2D.GetSideOfLine(foundline.Start.Position, foundline.End.Position, foundv.Position + new Vector2D(100, 0)) < 0;
            var inner = FindClosestPath(foundline, flFront, true);
            if (inner == null) continue;

            var innerPoly = new LinedefTracePath(inner).MakePolygon(true);
            var sidePt = GetSidePoint(foundline, flFront);
            var ib = innerPoly.CreateBBox();
            bool outsideBbox = sidePt.x < ib.Left || sidePt.x > ib.Right || sidePt.y < ib.Top || sidePt.y > ib.Bottom;
            // A genuine hole: the traced side faces outside its own loop.
            if (outsideBbox || !innerPoly.Intersect(sidePt))
            {
                all.AddRange(inner);
                p.InsertChild(innerPoly);
                findmore = true;
            }
        }
        while (findmore);
    }

    // A point just off the given side of a line (front = right of start->end), used for inside/outside tests.
    private static Vector2D GetSidePoint(Linedef l, bool front)
    {
        var mid = (l.Start.Position + l.End.Position) * 0.5;
        var d = l.End.Position - l.Start.Position;
        var perp = new Vector2D(d.y, -d.x).GetNormal(); // right-hand (front) perpendicular
        return front ? mid + perp : mid - perp;
    }

    /// <summary>
    /// Traces the closest closed path of linedef sides starting at <paramref name="startline"/> on the given side,
    /// returning the ordered loop, or null if the trace can't close (open geometry). When <paramref name="turnatends"/>
    /// is true, dead-end vertices reverse along the other side of the line.
    /// </summary>
    public static List<LinedefSide>? FindClosestPath(Linedef startline, bool startfront, bool turnatends)
        => FindClosestPath(startline, startfront, startline, startfront, turnatends);

    /// <summary>
    /// Traces the closest path from a start linedef side to an end linedef side, matching UDB Tools.FindClosestPath.
    /// </summary>
    public static List<LinedefSide>? FindClosestPath(Linedef startline, bool startfront, Linedef endline, bool endfront, bool turnatends)
    {
        var path = new List<LinedefSide>();
        var tracecount = new Dictionary<Linedef, int>(ReferenceEqualityComparer.Instance);
        Linedef? nextline = startline;
        bool nextfront = startfront;
        int guard = 0, maxSteps = 100000;

        do
        {
            if (nextline == null) { path = null; break; }
            path.Add(new LinedefSide(nextline, nextfront));

            // Move to the far vertex of the directed edge.
            Vertex v = nextfront ? nextline.End : nextline.Start;

            // Sort the linedefs around v by angle relative to the line we arrived on.
            var lines = new List<Linedef>(v.Linedefs);
            lines.Sort(new LinedefAngleSorter(nextline, nextfront, v));

            if (lines.Count == 1)
            {
                // Dead end: reverse along the other side, or stop.
                if (turnatends && (!tracecount.TryGetValue(nextline, out int tc) || tc < 3))
                {
                    nextfront = !nextfront;
                }
                else { path = null; }
            }
            else
            {
                Linedef prevline = nextline;
                nextline = (lines[0] == nextline ? lines[1] : lines[0]);
                int curcount = tracecount.TryGetValue(nextline, out int current) ? current : 0;

                if (curcount > 0 && !nextline.Marked && nextline != startline && nextline != endline)
                {
                    foreach (Linedef line in lines)
                    {
                        int linecount = tracecount.TryGetValue(line, out int value) ? value : 0;
                        if (line != nextline && line != prevline && linecount < curcount)
                        {
                            nextline = line;
                            break;
                        }
                    }
                }

                if (!tracecount.TryGetValue(nextline, out int tc2) || tc2 < 3)
                {
                    // Front side flips when consecutive lines share the same start or same end vertex.
                    if (ReferenceEquals(prevline.Start, nextline.Start) || ReferenceEquals(prevline.End, nextline.End))
                        nextfront = !nextfront;
                }
                else { path = null; }
            }

            if (nextline != null)
                tracecount[nextline] = tracecount.TryGetValue(nextline, out int c) ? c + 1 : 1;

            if (++guard > maxSteps) { path = null; break; }
        }
        while (path != null && (nextline != endline || nextfront != endfront));

        if (path != null && (startline != endline || startfront != endfront))
            path.Add(new LinedefSide(endline, endfront));

        return path;
    }

    /// <summary>Returns the ordered, de-duplicated vertices of a traced loop (each side contributes its start vertex).</summary>
    public static List<Vertex> LoopVertices(IReadOnlyList<LinedefSide> path)
    {
        var verts = new List<Vertex>(path.Count);
        foreach (var ls in path)
            verts.Add(ls.Front ? ls.Line.Start : ls.Line.End);
        return verts;
    }
}
