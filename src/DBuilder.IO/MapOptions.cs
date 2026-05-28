// ABOUTME: Minimal map-options container for UDB-compatible per-map settings backed by Configuration.
// ABOUTME: Ports map identity, selection group, tag-label, drawing-option, grid, script-tab, command and resource persistence.

using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed class MapOptions
{
    public const string SelectionGroupsPath = "selectiongroups";
    public const string TagLabelsPath = "taglabels";
    public const int SelectionGroupCount = 10;

    public Configuration MapConfiguration { get; }
    public Dictionary<int, string> TagLabels { get; } = new();
    public Dictionary<string, ScriptDocumentSettings> ScriptDocumentSettings { get; } = new(StringComparer.OrdinalIgnoreCase);
    private readonly DataLocationList resources = new();
    public ExternalCommandSettings ReloadResourcePreCommand { get; set; } = new();
    public ExternalCommandSettings ReloadResourcePostCommand { get; set; } = new();
    public ExternalCommandSettings TestPreCommand { get; set; } = new();
    public ExternalCommandSettings TestPostCommand { get; set; } = new();
    public string ConfigFile { get; set; } = "";
    public bool StrictPatches { get; set; }
    public string PreviousName { get; set; } = "";
    private string currentName = "";
    public string CurrentName
    {
        get => currentName;
        set
        {
            if (currentName == value) return;
            if (string.IsNullOrEmpty(PreviousName)) PreviousName = currentName;
            currentName = value;
        }
    }

    public bool LevelNameChanged => !string.IsNullOrEmpty(PreviousName) && PreviousName != CurrentName;
    public string LevelName => CurrentName;
    public string ScriptCompiler { get; set; } = "";
    public string DefaultTopTexture { get; set; } = "";
    public string DefaultWallTexture { get; set; } = "";
    public string DefaultBottomTexture { get; set; } = "";
    public string DefaultFloorTexture { get; set; } = "";
    public string DefaultCeilingTexture { get; set; } = "";
    public int CustomBrightness { get; set; } = 196;
    public int CustomFloorHeight { get; set; }
    public int CustomCeilingHeight { get; set; } = 128;
    public bool OverrideFloorTexture { get; set; }
    public bool OverrideCeilingTexture { get; set; }
    public bool OverrideTopTexture { get; set; }
    public bool OverrideMiddleTexture { get; set; }
    public bool OverrideBottomTexture { get; set; }
    public bool OverrideFloorHeight { get; set; }
    public bool OverrideCeilingHeight { get; set; }
    public bool OverrideBrightness { get; set; }
    public bool UseLongTextureNames { get; set; }
    public Vector2D ViewPosition { get; set; } = new(double.NaN, double.NaN);
    public double ViewScale { get; set; } = double.NaN;

    public MapOptions() : this(new Configuration(sorted: true)) { }

    public MapOptions(Configuration mapConfiguration)
    {
        MapConfiguration = mapConfiguration;
    }

    public void ReadRootOptions(Configuration wadConfiguration, string mapName)
    {
        ConfigFile = wadConfiguration.ReadSetting("gameconfig", "") ?? "";
        StrictPatches = wadConfiguration.ReadSetting("strictpatches", 0) != 0;
        PreviousName = "";
        currentName = mapName;
        MapConfiguration.Root = wadConfiguration.ReadSetting("maps." + mapName, new Hashtable()) ?? new Hashtable();
    }

    public void WriteRootOptions(Configuration wadConfiguration)
    {
        wadConfiguration.WriteSetting("type", "Doom Builder Map Settings Configuration");
        wadConfiguration.WriteSetting("gameconfig", ConfigFile);
        wadConfiguration.WriteSetting("strictpatches", StrictPatches ? 1 : 0);
        if (!string.IsNullOrEmpty(CurrentName)) wadConfiguration.WriteSetting("maps." + CurrentName, MapConfiguration.Root);
    }

    public void WriteSelectionGroups(MapSet map)
    {
        var groups = new ListDictionary();

        for (int groupIndex = 0; groupIndex < SelectionGroupCount; groupIndex++)
        {
            int mask = MapSet.GroupMask(groupIndex);
            var group = new ListDictionary();
            AddGroupIndices(group, "vertices", map.Vertices, mask);
            AddGroupIndices(group, "linedefs", map.Linedefs, mask);
            AddGroupIndices(group, "sectors", map.Sectors, mask);
            AddGroupIndices(group, "things", map.Things, mask);
            if (group.Count > 0) groups.Add(groupIndex, group);
        }

        MapConfiguration.DeleteSetting(SelectionGroupsPath);
        if (groups.Count > 0) MapConfiguration.WriteSetting(SelectionGroupsPath, groups);
    }

    public void ReadSelectionGroups(MapSet map)
    {
        ClearSelectionGroups(map);

        var groupList = MapConfiguration.ReadSetting(SelectionGroupsPath, new Hashtable());
        if (groupList == null) return;

        foreach (DictionaryEntry entry in groupList)
        {
            if (entry.Value is not IDictionary groupInfo) continue;
            if (!int.TryParse(entry.Key?.ToString(), out int groupIndex)) continue;

            groupIndex = System.Math.Clamp(groupIndex, 0, SelectionGroupCount - 1);
            int mask = MapSet.GroupMask(groupIndex);
            ApplyGroupIndices(map.Vertices, groupInfo, "vertices", mask);
            ApplyGroupIndices(map.Linedefs, groupInfo, "linedefs", mask);
            ApplyGroupIndices(map.Sectors, groupInfo, "sectors", mask);
            ApplyGroupIndices(map.Things, groupInfo, "things", mask);
        }
    }

    public void WriteTagLabels()
    {
        MapConfiguration.DeleteSetting(TagLabelsPath);
        if (TagLabels.Count == 0) return;

        var labelEntries = new ListDictionary();
        int counter = 1;
        foreach (var pair in TagLabels)
        {
            if (pair.Key == 0 || string.IsNullOrEmpty(pair.Value)) continue;

            var data = new ListDictionary
            {
                { "tag", pair.Key },
                { "label", pair.Value },
            };
            labelEntries.Add("taglabel" + counter.ToString(System.Globalization.CultureInfo.InvariantCulture), data);
            counter++;
        }

        if (labelEntries.Count > 0) MapConfiguration.WriteSetting(TagLabelsPath, labelEntries);
    }

    public void ReadTagLabels()
    {
        TagLabels.Clear();

        var labelEntries = MapConfiguration.ReadSetting(TagLabelsPath, new ListDictionary());
        if (labelEntries == null) return;

        foreach (DictionaryEntry entry in labelEntries)
        {
            if (entry.Value is not IDictionary data) continue;
            int tag = ReadInt(data, "tag");
            string label = ReadString(data, "label");
            if (tag != 0 && !string.IsNullOrEmpty(label)) TagLabels[tag] = label;
        }
    }

    public void WriteDrawingOptions()
    {
        MapConfiguration.WriteSetting("defaultfloortexture", DefaultFloorTexture);
        MapConfiguration.WriteSetting("defaultceiltexture", DefaultCeilingTexture);
        MapConfiguration.WriteSetting("defaulttoptexture", DefaultTopTexture);
        MapConfiguration.WriteSetting("defaultwalltexture", DefaultWallTexture);
        MapConfiguration.WriteSetting("defaultbottomtexture", DefaultBottomTexture);
        MapConfiguration.WriteSetting("custombrightness", CustomBrightness);
        MapConfiguration.WriteSetting("customfloorheight", CustomFloorHeight);
        MapConfiguration.WriteSetting("customceilheight", CustomCeilingHeight);
        MapConfiguration.WriteSetting("overridefloortexture", OverrideFloorTexture);
        MapConfiguration.WriteSetting("overrideceiltexture", OverrideCeilingTexture);
        MapConfiguration.WriteSetting("overridetoptexture", OverrideTopTexture);
        MapConfiguration.WriteSetting("overridemiddletexture", OverrideMiddleTexture);
        MapConfiguration.WriteSetting("overridebottomtexture", OverrideBottomTexture);
        MapConfiguration.WriteSetting("overridefloorheight", OverrideFloorHeight);
        MapConfiguration.WriteSetting("overrideceilheight", OverrideCeilingHeight);
        MapConfiguration.WriteSetting("overridebrightness", OverrideBrightness);
        MapConfiguration.WriteSetting("uselongtexturenames", UseLongTextureNames);

        if (!double.IsNaN(ViewPosition.x) && !double.IsNaN(ViewPosition.y))
        {
            MapConfiguration.WriteSetting("viewpositionx", ViewPosition.x);
            MapConfiguration.WriteSetting("viewpositiony", ViewPosition.y);
        }
        else
        {
            MapConfiguration.DeleteSetting("viewpositionx");
            MapConfiguration.DeleteSetting("viewpositiony");
        }

        if (!double.IsNaN(ViewScale)) MapConfiguration.WriteSetting("viewscale", ViewScale);
        else MapConfiguration.DeleteSetting("viewscale");

        MapConfiguration.DeleteSetting("scriptcompiler");
        if (!string.IsNullOrEmpty(ScriptCompiler)) MapConfiguration.WriteSetting("scriptcompiler", ScriptCompiler);
    }

    public void ReadDrawingOptions(bool longTextureNamesSupported)
    {
        DefaultFloorTexture = MapConfiguration.ReadSetting("defaultfloortexture", "") ?? "";
        DefaultCeilingTexture = MapConfiguration.ReadSetting("defaultceiltexture", "") ?? "";
        DefaultTopTexture = MapConfiguration.ReadSetting("defaulttoptexture", "") ?? "";
        DefaultWallTexture = MapConfiguration.ReadSetting("defaultwalltexture", "") ?? "";
        DefaultBottomTexture = MapConfiguration.ReadSetting("defaultbottomtexture", "") ?? "";
        CustomBrightness = System.Math.Clamp(MapConfiguration.ReadSetting("custombrightness", 196), 0, 255);
        CustomFloorHeight = MapConfiguration.ReadSetting("customfloorheight", 0);
        CustomCeilingHeight = MapConfiguration.ReadSetting("customceilheight", 128);
        OverrideFloorTexture = MapConfiguration.ReadSetting("overridefloortexture", false);
        OverrideCeilingTexture = MapConfiguration.ReadSetting("overrideceiltexture", false);
        OverrideTopTexture = MapConfiguration.ReadSetting("overridetoptexture", false);
        OverrideMiddleTexture = MapConfiguration.ReadSetting("overridemiddletexture", false);
        OverrideBottomTexture = MapConfiguration.ReadSetting("overridebottomtexture", false);
        OverrideFloorHeight = MapConfiguration.ReadSetting("overridefloorheight", false);
        OverrideCeilingHeight = MapConfiguration.ReadSetting("overrideceilheight", false);
        OverrideBrightness = MapConfiguration.ReadSetting("overridebrightness", false);
        UseLongTextureNames = longTextureNamesSupported && MapConfiguration.ReadSetting("uselongtexturenames", false);
        ScriptCompiler = MapConfiguration.ReadSetting("scriptcompiler", "") ?? "";

        double x = MapConfiguration.ReadSetting("viewpositionx", double.NaN);
        double y = MapConfiguration.ReadSetting("viewpositiony", double.NaN);
        ViewPosition = !double.IsNaN(x) && !double.IsNaN(y) ? new Vector2D(x, y) : new Vector2D(double.NaN, double.NaN);
        ViewScale = MapConfiguration.ReadSetting("viewscale", double.NaN);
    }

    public void WriteScriptDocumentSettings()
    {
        MapConfiguration.DeleteSetting("scriptdocuments");
        int counter = 0;
        foreach (var settings in ScriptDocumentSettings.Values)
        {
            var data = new ListDictionary
            {
                { "filename", settings.Filename },
                { "hash", settings.Hash },
                { "resource", settings.ResourceLocation },
                { "tabtype", (int)settings.TabType },
                { "scripttype", (int)settings.ScriptType },
            };

            if (settings.CaretPosition > 0) data.Add("caretposition", settings.CaretPosition);
            if (settings.FirstVisibleLine > 0) data.Add("firstvisibleline", settings.FirstVisibleLine);
            if (settings.IsActiveTab) data.Add("activetab", true);

            string foldLevels = FormatFoldLevels(settings.FoldLevels);
            if (!string.IsNullOrEmpty(foldLevels)) data.Add("foldlevels", foldLevels);

            MapConfiguration.WriteSetting("scriptdocuments.document" + counter.ToString(CultureInfo.InvariantCulture), data);
            counter++;
        }
    }

    public void ReadScriptDocumentSettings()
    {
        ScriptDocumentSettings.Clear();

        var documents = MapConfiguration.ReadSetting("scriptdocuments", new Hashtable());
        if (documents == null) return;

        foreach (DictionaryEntry entry in documents)
        {
            if (entry.Value is not IDictionary data) continue;
            var settings = ReadScriptDocument(data);
            if (!string.IsNullOrEmpty(settings.Filename)) ScriptDocumentSettings[settings.Filename] = settings;
        }
    }

    public void WriteExternalCommandSettings()
    {
        ReloadResourcePreCommand.WriteSettings(MapConfiguration, "reloadresourceprecommand");
        ReloadResourcePostCommand.WriteSettings(MapConfiguration, "reloadresourcepostcommand");
        TestPreCommand.WriteSettings(MapConfiguration, "testprecommand");
        TestPostCommand.WriteSettings(MapConfiguration, "testpostcommand");
    }

    public void ReadExternalCommandSettings()
    {
        ReloadResourcePreCommand = new ExternalCommandSettings(MapConfiguration, "reloadresourceprecommand");
        ReloadResourcePostCommand = new ExternalCommandSettings(MapConfiguration, "reloadresourcepostcommand");
        TestPreCommand = new ExternalCommandSettings(MapConfiguration, "testprecommand");
        TestPostCommand = new ExternalCommandSettings(MapConfiguration, "testpostcommand");
    }

    public void WriteResources()
    {
        resources.WriteToConfig(MapConfiguration, "resources");
    }

    public void ReadResources()
    {
        resources.ReadFromConfig(MapConfiguration, "resources");
    }

    public int AddResource(DataLocation location)
    {
        var normalized = CloneLocation(location);
        normalized.Location = Path.GetFullPath(normalized.Location);

        for (int i = 0; i < resources.Count; i++)
        {
            if (!string.Equals(Path.GetFullPath(resources[i].Location), normalized.Location, StringComparison.OrdinalIgnoreCase)) continue;
            resources[i] = normalized;
            return i;
        }

        resources.Add(normalized);
        return resources.Count - 1;
    }

    public DataLocationList GetResources() => new(resources);

    public void ClearResources()
    {
        resources.Clear();
    }

    public void RemoveResource(int index)
    {
        resources.RemoveAt(index);
    }

    public void CopyResources(DataLocationList source)
    {
        resources.Clear();
        resources.AddRange(new DataLocationList(source));
    }

    public void WriteGridSetup(GridSetup grid)
    {
        grid.WriteToConfig(MapConfiguration, "grid");
    }

    public void ReadGridSetup(GridSetup grid)
    {
        grid.ReadFromConfig(MapConfiguration, "grid");
    }

    public string? ReadPluginSetting(string pluginName, string setting, string? defaultValue)
        => MapConfiguration.ReadSetting(PluginSettingPath(pluginName, setting), defaultValue);

    public int ReadPluginSetting(string pluginName, string setting, int defaultValue)
        => MapConfiguration.ReadSetting(PluginSettingPath(pluginName, setting), defaultValue);

    public float ReadPluginSetting(string pluginName, string setting, float defaultValue)
        => MapConfiguration.ReadSetting(PluginSettingPath(pluginName, setting), defaultValue);

    public double ReadPluginSetting(string pluginName, string setting, double defaultValue)
        => MapConfiguration.ReadSetting(PluginSettingPath(pluginName, setting), defaultValue);

    public short ReadPluginSetting(string pluginName, string setting, short defaultValue)
        => MapConfiguration.ReadSetting(PluginSettingPath(pluginName, setting), defaultValue);

    public long ReadPluginSetting(string pluginName, string setting, long defaultValue)
        => MapConfiguration.ReadSetting(PluginSettingPath(pluginName, setting), defaultValue);

    public bool ReadPluginSetting(string pluginName, string setting, bool defaultValue)
        => MapConfiguration.ReadSetting(PluginSettingPath(pluginName, setting), defaultValue);

    public byte ReadPluginSetting(string pluginName, string setting, byte defaultValue)
        => MapConfiguration.ReadSetting(PluginSettingPath(pluginName, setting), defaultValue);

    public IDictionary? ReadPluginSetting(string pluginName, string setting, IDictionary? defaultValue)
        => MapConfiguration.ReadSetting(PluginSettingPath(pluginName, setting), defaultValue);

    public bool WritePluginSetting(string pluginName, string setting, object? value)
        => MapConfiguration.WriteSetting(PluginSettingPath(pluginName, setting), value);

    public bool DeletePluginSetting(string pluginName, string setting)
        => MapConfiguration.DeleteSetting(PluginSettingPath(pluginName, setting));

    public int GetUniversalFieldType(string elementName, string fieldName, int defaultType, Configuration? gameConfiguration = null)
    {
        int configuredType = gameConfiguration?.ReadSetting("universalfields." + elementName + "." + fieldName + ".type", -1) ?? -1;
        return configuredType != -1
            ? configuredType
            : MapConfiguration.ReadSetting("fieldtypes." + elementName + "." + fieldName, defaultType);
    }

    public void SetUniversalFieldType(string elementName, string fieldName, int type, Configuration? gameConfiguration = null)
    {
        int configuredType = gameConfiguration?.ReadSetting("universalfields." + elementName + "." + fieldName + ".type", -1) ?? -1;
        if (configuredType == -1)
            MapConfiguration.WriteSetting("fieldtypes." + elementName + "." + fieldName, type);
    }

    public void ForgetUniversalFieldTypes()
    {
        MapConfiguration.DeleteSetting("fieldtypes");
    }

    public override string ToString() => CurrentName;

    private static void AddGroupIndices<T>(IDictionary group, string key, IReadOnlyList<T> items, int mask)
        where T : IGroupable
    {
        var indices = new List<string>();
        for (int i = 0; i < items.Count; i++)
            if ((items[i].Groups & mask) != 0) indices.Add(i.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (indices.Count > 0) group.Add(key, string.Join(" ", indices));
    }

    private static void ApplyGroupIndices<T>(IList<T> items, IDictionary groupInfo, string key, int mask)
        where T : IGroupable
    {
        if (!groupInfo.Contains(key) || groupInfo[key] is not string value) return;

        foreach (int index in ParseIndices(value))
        {
            if (index < 0 || index >= items.Count) continue;
            items[index].Groups |= mask;
        }
    }

    private static IEnumerable<int> ParseIndices(string value)
    {
        string[] parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            if (int.TryParse(part, out int index)) yield return index;
        }
    }

    private static void ClearSelectionGroups(MapSet map)
    {
        int mask = 0;
        for (int groupIndex = 0; groupIndex < SelectionGroupCount; groupIndex++) mask |= MapSet.GroupMask(groupIndex);
        ClearGroupBits(map.Vertices, mask);
        ClearGroupBits(map.Linedefs, mask);
        ClearGroupBits(map.Sectors, mask);
        ClearGroupBits(map.Things, mask);
    }

    private static void ClearGroupBits<T>(IEnumerable<T> items, int mask)
        where T : IGroupable
    {
        foreach (var item in items) item.Groups &= ~mask;
    }

    private static int ReadInt(IDictionary data, string key)
    {
        if (!data.Contains(key) || data[key] == null) return 0;
        if (data[key] is int value) return value;
        return int.TryParse(data[key]!.ToString(), out int parsed) ? parsed : 0;
    }

    private static string ReadString(IDictionary data, string key)
    {
        if (!data.Contains(key) || data[key] == null) return "";
        return data[key]!.ToString() ?? "";
    }

    private static long ReadLong(IDictionary data, string key)
    {
        if (!data.Contains(key) || data[key] == null) return 0;
        if (data[key] is long longValue) return longValue;
        if (data[key] is int intValue) return intValue;
        return long.TryParse(data[key]!.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : 0;
    }

    private static bool ReadBool(IDictionary data, string key)
    {
        if (!data.Contains(key) || data[key] == null) return false;
        if (data[key] is bool value) return value;
        return bool.TryParse(data[key]!.ToString(), out bool parsed) && parsed;
    }

    private static ScriptDocumentSettings ReadScriptDocument(IDictionary data)
    {
        var settings = new ScriptDocumentSettings
        {
            Filename = ReadString(data, "filename"),
            Hash = ReadLong(data, "hash"),
            ResourceLocation = ReadString(data, "resource"),
            TabType = (ScriptDocumentTabType)ReadInt(data, "tabtype"),
            ScriptType = (ScriptType)ReadInt(data, "scripttype"),
            CaretPosition = ReadInt(data, "caretposition"),
            FirstVisibleLine = ReadInt(data, "firstvisibleline"),
            IsActiveTab = ReadBool(data, "activetab"),
        };
        ReadFoldLevels(ReadString(data, "foldlevels"), settings.FoldLevels);
        return settings;
    }

    private static string FormatFoldLevels(Dictionary<int, HashSet<int>> foldLevels)
    {
        var groups = new List<string>();
        foreach (var group in foldLevels)
        {
            if (group.Value.Count == 0) continue;

            var lines = new List<string>();
            foreach (int line in group.Value) lines.Add(line.ToString(CultureInfo.InvariantCulture));
            groups.Add(group.Key.ToString(CultureInfo.InvariantCulture) + ":" + string.Join(",", lines));
        }

        return string.Join(";", groups);
    }

    private static void ReadFoldLevels(string value, Dictionary<int, HashSet<int>> foldLevels)
    {
        if (string.IsNullOrEmpty(value)) return;

        string[] groups = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string group in groups)
        {
            string[] parts = group.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) continue;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)) continue;
            if (foldLevels.ContainsKey(level)) continue;

            string[] lineParts = parts[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (lineParts.Length == 0) continue;

            var lines = new HashSet<int>();
            foreach (string linePart in lineParts)
            {
                if (!int.TryParse(linePart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int line))
                {
                    lines.Clear();
                    break;
                }
                lines.Add(line);
            }

            if (lines.Count == lineParts.Length) foldLevels[level] = lines;
        }
    }

    private static DataLocation CloneLocation(DataLocation source)
    {
        var clone = new DataLocation(source.Type, source.Location, source.Option1, source.Option2, source.NotForTesting);
        clone.InitialLocation = source.InitialLocation;
        clone.RequiredArchives.AddRange(source.RequiredArchives);
        return clone;
    }

    private static string PluginSettingPath(string pluginName, string setting)
        => pluginName.ToLowerInvariant() + "." + setting;
}
