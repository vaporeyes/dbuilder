// ABOUTME: Hard-coded Doom-engine flat animation chains - NUKAGE/FWATER/LAVA/BLOOD/etc. cycle through their numbered frames.
// ABOUTME: The animation logic itself is in the Doom EXE (not in WAD data), so renderers must replicate the chains to play back authentic animation.

using System;
using System.Collections.Generic;

namespace DBuilder.IO;

public static class FlatAnimations
{
    /// <summary>Doom's flat animation tic period - the engine advances one frame every 8 game tics at 35 tics/sec.</summary>
    public const double FramePeriodSeconds = 8.0 / 35.0;

    // Known flat animation chains across the original Doom games + Heretic/Hexen.
    // Each row is one cycle in order; the last frame loops back to the first.
    private static readonly string[][] _chains =
    {
        new[] { "NUKAGE1", "NUKAGE2", "NUKAGE3" },
        new[] { "FWATER1", "FWATER2", "FWATER3", "FWATER4" },
        new[] { "SWATER1", "SWATER2", "SWATER3", "SWATER4" }, // Doom 2 + Final Doom
        new[] { "LAVA1",   "LAVA2",   "LAVA3",   "LAVA4"   },
        new[] { "BLOOD1",  "BLOOD2",  "BLOOD3"  },
        new[] { "RROCK05", "RROCK06", "RROCK07", "RROCK08" },
        new[] { "SLIME01", "SLIME02", "SLIME03", "SLIME04" },
        new[] { "SLIME05", "SLIME06", "SLIME07", "SLIME08" },
        new[] { "SLIME09", "SLIME10", "SLIME11", "SLIME12" },
        // Heretic / Hexen additions
        new[] { "FLTWAWA1", "FLTWAWA2", "FLTWAWA3" },
        new[] { "FLTSLUD1", "FLTSLUD2", "FLTSLUD3" },
        new[] { "FLTTELE1", "FLTTELE2", "FLTTELE3", "FLTTELE4" },
        new[] { "FLTFLWW1", "FLTFLWW2", "FLTFLWW3" },
        new[] { "FLTLAVA1", "FLTLAVA2", "FLTLAVA3", "FLTLAVA4" },
        new[] { "FLATHUH1", "FLATHUH2", "FLATHUH3", "FLATHUH4" },
        new[] { "X_005",    "X_006",    "X_007",    "X_008"    },
        new[] { "X_009",    "X_010",    "X_011",    "X_012"    },
    };

    // Lazy reverse index: each frame -> (chain, index-within-chain).
    private static readonly Dictionary<string, (string[] chain, int idx)> _index = BuildIndex();

    private static Dictionary<string, (string[], int)> BuildIndex()
    {
        var dict = new Dictionary<string, (string[], int)>(StringComparer.OrdinalIgnoreCase);
        foreach (var chain in _chains)
        {
            for (int i = 0; i < chain.Length; i++)
            {
                dict[chain[i]] = (chain, i);
            }
        }
        return dict;
    }

    /// <summary>
    /// If <paramref name="flatName"/> is part of an animation chain, returns the full chain rotated so that
    /// <paramref name="flatName"/> is the first entry.  Returns null for static flats.
    /// </summary>
    /// <example>
    /// GetChainStarting("NUKAGE2") -> ["NUKAGE2", "NUKAGE3", "NUKAGE1"]
    /// GetChainStarting("FLOOR4_8") -> null
    /// </example>
    public static IReadOnlyList<string>? GetChainStarting(string flatName)
    {
        if (!_index.TryGetValue(flatName, out var entry)) return null;
        var (chain, idx) = entry;
        if (chain.Length <= 1) return null;

        // Rotate the chain so flatName is first.
        var rotated = new string[chain.Length];
        for (int i = 0; i < chain.Length; i++)
            rotated[i] = chain[(idx + i) % chain.Length];
        return rotated;
    }

    /// <summary>True when <paramref name="flatName"/> is part of any known animation chain.</summary>
    public static bool IsAnimated(string flatName) => _index.ContainsKey(flatName);
}
