// ABOUTME: Applies UDB-style default options to game-configuration resources.
// ABOUTME: Keeps configuration resource option defaults testable outside the editor UI.

namespace DBuilder.IO;

public static class ConfigResourceDefaultsModel
{
    public static void ApplyRequiredArchiveDefaults(GameConfiguration? configuration, DataLocation resource)
    {
        var archives = RequiredArchiveDetector.Detect(configuration, resource);
        ApplyRequiredArchiveDefaults(
            resource,
            archives,
            RequiredArchiveDetector.RequiresTestExclusion(configuration, archives));
    }

    public static void ApplyRequiredArchiveDefaults(
        DataLocation resource,
        IEnumerable<string> requiredArchives,
        bool notForTesting)
    {
        resource.RequiredArchives.Clear();
        resource.RequiredArchives.AddRange(requiredArchives);
        if (notForTesting) resource.NotForTesting = true;
    }
}
