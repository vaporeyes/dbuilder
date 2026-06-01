// ABOUTME: Mutable UDB-compatible draft model for user-created things filters.
// ABOUTME: Writes custom filters back to Configuration using UDB's thingsfilters schema.

using System;
using System.Collections.Generic;

namespace DBuilder.IO;

public sealed class ThingsFilterDraft
{
    public const string DefaultName = "Unnamed filter";

    public ThingsFilterDraft()
    {
        for (int i = 0; i < ThingArgs.Length; i++) ThingArgs[i] = -1;
    }

    public string Name { get; set; } = DefaultName;
    public string Category { get; set; } = "";
    public bool Invert { get; set; }
    public int DisplayMode { get; set; }
    public int ThingType { get; set; } = -1;
    public int ThingAngle { get; set; } = -1;
    public int ThingZHeight { get; set; } = int.MinValue;
    public int ThingAction { get; set; } = -1;
    public int[] ThingArgs { get; } = new int[5];
    public int ThingTag { get; set; } = -1;
    public List<string> RequiredFields { get; } = new();
    public List<string> ForbiddenFields { get; } = new();
    public Dictionary<string, ThingsFilterCustomFieldInfo> CustomFields { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static ThingsFilterDraft FromInfo(ThingsFilterInfo info)
    {
        var draft = new ThingsFilterDraft
        {
            Name = info.Name,
            Category = info.Category,
            Invert = info.Invert,
            DisplayMode = info.DisplayMode,
            ThingType = info.ThingType,
            ThingAngle = info.ThingAngle,
            ThingZHeight = info.ThingZHeight,
            ThingAction = info.ThingAction,
            ThingTag = info.ThingTag,
        };

        for (int i = 0; i < draft.ThingArgs.Length && i < info.ThingArgs.Count; i++)
            draft.ThingArgs[i] = info.ThingArgs[i];
        draft.RequiredFields.AddRange(info.RequiredFields);
        draft.ForbiddenFields.AddRange(info.ForbiddenFields);
        foreach (var (key, value) in info.CustomFields) draft.CustomFields[key] = value;

        return draft;
    }

    public ThingsFilterInfo ToInfo(string key)
        => new(
            key,
            Name,
            Category,
            Invert,
            DisplayMode,
            ThingType,
            ThingAngle,
            ThingZHeight,
            ThingAction,
            (int[])ThingArgs.Clone(),
            ThingTag,
            RequiredFields.ToArray(),
            ForbiddenFields.ToArray(),
            new Dictionary<string, ThingsFilterCustomFieldInfo>(CustomFields, StringComparer.OrdinalIgnoreCase));

    public bool IsValid()
        => !string.IsNullOrEmpty(Category)
            || ThingType > 0
            || ThingAngle != -1
            || ThingZHeight != int.MinValue
            || ThingAction != -1
            || ThingTag != -1
            || RequiredFields.Count > 0
            || ForbiddenFields.Count > 0
            || CustomFields.Count > 0;

    public void WriteSettings(Configuration configuration, string path)
    {
        configuration.WriteSetting(path + ".name", Name);
        configuration.WriteSetting(path + ".category", Category);
        configuration.WriteSetting(path + ".invert", Invert);
        configuration.WriteSetting(path + ".displaymode", DisplayMode);
        configuration.WriteSetting(path + ".type", ThingType);
        configuration.WriteSetting(path + ".angle", ThingAngle);
        configuration.WriteSetting(path + ".zheight", ThingZHeight);
        configuration.WriteSetting(path + ".action", ThingAction);
        for (int i = 0; i < ThingArgs.Length; i++) configuration.WriteSetting(path + ".arg" + i, ThingArgs[i]);
        configuration.WriteSetting(path + ".tag", ThingTag);

        foreach (string field in RequiredFields)
            configuration.WriteSetting(path + ".fields." + field, true);
        foreach (string field in ForbiddenFields)
            configuration.WriteSetting(path + ".fields." + field, false);

        foreach (var (key, field) in CustomFields)
        {
            configuration.WriteSetting(path + ".customfieldtypes." + key, field.Type);
            configuration.WriteSetting(path + ".customfieldvalues." + key, field.Value);
        }
    }
}
