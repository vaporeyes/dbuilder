// ABOUTME: Applies UDB-style sector brightness stepping through configured brightness levels.
// ABOUTME: Keeps selected-sector brightness action messages and undo labels testable outside the editor.

namespace DBuilder.Map;

public readonly record struct SectorBrightnessAdjustmentResult(int ChangedCount, int Delta, string UndoDescription, string StatusMessage);

public static class SectorBrightnessAdjustment
{
    public const string UndoDescription = "Sector brightness change";

    public static SectorBrightnessAdjustmentResult Apply(IReadOnlyList<Sector> sectors, IReadOnlyList<int> brightnessLevels, bool raise)
    {
        if (sectors.Count == 0)
            return new SectorBrightnessAdjustmentResult(0, 0, UndoDescription, "This action requires a selection!");

        int first = sectors[0].Brightness;
        int firstNext = raise ? NextHigher(brightnessLevels, first) : NextLower(brightnessLevels, first);
        int diff = raise ? firstNext - first : first - firstNext;

        foreach (Sector sector in sectors)
            sector.Brightness = raise
                ? NextHigher(brightnessLevels, sector.Brightness)
                : NextLower(brightnessLevels, sector.Brightness);

        string verb = raise ? "Raised" : "Lowered";
        return new SectorBrightnessAdjustmentResult(
            sectors.Count,
            diff,
            UndoDescription,
            verb + " sector brightness by " + diff + ".");
    }

    public static int NextHigher(IReadOnlyList<int> brightnessLevels, int level)
    {
        IReadOnlyList<int> levels = NormalizedLevels(brightnessLevels);
        int low = 0;
        int high = levels.Count - 1;

        while (low < high)
        {
            int mid = (int)Math.Floor((low + high) * 0.5);
            int current = levels[mid];

            if (current <= level)
                low = mid + 1;
            else
                high = mid;
        }

        return levels[high];
    }

    public static int NextLower(IReadOnlyList<int> brightnessLevels, int level)
    {
        IReadOnlyList<int> levels = NormalizedLevels(brightnessLevels);
        int low = 0;
        int high = levels.Count - 1;

        while (low < high)
        {
            int mid = (int)Math.Ceiling((low + high) * 0.5);
            int current = levels[mid];

            if (current >= level)
                high = mid - 1;
            else
                low = mid;
        }

        return levels[low];
    }

    private static IReadOnlyList<int> NormalizedLevels(IReadOnlyList<int> brightnessLevels)
        => brightnessLevels.Count == 0 ? [0, 8, 16, 32, 64, 96, 128, 160, 192, 224, 255] : brightnessLevels;
}
