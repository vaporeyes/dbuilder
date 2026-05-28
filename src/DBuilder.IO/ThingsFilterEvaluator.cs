// ABOUTME: Evaluates parsed UDB things filter definitions against map things.
// ABOUTME: Produces classic and visual visibility results without depending on editor UI state.

namespace DBuilder.IO;

using DBuilder.Map;

public enum ThingsFilterDisplayMode
{
    Always = 0,
    ClassicModesOnly = 1,
    VisualModesOnly = 2,
}

public sealed record ThingsFilterResult(
    IReadOnlyList<Thing> VisibleThings,
    IReadOnlyList<Thing> HiddenThings,
    IReadOnlyDictionary<Thing, bool> VisualVisibility);

public static class ThingsFilterEvaluator
{
    public static ThingsFilterResult Evaluate(MapSet map, GameConfiguration config, ThingsFilterInfo filter)
    {
        var visible = new List<Thing>();
        var hidden = new List<Thing>();
        var visualVisibility = new Dictionary<Thing, bool>(ReferenceEqualityComparer.Instance);

        foreach (var thing in map.Things)
        {
            bool qualifies = Qualifies(thing, config, filter);
            bool classicVisible = qualifies || filter.DisplayMode == (int)ThingsFilterDisplayMode.VisualModesOnly;
            bool visualVisible = qualifies || filter.DisplayMode == (int)ThingsFilterDisplayMode.ClassicModesOnly;

            if (classicVisible) visible.Add(thing);
            else hidden.Add(thing);

            visualVisibility[thing] = visualVisible;
        }

        return new ThingsFilterResult(visible, hidden, visualVisibility);
    }

    public static bool Qualifies(Thing thing, GameConfiguration config, ThingsFilterInfo filter)
    {
        bool qualifies =
            MatchesScalar(filter.ThingType, thing.Type) &&
            MatchesScalar(filter.ThingAngle, thing.Angle) &&
            MatchesHeight(filter.ThingZHeight, thing.Height) &&
            MatchesScalar(filter.ThingAction, thing.Action) &&
            MatchesScalar(filter.ThingTag, thing.Tag) &&
            MatchesArgs(filter.ThingArgs, thing.Args) &&
            MatchesCategory(thing, config, filter.Category) &&
            MatchesFields(thing, filter);

        return filter.Invert ? !qualifies : qualifies;
    }

    private static bool MatchesScalar(int filterValue, int thingValue)
        => filterValue == -1 || thingValue == filterValue;

    private static bool MatchesHeight(int filterValue, double thingHeight)
        => filterValue == int.MinValue || (int)thingHeight == filterValue;

    private static bool MatchesArgs(IReadOnlyList<int> filterArgs, int[] thingArgs)
    {
        for (int i = 0; i < filterArgs.Count && i < thingArgs.Length; i++)
            if (filterArgs[i] != -1 && thingArgs[i] != filterArgs[i]) return false;

        return true;
    }

    private static bool MatchesCategory(Thing thing, GameConfiguration config, string category)
    {
        if (category.Length == 0) return true;

        string thingCategory = config.GetThing(thing.Type)?.Category ?? "";
        return string.Equals(thingCategory, category, StringComparison.Ordinal);
    }

    private static bool MatchesFields(Thing thing, ThingsFilterInfo filter)
    {
        foreach (string field in filter.RequiredFields)
            if (!thing.UdmfFlags.Contains(field)) return false;

        foreach (string field in filter.ForbiddenFields)
            if (thing.UdmfFlags.Contains(field)) return false;

        foreach (var (_, custom) in filter.CustomFields)
            if (!MatchesCustomField(thing, custom)) return false;

        return true;
    }

    private static bool MatchesCustomField(Thing thing, ThingsFilterCustomFieldInfo custom)
    {
        if (!thing.Fields.TryGetValue(custom.Name, out var actual)) return false;

        var registry = new UniversalTypeRegistry();
        var expectedHandler = registry.CreateHandler(custom.Type);
        var actualHandler = registry.CreateHandler(custom.Type);
        expectedHandler.SetValue(custom.Value);
        actualHandler.SetValue(actual);

        return Equals(expectedHandler.GetValue(), actualHandler.GetValue());
    }
}
