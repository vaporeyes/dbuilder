// ABOUTME: Applies game-configuration metadata to map find/replace categories that need UDB config semantics.
// ABOUTME: Keeps generalized linedef action matching out of the core map model while sharing MapSearch behavior.

using System;
using System.Collections.Generic;
using DBuilder.Map;

namespace DBuilder.IO;

public static class ConfiguredMapSearch
{
    public static SearchResult Find(MapSet map, FindCategory category, string value, GameConfiguration? config)
        => MapSearch.Find(map, category, value, TagSearchOptions.All, LinedefActionMatcher(config), SectorEffectMatcher(config));

    public static SearchResult Find(MapSet map, FindCategory category, string value, GameConfiguration? config, bool withinSelection)
        => MapSearch.Find(map, category, value, TagSearchOptions.All, LinedefActionMatcher(config), SectorEffectMatcher(config), withinSelection);

    public static int Replace(MapSet map, FindCategory category, string find, string replace, GameConfiguration? config)
    {
        var (minThingType, maxThingType) = ThingTypeRange(config);
        return MapSearch.Replace(map, category, find, replace, TagSearchOptions.All, LinedefActionMatcher(config), SectorEffectMatcher(config), false, config?.MixTexturesFlats == true, config?.MaxTextureNameLength ?? 8, minThingType, maxThingType);
    }

    public static int Replace(MapSet map, FindCategory category, string find, string replace, GameConfiguration? config, bool withinSelection)
    {
        var (minThingType, maxThingType) = ThingTypeRange(config);
        return MapSearch.Replace(map, category, find, replace, TagSearchOptions.All, LinedefActionMatcher(config), SectorEffectMatcher(config), withinSelection, config?.MixTexturesFlats == true, config?.MaxTextureNameLength ?? 8, minThingType, maxThingType);
    }

    private static (int Min, int Max) ThingTypeRange(GameConfiguration? config)
        => config?.MapFormat == MapFormat.Udmf
            ? (int.MinValue, int.MaxValue)
            : (short.MinValue, short.MaxValue);

    private static Func<int, int, bool>? LinedefActionMatcher(GameConfiguration? config)
        => config?.GeneralizedActions == true && config.GeneralizedLinedefs.Count > 0
            ? (actual, expected) => GeneralizedActionBitsOverlap(actual, expected, config)
            : null;

    private static Func<int, int, bool>? SectorEffectMatcher(GameConfiguration? config)
        => config?.GeneralizedEffects == true && config.GeneralizedSectorEffects.Count > 0
            ? (actual, expected) => SectorEffectMatches(actual, expected, config)
            : null;

    private static bool GeneralizedActionBitsOverlap(int actual, int expected, GameConfiguration config)
    {
        HashSet<int> expectedBits = GeneralizedBits(expected, config.GeneralizedLinedefs);
        if (expectedBits.Count == 0) return false;

        HashSet<int> actualBits = GeneralizedBits(actual, config.GeneralizedLinedefs);
        if (actualBits.Count == 0) return false;

        foreach (int bit in expectedBits)
            if (actualBits.Contains(bit)) return true;

        return false;
    }

    private static HashSet<int> GeneralizedBits(int action, IReadOnlyList<GeneralizedCategory> categories)
    {
        var result = new HashSet<int>();
        foreach (GeneralizedCategory category in categories)
        {
            if (!category.Contains(action)) continue;
            int local = action - category.Offset;
            foreach (GeneralizedOption option in category.Options)
            {
                foreach (GeneralizedBit bit in option.Bits)
                {
                    if (bit.Value > 0 && (local & bit.Value) == bit.Value)
                        result.Add(bit.Value);
                }
            }
        }

        return result;
    }

    private static bool SectorEffectMatches(int actual, int expected, GameConfiguration config)
    {
        if (actual == 0 || expected == 0) return false;

        SectorEffectDataInfo expectedData = config.GetSectorEffectData(expected);
        SectorEffectDataInfo actualData = config.GetSectorEffectData(actual);
        if (expectedData.GeneralizedBits.Count > 0 && expectedData.Effect != 0)
            return expectedData.Effect == actualData.Effect && IsSubsetOf(expectedData.GeneralizedBits, actualData.GeneralizedBits);

        if (expectedData.GeneralizedBits.Count > 0)
            return IsSubsetOf(expectedData.GeneralizedBits, actualData.GeneralizedBits);

        return expectedData.Effect == actualData.Effect;
    }

    private static bool IsSubsetOf(IReadOnlySet<int> subset, IReadOnlySet<int> superset)
    {
        foreach (int value in subset)
            if (!superset.Contains(value)) return false;
        return true;
    }
}
