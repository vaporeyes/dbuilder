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

    public static string DisplayText(DataLocation location)
    {
        var details = new List<string> { TypeText(location.Type) };
        if (location.Type == DataLocationType.Wad && location.Option1) details.Add("strict patches");
        if (SupportsRootImages(location.Type) && location.Option1) details.Add("root textures");
        if (SupportsRootImages(location.Type) && location.Option2) details.Add("root flats");
        if (location.NotForTesting) details.Add("excluded from Test Map");
        if (!string.IsNullOrWhiteSpace(location.RequiredArchivesText)) details.Add($"requires {location.RequiredArchivesText}");

        return $"{location.GetDisplayName()} ({string.Join("; ", details)})";
    }

    private static string TypeText(DataLocationType type) => type switch
    {
        DataLocationType.Wad => "WAD",
        DataLocationType.Pk3 => "PK3",
        DataLocationType.Directory => "Directory",
        _ => type.ToString(),
    };
}
