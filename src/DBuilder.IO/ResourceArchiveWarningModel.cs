// ABOUTME: Builds UDB-style warnings for resource lists that satisfy required archive metadata.
// ABOUTME: Keeps required-archive warning rules testable outside editor resource dialogs.

namespace DBuilder.IO;

public static class ResourceArchiveWarningModel
{
    public static IReadOnlyList<string> BuildWarnings(
        GameConfiguration? configuration,
        IEnumerable<DataLocation> resources,
        bool includeEmptyMapWarning = false)
    {
        var resourceList = resources as IReadOnlyCollection<DataLocation> ?? resources.ToList();
        if (configuration is null && (!includeEmptyMapWarning || resourceList.Count != 0))
            return Array.Empty<string>();

        var requiredArchives = new List<string>();
        foreach (var resource in resourceList)
        {
            if (resource.RequiredArchives != null)
                requiredArchives.AddRange(resource.RequiredArchives);
        }

        var warnings = new List<string>();
        if (configuration != null)
        {
            foreach (var archive in configuration.RequiredArchives)
            {
                if (!requiredArchives.Contains(archive.Name, StringComparer.OrdinalIgnoreCase))
                    warnings.Add(MissingArchiveWarning(archive.Filename));
            }

            for (int i = 0; i < requiredArchives.Count; i++)
            {
                if (requiredArchives.FindIndex(name => string.Equals(name, requiredArchives[i], StringComparison.OrdinalIgnoreCase)) == i) continue;

                foreach (var archive in configuration.RequiredArchives)
                {
                    if (string.Equals(archive.Name, requiredArchives[i], StringComparison.OrdinalIgnoreCase))
                        warnings.Add(DuplicateArchiveWarning(archive.Filename));
                }
            }
        }

        if (includeEmptyMapWarning && resourceList.Count == 0)
            warnings.Add(EmptyMapResourcesWarning());

        return warnings;
    }

    private static string MissingArchiveWarning(string filename)
        => "Warning: a resource archive is required for this game configuration, but not present:\n"
            + $"  \"{filename}\"\n"
            + "Without it, UDB will have severely limited capabilities.";

    private static string DuplicateArchiveWarning(string filename)
        => "Warning: required archive was added more than once:\n"
            + $"  \"{filename}\"\n"
            + "This will most likely not work.";

    private static string EmptyMapResourcesWarning()
        => "Warning: you are about to edit a map without any resources.\n"
            + "Textures, flats and sprites may not be shown correctly or may not show up at all.";
}
