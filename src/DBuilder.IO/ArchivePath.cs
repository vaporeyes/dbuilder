// ABOUTME: Provides shared path classification for ZIP-family Doom resource archives.
// ABOUTME: Keeps PK3-family extension behavior consistent across map and resource readers.

namespace DBuilder.IO;

internal static class ArchivePath
{
    public static bool IsPk3FamilyPath(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".pk3", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pk7", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pkz", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pke", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ipk3", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ipk7", StringComparison.OrdinalIgnoreCase);
    }
}
