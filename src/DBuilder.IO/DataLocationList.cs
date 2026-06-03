// ABOUTME: UDB-compatible list helpers for map option resource locations.
// ABOUTME: Reads, writes and combines resource metadata without loading resource contents.

using System.Collections;
using System.Collections.Specialized;
using System.Globalization;

namespace DBuilder.IO;

public sealed class DataLocationList : List<DataLocation>
{
    public DataLocationList() { }

    public DataLocationList(IEnumerable<DataLocation> list) : base(list.Select(Clone)) { }

    public DataLocationList(Configuration configuration, string path)
    {
        ReadFromConfig(configuration, path);
    }

    public static DataLocationList Combined(DataLocationList first, DataLocationList second)
    {
        var result = new DataLocationList(first);
        foreach (var location in second)
        {
            result.Remove(location);
            result.Add(Clone(location));
        }
        return result;
    }

    public void ReadFromConfig(Configuration configuration, string path)
    {
        Clear();

        var resources = configuration.ReadSetting(path, new ListDictionary());
        if (resources == null) return;

        foreach (DictionaryEntry entry in resources)
        {
            if (entry.Value is not IDictionary data) continue;
            Add(ReadLocation(data));
        }
    }

    public void WriteToConfig(Configuration configuration, string path)
    {
        var resources = new ListDictionary();
        for (int i = 0; i < Count; i++)
        {
            var location = this[i];
            var data = new ListDictionary
            {
                { "type", (int)location.Type },
                { "location", location.Location },
                { "option1", location.Option1 ? 1 : 0 },
                { "option2", location.Option2 ? 1 : 0 },
                { "notfortesting", location.NotForTesting ? 1 : 0 },
            };

            if (location.RequiredArchives is { Count: > 0 })
                data.Add("requiredarchives", string.Join(",", location.RequiredArchives));

            resources.Add("resource" + i.ToString(CultureInfo.InvariantCulture), data);
        }

        configuration.WriteSetting(path, resources);
    }

    public bool IsValid() => this.All(location => location.IsValid());

    private static DataLocation ReadLocation(IDictionary data)
    {
        var location = new DataLocation
        {
            Type = (DataLocationType)ReadInt(data, "type"),
            Location = ReadString(data, "location"),
            Option1 = ReadInt(data, "option1") != 0,
            Option2 = ReadInt(data, "option2") != 0,
            NotForTesting = ReadInt(data, "notfortesting") != 0,
        };

        string requiredArchives = ReadString(data, "requiredarchives");
        if (!string.IsNullOrEmpty(requiredArchives))
        {
            foreach (string archive in requiredArchives.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = archive.Trim();
                if (trimmed.Length > 0) location.RequiredArchives.Add(trimmed);
            }
        }

        return location;
    }

    private static DataLocation Clone(DataLocation source)
    {
        var clone = new DataLocation(source.Type, source.Location, source.Option1, source.Option2, source.NotForTesting);
        clone.InitialLocation = source.InitialLocation;
        if (source.RequiredArchives != null) clone.RequiredArchives.AddRange(source.RequiredArchives);
        return clone;
    }

    private static int ReadInt(IDictionary data, string key)
    {
        if (!data.Contains(key) || data[key] == null) return 0;
        if (data[key] is int value) return value;
        return int.TryParse(data[key]!.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
    }

    private static string ReadString(IDictionary data, string key)
    {
        if (!data.Contains(key) || data[key] == null) return "";
        return data[key]!.ToString() ?? "";
    }
}
