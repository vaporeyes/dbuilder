// ABOUTME: Models plugin descriptors, contribution kinds and lifecycle hook planning for DBuilder.
// ABOUTME: Keeps plugin-host rules testable before any runtime assembly loading is added.

namespace DBuilder.IO;

public enum DBuilderPluginContributionKind
{
    Action,
    Menu,
    Toolbar,
    EditMode,
    Docker,
    ResourceHandler
}

public enum DBuilderPluginLifecycleHook
{
    Load,
    Initialize,
    RegisterActions,
    RegisterUi,
    RegisterEditModes,
    RegisterDockers,
    RegisterResourceHandlers,
    MapOpened,
    Engage,
    Disengage,
    Dispose
}

public sealed record DBuilderPluginContribution(
    DBuilderPluginContributionKind Kind,
    string Id,
    string Title);

public sealed record DBuilderPluginDescriptor(
    string Name,
    string AssemblyPath,
    bool Enabled = true,
    bool RequiresMap = false,
    IReadOnlyList<DBuilderPluginContribution>? Contributions = null);

public sealed record DBuilderPluginLifecycleRequest(
    bool MapOpen = false,
    bool Engage = false,
    bool Disengage = false,
    bool Shutdown = false);

public sealed record DBuilderPluginLifecyclePlan(
    DBuilderPluginDescriptor Descriptor,
    IReadOnlyList<DBuilderPluginLifecycleHook> Hooks,
    IReadOnlyList<string> Warnings);

public static class DBuilderPluginHostModel
{
    public static IReadOnlyList<DBuilderPluginDescriptor> NormalizeDescriptors(
        IEnumerable<DBuilderPluginDescriptor> descriptors)
    {
        var result = new List<DBuilderPluginDescriptor>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (DBuilderPluginDescriptor descriptor in descriptors)
        {
            string name = descriptor.Name.Trim();
            string assemblyPath = descriptor.AssemblyPath.Trim();
            if (name.Length == 0 || assemblyPath.Length == 0) continue;
            if (!names.Add(name)) continue;

            result.Add(descriptor with
            {
                Name = name,
                AssemblyPath = assemblyPath,
                Contributions = NormalizeContributions(descriptor.Contributions)
            });
        }

        return result
            .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static DBuilderPluginLifecyclePlan PlanLifecycle(
        DBuilderPluginDescriptor descriptor,
        DBuilderPluginLifecycleRequest request)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(descriptor.Name)) warnings.Add("Plugin name is missing.");
        if (string.IsNullOrWhiteSpace(descriptor.AssemblyPath)) warnings.Add("Plugin assembly path is missing.");
        if (!descriptor.Enabled) warnings.Add($"Plugin {descriptor.Name.Trim()} is disabled.");
        if (warnings.Count > 0) return new DBuilderPluginLifecyclePlan(descriptor, Array.Empty<DBuilderPluginLifecycleHook>(), warnings);

        IReadOnlyList<DBuilderPluginContribution> contributions = NormalizeContributions(descriptor.Contributions);
        var hooks = new List<DBuilderPluginLifecycleHook>
        {
            DBuilderPluginLifecycleHook.Load,
            DBuilderPluginLifecycleHook.Initialize
        };

        if (contributions.Any(contribution => contribution.Kind == DBuilderPluginContributionKind.Action))
            hooks.Add(DBuilderPluginLifecycleHook.RegisterActions);

        if (contributions.Any(contribution =>
                contribution.Kind is DBuilderPluginContributionKind.Menu or DBuilderPluginContributionKind.Toolbar))
            hooks.Add(DBuilderPluginLifecycleHook.RegisterUi);

        if (contributions.Any(contribution => contribution.Kind == DBuilderPluginContributionKind.EditMode))
            hooks.Add(DBuilderPluginLifecycleHook.RegisterEditModes);

        if (contributions.Any(contribution => contribution.Kind == DBuilderPluginContributionKind.Docker))
            hooks.Add(DBuilderPluginLifecycleHook.RegisterDockers);

        if (contributions.Any(contribution => contribution.Kind == DBuilderPluginContributionKind.ResourceHandler))
            hooks.Add(DBuilderPluginLifecycleHook.RegisterResourceHandlers);

        if (descriptor.RequiresMap && request.MapOpen) hooks.Add(DBuilderPluginLifecycleHook.MapOpened);
        if (request.Engage) hooks.Add(DBuilderPluginLifecycleHook.Engage);
        if (request.Disengage) hooks.Add(DBuilderPluginLifecycleHook.Disengage);
        if (request.Shutdown) hooks.Add(DBuilderPluginLifecycleHook.Dispose);

        return new DBuilderPluginLifecyclePlan(
            descriptor with { Contributions = contributions },
            hooks,
            warnings);
    }

    private static IReadOnlyList<DBuilderPluginContribution> NormalizeContributions(
        IReadOnlyList<DBuilderPluginContribution>? contributions)
    {
        if (contributions == null || contributions.Count == 0) return Array.Empty<DBuilderPluginContribution>();

        var result = new List<DBuilderPluginContribution>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DBuilderPluginContribution contribution in contributions)
        {
            string id = contribution.Id.Trim();
            string title = contribution.Title.Trim();
            if (id.Length == 0 || title.Length == 0) continue;
            if (!ids.Add(id)) continue;

            result.Add(contribution with { Id = id, Title = title });
        }

        return result
            .OrderBy(contribution => contribution.Kind)
            .ThenBy(contribution => contribution.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
