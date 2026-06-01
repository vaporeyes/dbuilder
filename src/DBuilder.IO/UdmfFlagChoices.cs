// ABOUTME: Builds known UDMF flag lists for selected map elements.
// ABOUTME: Combines game-configuration flag metadata with current element flags.

using System;
using System.Collections.Generic;
using System.Linq;
using DBuilder.Map;

namespace DBuilder.IO;

public static class UdmfFlagChoices
{
    public static IReadOnlyList<string> KnownLinedefFlags(GameConfiguration? config, Linedef line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return OrderedKnownFlags(
            line.UdmfFlags,
            config?.LinedefFlagsTranslation.SelectMany(flag => flag.Fields) ?? Array.Empty<string>());
    }

    public static IReadOnlyList<string> KnownThingFlags(GameConfiguration? config, Thing thing)
    {
        ArgumentNullException.ThrowIfNull(thing);
        return OrderedKnownFlags(
            thing.UdmfFlags,
            config?.ThingFlagsTranslation.SelectMany(flag => flag.Fields) ?? Array.Empty<string>());
    }

    public static IReadOnlyList<string> KnownSectorFlags(GameConfiguration? config, Sector sector)
    {
        ArgumentNullException.ThrowIfNull(sector);
        return OrderedKnownFlags(
            sector.UdmfFlags,
            config == null
                ? Array.Empty<string>()
                : config.SectorFlags.Keys
                    .Concat(config.CeilingPortalFlags.Keys)
                    .Concat(config.FloorPortalFlags.Keys));
    }

    public static IReadOnlyList<string> KnownSidedefFlags(GameConfiguration? config, Sidedef sidedef)
    {
        ArgumentNullException.ThrowIfNull(sidedef);
        return OrderedKnownFlags(sidedef.UdmfFlags, config?.SidedefFlags.Keys ?? Array.Empty<string>());
    }

    public static void ApplyFlags(HashSet<string> target, IEnumerable<string> result)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(result);

        target.Clear();
        foreach (string flag in result)
            if (!string.IsNullOrWhiteSpace(flag)) target.Add(flag.Trim());
    }

    private static IReadOnlyList<string> OrderedKnownFlags(IEnumerable<string> currentFlags, IEnumerable<string> configuredFlags)
        => currentFlags
            .Concat(configuredFlags)
            .Where(flag => !string.IsNullOrWhiteSpace(flag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(flag => flag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
