// ABOUTME: Models UDBScript QueryOptions runtime prompt state and option validation.
// ABOUTME: Keeps query option behavior aligned with upstream addOption and clear semantics.

using System.Collections;

namespace DBuilder.IO;

public sealed record UdbScriptQueryOptionAddResult(
    bool Added,
    string ErrorDescription = "");

public sealed record UdbScriptQueryOptionRow(
    string DescriptionCellValue,
    object? ValueCellValue,
    bool StoresOptionTag);

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

public sealed record UdbScriptQueryOptionsDialogControl(
    string Name,
    int X,
    int Y,
    int Width,
    int Height,
    int TabIndex);

public sealed record UdbScriptQueryOptionsDialogLayout(
    int ClientWidth,
    int ClientHeight,
    string AcceptButtonName,
    string CancelButtonName,
    bool MaximizeBox,
    bool MinimizeBox,
    IReadOnlyList<UdbScriptQueryOptionsDialogControl> Controls);

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
    public const string OkButtonName = "btnOK";
    public const string CancelButtonName = "btnCancel";
    public const string ParametersViewName = "parametersview";

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

    public static UdbScriptQueryOptionsDialogLayout DialogLayout()
        => new(
            ClientWidth: 432,
            ClientHeight: 321,
            AcceptButtonName: OkButtonName,
            CancelButtonName: CancelButtonName,
            MaximizeBox: false,
            MinimizeBox: false,
            Controls:
            [
                new(OkButtonName, 265, 286, 75, 23, TabIndex: 1),
                new(CancelButtonName, 346, 286, 75, 23, TabIndex: 2),
                new(ParametersViewName, 12, 12, 408, 268, TabIndex: 3),
            ]);

    public static UdbScriptQueryOptionsQueryPlan QueryPlan(UdbScriptQueryOptionsDialogResult dialogResult)
        => new(
            InvokesRunnerPaused: true,
            PromptMetadata(),
            dialogResult,
            dialogResult == UdbScriptQueryOptionsDialogResult.Ok);

    public static UdbScriptQueryOptionRow OptionRow(UdbScriptOption option)
        => new(option.Description, option.Value, StoresOptionTag: true);

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

        if (!TryReadEnumValues(enumValues, out IReadOnlyList<UdbScriptEnumValue> values))
            return new UdbScriptQueryOptionAddResult(false);

        object? effectiveDefault = UdbScriptDiscovery.EffectiveDefault(defaultValue, values);

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

    private static bool TryReadEnumValues(object? values, out IReadOnlyList<UdbScriptEnumValue> enumValues)
    {
        if (values is null)
        {
            enumValues = Array.Empty<UdbScriptEnumValue>();
            return true;
        }

        if (values is IDictionary dictionary)
        {
            enumValues = ReadDictionary(dictionary);
            return true;
        }

        if (values is IEnumerable<KeyValuePair<string, object?>> typedDictionary)
        {
            enumValues = ReadDictionary(typedDictionary);
            return true;
        }

        enumValues = Array.Empty<UdbScriptEnumValue>();
        return false;
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
