// ABOUTME: Converts universal type handlers into string editor option lists for property UIs.
// ABOUTME: Keeps configured class-name choices driven by loaded game configuration catalogs.

namespace DBuilder.IO;

public readonly record struct UniversalStringOption(string Value, string Title);

public static class UniversalStringOptions
{
    public static IReadOnlyList<UniversalStringOption> ForThingClassEditor(
        UniversalTypeHandler handler,
        GameConfiguration? config)
    {
        if (handler is not ThingClassTypeHandler || config == null) return Array.Empty<UniversalStringOption>();

        return config.Things.Values
            .Where(thing => !string.IsNullOrWhiteSpace(thing.ClassName))
            .GroupBy(thing => thing.ClassName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new UniversalStringOption(group.First().ClassName, group.First().Title))
            .OrderBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
