// ABOUTME: Models UDBScript QueryOptions runtime prompt state and option validation.
// ABOUTME: Keeps query option behavior aligned with upstream addOption and clear semantics.

using System.Collections;

namespace DBuilder.IO;

public sealed record UdbScriptQueryOptionAddResult(
    bool Added,
    string ErrorDescription = "");

public enum UdbScriptQueryOptionsDialogResult
{
    Ok,
    Cancel,
}

public sealed record UdbScriptQueryOptionsPromptMetadata(
    string Title,
    string OkButtonText,
    string CancelButtonText,
    bool IsFixedDialog,
    bool AcceptsOk,
    bool Cancels);

public sealed record UdbScriptQueryOptionsQueryPlan(
    bool InvokesRunnerPaused,
    UdbScriptQueryOptionsPromptMetadata Prompt,
    UdbScriptQueryOptionsDialogResult DialogResult,
    bool ReturnValue);

public enum UdbScriptQueryOptionsMemberKind
{
    Property,
    Method,
}

public sealed record UdbScriptQueryOptionsApiMember(
    string Name,
    UdbScriptQueryOptionsMemberKind Kind,
    string ReturnType,
    IReadOnlyList<string> Parameters);

public sealed class UdbScriptQueryOptionsModel
{
    public const string PromptTitle = "Query options";
    public const string OkButtonText = "OK";
    public const string CancelButtonText = "Cancel";

    private readonly List<UdbScriptOption> options = new();

    public IReadOnlyList<UdbScriptOption> Options => options;

    public static IReadOnlyList<UdbScriptQueryOptionsApiMember> ApiMembers { get; } =
    [
        new("options", UdbScriptQueryOptionsMemberKind.Property, "ExpandoObject", Array.Empty<string>()),
        new("addOption", UdbScriptQueryOptionsMemberKind.Method, "void", ["string", "string", "int", "object"]),
        new("addOption", UdbScriptQueryOptionsMemberKind.Method, "void", ["string", "string", "int", "object", "object"]),
        new("clear", UdbScriptQueryOptionsMemberKind.Method, "void", Array.Empty<string>()),
        new("query", UdbScriptQueryOptionsMemberKind.Method, "bool", Array.Empty<string>()),
    ];

    public static UdbScriptQueryOptionsPromptMetadata PromptMetadata()
        => new(
            PromptTitle,
            OkButtonText,
            CancelButtonText,
            IsFixedDialog: true,
            AcceptsOk: true,
            Cancels: true);

    public static UdbScriptQueryOptionsQueryPlan QueryPlan(UdbScriptQueryOptionsDialogResult dialogResult)
        => new(
            InvokesRunnerPaused: true,
            PromptMetadata(),
            dialogResult,
            dialogResult == UdbScriptQueryOptionsDialogResult.Ok);

    public UdbScriptQueryOptionAddResult AddOption(
        string scriptFile,
        string name,
        string description,
        int type,
        object defaultValue,
        object? enumValues = null)
    {
        if (!UdbScriptDiscovery.IsValidOptionType(type))
        {
            return new UdbScriptQueryOptionAddResult(
                false,
                "Error in script " + scriptFile + ": option " + name + " has invalid type " + type);
        }

        IReadOnlyList<UdbScriptEnumValue> values = ReadEnumValues(enumValues);
        object effectiveDefault = UdbScriptDiscovery.EffectiveDefault(defaultValue, values);

        options.Add(new UdbScriptOption(
            name,
            description,
            type,
            effectiveDefault,
            effectiveDefault,
            values,
            name));

        return new UdbScriptQueryOptionAddResult(true);
    }

    public IReadOnlyDictionary<string, object> GetScriptOptions()
        => UdbScriptOptionsUiModel.GetScriptOptions(options);

    public bool SetValue(string name, object value)
    {
        int index = options.FindIndex(option => option.Name == name);
        if (index == -1)
            return false;

        options[index] = options[index] with { Value = value };
        return true;
    }

    public void Clear()
        => options.Clear();

    private static IReadOnlyList<UdbScriptEnumValue> ReadEnumValues(object? values)
    {
        if (values is null)
            return Array.Empty<UdbScriptEnumValue>();

        if (values is IDictionary dictionary)
            return ReadDictionary(dictionary);

        if (values is IEnumerable<KeyValuePair<string, object?>> typedDictionary)
            return ReadDictionary(typedDictionary);

        return Array.Empty<UdbScriptEnumValue>();
    }

    private static IReadOnlyList<UdbScriptEnumValue> ReadDictionary(IDictionary values)
    {
        if (values.Count == 0)
            return Array.Empty<UdbScriptEnumValue>();

        var result = new List<UdbScriptEnumValue>();
        foreach (DictionaryEntry entry in values)
            result.Add(new UdbScriptEnumValue(entry.Key.ToString() ?? "", entry.Value?.ToString()));

        return result;
    }

    private static IReadOnlyList<UdbScriptEnumValue> ReadDictionary(IEnumerable<KeyValuePair<string, object?>> values)
    {
        var result = new List<UdbScriptEnumValue>();
        foreach (KeyValuePair<string, object?> entry in values)
            result.Add(new UdbScriptEnumValue(entry.Key, entry.Value?.ToString()));

        return result;
    }
}
