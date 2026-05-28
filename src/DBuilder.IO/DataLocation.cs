// ABOUTME: UDB-compatible resource location metadata for map options.
// ABOUTME: Describes WAD, directory and PK3 resources without loading or owning those resources.

namespace DBuilder.IO;

public enum DataLocationType
{
    Wad = 0,
    Directory = 1,
    Pk3 = 2,
}

public sealed class DataLocation : IEquatable<DataLocation>, IComparable<DataLocation>
{
    public DataLocationType Type { get; set; }
    public string Location { get; set; } = "";
    public string InitialLocation { get; set; } = "";
    public bool Option1 { get; set; }
    public bool Option2 { get; set; }
    public bool NotForTesting { get; set; }
    public List<string> RequiredArchives { get; } = new();

    public DataLocation() { }

    public DataLocation(DataLocationType type, string location, bool option1 = false, bool option2 = false, bool notForTesting = false)
    {
        Type = type;
        Location = location;
        Option1 = option1;
        Option2 = option2;
        NotForTesting = notForTesting;
    }

    public override string ToString() => Location;

    public string GetDisplayName()
    {
        return Type switch
        {
            DataLocationType.Directory => Location.Substring(Location.LastIndexOf(Path.DirectorySeparatorChar) + 1),
            DataLocationType.Wad => !string.IsNullOrEmpty(InitialLocation) ? InitialLocation : Path.GetFileName(Location),
            DataLocationType.Pk3 => Path.GetFileName(Location),
            _ => "",
        };
    }

    public int CompareTo(DataLocation? other)
        => string.Compare(Location, other?.Location, StringComparison.OrdinalIgnoreCase);

    public bool Equals(DataLocation? other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is DataLocation other && Equals(other);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Location);

    public bool IsValid() => Type switch
    {
        DataLocationType.Directory => Directory.Exists(Location),
        DataLocationType.Wad or DataLocationType.Pk3 => File.Exists(Location),
        _ => false,
    };
}
