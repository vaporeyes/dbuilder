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

public enum DBuilderPluginDiagnosticSeverity
{
    Warning,
    Error
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
    IReadOnlyList<DBuilderPluginLifecyclePlan> LifecyclePlans,
    DBuilderPluginUiContributionPlan UiContributions,
    DBuilderPluginApiContributionPlan ApiContributions,
    DBuilderPluginResourceHandlerPlan ResourceHandlers);

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

        return new DBuilderPluginHostPlan(
            descriptorPlan,
            normalizedDescriptors
                .Select(descriptor => PlanLifecycle(descriptor, request))
                .ToArray(),
            PlanUiContributions(normalizedDescriptors),
            PlanApiContributions(normalizedDescriptors),
            PlanResourceHandlers(normalizedDescriptors));
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

        DBuilderPluginCallbackInvocation[] invocations = hostPlan.DescriptorPlan.Descriptors
            .Select((descriptor, index) => new DBuilderPluginCallbackInvocation(
                descriptor.Name,
                callback.Name,
                index,
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
