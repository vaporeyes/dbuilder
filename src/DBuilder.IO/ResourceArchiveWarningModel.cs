// ABOUTME: Builds UDB-style warnings for resource lists that satisfy required archive metadata.
// ABOUTME: Keeps required-archive warning rules testable outside editor resource dialogs.

namespace DBuilder.IO;

public static class ResourceArchiveWarningModel
{
    public static IReadOnlyList<string> BuildWarnings(GameConfiguration? configuration, IEnumerable<DataLocation> resources)
    {
        if (configuration is null) return Array.Empty<string>();

        var requiredArchives = new List<string>();
        foreach (var resource in resources)
        {
            if (resource.RequiredArchives != null)
                requiredArchives.AddRange(resource.RequiredArchives);
        }

        var warnings = new List<string>();
        foreach (var archive in configuration.RequiredArchives)
        {
            if (!requiredArchives.Contains(archive.Name))
                warnings.Add(MissingArchiveWarning(archive.Filename));
        }

        for (int i = 0; i < requiredArchives.Count; i++)
        {
            if (requiredArchives.IndexOf(requiredArchives[i]) == i) continue;

            foreach (var archive in configuration.RequiredArchives)
            {
                if (archive.Name == requiredArchives[i])
                    warnings.Add(DuplicateArchiveWarning(archive.Filename));
            }
        }

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
}
