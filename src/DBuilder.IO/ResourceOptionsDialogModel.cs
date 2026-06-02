// ABOUTME: Normalizes resource-options dialog values into UDB-compatible DataLocation flags.
// ABOUTME: Keeps editor UI mapping testable without requiring an Avalonia dialog harness.

namespace DBuilder.IO;

public static class ResourceOptionsDialogModel
{
    public static DataLocation BuildLocation(
        DataLocationType type,
        string location,
        string initialLocation,
        bool strictPatches,
        bool rootTextures,
        bool rootFlats,
        bool notForTesting,
        string requiredArchivesText)
    {
        var result = new DataLocation(type, location.Trim())
        {
            InitialLocation = initialLocation,
            NotForTesting = notForTesting,
            RequiredArchivesText = requiredArchivesText.Trim(),
        };

        if (type == DataLocationType.Wad)
        {
            result.Option1 = strictPatches;
            result.Option2 = false;
        }
        else if (SupportsRootImages(type))
        {
            result.Option1 = rootTextures;
            result.Option2 = rootFlats;
        }

        return result;
    }

    public static bool SupportsRootImages(DataLocationType type)
        => type is DataLocationType.Pk3 or DataLocationType.Directory;
}
