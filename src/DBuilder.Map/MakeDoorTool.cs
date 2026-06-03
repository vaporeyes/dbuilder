// ABOUTME: Applies UDB BuilderModes Make Door data edits to selected sectors.
// ABOUTME: Keeps the map-level door texture, action, tag, flag, and line-facing behavior testable.

using System.Globalization;

namespace DBuilder.Map;

public sealed class MakeDoorOptions
{
    public string DoorTexture { get; init; } = "-";
    public string TrackTexture { get; init; } = "-";
    public string? FloorTexture { get; init; }
    public string CeilingTexture { get; init; } = "-";
    public bool ResetOffsets { get; init; } = true;
    public bool ApplyActionSpecials { get; init; } = true;
    public bool ApplyTag { get; init; }
    public int Action { get; init; }
    public int Activate { get; init; }
    public int[] Args { get; init; } = new int[5];
    public IReadOnlyDictionary<string, bool> Flags { get; init; } = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    public string UpperUnpeggedFlag { get; init; } = "upperunpegged";
    public string LowerUnpeggedFlag { get; init; } = "lowerunpegged";
}

public readonly record struct MakeDoorResult(int SectorsChanged, int OneSidedLinesChanged, int DoorLinesChanged)
{
    public string StatusText
        => $"Made {CountLabel(SectorsChanged, "door sector")}, updated {CountLabel(DoorLinesChanged, "door line")} and {CountLabel(OneSidedLinesChanged, "track line")}.";

    private static string CountLabel(int count, string singular, string? plural = null)
        => $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? singular : plural ?? singular + "s")}";
}

public static class MakeDoorTool
{
    public static string DefaultFloorTexture(IEnumerable<Sector> sectors)
    {
        ArgumentNullException.ThrowIfNull(sectors);

        string? floorTexture = null;
        bool found = false;
        foreach (Sector sector in sectors)
        {
            if (sector == null || sector.IsDisposed) continue;
            if (!found)
            {
                floorTexture = sector.FloorTexture;
                found = true;
                continue;
            }

            if (!string.Equals(floorTexture, sector.FloorTexture, StringComparison.Ordinal))
                return "";
        }

        return found ? floorTexture ?? "" : "";
    }

    public static MakeDoorResult Apply(MapSet map, IEnumerable<Sector> sectors, MakeDoorOptions options)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(sectors);
        ArgumentNullException.ThrowIfNull(options);

        var orderedSectors = sectors.Where(sector => sector != null && !sector.IsDisposed).Distinct().ToList();
        if (orderedSectors.Count == 0) return new MakeDoorResult();

        var usedTags = new List<int>();
        int oneSidedLinesChanged = 0;
        int doorLinesChanged = 0;

        foreach (Sector sector in orderedSectors)
        {
            sector.CeilHeight = sector.FloorHeight;
            int tag = map.GetNewTag(usedTags);
            if (tag > 0) usedTags.Add(tag);

            foreach (Sidedef side in sector.Sidedefs.ToList())
            {
                if (side.Other == null)
                {
                    ApplyTrackSide(side, options);
                    oneSidedLinesChanged++;
                }
                else
                {
                    ApplyDoorSide(sector, side, tag, options);
                    doorLinesChanged++;
                }

                if (options.ResetOffsets)
                {
                    ResetOffsets(side);
                    if (side.Other != null) ResetOffsets(side.Other);
                }
            }
        }

        map.BuildIndexes();
        return new MakeDoorResult(orderedSectors.Count, oneSidedLinesChanged, doorLinesChanged);
    }

    private static void ApplyTrackSide(Sidedef side, MakeDoorOptions options)
    {
        side.SetTextureHigh("-");
        if (!string.IsNullOrEmpty(options.TrackTexture)) side.SetTextureMid(options.TrackTexture);
        side.SetTextureLow("-");

        side.Line.SetFlag(options.UpperUnpeggedFlag, false);
        side.Line.SetFlag(options.LowerUnpeggedFlag, true);
    }

    private static void ApplyDoorSide(Sector sector, Sidedef side, int tag, MakeDoorOptions options)
    {
        if (!string.IsNullOrEmpty(options.FloorTexture)) sector.SetFloorTexture(options.FloorTexture);
        if (!string.IsNullOrEmpty(options.CeilingTexture)) sector.SetCeilTexture(options.CeilingTexture);
        if (!string.IsNullOrEmpty(options.DoorTexture)) side.Other!.SetTextureHigh(options.DoorTexture);

        side.Line.SetFlag(options.UpperUnpeggedFlag, false);
        side.Line.SetFlag(options.LowerUnpeggedFlag, false);

        if (options.ApplyActionSpecials)
        {
            side.Line.Action = options.Action;
            side.Line.Activate = options.Activate;
            foreach (var flag in options.Flags)
                side.Line.SetFlag(flag.Key, flag.Value);
        }

        if (options.ApplyTag && tag > 0)
            sector.Tag = tag;

        for (int i = 0; i < side.Line.Args.Length; i++)
        {
            int configuredArg = i < options.Args.Length ? options.Args[i] : 0;
            if (configuredArg == -1 && tag > 0)
            {
                side.Line.Args[i] = tag;
                sector.Tag = tag;
            }
            else
            {
                side.Line.Args[i] = configuredArg;
            }
        }

        if (side.IsFront)
        {
            side.Line.FlipVertices();
            side.Line.FlipSidedefs();
        }
    }

    private static void ResetOffsets(Sidedef side)
    {
        side.OffsetX = 0;
        side.OffsetY = 0;
    }
}
