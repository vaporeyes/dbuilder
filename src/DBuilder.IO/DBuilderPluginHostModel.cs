// ABOUTME: Models plugin descriptors, contribution kinds and lifecycle hook planning for DBuilder.
// ABOUTME: Keeps plugin-host rules and reflection runtime execution testable outside the editor UI.

using System.IO;
using System.Reflection;

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

public enum DBuilderPluginDiagnosticSeverity
{
    Warning,
    Error
}

public interface IDBuilderPlugin
{
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

public sealed record DBuilderPluginDiagnostic(
    DBuilderPluginDiagnosticSeverity Severity,
    string PluginName,
    string Message);

public sealed record DBuilderPluginDescriptorPlan(
    IReadOnlyList<DBuilderPluginDescriptor> Descriptors,
    IReadOnlyList<DBuilderPluginDiagnostic> Diagnostics);

public sealed record DBuilderPluginLoadCandidate(
    string PluginName,
    string AssemblyPath,
    int Order,
    bool RequiresMap);

public sealed record DBuilderPluginLoadPlan(
    IReadOnlyList<DBuilderPluginLoadCandidate> Candidates,
    IReadOnlyList<DBuilderPluginDiagnostic> Diagnostics);

public sealed record DBuilderPluginAssemblyLoadAttempt(
    string PluginName,
    string AssemblyPath,
    int Order,
    bool AssemblyFound);

public sealed record DBuilderPluginAssemblyLoadPlan(
    IReadOnlyList<DBuilderPluginAssemblyLoadAttempt> Attempts,
    IReadOnlyList<DBuilderPluginDiagnostic> Diagnostics);

public sealed record DBuilderPluginTypeDiscovery(
    string PluginName,
    string AssemblyPath,
    int Order,
    string PluginTypeName);

public sealed record DBuilderPluginTypeDiscoveryPlan(
    IReadOnlyList<DBuilderPluginTypeDiscovery> Discoveries,
    IReadOnlyList<DBuilderPluginDiagnostic> Diagnostics);

public sealed record DBuilderPluginActivationAttempt(
    string PluginName,
    string AssemblyPath,
    string PluginTypeName,
    int Order,
    bool Activated,
    string? Error = null);

public sealed record DBuilderPluginActivationPlan(
    IReadOnlyList<DBuilderPluginActivationAttempt> Attempts,
    IReadOnlyList<DBuilderPluginDiagnostic> Diagnostics);

public sealed record DBuilderPluginRuntimeInstance(
    string PluginName,
    string AssemblyPath,
    string PluginTypeName,
    int Order,
    IDBuilderPlugin Instance);

public sealed record DBuilderPluginRuntimeInstancePlan(
    IReadOnlyList<DBuilderPluginRuntimeInstance> Instances,
    IReadOnlyList<DBuilderPluginDiagnostic> Diagnostics);

public sealed record DBuilderPluginCompatibilityCheck(
    string PluginName,
    string AssemblyPath,
    string PluginTypeName,
    int Order,
    int MinimumRevision,
    bool StrictRevisionMatching,
    bool Compatible,
    string? Error = null);

public sealed record DBuilderPluginCompatibilityPlan(
    IReadOnlyList<DBuilderPluginCompatibilityCheck> Checks,
    IReadOnlyList<DBuilderPluginRuntimeInstance> Instances,
    IReadOnlyList<DBuilderPluginDiagnostic> Diagnostics);

public sealed record DBuilderPluginShutdownAttempt(
    string PluginName,
    string AssemblyPath,
    string PluginTypeName,
    int Order,
    bool Disposed,
    string? Error = null);

public sealed record DBuilderPluginShutdownPlan(
    IReadOnlyList<DBuilderPluginShutdownAttempt> Attempts,
    IReadOnlyList<DBuilderPluginDiagnostic> Diagnostics);

public sealed record DBuilderPluginRuntimePlan(
    DBuilderPluginHostPlan HostPlan,
    DBuilderPluginAssemblyLoadPlan AssemblyLoadPlan,
    DBuilderPluginTypeDiscoveryPlan TypeDiscoveryPlan,
    DBuilderPluginActivationPlan ActivationPlan,
    DBuilderPluginHostPlan ReadyHostPlan);

public sealed record DBuilderPluginReflectionRuntimePlan(
    DBuilderPluginHostPlan HostPlan,
    DBuilderPluginAssemblyLoadPlan AssemblyLoadPlan,
    DBuilderPluginTypeDiscoveryPlan TypeDiscoveryPlan,
    DBuilderPluginRuntimeInstancePlan InstancePlan,
    DBuilderPluginCompatibilityPlan CompatibilityPlan,
    DBuilderPluginHostPlan ReadyHostPlan);

public sealed record DBuilderPluginSettingDescriptor(
    string Key,
    object? DefaultValue = null);

public sealed record DBuilderPluginSettingsSnapshot(
    string PluginName,
    IReadOnlyDictionary<string, object?> Values,
    IReadOnlyList<string> Warnings);

public sealed record DBuilderPluginUiContribution(
    string PluginName,
    DBuilderPluginContributionKind Kind,
    string Id,
    string Title);

public sealed record DBuilderPluginUiContributionPlan(
    IReadOnlyList<DBuilderPluginUiContribution> Menus,
    IReadOnlyList<DBuilderPluginUiContribution> Toolbars,
    IReadOnlyList<string> Warnings);

public sealed record DBuilderPluginResourceHandler(
    string PluginName,
    string Id,
    string Title);

public sealed record DBuilderPluginResourceHandlerPlan(
    IReadOnlyList<DBuilderPluginResourceHandler> Handlers,
    IReadOnlyList<string> Warnings);

public sealed record DBuilderPluginApiContribution(
    string PluginName,
    DBuilderPluginContributionKind Kind,
    string Id,
    string Title);

public sealed record DBuilderPluginApiContributionPlan(
    IReadOnlyList<DBuilderPluginApiContribution> Actions,
    IReadOnlyList<DBuilderPluginApiContribution> EditModes,
    IReadOnlyList<DBuilderPluginApiContribution> Dockers,
    IReadOnlyList<string> Warnings);

public sealed record DBuilderPluginHostPlan(
    DBuilderPluginDescriptorPlan DescriptorPlan,
    DBuilderPluginLoadPlan LoadPlan,
    IReadOnlyList<DBuilderPluginLifecyclePlan> LifecyclePlans,
    DBuilderPluginHostApiPlan HostApi,
    DBuilderPluginUiContributionPlan UiContributions,
    DBuilderPluginApiContributionPlan ApiContributions,
    DBuilderPluginResourceHandlerPlan ResourceHandlers);

public sealed record DBuilderPluginHostApiDescriptor(
    string Name,
    string Source,
    bool RequiresMap = false);

public sealed record DBuilderPluginHostApiService(
    string Name,
    string Source,
    bool RequiresMap,
    bool Available);

public sealed record DBuilderPluginHostApiPlan(
    IReadOnlyList<DBuilderPluginHostApiService> Services,
    IReadOnlyList<DBuilderPluginDiagnostic> Diagnostics);

public sealed record DBuilderPluginCallbackDescriptor(
    string Name,
    string Category,
    bool CanAbort = false);

public sealed record DBuilderPluginCallbackInvocation(
    string PluginName,
    string CallbackName,
    int Order,
    bool CanAbort);

public sealed record DBuilderPluginCallbackInvocationPlan(
    DBuilderPluginCallbackDescriptor? Callback,
    IReadOnlyList<DBuilderPluginCallbackInvocation> Invocations,
    IReadOnlyList<DBuilderPluginDiagnostic> Diagnostics);

public sealed record DBuilderPluginCallbackOutcome(
    string PluginName,
    bool Completed = true,
    bool Aborted = false,
    string? Error = null);

public sealed record DBuilderPluginCallbackExecutionResult(
    bool Completed,
    bool Aborted,
    IReadOnlyList<DBuilderPluginCallbackOutcome> Outcomes,
    IReadOnlyList<DBuilderPluginDiagnostic> Diagnostics);

public static class DBuilderPluginHostModel
{
    public static IReadOnlyList<DBuilderPluginCallbackDescriptor> UdbCallbackDescriptors { get; } = new[]
    {
        new DBuilderPluginCallbackDescriptor("OnInitialize", "Load"),
        new DBuilderPluginCallbackDescriptor("Dispose", "Load"),
        new DBuilderPluginCallbackDescriptor("OnMapOpenBegin", "Map"),
        new DBuilderPluginCallbackDescriptor("OnMapOpenEnd", "Map"),
        new DBuilderPluginCallbackDescriptor("OnMapNewBegin", "Map"),
        new DBuilderPluginCallbackDescriptor("OnMapNewEnd", "Map"),
        new DBuilderPluginCallbackDescriptor("OnMapCloseBegin", "Map"),
        new DBuilderPluginCallbackDescriptor("OnMapCloseEnd", "Map"),
        new DBuilderPluginCallbackDescriptor("OnMapSaveBegin", "Map"),
        new DBuilderPluginCallbackDescriptor("OnMapSaveEnd", "Map"),
        new DBuilderPluginCallbackDescriptor("OnMapSetChangeBegin", "Map"),
        new DBuilderPluginCallbackDescriptor("OnMapSetChangeEnd", "Map"),
        new DBuilderPluginCallbackDescriptor("OnMapReconfigure", "Configuration"),
        new DBuilderPluginCallbackDescriptor("OnProgramReconfigure", "Configuration"),
        new DBuilderPluginCallbackDescriptor("OnReloadResources", "Resources"),
        new DBuilderPluginCallbackDescriptor("OnMapNodesRebuilt", "Resources"),
        new DBuilderPluginCallbackDescriptor("OnModeChange", "EditMode", CanAbort: true),
        new DBuilderPluginCallbackDescriptor("OnEditEngage", "EditMode"),
        new DBuilderPluginCallbackDescriptor("OnEditDisengage", "EditMode"),
        new DBuilderPluginCallbackDescriptor("OnEditCancel", "EditMode"),
        new DBuilderPluginCallbackDescriptor("OnEditAccept", "EditMode"),
        new DBuilderPluginCallbackDescriptor("OnCopyBegin", "EditOperation", CanAbort: true),
        new DBuilderPluginCallbackDescriptor("OnCopyEnd", "EditOperation"),
        new DBuilderPluginCallbackDescriptor("OnPasteBegin", "EditOperation", CanAbort: true),
        new DBuilderPluginCallbackDescriptor("OnPasteEnd", "EditOperation"),
        new DBuilderPluginCallbackDescriptor("OnUndoBegin", "EditOperation", CanAbort: true),
        new DBuilderPluginCallbackDescriptor("OnUndoEnd", "EditOperation"),
        new DBuilderPluginCallbackDescriptor("OnRedoBegin", "EditOperation", CanAbort: true),
        new DBuilderPluginCallbackDescriptor("OnRedoEnd", "EditOperation"),
        new DBuilderPluginCallbackDescriptor("OnUndoCreated", "EditOperation"),
        new DBuilderPluginCallbackDescriptor("OnUndoWithdrawn", "EditOperation"),
        new DBuilderPluginCallbackDescriptor("OnShowPreferences", "Preferences"),
        new DBuilderPluginCallbackDescriptor("OnClosePreferences", "Preferences"),
        new DBuilderPluginCallbackDescriptor("OnActionBegin", "Action"),
        new DBuilderPluginCallbackDescriptor("OnActionEnd", "Action"),
        new DBuilderPluginCallbackDescriptor("OnEditMouseClick", "Input"),
        new DBuilderPluginCallbackDescriptor("OnEditMouseDoubleClick", "Input"),
        new DBuilderPluginCallbackDescriptor("OnEditMouseDown", "Input"),
        new DBuilderPluginCallbackDescriptor("OnEditMouseEnter", "Input"),
        new DBuilderPluginCallbackDescriptor("OnEditMouseLeave", "Input"),
        new DBuilderPluginCallbackDescriptor("OnEditMouseMove", "Input"),
        new DBuilderPluginCallbackDescriptor("OnEditMouseUp", "Input"),
        new DBuilderPluginCallbackDescriptor("OnEditKeyDown", "Input"),
        new DBuilderPluginCallbackDescriptor("OnEditKeyUp", "Input"),
        new DBuilderPluginCallbackDescriptor("OnEditMouseInput", "Input"),
        new DBuilderPluginCallbackDescriptor("OnEditRedrawDisplayBegin", "Rendering"),
        new DBuilderPluginCallbackDescriptor("OnEditRedrawDisplayEnd", "Rendering"),
        new DBuilderPluginCallbackDescriptor("OnPresentDisplayBegin", "Rendering"),
        new DBuilderPluginCallbackDescriptor("OnSectorCeilingSurfaceUpdate", "Rendering"),
        new DBuilderPluginCallbackDescriptor("OnSectorFloorSurfaceUpdate", "Rendering"),
        new DBuilderPluginCallbackDescriptor("OnHighlightSector", "Highlight"),
        new DBuilderPluginCallbackDescriptor("OnHighlightLinedef", "Highlight"),
        new DBuilderPluginCallbackDescriptor("OnHighlightThing", "Highlight"),
        new DBuilderPluginCallbackDescriptor("OnHighlightVertex", "Highlight"),
        new DBuilderPluginCallbackDescriptor("OnHighlightRefreshed", "Highlight"),
        new DBuilderPluginCallbackDescriptor("OnHighlightLost", "Highlight")
    };

    public static IReadOnlyList<DBuilderPluginHostApiDescriptor> UdbHostApiDescriptors { get; } = new[]
    {
        new DBuilderPluginHostApiDescriptor("General.Interface", "General.Interface"),
        new DBuilderPluginHostApiDescriptor("General.Actions", "General.Actions"),
        new DBuilderPluginHostApiDescriptor("General.Settings", "General.Settings"),
        new DBuilderPluginHostApiDescriptor("General.Colors", "General.Colors"),
        new DBuilderPluginHostApiDescriptor("General.Types", "General.Types"),
        new DBuilderPluginHostApiDescriptor("General.Editing", "General.Editing"),
        new DBuilderPluginHostApiDescriptor("General.Map", "General.Map", RequiresMap: true),
        new DBuilderPluginHostApiDescriptor("General.Map.Map", "MapManager.Map", RequiresMap: true),
        new DBuilderPluginHostApiDescriptor("General.Map.Data", "MapManager.Data", RequiresMap: true),
        new DBuilderPluginHostApiDescriptor("General.Map.Config", "MapManager.Config", RequiresMap: true),
        new DBuilderPluginHostApiDescriptor("General.Map.ThingsFilter", "MapManager.ThingsFilter", RequiresMap: true),
        new DBuilderPluginHostApiDescriptor("General.Map.UndoRedo", "MapManager.UndoRedo", RequiresMap: true),
        new DBuilderPluginHostApiDescriptor("General.Map.VisualCamera", "MapManager.VisualCamera", RequiresMap: true)
    };

    public static DBuilderPluginDescriptorPlan PlanDescriptors(
        IEnumerable<DBuilderPluginDescriptor> descriptors)
    {
        var result = new List<DBuilderPluginDescriptor>();
        var diagnostics = new List<DBuilderPluginDiagnostic>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (DBuilderPluginDescriptor descriptor in descriptors)
        {
            string name = descriptor.Name.Trim();
            string assemblyPath = descriptor.AssemblyPath.Trim();
            string label = name.Length == 0 ? "(unnamed plugin)" : name;
            if (name.Length == 0)
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    label,
                    "Plugin name is missing."));
                continue;
            }

            if (assemblyPath.Length == 0)
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    label,
                    "Plugin assembly path is missing."));
                continue;
            }

            if (!names.Add(name))
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Warning,
                    label,
                    $"Duplicate plugin {name} was ignored."));
                continue;
            }

            if (!descriptor.Enabled)
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Warning,
                    label,
                    $"Plugin {name} is disabled."));
                continue;
            }

            result.Add(descriptor with
            {
                Name = name,
                AssemblyPath = assemblyPath,
                Contributions = NormalizeContributions(descriptor.Contributions)
            });
        }

        return new DBuilderPluginDescriptorPlan(
            result
                .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            diagnostics);
    }

    public static DBuilderPluginHostPlan BuildHostPlan(
        IEnumerable<DBuilderPluginDescriptor> descriptors,
        DBuilderPluginLifecycleRequest request)
    {
        DBuilderPluginDescriptorPlan descriptorPlan = PlanDescriptors(descriptors);
        DBuilderPluginDescriptor[] normalizedDescriptors = descriptorPlan.Descriptors.ToArray();
        DBuilderPluginLoadPlan loadPlan = PlanLoadCandidates(descriptorPlan);
        var loadCandidateNames = loadPlan.Candidates
            .Select(candidate => candidate.PluginName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        DBuilderPluginDescriptor[] loadableDescriptors = normalizedDescriptors
            .Where(descriptor => loadCandidateNames.Contains(descriptor.Name))
            .ToArray();

        return new DBuilderPluginHostPlan(
            descriptorPlan,
            loadPlan,
            loadableDescriptors
                .Select(descriptor => PlanLifecycle(descriptor, request))
                .ToArray(),
            PlanHostApiServices(request.MapOpen),
            PlanUiContributions(loadableDescriptors),
            PlanApiContributions(loadableDescriptors),
            PlanResourceHandlers(loadableDescriptors));
    }

    public static DBuilderPluginHostApiPlan PlanHostApiServices(bool mapOpen)
    {
        var diagnostics = new List<DBuilderPluginDiagnostic>();
        DBuilderPluginHostApiService[] services = UdbHostApiDescriptors
            .Select(descriptor => new DBuilderPluginHostApiService(
                descriptor.Name,
                descriptor.Source,
                descriptor.RequiresMap,
                !descriptor.RequiresMap || mapOpen))
            .ToArray();

        if (!mapOpen && services.Any(service => service.RequiresMap))
        {
            diagnostics.Add(new DBuilderPluginDiagnostic(
                DBuilderPluginDiagnosticSeverity.Warning,
                "(host)",
                "Map-scoped plugin API services are unavailable until a map is open."));
        }

        return new DBuilderPluginHostApiPlan(services, diagnostics);
    }

    public static DBuilderPluginLoadPlan PlanLoadCandidates(DBuilderPluginDescriptorPlan descriptorPlan)
        => PlanLoadCandidates(descriptorPlan, Array.Empty<string>());

    public static DBuilderPluginLoadPlan PlanLoadCandidates(
        DBuilderPluginDescriptorPlan descriptorPlan,
        IEnumerable<string> loadOrderFilenames)
    {
        var candidates = new List<DBuilderPluginLoadCandidate>();
        var diagnostics = new List<DBuilderPluginDiagnostic>(descriptorPlan.Diagnostics);
        DBuilderPluginDescriptor[] descriptors = ApplyLoadOrder(
            descriptorPlan.Descriptors,
            loadOrderFilenames);

        foreach (DBuilderPluginDescriptor descriptor in descriptors)
        {
            if (!descriptor.AssemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    descriptor.Name,
                    $"Plugin {descriptor.Name} assembly path must point to a .dll file."));
                continue;
            }

            candidates.Add(new DBuilderPluginLoadCandidate(
                descriptor.Name,
                descriptor.AssemblyPath,
                candidates.Count,
                descriptor.RequiresMap));
        }

        return new DBuilderPluginLoadPlan(candidates, diagnostics);
    }

    public static DBuilderPluginAssemblyLoadPlan PlanAssemblyLoadAttempts(
        DBuilderPluginLoadPlan loadPlan,
        Func<string, bool> assemblyExists)
    {
        var attempts = new List<DBuilderPluginAssemblyLoadAttempt>();
        var diagnostics = new List<DBuilderPluginDiagnostic>(loadPlan.Diagnostics);

        foreach (DBuilderPluginLoadCandidate candidate in loadPlan.Candidates)
        {
            bool found = assemblyExists(candidate.AssemblyPath);
            attempts.Add(new DBuilderPluginAssemblyLoadAttempt(
                candidate.PluginName,
                candidate.AssemblyPath,
                candidate.Order,
                found));

            if (!found)
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    candidate.PluginName,
                    $"Plugin {candidate.PluginName} assembly was not found at {candidate.AssemblyPath}."));
            }
        }

        return new DBuilderPluginAssemblyLoadPlan(attempts, diagnostics);
    }

    public static DBuilderPluginTypeDiscoveryPlan PlanTypeDiscovery(
        DBuilderPluginAssemblyLoadPlan assemblyLoadPlan,
        Func<DBuilderPluginAssemblyLoadAttempt, string?> discoverPluginTypeName)
    {
        var discoveries = new List<DBuilderPluginTypeDiscovery>();
        var diagnostics = new List<DBuilderPluginDiagnostic>(assemblyLoadPlan.Diagnostics);

        foreach (DBuilderPluginAssemblyLoadAttempt attempt in assemblyLoadPlan.Attempts)
        {
            if (!attempt.AssemblyFound) continue;

            string typeName = discoverPluginTypeName(attempt)?.Trim() ?? "";
            if (typeName.Length == 0)
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    attempt.PluginName,
                    $"Plugin {attempt.PluginName} assembly does not expose a plugin type."));
                continue;
            }

            discoveries.Add(new DBuilderPluginTypeDiscovery(
                attempt.PluginName,
                attempt.AssemblyPath,
                attempt.Order,
                typeName));
        }

        return new DBuilderPluginTypeDiscoveryPlan(discoveries, diagnostics);
    }

    public static DBuilderPluginTypeDiscoveryPlan PlanReflectionTypeDiscovery(
        DBuilderPluginAssemblyLoadPlan assemblyLoadPlan)
        => PlanReflectionTypeDiscovery(assemblyLoadPlan, typeof(IDBuilderPlugin));

    public static DBuilderPluginTypeDiscoveryPlan PlanReflectionTypeDiscovery(
        DBuilderPluginAssemblyLoadPlan assemblyLoadPlan,
        Type pluginContractType)
    {
        var discoveries = new List<DBuilderPluginTypeDiscovery>();
        var diagnostics = new List<DBuilderPluginDiagnostic>(assemblyLoadPlan.Diagnostics);
        string contractName = pluginContractType.FullName ?? pluginContractType.Name;

        foreach (DBuilderPluginAssemblyLoadAttempt attempt in assemblyLoadPlan.Attempts)
        {
            if (!attempt.AssemblyFound) continue;

            Type[] assemblyTypes;
            try
            {
                assemblyTypes = Assembly.LoadFrom(attempt.AssemblyPath).GetTypes();
            }
            catch (Exception ex)
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    attempt.PluginName,
                    $"Plugin {attempt.PluginName} assembly could not be inspected: {ex.Message}"));
                continue;
            }

            Type[] pluginTypes = assemblyTypes
                .Where(type => type.IsClass
                    && type.IsPublic
                    && !type.IsAbstract
                    && pluginContractType.IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
            if (pluginTypes.Length == 0)
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    attempt.PluginName,
                    $"Plugin {attempt.PluginName} assembly does not expose a {contractName} type."));
                continue;
            }

            Type pluginType = pluginTypes[0];
            string pluginTypeName = pluginType.FullName ?? pluginType.Name;
            if (pluginTypes.Length > 1)
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Warning,
                    attempt.PluginName,
                    $"Plugin {attempt.PluginName} assembly exposes multiple {contractName} types; using {pluginTypeName}."));
            }

            discoveries.Add(new DBuilderPluginTypeDiscovery(
                attempt.PluginName,
                attempt.AssemblyPath,
                attempt.Order,
                pluginTypeName));
        }

        return new DBuilderPluginTypeDiscoveryPlan(discoveries, diagnostics);
    }

    public static DBuilderPluginActivationPlan PlanActivationAttempts(
        DBuilderPluginTypeDiscoveryPlan typeDiscoveryPlan,
        Func<DBuilderPluginTypeDiscovery, string?> activatePlugin)
    {
        var attempts = new List<DBuilderPluginActivationAttempt>();
        var diagnostics = new List<DBuilderPluginDiagnostic>(typeDiscoveryPlan.Diagnostics);

        foreach (DBuilderPluginTypeDiscovery discovery in typeDiscoveryPlan.Discoveries)
        {
            string error = activatePlugin(discovery)?.Trim() ?? "";
            bool activated = error.Length == 0;
            attempts.Add(new DBuilderPluginActivationAttempt(
                discovery.PluginName,
                discovery.AssemblyPath,
                discovery.PluginTypeName,
                discovery.Order,
                activated,
                activated ? null : error));

            if (!activated)
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    discovery.PluginName,
                    error));
            }
        }

        return new DBuilderPluginActivationPlan(attempts, diagnostics);
    }

    public static DBuilderPluginRuntimeInstancePlan ActivateReflectionPlugins(
        DBuilderPluginTypeDiscoveryPlan typeDiscoveryPlan)
    {
        var instances = new List<DBuilderPluginRuntimeInstance>();
        var diagnostics = new List<DBuilderPluginDiagnostic>(typeDiscoveryPlan.Diagnostics);

        foreach (DBuilderPluginTypeDiscovery discovery in typeDiscoveryPlan.Discoveries)
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(discovery.AssemblyPath);
                Type? pluginType = assembly.GetType(discovery.PluginTypeName, throwOnError: false);
                if (pluginType == null)
                {
                    diagnostics.Add(new DBuilderPluginDiagnostic(
                        DBuilderPluginDiagnosticSeverity.Error,
                        discovery.PluginName,
                        $"Plugin {discovery.PluginName} type {discovery.PluginTypeName} was not found."));
                    continue;
                }

                if (!typeof(IDBuilderPlugin).IsAssignableFrom(pluginType))
                {
                    diagnostics.Add(new DBuilderPluginDiagnostic(
                        DBuilderPluginDiagnosticSeverity.Error,
                        discovery.PluginName,
                        $"Plugin {discovery.PluginName} type {discovery.PluginTypeName} does not implement {typeof(IDBuilderPlugin).FullName}."));
                    continue;
                }

                if (Activator.CreateInstance(pluginType) is not IDBuilderPlugin instance)
                {
                    diagnostics.Add(new DBuilderPluginDiagnostic(
                        DBuilderPluginDiagnosticSeverity.Error,
                        discovery.PluginName,
                        $"Plugin {discovery.PluginName} type {discovery.PluginTypeName} could not be activated."));
                    continue;
                }

                instances.Add(new DBuilderPluginRuntimeInstance(
                    discovery.PluginName,
                    discovery.AssemblyPath,
                    discovery.PluginTypeName,
                    discovery.Order,
                    instance));
            }
            catch (Exception ex)
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    discovery.PluginName,
                    $"Plugin {discovery.PluginName} type {discovery.PluginTypeName} could not be activated: {ex.Message}"));
            }
        }

        return new DBuilderPluginRuntimeInstancePlan(instances, diagnostics);
    }

    public static DBuilderPluginCompatibilityPlan PlanReflectionPluginCompatibility(
        DBuilderPluginRuntimeInstancePlan instancePlan,
        int hostRevision)
    {
        var checks = new List<DBuilderPluginCompatibilityCheck>();
        var instances = new List<DBuilderPluginRuntimeInstance>();
        var diagnostics = new List<DBuilderPluginDiagnostic>(instancePlan.Diagnostics);

        foreach (DBuilderPluginRuntimeInstance runtimeInstance in instancePlan.Instances)
        {
            Type pluginType = runtimeInstance.Instance.GetType();
            int minimumRevision = ReadPluginIntProperty(pluginType, runtimeInstance.Instance, "MinimumRevision") ?? 0;
            bool strictRevisionMatching = ReadPluginBoolProperty(
                pluginType,
                runtimeInstance.Instance,
                "StrictRevisionMatching") ?? false;
            string? error = null;

            if (strictRevisionMatching && minimumRevision != hostRevision)
            {
                error = $"Plugin {runtimeInstance.PluginName} revision {minimumRevision} must match host revision {hostRevision}.";
            }
            else if (hostRevision != 0 && minimumRevision > hostRevision)
            {
                error = $"Plugin {runtimeInstance.PluginName} requires host revision {minimumRevision} or newer; host revision is {hostRevision}.";
            }

            bool compatible = error == null;
            checks.Add(new DBuilderPluginCompatibilityCheck(
                runtimeInstance.PluginName,
                runtimeInstance.AssemblyPath,
                runtimeInstance.PluginTypeName,
                runtimeInstance.Order,
                minimumRevision,
                strictRevisionMatching,
                compatible,
                error));

            if (compatible)
            {
                instances.Add(runtimeInstance);
            }
            else
            {
                string compatibilityError = error ?? "Plugin compatibility check failed.";
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    runtimeInstance.PluginName,
                    compatibilityError));
            }
        }

        return new DBuilderPluginCompatibilityPlan(checks, instances, diagnostics);
    }

    public static DBuilderPluginShutdownPlan PlanShutdownAttempts(
        DBuilderPluginActivationPlan activationPlan,
        Func<DBuilderPluginActivationAttempt, string?> disposePlugin)
    {
        var attempts = new List<DBuilderPluginShutdownAttempt>();
        var diagnostics = new List<DBuilderPluginDiagnostic>(activationPlan.Diagnostics);

        foreach (DBuilderPluginActivationAttempt activation in activationPlan.Attempts
                     .Where(attempt => attempt.Activated)
                     .OrderByDescending(attempt => attempt.Order))
        {
            string error = disposePlugin(activation)?.Trim() ?? "";
            bool disposed = error.Length == 0;
            attempts.Add(new DBuilderPluginShutdownAttempt(
                activation.PluginName,
                activation.AssemblyPath,
                activation.PluginTypeName,
                activation.Order,
                disposed,
                disposed ? null : error));

            if (!disposed)
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    activation.PluginName,
                    error));
            }
        }

        return new DBuilderPluginShutdownPlan(attempts, diagnostics);
    }

    public static DBuilderPluginRuntimePlan BuildRuntimePlan(
        IEnumerable<DBuilderPluginDescriptor> descriptors,
        DBuilderPluginLifecycleRequest request,
        Func<string, bool> assemblyExists)
        => BuildRuntimePlan(
            descriptors,
            request,
            assemblyExists,
            attempt => attempt.PluginName,
            _ => null);

    public static DBuilderPluginRuntimePlan BuildRuntimePlan(
        IEnumerable<DBuilderPluginDescriptor> descriptors,
        DBuilderPluginLifecycleRequest request,
        Func<string, bool> assemblyExists,
        Func<DBuilderPluginAssemblyLoadAttempt, string?> discoverPluginTypeName)
        => BuildRuntimePlan(
            descriptors,
            request,
            assemblyExists,
            discoverPluginTypeName,
            _ => null);

    public static DBuilderPluginRuntimePlan BuildRuntimePlan(
        IEnumerable<DBuilderPluginDescriptor> descriptors,
        DBuilderPluginLifecycleRequest request,
        Func<string, bool> assemblyExists,
        Func<DBuilderPluginAssemblyLoadAttempt, string?> discoverPluginTypeName,
        Func<DBuilderPluginTypeDiscovery, string?> activatePlugin)
    {
        DBuilderPluginDescriptor[] descriptorRows = descriptors.ToArray();
        DBuilderPluginHostPlan hostPlan = BuildHostPlan(descriptorRows, request);
        DBuilderPluginAssemblyLoadPlan assemblyLoadPlan = PlanAssemblyLoadAttempts(
            hostPlan.LoadPlan,
            assemblyExists);
        DBuilderPluginTypeDiscoveryPlan typeDiscoveryPlan = PlanTypeDiscovery(
            assemblyLoadPlan,
            discoverPluginTypeName);
        DBuilderPluginActivationPlan activationPlan = PlanActivationAttempts(
            typeDiscoveryPlan,
            activatePlugin);
        var activatedPlugins = activationPlan.Attempts
            .Where(attempt => attempt.Activated)
            .Select(attempt => attempt.PluginName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        DBuilderPluginHostPlan readyHostPlan = BuildHostPlan(
            descriptorRows.Where(descriptor => activatedPlugins.Contains(descriptor.Name.Trim())),
            request);

        return new DBuilderPluginRuntimePlan(
            hostPlan,
            assemblyLoadPlan,
            typeDiscoveryPlan,
            activationPlan,
            readyHostPlan);
    }

    public static DBuilderPluginReflectionRuntimePlan BuildReflectionRuntimePlan(
        IEnumerable<DBuilderPluginDescriptor> descriptors,
        DBuilderPluginLifecycleRequest request)
        => BuildReflectionRuntimePlan(descriptors, request, File.Exists, hostRevision: 0);

    public static DBuilderPluginReflectionRuntimePlan BuildReflectionRuntimePlan(
        IEnumerable<DBuilderPluginDescriptor> descriptors,
        DBuilderPluginLifecycleRequest request,
        Func<string, bool> assemblyExists)
        => BuildReflectionRuntimePlan(descriptors, request, assemblyExists, hostRevision: 0);

    public static DBuilderPluginReflectionRuntimePlan BuildReflectionRuntimePlan(
        IEnumerable<DBuilderPluginDescriptor> descriptors,
        DBuilderPluginLifecycleRequest request,
        Func<string, bool> assemblyExists,
        int hostRevision)
    {
        DBuilderPluginDescriptor[] descriptorRows = descriptors.ToArray();
        DBuilderPluginHostPlan hostPlan = BuildHostPlan(descriptorRows, request);
        DBuilderPluginAssemblyLoadPlan assemblyLoadPlan = PlanAssemblyLoadAttempts(
            hostPlan.LoadPlan,
            assemblyExists);
        DBuilderPluginTypeDiscoveryPlan typeDiscoveryPlan = PlanReflectionTypeDiscovery(assemblyLoadPlan);
        DBuilderPluginRuntimeInstancePlan instancePlan = ActivateReflectionPlugins(typeDiscoveryPlan);
        DBuilderPluginCompatibilityPlan compatibilityPlan = PlanReflectionPluginCompatibility(
            instancePlan,
            hostRevision);
        var activatedPlugins = compatibilityPlan.Instances
            .Select(instance => instance.PluginName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        DBuilderPluginHostPlan readyHostPlan = BuildHostPlan(
            descriptorRows.Where(descriptor => activatedPlugins.Contains(descriptor.Name.Trim())),
            request);

        return new DBuilderPluginReflectionRuntimePlan(
            hostPlan,
            assemblyLoadPlan,
            typeDiscoveryPlan,
            instancePlan,
            compatibilityPlan,
            readyHostPlan);
    }

    public static DBuilderPluginCallbackInvocationPlan PlanCallbackInvocations(
        DBuilderPluginHostPlan hostPlan,
        string callbackName)
    {
        string name = callbackName.Trim();
        DBuilderPluginCallbackDescriptor? callback = UdbCallbackDescriptors.FirstOrDefault(
            descriptor => string.Equals(descriptor.Name, name, StringComparison.Ordinal));
        if (callback == null)
        {
            return new DBuilderPluginCallbackInvocationPlan(
                null,
                Array.Empty<DBuilderPluginCallbackInvocation>(),
                new[]
                {
                    new DBuilderPluginDiagnostic(
                        DBuilderPluginDiagnosticSeverity.Error,
                        "(plugin host)",
                        $"Unknown plugin callback {name}.")
                });
        }

        DBuilderPluginCallbackInvocation[] invocations = hostPlan.LoadPlan.Candidates
            .Select(candidate => new DBuilderPluginCallbackInvocation(
                candidate.PluginName,
                callback.Name,
                candidate.Order,
                callback.CanAbort))
            .ToArray();

        return new DBuilderPluginCallbackInvocationPlan(
            callback,
            invocations,
            hostPlan.DescriptorPlan.Diagnostics);
    }

    public static DBuilderPluginCallbackExecutionResult PlanCallbackExecutionResult(
        DBuilderPluginCallbackInvocationPlan plan,
        IEnumerable<DBuilderPluginCallbackOutcome> outcomes)
    {
        var outcomeByPlugin = outcomes
            .GroupBy(outcome => outcome.PluginName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var normalizedOutcomes = new List<DBuilderPluginCallbackOutcome>();
        var diagnostics = new List<DBuilderPluginDiagnostic>(plan.Diagnostics);

        foreach (DBuilderPluginCallbackInvocation invocation in plan.Invocations)
        {
            if (!outcomeByPlugin.TryGetValue(invocation.PluginName, out DBuilderPluginCallbackOutcome? outcome))
            {
                outcome = new DBuilderPluginCallbackOutcome(invocation.PluginName);
            }

            if (!invocation.CanAbort && outcome.Aborted)
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Warning,
                    invocation.PluginName,
                    $"Plugin {invocation.PluginName} returned an abort for non-abortable callback {invocation.CallbackName}."));
                outcome = outcome with { Aborted = false };
            }

            if (!string.IsNullOrWhiteSpace(outcome.Error))
            {
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    invocation.PluginName,
                    outcome.Error.Trim()));
                outcome = outcome with { Completed = false, Error = outcome.Error.Trim() };
            }

            normalizedOutcomes.Add(outcome with { PluginName = invocation.PluginName });
        }

        return new DBuilderPluginCallbackExecutionResult(
            normalizedOutcomes.All(outcome => outcome.Completed),
            normalizedOutcomes.Any(outcome => outcome.Aborted),
            normalizedOutcomes,
            diagnostics);
    }

    public static DBuilderPluginCallbackExecutionResult ExecuteReflectionCallback(
        DBuilderPluginRuntimeInstancePlan instancePlan,
        string callbackName)
    {
        string name = callbackName.Trim();
        DBuilderPluginCallbackDescriptor? callback = UdbCallbackDescriptors.FirstOrDefault(
            descriptor => string.Equals(descriptor.Name, name, StringComparison.Ordinal));
        if (callback == null)
        {
            return new DBuilderPluginCallbackExecutionResult(
                Completed: false,
                Aborted: false,
                Array.Empty<DBuilderPluginCallbackOutcome>(),
                new[]
                {
                    new DBuilderPluginDiagnostic(
                        DBuilderPluginDiagnosticSeverity.Error,
                        "(plugin host)",
                        $"Unknown plugin callback {name}.")
                });
        }

        var diagnostics = new List<DBuilderPluginDiagnostic>(instancePlan.Diagnostics);
        var outcomes = new List<DBuilderPluginCallbackOutcome>();
        foreach (DBuilderPluginRuntimeInstance runtimeInstance in instancePlan.Instances.OrderBy(instance => instance.Order))
        {
            MethodInfo? method = runtimeInstance.Instance.GetType().GetMethod(
                callback.Name,
                BindingFlags.Instance | BindingFlags.Public);
            if (method == null)
            {
                outcomes.Add(new DBuilderPluginCallbackOutcome(runtimeInstance.PluginName));
                continue;
            }

            if (method.GetParameters().Length != 0)
            {
                string error = $"Plugin {runtimeInstance.PluginName} callback {callback.Name} must not declare parameters.";
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    runtimeInstance.PluginName,
                    error));
                outcomes.Add(new DBuilderPluginCallbackOutcome(runtimeInstance.PluginName, Completed: false, Error: error));
                continue;
            }

            try
            {
                object? result = method.Invoke(runtimeInstance.Instance, Array.Empty<object>());
                bool aborted = callback.CanAbort && result is bool abort && abort;
                outcomes.Add(new DBuilderPluginCallbackOutcome(runtimeInstance.PluginName, Aborted: aborted));
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                string error = ex.InnerException.Message.Trim();
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    runtimeInstance.PluginName,
                    error));
                outcomes.Add(new DBuilderPluginCallbackOutcome(runtimeInstance.PluginName, Completed: false, Error: error));
            }
            catch (Exception ex)
            {
                string error = ex.Message.Trim();
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    runtimeInstance.PluginName,
                    error));
                outcomes.Add(new DBuilderPluginCallbackOutcome(runtimeInstance.PluginName, Completed: false, Error: error));
            }
        }

        return new DBuilderPluginCallbackExecutionResult(
            outcomes.All(outcome => outcome.Completed),
            outcomes.Any(outcome => outcome.Aborted),
            outcomes,
            diagnostics);
    }

    public static DBuilderPluginShutdownPlan ExecuteReflectionShutdown(
        DBuilderPluginRuntimeInstancePlan instancePlan)
    {
        var attempts = new List<DBuilderPluginShutdownAttempt>();
        var diagnostics = new List<DBuilderPluginDiagnostic>(instancePlan.Diagnostics);

        foreach (DBuilderPluginRuntimeInstance runtimeInstance in instancePlan.Instances.OrderByDescending(instance => instance.Order))
        {
            MethodInfo? method = runtimeInstance.Instance.GetType().GetMethod(
                "Dispose",
                BindingFlags.Instance | BindingFlags.Public);
            if (method == null)
            {
                attempts.Add(new DBuilderPluginShutdownAttempt(
                    runtimeInstance.PluginName,
                    runtimeInstance.AssemblyPath,
                    runtimeInstance.PluginTypeName,
                    runtimeInstance.Order,
                    Disposed: true));
                continue;
            }

            if (method.GetParameters().Length != 0)
            {
                string error = $"Plugin {runtimeInstance.PluginName} Dispose callback must not declare parameters.";
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    runtimeInstance.PluginName,
                    error));
                attempts.Add(new DBuilderPluginShutdownAttempt(
                    runtimeInstance.PluginName,
                    runtimeInstance.AssemblyPath,
                    runtimeInstance.PluginTypeName,
                    runtimeInstance.Order,
                    Disposed: false,
                    error));
                continue;
            }

            try
            {
                method.Invoke(runtimeInstance.Instance, Array.Empty<object>());
                attempts.Add(new DBuilderPluginShutdownAttempt(
                    runtimeInstance.PluginName,
                    runtimeInstance.AssemblyPath,
                    runtimeInstance.PluginTypeName,
                    runtimeInstance.Order,
                    Disposed: true));
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                string error = ex.InnerException.Message.Trim();
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    runtimeInstance.PluginName,
                    error));
                attempts.Add(new DBuilderPluginShutdownAttempt(
                    runtimeInstance.PluginName,
                    runtimeInstance.AssemblyPath,
                    runtimeInstance.PluginTypeName,
                    runtimeInstance.Order,
                    Disposed: false,
                    error));
            }
            catch (Exception ex)
            {
                string error = ex.Message.Trim();
                diagnostics.Add(new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    runtimeInstance.PluginName,
                    error));
                attempts.Add(new DBuilderPluginShutdownAttempt(
                    runtimeInstance.PluginName,
                    runtimeInstance.AssemblyPath,
                    runtimeInstance.PluginTypeName,
                    runtimeInstance.Order,
                    Disposed: false,
                    error));
            }
        }

        return new DBuilderPluginShutdownPlan(attempts, diagnostics);
    }

    public static string? FindPluginResourceName(
        IEnumerable<string> resourceNames,
        string resourceName)
    {
        string suffix = "." + resourceName;
        return resourceNames.FirstOrDefault(
            candidate => candidate.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    public static Stream? OpenReflectionPluginResourceStream(
        DBuilderPluginRuntimeInstance runtimeInstance,
        string resourceName)
    {
        Assembly assembly = runtimeInstance.Instance.GetType().Assembly;
        string? resource = FindPluginResourceName(assembly.GetManifestResourceNames(), resourceName);
        return resource == null ? null : assembly.GetManifestResourceStream(resource);
    }

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

    public static DBuilderPluginUiContributionPlan PlanUiContributions(
        IEnumerable<DBuilderPluginDescriptor> descriptors)
    {
        var menus = new List<DBuilderPluginUiContribution>();
        var toolbars = new List<DBuilderPluginUiContribution>();
        var warnings = new List<string>();

        foreach (DBuilderPluginDescriptor descriptor in NormalizeDescriptors(descriptors))
        {
            DBuilderPluginLifecyclePlan lifecycle = PlanLifecycle(descriptor, new DBuilderPluginLifecycleRequest());
            if (lifecycle.Warnings.Count > 0)
            {
                warnings.AddRange(lifecycle.Warnings);
                continue;
            }

            foreach (DBuilderPluginContribution contribution in lifecycle.Descriptor.Contributions ?? Array.Empty<DBuilderPluginContribution>())
            {
                var uiContribution = new DBuilderPluginUiContribution(
                    lifecycle.Descriptor.Name,
                    contribution.Kind,
                    contribution.Id,
                    contribution.Title);

                if (contribution.Kind == DBuilderPluginContributionKind.Menu) menus.Add(uiContribution);
                if (contribution.Kind == DBuilderPluginContributionKind.Toolbar) toolbars.Add(uiContribution);
            }
        }

        return new DBuilderPluginUiContributionPlan(
            SortUiContributions(menus),
            SortUiContributions(toolbars),
            warnings);
    }

    public static DBuilderPluginResourceHandlerPlan PlanResourceHandlers(
        IEnumerable<DBuilderPluginDescriptor> descriptors)
    {
        var handlers = new List<DBuilderPluginResourceHandler>();
        var warnings = new List<string>();

        foreach (DBuilderPluginDescriptor descriptor in NormalizeDescriptors(descriptors))
        {
            DBuilderPluginLifecyclePlan lifecycle = PlanLifecycle(descriptor, new DBuilderPluginLifecycleRequest());
            if (lifecycle.Warnings.Count > 0)
            {
                warnings.AddRange(lifecycle.Warnings);
                continue;
            }

            foreach (DBuilderPluginContribution contribution in lifecycle.Descriptor.Contributions ?? Array.Empty<DBuilderPluginContribution>())
            {
                if (contribution.Kind != DBuilderPluginContributionKind.ResourceHandler) continue;

                handlers.Add(new DBuilderPluginResourceHandler(
                    lifecycle.Descriptor.Name,
                    contribution.Id,
                    contribution.Title));
            }
        }

        return new DBuilderPluginResourceHandlerPlan(
            handlers
                .OrderBy(handler => handler.PluginName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(handler => handler.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(handler => handler.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            warnings);
    }

    public static DBuilderPluginApiContributionPlan PlanApiContributions(
        IEnumerable<DBuilderPluginDescriptor> descriptors)
    {
        var actions = new List<DBuilderPluginApiContribution>();
        var editModes = new List<DBuilderPluginApiContribution>();
        var dockers = new List<DBuilderPluginApiContribution>();
        var warnings = new List<string>();

        foreach (DBuilderPluginDescriptor descriptor in NormalizeDescriptors(descriptors))
        {
            DBuilderPluginLifecyclePlan lifecycle = PlanLifecycle(descriptor, new DBuilderPluginLifecycleRequest());
            if (lifecycle.Warnings.Count > 0)
            {
                warnings.AddRange(lifecycle.Warnings);
                continue;
            }

            foreach (DBuilderPluginContribution contribution in lifecycle.Descriptor.Contributions ?? Array.Empty<DBuilderPluginContribution>())
            {
                var apiContribution = new DBuilderPluginApiContribution(
                    lifecycle.Descriptor.Name,
                    contribution.Kind,
                    contribution.Id,
                    contribution.Title);

                if (contribution.Kind == DBuilderPluginContributionKind.Action) actions.Add(apiContribution);
                if (contribution.Kind == DBuilderPluginContributionKind.EditMode) editModes.Add(apiContribution);
                if (contribution.Kind == DBuilderPluginContributionKind.Docker) dockers.Add(apiContribution);
            }
        }

        return new DBuilderPluginApiContributionPlan(
            SortApiContributions(actions),
            SortApiContributions(editModes),
            SortApiContributions(dockers),
            warnings);
    }

    public static Dictionary<string, Dictionary<string, object?>> NormalizeSettingsStore(
        IDictionary<string, Dictionary<string, object?>>? settings)
    {
        var result = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        if (settings == null || settings.Count == 0) return result;

        var pluginNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in settings)
        {
            string pluginName = plugin.Key.Trim();
            if (pluginName.Length == 0 || !pluginNames.Add(pluginName)) continue;

            result.Add(pluginName, NormalizeSettings(plugin.Value));
        }

        return result
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value,
                StringComparer.Ordinal);
    }

    public static DBuilderPluginSettingsSnapshot PlanSettings(
        DBuilderPluginDescriptor descriptor,
        IDictionary<string, Dictionary<string, object?>>? settings,
        IEnumerable<DBuilderPluginSettingDescriptor> settingDescriptors)
    {
        var warnings = new List<string>();
        string pluginName = descriptor.Name.Trim();
        if (pluginName.Length == 0) warnings.Add("Plugin name is missing.");
        if (!descriptor.Enabled && pluginName.Length > 0) warnings.Add($"Plugin {pluginName} is disabled.");
        if (warnings.Count > 0)
        {
            return new DBuilderPluginSettingsSnapshot(
                pluginName,
                new Dictionary<string, object?>(StringComparer.Ordinal),
                warnings);
        }

        Dictionary<string, Dictionary<string, object?>> normalizedStore = NormalizeSettingsStore(settings);
        string? persistedKey = normalizedStore.Keys.FirstOrDefault(
            key => string.Equals(key, pluginName, StringComparison.OrdinalIgnoreCase));
        Dictionary<string, object?> persisted = persistedKey != null
            && normalizedStore.TryGetValue(persistedKey, out var storedSettings)
            ? storedSettings
            : new Dictionary<string, object?>(StringComparer.Ordinal);

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DBuilderPluginSettingDescriptor setting in settingDescriptors)
        {
            string key = setting.Key.Trim();
            if (key.Length == 0 || !keys.Add(key)) continue;
            values[key] = persisted.TryGetValue(key, out object? persistedValue)
                ? persistedValue
                : setting.DefaultValue;
        }

        foreach (var setting in persisted)
        {
            if (keys.Add(setting.Key)) values[setting.Key] = setting.Value;
        }

        return new DBuilderPluginSettingsSnapshot(
            pluginName,
            values
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value,
                    StringComparer.Ordinal),
            warnings);
    }

    public static void WriteSettings(
        IDictionary<string, Dictionary<string, object?>> settings,
        DBuilderPluginSettingsSnapshot snapshot)
    {
        string pluginName = snapshot.PluginName.Trim();
        if (pluginName.Length == 0 || snapshot.Warnings.Count > 0) return;

        string? existingKey = settings.Keys.FirstOrDefault(
            key => string.Equals(key, pluginName, StringComparison.OrdinalIgnoreCase));
        if (existingKey != null) settings.Remove(existingKey);

        settings[pluginName] = NormalizeSettings(snapshot.Values);
    }

    private static int? ReadPluginIntProperty(Type pluginType, object instance, string propertyName)
    {
        PropertyInfo? property = pluginType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property == null || property.PropertyType != typeof(int)) return null;
        return (int?)property.GetValue(instance);
    }

    private static bool? ReadPluginBoolProperty(Type pluginType, object instance, string propertyName)
    {
        PropertyInfo? property = pluginType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property == null || property.PropertyType != typeof(bool)) return null;
        return (bool?)property.GetValue(instance);
    }

    private static DBuilderPluginDescriptor[] ApplyLoadOrder(
        IReadOnlyList<DBuilderPluginDescriptor> descriptors,
        IEnumerable<string> loadOrderFilenames)
    {
        var result = new List<DBuilderPluginDescriptor>();
        var usedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string loadOrderFilename in loadOrderFilenames)
        {
            string filename = loadOrderFilename.Trim();
            if (filename.Length == 0) continue;

            DBuilderPluginDescriptor? descriptor = descriptors.FirstOrDefault(candidate =>
                !usedPlugins.Contains(candidate.Name)
                && string.Equals(
                    Path.GetFileName(candidate.AssemblyPath),
                    filename,
                    StringComparison.OrdinalIgnoreCase));
            if (descriptor == null) continue;

            result.Add(descriptor);
            usedPlugins.Add(descriptor.Name);
        }

        result.AddRange(descriptors.Where(descriptor => !usedPlugins.Contains(descriptor.Name)));
        return result.ToArray();
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

    private static Dictionary<string, object?> NormalizeSettings(
        IEnumerable<KeyValuePair<string, object?>>? settings)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (settings == null) return result;

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in settings)
        {
            string key = setting.Key.Trim();
            if (key.Length == 0 || !keys.Add(key)) continue;
            result[key] = setting.Value;
        }

        return result
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value,
                StringComparer.Ordinal);
    }

    private static IReadOnlyList<DBuilderPluginUiContribution> SortUiContributions(
        IEnumerable<DBuilderPluginUiContribution> contributions)
        => contributions
            .OrderBy(contribution => contribution.PluginName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(contribution => contribution.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(contribution => contribution.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<DBuilderPluginApiContribution> SortApiContributions(
        IEnumerable<DBuilderPluginApiContribution> contributions)
        => contributions
            .OrderBy(contribution => contribution.PluginName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(contribution => contribution.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(contribution => contribution.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
