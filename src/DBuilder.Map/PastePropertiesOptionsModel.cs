// ABOUTME: Models UDB paste-properties option tabs for copied map element fields.
// ABOUTME: Keeps paste option availability and apply behavior independent from editor dialogs.

namespace DBuilder.Map;

public enum PastePropertiesElementKind
{
    Vertex,
    Linedef,
    Sidedef,
    Sector,
    Thing,
}

public sealed record PastePropertiesCopiedState(
    bool Vertex = false,
    bool Linedef = false,
    bool Sidedef = false,
    bool Sector = false,
    bool Thing = false);

public sealed class PastePropertiesOption
{
    public PastePropertiesOption(
        string key,
        string description,
        bool isChecked,
        bool supportsCurrentMapFormat = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Key = key;
        Description = description;
        IsChecked = isChecked;
        SupportsCurrentMapFormat = supportsCurrentMapFormat;
    }

    public string Key { get; }
    public string Description { get; }
    public bool SupportsCurrentMapFormat { get; }
    public bool IsChecked { get; private set; }

    public void SetChecked(bool isChecked) => IsChecked = isChecked;
}

public sealed record PastePropertiesOptionGroup(
    PastePropertiesElementKind Kind,
    string Title,
    IReadOnlyList<PastePropertiesOption> Options);

public sealed record PastePropertiesOptionsCatalog(
    PastePropertiesOptionGroup Vertex,
    PastePropertiesOptionGroup Linedef,
    PastePropertiesOptionGroup Sidedef,
    PastePropertiesOptionGroup Sector,
    PastePropertiesOptionGroup Thing);

public sealed record PastePropertiesOptionsTab(
    PastePropertiesElementKind Kind,
    string Title,
    IReadOnlyList<PastePropertiesOption> Options);

public sealed record PastePropertiesOptionsResult(
    bool IsAvailable,
    string? StatusMessage,
    IReadOnlyList<PastePropertiesOptionsTab> Tabs);

public static class PastePropertiesOptionsModel
{
    public const string NoCopiedPropertiesMessage = "No copied properties to apply!";
    public const string NoSupportedPropertiesMessage =
        "Current map format doesn't support any properties for selected map elements!";

    public static PastePropertiesOptionsResult Build(
        PastePropertiesCopiedState copied,
        IEnumerable<PastePropertiesElementKind> targetKinds,
        PastePropertiesOptionsCatalog catalog)
    {
        var groups = new List<PastePropertiesOptionGroup>();
        var added = new HashSet<PastePropertiesElementKind>();

        foreach (PastePropertiesElementKind targetKind in targetKinds)
        {
            switch (targetKind)
            {
                case PastePropertiesElementKind.Thing:
                    if (copied.Thing) Add(groups, added, catalog.Thing);
                    break;

                case PastePropertiesElementKind.Sector:
                    if (copied.Sector) Add(groups, added, catalog.Sector);
                    break;

                case PastePropertiesElementKind.Linedef:
                case PastePropertiesElementKind.Sidedef:
                    if (copied.Linedef || copied.Sidedef)
                    {
                        Add(groups, added, catalog.Linedef);
                        Add(groups, added, catalog.Sidedef);
                    }
                    break;

                case PastePropertiesElementKind.Vertex:
                    if (copied.Vertex) Add(groups, added, catalog.Vertex);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(targetKinds), targetKind, "Unknown map element type.");
            }
        }

        if (groups.Count == 0)
            return new PastePropertiesOptionsResult(false, NoCopiedPropertiesMessage, []);

        var tabs = groups
            .Select(group => new PastePropertiesOptionsTab(
                group.Kind,
                group.Title,
                group.Options.Where(option => option.SupportsCurrentMapFormat).ToList()))
            .Where(tab => tab.Options.Count > 0)
            .ToList();

        if (tabs.Count == 0)
            return new PastePropertiesOptionsResult(false, NoSupportedPropertiesMessage, []);

        return new PastePropertiesOptionsResult(true, null, tabs);
    }

    public static void Apply(
        PastePropertiesOptionsResult result,
        IReadOnlyDictionary<string, bool> checkedOptions)
    {
        if (!result.IsAvailable) return;

        foreach (PastePropertiesOptionsTab tab in result.Tabs)
        {
            foreach (PastePropertiesOption option in tab.Options)
            {
                if (checkedOptions.TryGetValue(option.Key, out bool isChecked))
                    option.SetChecked(isChecked);
            }
        }
    }

    public static void SetTabEnabled(PastePropertiesOptionsTab tab, bool isChecked)
    {
        foreach (PastePropertiesOption option in tab.Options)
            option.SetChecked(isChecked);
    }

    private static void Add(
        List<PastePropertiesOptionGroup> groups,
        HashSet<PastePropertiesElementKind> added,
        PastePropertiesOptionGroup group)
    {
        if (added.Add(group.Kind))
            groups.Add(group);
    }
}
