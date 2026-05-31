// ABOUTME: Models UDB-style script document tab identity and persisted view settings.
// ABOUTME: Keeps file, lump and resource tab behavior testable outside the editor UI.

using System;
using System.Collections.Generic;
using System.IO;

namespace DBuilder.IO;

public sealed record ScriptDocumentTabViewState(
    int CaretPosition = 0,
    int FirstVisibleLine = 0,
    bool IsActiveTab = false,
    Dictionary<int, HashSet<int>>? FoldLevels = null);

public sealed class ScriptDocumentTabModel
{
    private ScriptDocumentTabModel(
        ScriptDocumentTabType tabType,
        ScriptType scriptType,
        string filename,
        string title,
        string tooltip,
        bool explicitSave,
        bool isSaveAsRequired,
        bool isClosable,
        bool isReconfigurable,
        bool isReadOnly,
        string resourceLocation = "")
    {
        TabType = tabType;
        ScriptType = scriptType;
        Filename = filename;
        Title = title;
        ToolTip = tooltip;
        ExplicitSave = explicitSave;
        IsSaveAsRequired = isSaveAsRequired;
        IsClosable = isClosable;
        IsReconfigurable = isReconfigurable;
        IsReadOnly = isReadOnly;
        ResourceLocation = resourceLocation;
    }

    public ScriptDocumentTabType TabType { get; }

    public ScriptType ScriptType { get; }

    public string Filename { get; }

    public string Title { get; }

    public string ToolTip { get; }

    public bool ExplicitSave { get; }

    public bool IsSaveAsRequired { get; }

    public bool IsClosable { get; }

    public bool IsReconfigurable { get; }

    public bool IsReadOnly { get; }

    public string ResourceLocation { get; }

    public static ScriptDocumentTabModel NewFile(ScriptConfigurationInfo configuration)
    {
        string extension = configuration.Extensions.Count > 0 && configuration.Extensions[0].Length > 0
            ? "." + configuration.Extensions[0]
            : "";
        return new ScriptDocumentTabModel(
            ScriptDocumentTabType.File,
            configuration.ScriptType,
            "",
            "Untitled" + extension,
            "",
            explicitSave: true,
            isSaveAsRequired: true,
            isClosable: true,
            isReconfigurable: true,
            isReadOnly: false);
    }

    public static ScriptDocumentTabModel OpenFile(string path, ScriptConfigurationInfo configuration)
        => new(
            ScriptDocumentTabType.File,
            configuration.ScriptType,
            path,
            Path.GetFileName(path),
            path,
            explicitSave: true,
            isSaveAsRequired: false,
            isClosable: true,
            isReconfigurable: true,
            isReadOnly: false);

    public static ScriptDocumentTabModel Lump(string lumpName, string title, ScriptConfigurationInfo configuration)
        => new(
            ScriptDocumentTabType.Lump,
            configuration.ScriptType,
            lumpName,
            title,
            "",
            explicitSave: false,
            isSaveAsRequired: false,
            isClosable: false,
            isReconfigurable: false,
            isReadOnly: false);

    public static ScriptDocumentTabModel Resource(ScriptResource resource)
        => new(
            ScriptDocumentTabType.Resource,
            resource.ScriptType,
            resource.FilePathName,
            resource.ToString(),
            resource.FilePathName,
            explicitSave: true,
            isSaveAsRequired: false,
            isClosable: true,
            isReconfigurable: false,
            resource.IsReadOnly,
            resource.ResourcePath);

    public ScriptDocumentSettings GetViewSettings(string text, ScriptDocumentTabViewState viewState)
    {
        var settings = new ScriptDocumentSettings
        {
            Filename = SettingsFilename(),
            ResourceLocation = ResourceLocation,
            TabType = TabType,
            ScriptType = ScriptType,
            CaretPosition = viewState.CaretPosition,
            FirstVisibleLine = viewState.FirstVisibleLine,
            IsActiveTab = viewState.IsActiveTab,
            Hash = MurmurHash2.Hash(text),
        };

        if (viewState.FoldLevels != null)
        {
            foreach (var group in viewState.FoldLevels)
                settings.FoldLevels[group.Key] = new HashSet<int>(group.Value);
        }

        return settings;
    }

    private string SettingsFilename()
        => TabType == ScriptDocumentTabType.Resource && ResourceLocation.Length > 0
            ? Path.Combine(ResourceLocation, Filename)
            : Filename;
}
