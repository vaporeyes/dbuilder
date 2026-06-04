// ABOUTME: Applies game-configuration metadata to map find/replace categories that need UDB config semantics.
// ABOUTME: Keeps generalized linedef action matching out of the core map model while sharing MapSearch behavior.

using System;
using System.Collections.Generic;
using System.Linq;
using DBuilder.Map;

namespace DBuilder.IO;

public static class ConfiguredMapSearch
{
    public static IReadOnlyList<FindCategoryDescriptor> CategoryDescriptors(GameConfiguration? config)
        => MapSearch.CategoryDescriptors
            .Where(descriptor => CategoryIsVisible(descriptor.Category, config))
            .ToArray();

    public static SearchResult Find(MapSet map, FindCategory category, string value, GameConfiguration? config)
        => MapSearch.Find(map, category, KnownFindFlagsOrOriginal(category, value, config), TagSearchOptions.All, LinedefActionMatcher(config), SectorEffectMatcher(config));

    public static SearchResult Find(MapSet map, FindCategory category, string value, GameConfiguration? config, bool withinSelection)
        => MapSearch.Find(map, category, KnownFindFlagsOrOriginal(category, value, config), TagSearchOptions.All, LinedefActionMatcher(config), SectorEffectMatcher(config), withinSelection);

    public static int Replace(MapSet map, FindCategory category, string find, string replace, GameConfiguration? config)
    {
        if (!ReplacementFlagsAreKnown(category, replace, config)) return 0;
        var (minThingType, maxThingType) = ThingTypeRange(config);
        return MapSearch.Replace(map, category, KnownFindFlagsOrOriginal(category, find, config), replace, TagSearchOptions.All, LinedefActionMatcher(config), SectorEffectMatcher(config), false, config?.MixTexturesFlats == true, config?.MaxTextureNameLength ?? 8, minThingType, maxThingType);
    }

    public static int Replace(MapSet map, FindCategory category, string find, string replace, GameConfiguration? config, bool withinSelection)
    {
        if (!ReplacementFlagsAreKnown(category, replace, config)) return 0;
        var (minThingType, maxThingType) = ThingTypeRange(config);
        return MapSearch.Replace(map, category, KnownFindFlagsOrOriginal(category, find, config), replace, TagSearchOptions.All, LinedefActionMatcher(config), SectorEffectMatcher(config), withinSelection, config?.MixTexturesFlats == true, config?.MaxTextureNameLength ?? 8, minThingType, maxThingType);
    }

    private static (int Min, int Max) ThingTypeRange(GameConfiguration? config)
        => config?.MapFormat == MapFormat.Udmf
            ? (int.MinValue, int.MaxValue)
            : (short.MinValue, short.MaxValue);

    private static bool CategoryIsVisible(FindCategory category, GameConfiguration? config)
        => category switch
        {
            FindCategory.ThingActionArguments => (config?.HasThingAction ?? true) && (config?.HasActionArgs ?? true),
            FindCategory.LinedefSectorReference or FindCategory.LinedefThingReference => config?.HasActionArgs ?? true,
            FindCategory.ThingSectorReference => (config?.HasThingAction ?? true) && (config?.HasActionArgs ?? true),
            FindCategory.ThingThingReference => (config?.HasThingAction ?? true) && (config?.HasThingTag ?? true),
            FindCategory.LinedefTag => config?.HasLinedefTag ?? true,
            FindCategory.ThingTag => config?.HasThingTag ?? true,
            FindCategory.SidedefFlags => config is null || config.SidedefFlags.Count > 0,
            FindCategory.SectorFlags => config is null || config.SectorFlags.Count > 0 || config.CeilingPortalFlags.Count > 0 || config.FloorPortalFlags.Count > 0,
            FindCategory.ThingFlags => config is null || config.ThingFlagKeys.Count > 0,
            FindCategory.AnyUdmfField or
            FindCategory.VertexUdmfField or
            FindCategory.LinedefUdmfField or
            FindCategory.SidedefUdmfField or
            FindCategory.SectorUdmfField or
            FindCategory.ThingUdmfField => config is null || config.MapFormat == MapFormat.Udmf,
            _ => true,
        };

    private static bool ReplacementFlagsAreKnown(FindCategory category, string replace, GameConfiguration? config)
    {
        HashSet<string>? known = KnownReplacementFlags(category, config);
        if (known is null) return true;
        foreach (var flag in ParsedFlagTokens(replace))
            if (!known.Contains(flag.Name)) return false;
        return true;
    }

    private static string KnownFindFlagsOrOriginal(FindCategory category, string find, GameConfiguration? config)
    {
        HashSet<string>? known = KnownReplacementFlags(category, config);
        if (known is null) return find;

        var flags = ParsedFlagTokens(find)
            .Where(flag => known.Contains(flag.Name))
            .Select(flag => flag.Set ? flag.Name : "!" + flag.Name);
        return string.Join(", ", flags);
    }

    private static HashSet<string>? KnownReplacementFlags(FindCategory category, GameConfiguration? config)
    {
        if (config is null) return null;

        return category switch
        {
            FindCategory.LinedefFlags => Set(
                config.LinedefFlagsTranslation.SelectMany(flag => flag.Fields)
                    .Concat(config.LinedefActivations.Select(activation => activation.Key))),
            FindCategory.SidedefFlags => Set(config.SidedefFlags.Keys),
            FindCategory.SectorFlags => Set(config.SectorFlags.Keys
                .Concat(config.CeilingPortalFlags.Keys)
                .Concat(config.FloorPortalFlags.Keys)),
            FindCategory.ThingFlags => Set(config.ThingFlagsTranslation.SelectMany(flag => flag.Fields)
                .Concat(config.ThingFlagKeys)
                .Concat(config.ThingFlagsCompare.Values.SelectMany(group => group.Flags.Keys))),
            _ => null,
        };
    }

    private static IEnumerable<(string Name, bool Set)> ParsedFlagTokens(string value)
    {
        foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            bool set = !part.StartsWith("!", StringComparison.Ordinal);
            string flag = set ? part : part[1..].Trim();
            if (!string.IsNullOrWhiteSpace(flag)) yield return (flag, set);
        }
    }

    private static HashSet<string> Set(IEnumerable<string> values)
        => new(values.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);

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
