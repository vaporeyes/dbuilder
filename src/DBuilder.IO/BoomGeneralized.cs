// ABOUTME: Decodes Boom "generalized" linedef action numbers into a human-readable description.
// ABOUTME: Generalized types pack a category base + parameter bit fields; masks/shifts follow the canonical Boom spec (p_spec.h).

using System.Text;

namespace DBuilder.IO;

/// <summary>
/// Boom generalized linedef types occupy the numeric range [0x2F80, 0x8000). The high bits select a
/// category (floor/ceiling/door/locked/lift/stairs/crusher) and the low bits encode that category's
/// parameters (trigger, speed, target, delay, ...). This decodes a number into a readable summary.
/// </summary>
public static class BoomGeneralized
{
    public const int GenFloorBase = 0x6000;
    public const int GenCeilingBase = 0x4000;
    public const int GenDoorBase = 0x3C00;
    public const int GenLockedBase = 0x3800;
    public const int GenLiftBase = 0x3400;
    public const int GenStairsBase = 0x3000;
    public const int GenCrusherBase = 0x2F80;
    public const int GenEnd = 0x8000;

    private static readonly string[] Triggers = { "W1", "WR", "S1", "SR", "G1", "GR", "D1", "DR" };
    private static readonly string[] Speeds = { "Slow", "Normal", "Fast", "Turbo" };
    private static readonly string[] Changes = { "No change", "Copy texture, remove type", "Copy texture", "Copy texture and type" };

    private static readonly string[] FloorTargets =
        { "Highest neighbor floor", "Lowest neighbor floor", "Next neighbor floor", "Lowest neighbor ceiling",
          "Ceiling", "Shortest lower texture", "24 units", "32 units" };
    private static readonly string[] CeilingTargets =
        { "Highest neighbor ceiling", "Lowest neighbor ceiling", "Next neighbor ceiling", "Highest neighbor floor",
          "Floor", "Shortest lower texture", "24 units", "32 units" };

    private static readonly string[] DoorKinds = { "Open Wait Close", "Open Stay", "Close Wait Open", "Close Stay" };
    private static readonly string[] DoorDelays = { "1s", "4s", "9s", "30s" };
    private static readonly string[] LockedKinds = { "Open Wait Close", "Open Stay" };
    private static readonly string[] Keys =
        { "Any key", "Red keycard", "Blue keycard", "Yellow keycard", "Red skull", "Blue skull", "Yellow skull", "All keys" };
    private static readonly string[] LiftDelays = { "1s", "3s", "5s", "10s" };
    private static readonly string[] LiftTargets =
        { "Lowest neighbor floor", "Next neighbor floor", "Lowest neighbor ceiling", "Perpetual lowest<->highest" };
    private static readonly string[] StairSteps = { "4", "8", "16", "24" };

    /// <summary>True when the action number falls in the Boom generalized range.</summary>
    public static bool IsGeneralized(int action) => action >= GenCrusherBase && action < GenEnd;

    /// <summary>The category name for a generalized action, or null if it is not generalized.</summary>
    public static string? Category(int action)
    {
        if (!IsGeneralized(action)) return null;
        if (action >= GenFloorBase) return "Floor";
        if (action >= GenCeilingBase) return "Ceiling";
        if (action >= GenDoorBase) return "Door";
        if (action >= GenLockedBase) return "Locked Door";
        if (action >= GenLiftBase) return "Lift";
        if (action >= GenStairsBase) return "Stairs";
        return "Crusher";
    }

    /// <summary>A readable summary of a generalized action, or null if the number is not generalized.</summary>
    public static string? Describe(int action)
    {
        if (!IsGeneralized(action)) return null;
        if (action >= GenFloorBase) return DescribeMover(action, "Floor", FloorTargets);
        if (action >= GenCeilingBase) return DescribeMover(action, "Ceiling", CeilingTargets);
        if (action >= GenDoorBase) return DescribeDoor(action);
        if (action >= GenLockedBase) return DescribeLocked(action);
        if (action >= GenLiftBase) return DescribeLift(action);
        if (action >= GenStairsBase) return DescribeStairs(action);
        return DescribeCrusher(action);
    }

    private static string Trig(int a) => Triggers[a & 0x7];

    // Floors and ceilings share the same field layout, differing only in their target names.
    private static string DescribeMover(int a, string name, string[] targets)
    {
        int speed = (a & 0x0018) >> 3;
        int model = (a & 0x0020) >> 5;
        int dir = (a & 0x0040) >> 6;
        int target = (a & 0x0380) >> 7;
        int change = (a & 0x0C00) >> 10;
        int crush = (a & 0x1000) >> 12;

        var sb = new StringBuilder();
        sb.Append(name).Append(" [").Append(Trig(a)).Append("]: ");
        sb.Append(dir == 1 ? "Up" : "Down").Append(" to ").Append(targets[target]);
        sb.Append(", ").Append(Speeds[speed]);
        if (model == 1) sb.Append(", Numeric model");
        if (change != 0) sb.Append(", ").Append(Changes[change]);
        if (crush == 1) sb.Append(", Crushes");
        return sb.ToString();
    }

    private static string DescribeDoor(int a)
    {
        int speed = (a & 0x0018) >> 3;
        int kind = (a & 0x0060) >> 5;
        int monster = (a & 0x0080) >> 7;
        int delay = (a & 0x0300) >> 8;

        var sb = new StringBuilder();
        sb.Append("Door [").Append(Trig(a)).Append("]: ").Append(DoorKinds[kind]);
        sb.Append(", ").Append(Speeds[speed]);
        if (kind == 0 || kind == 2) sb.Append(", wait ").Append(DoorDelays[delay]);
        if (monster == 1) sb.Append(", monsters can activate");
        return sb.ToString();
    }

    private static string DescribeLocked(int a)
    {
        int speed = (a & 0x0018) >> 3;
        int kind = (a & 0x0020) >> 5;
        int key = (a & 0x01C0) >> 6;
        int nkeys = (a & 0x0200) >> 9;

        var sb = new StringBuilder();
        sb.Append("Locked Door [").Append(Trig(a)).Append("]: ").Append(LockedKinds[kind]);
        sb.Append(", ").Append(Speeds[speed]);
        sb.Append(", key: ").Append(Keys[key]);
        if (nkeys == 1 && key >= 1 && key <= 6) sb.Append(" (card or skull)");
        return sb.ToString();
    }

    private static string DescribeLift(int a)
    {
        int speed = (a & 0x0018) >> 3;
        int monster = (a & 0x0020) >> 5;
        int delay = (a & 0x00C0) >> 6;
        int target = (a & 0x0300) >> 8;

        var sb = new StringBuilder();
        sb.Append("Lift [").Append(Trig(a)).Append("]: ").Append(LiftTargets[target]);
        sb.Append(", ").Append(Speeds[speed]);
        sb.Append(", wait ").Append(LiftDelays[delay]);
        if (monster == 1) sb.Append(", monsters can activate");
        return sb.ToString();
    }

    private static string DescribeStairs(int a)
    {
        int speed = (a & 0x0018) >> 3;
        int monster = (a & 0x0020) >> 5;
        int step = (a & 0x00C0) >> 6;
        int dir = (a & 0x0100) >> 8;
        int ignore = (a & 0x0200) >> 9;

        var sb = new StringBuilder();
        sb.Append("Stairs [").Append(Trig(a)).Append("]: ");
        sb.Append(dir == 1 ? "Up" : "Down").Append(", step ").Append(StairSteps[step]);
        sb.Append(", ").Append(Speeds[speed]);
        if (ignore == 1) sb.Append(", ignore texture");
        if (monster == 1) sb.Append(", monsters can activate");
        return sb.ToString();
    }

    private static string DescribeCrusher(int a)
    {
        int speed = (a & 0x0018) >> 3;
        int monster = (a & 0x0020) >> 5;
        int silent = (a & 0x0040) >> 6;

        var sb = new StringBuilder();
        sb.Append("Crusher [").Append(Trig(a)).Append("]: ").Append(Speeds[speed]);
        if (silent == 1) sb.Append(", silent");
        if (monster == 1) sb.Append(", monsters can activate");
        return sb.ToString();
    }
}
