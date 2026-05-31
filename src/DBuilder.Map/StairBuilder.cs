// ABOUTME: Turns sectors into stairs by applying UDB-style height, flat, and wall texture options.
// ABOUTME: Also keeps legacy helpers for simple floor stepping and ceiling movement.

using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public static class StairBuilder
{
    public const int DefaultUpperUnpeggedBit = 8;
    public const int DefaultLowerUnpeggedBit = 16;

    public static IReadOnlyList<StairBuilderSectorPlan> PlanStraightSectorsFromLines(
        IReadOnlyList<Linedef> selectedLinedefs,
        StairBuilderStraightOptions options)
    {
        var sectors = new List<StairBuilderSectorPlan>();

        foreach (Linedef line in selectedLinedefs)
        {
            Vector2D direction = line.Line.GetPerpendicular().GetNormal() * (options.SideFront ? -1 : 1);

            for (int i = 0; i < options.NumberOfSectors; i++)
            {
                Vector2D v1 = line.Start.Position + direction * options.SectorDepth * i;
                Vector2D v2 = line.End.Position + direction * options.SectorDepth * i;
                Vector2D v3 = v1 + direction * options.SectorDepth;
                Vector2D v4 = v2 + direction * options.SectorDepth;

                if (options.Spacing > 0)
                {
                    Vector2D offset = direction * options.Spacing * i;
                    v1 += offset;
                    v2 += offset;
                    v3 += offset;
                    v4 += offset;
                }

                sectors.Add(new StairBuilderSectorPlan(new[] { v1, v3, v4, v2, v1 }));
            }
        }

        return sectors;
    }

    /// <summary>
    /// Sets sector i's floor to <paramref name="startFloor"/> + i*<paramref name="step"/> (in list order). When
    /// <paramref name="moveCeiling"/> is true the ceiling shifts by the same delta, preserving each room's height.
    /// Returns the number of sectors changed.
    /// </summary>
    public static int Apply(IReadOnlyList<Sector> sectors, int startFloor, int step, bool moveCeiling)
    {
        for (int i = 0; i < sectors.Count; i++)
        {
            int newFloor = startFloor + i * step;
            int delta = newFloor - sectors[i].FloorHeight;
            sectors[i].FloorHeight = newFloor;
            if (moveCeiling) sectors[i].CeilHeight += delta;
        }
        return sectors.Count;
    }

    /// <summary>
    /// Applies UDB-style independent floor and ceiling height steps. Floor heights are always changed; ceiling
    /// heights are changed only when <paramref name="applyCeiling"/> is true.
    /// </summary>
    public static int Apply(IReadOnlyList<Sector> sectors, int startFloor, int floorStep,
        bool applyCeiling, int startCeiling, int ceilingStep)
    {
        for (int i = 0; i < sectors.Count; i++)
        {
            sectors[i].FloorHeight = startFloor + i * floorStep;
            if (applyCeiling) sectors[i].CeilHeight = startCeiling + i * ceilingStep;
        }
        return sectors.Count;
    }

    public static int Apply(IReadOnlyList<Sector> sectors, StairBuilderOptions options)
    {
        for (int i = 0; i < sectors.Count; i++)
        {
            Sector sector = sectors[i];
            int stepCounter = i + 1;

            if (options.ApplyFloorHeight)
            {
                int baseHeight = options.DistinctBaseHeights ? sector.FloorHeight : options.FloorBase;
                sector.FloorHeight = baseHeight + options.FloorStep * stepCounter;
            }

            if (options.ApplyCeilingHeight)
            {
                int baseHeight = options.DistinctBaseHeights ? sector.CeilHeight : options.CeilingBase;
                sector.CeilHeight = baseHeight + options.CeilingStep * stepCounter;
            }

            if (options.ApplyFloorTexture) sector.SetFloorTexture(options.FloorTexture);
            if (options.ApplyCeilingTexture) sector.SetCeilTexture(options.CeilingTexture);

            ApplySidedefOptions(sector, options);
        }

        return sectors.Count;
    }

    private static void ApplySidedefOptions(Sector sector, StairBuilderOptions options)
    {
        foreach (Sidedef side in sector.Sidedefs)
        {
            Linedef? line = side.Line;
            if (line == null) continue;

            if (options.ApplyUpperTexture)
            {
                if (line.Back?.HighRequired() == true) line.Back.SetTextureHigh(options.UpperTexture);
                if (line.Front?.HighRequired() == true) line.Front.SetTextureHigh(options.UpperTexture);
                if (options.UpperUnpegged) line.Flags |= options.UpperUnpeggedBit;
            }

            if (options.ApplyLowerTexture)
            {
                if (line.Front?.LowRequired() == true) line.Front.SetTextureLow(options.LowerTexture);
                if (line.Back?.LowRequired() == true) line.Back.SetTextureLow(options.LowerTexture);
                if (options.LowerUnpegged) line.Flags |= options.LowerUnpeggedBit;
            }

            if (options.ApplyMiddleTexture)
            {
                if (line.Front?.MiddleRequired() == true) line.Front.SetTextureMid(options.MiddleTexture);
                if (line.Back?.MiddleRequired() == true) line.Back.SetTextureMid(options.MiddleTexture);
            }
        }
    }
}

public sealed record StairBuilderSectorPlan(IReadOnlyList<Vector2D> Vertices);

public sealed record StairBuilderStraightOptions
{
    public int NumberOfSectors { get; init; } = 1;
    public int SectorDepth { get; init; } = 32;
    public int Spacing { get; init; }
    public bool SideFront { get; init; } = true;
}

public sealed class StairBuilderOptions
{
    public bool ApplyFloorHeight { get; init; } = true;
    public int FloorBase { get; init; }
    public int FloorStep { get; init; }
    public bool ApplyCeilingHeight { get; init; }
    public int CeilingBase { get; init; }
    public int CeilingStep { get; init; }
    public bool DistinctBaseHeights { get; init; }
    public bool ApplyFloorTexture { get; init; }
    public string FloorTexture { get; init; } = "-";
    public bool ApplyCeilingTexture { get; init; }
    public string CeilingTexture { get; init; } = "-";
    public bool ApplyUpperTexture { get; init; }
    public string UpperTexture { get; init; } = "-";
    public bool UpperUnpegged { get; init; }
    public int UpperUnpeggedBit { get; init; } = StairBuilder.DefaultUpperUnpeggedBit;
    public bool ApplyMiddleTexture { get; init; }
    public string MiddleTexture { get; init; } = "-";
    public bool ApplyLowerTexture { get; init; }
    public string LowerTexture { get; init; } = "-";
    public bool LowerUnpegged { get; init; }
    public int LowerUnpeggedBit { get; init; } = StairBuilder.DefaultLowerUnpeggedBit;
}
