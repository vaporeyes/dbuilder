// ABOUTME: Detects which configured required archives a resource location satisfies.
// ABOUTME: Matches UDB resource option defaults by checking configured lump and actor class entries.

namespace DBuilder.IO;

public static class RequiredArchiveDetector
{
    public static IReadOnlyList<string> Detect(GameConfiguration? configuration, DataLocation location)
    {
        if (configuration is null || configuration.RequiredArchives.Count == 0 || !location.IsValid())
            return Array.Empty<string>();

        try
        {
            using var resources = new ResourceManager(configuration);
            resources.AddResource(location);

            var result = new List<string>();
            HashSet<string>? classes = null;

            foreach (var archive in configuration.RequiredArchives)
            {
                bool found = true;
                foreach (var entry in archive.Entries)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Lump) && resources.GetTextResource(entry.Lump) is null)
                    {
                        found = false;
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(entry.ClassName))
                    {
                        classes ??= CollectActorClasses(resources);
                        if (!classes.Contains(entry.ClassName))
                        {
                            found = false;
                            break;
                        }
                    }
                }

                if (found) result.Add(archive.Name);
            }

            return result;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static bool RequiresTestExclusion(GameConfiguration? configuration, IEnumerable<string> archiveNames)
    {
        if (configuration is null) return false;

        var names = new HashSet<string>(archiveNames, StringComparer.OrdinalIgnoreCase);
        return configuration.RequiredArchives.Any(archive => archive.NeedExclude && names.Contains(archive.Name));
    }

    private static HashSet<string> CollectActorClasses(ResourceManager resources)
    {
        var classes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string text in resources.GetTextLumps("ZSCRIPT"))
            foreach (var actor in ZScriptParser.Parse(text, resources.GetTextResource))
                classes.Add(actor.ClassName);

        foreach (string text in resources.GetTextLumps("DECORATE"))
            foreach (var actor in DecorateParser.Parse(text, resources.GetTextResource))
                classes.Add(actor.ClassName);

        return classes;
    }
}
