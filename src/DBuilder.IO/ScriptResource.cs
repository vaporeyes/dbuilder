// ABOUTME: Models UDB-style script resources with resource identity and searchable text.
// ABOUTME: Keeps script resource find-usages behavior independent of editor UI tabs.

using System;
using System.Collections.Generic;
using System.IO;

namespace DBuilder.IO;

public sealed record ScriptResourceUsageResult(
    ScriptResource Resource,
    string Line,
    int LineIndex,
    int MatchStart,
    int MatchEnd);

public sealed class ScriptResource
{
    private readonly Func<string?> loadText;

    public ScriptResource(
        string resourcePath,
        string resourceDisplayName,
        string filename,
        ScriptType scriptType,
        Func<string?> loadText,
        int lumpIndex = -1,
        bool isReadOnly = false,
        string parentResourceLocation = "")
    {
        ResourcePath = resourcePath;
        ResourceDisplayName = resourceDisplayName;
        Filename = NormalizeFilename(filename);
        FilePathName = Path.Combine(resourcePath, Filename);
        ScriptType = scriptType;
        this.loadText = loadText;
        LumpIndex = lumpIndex;
        IsReadOnly = isReadOnly;
        ParentResourceLocation = parentResourceLocation;
    }

    public string Filename { get; }

    public string FilePathName { get; }

    public int LumpIndex { get; }

    public string ResourcePath { get; }

    public string ResourceDisplayName { get; }

    public string ParentResourceLocation { get; }

    public HashSet<string> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ScriptType ScriptType { get; }

    public bool IsReadOnly { get; }

    public static ScriptResource FromText(
        string resourcePath,
        string resourceDisplayName,
        string filename,
        ScriptType scriptType,
        string text,
        int lumpIndex = -1,
        bool isReadOnly = false,
        string parentResourceLocation = "")
        => new(
            resourcePath,
            resourceDisplayName,
            filename,
            scriptType,
            () => text,
            lumpIndex,
            isReadOnly,
            parentResourceLocation);

    public bool ContainsText(
        string findText,
        bool wholeWord = false,
        bool caseSensitive = false)
    {
        string? text = loadText();
        return text != null && ScriptFindUsages.ContainsText(text, findText, wholeWord, caseSensitive);
    }

    public IReadOnlyList<ScriptResourceUsageResult> FindUsages(
        string findText,
        bool wholeWord = false,
        bool caseSensitive = false)
    {
        string? text = loadText();
        if (text == null) return Array.Empty<ScriptResourceUsageResult>();

        var results = new List<ScriptResourceUsageResult>();
        foreach (ScriptUsageResult usage in ScriptFindUsages.Find(text, findText, wholeWord, caseSensitive))
        {
            results.Add(new ScriptResourceUsageResult(
                this,
                usage.Line,
                usage.LineIndex,
                usage.MatchStart,
                usage.MatchEnd));
        }

        return results;
    }

    public bool MatchesResourceContainer(
        string resourcePath,
        string resourceDisplayName,
        string parentResourceLocation = "")
        => ParentResourceLocation.Length > 0
            ? string.Equals(ParentResourceLocation, parentResourceLocation, StringComparison.Ordinal)
                && string.Equals(ResourceDisplayName, resourceDisplayName, StringComparison.Ordinal)
            : string.Equals(ResourcePath, resourcePath, StringComparison.Ordinal);

    private static string NormalizeFilename(string filename)
        => filename
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

    public override string ToString()
        => (LumpIndex != -1 ? LumpIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" : "")
            + Path.GetFileName(Filename);
}
