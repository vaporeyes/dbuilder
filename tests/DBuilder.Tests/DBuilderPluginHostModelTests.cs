// ABOUTME: Tests plugin descriptor normalization and lifecycle hook planning.
// ABOUTME: Covers UI-independent plugin host planning and reflection runtime execution helpers.

using DBuilder.IO;
using System.Reflection;

namespace DBuilder.Tests;

public sealed class DBuilderPluginHostModelTests
{
    private interface MissingPluginContract
    {
    }

    [Fact]
    public void UdbCallbackCatalogCoversCorePlugAndManagerSurface()
    {
        string[] names = DBuilderPluginHostModel.UdbCallbackDescriptors
            .Select(callback => callback.Name)
            .ToArray();

        Assert.Equal(new[]
        {
            "OnInitialize",
            "Dispose",
            "OnMapOpenBegin",
            "OnMapOpenEnd",
            "OnMapNewBegin",
            "OnMapNewEnd",
            "OnMapCloseBegin",
            "OnMapCloseEnd",
            "OnMapSaveBegin",
            "OnMapSaveEnd",
            "OnMapSetChangeBegin",
            "OnMapSetChangeEnd",
            "OnMapReconfigure",
            "OnProgramReconfigure",
            "OnReloadResources",
            "OnMapNodesRebuilt",
            "OnModeChange",
            "OnEditEngage",
            "OnEditDisengage",
            "OnEditCancel",
            "OnEditAccept",
            "OnCopyBegin",
            "OnCopyEnd",
            "OnPasteBegin",
            "OnPasteEnd",
            "OnUndoBegin",
            "OnUndoEnd",
            "OnRedoBegin",
            "OnRedoEnd",
            "OnUndoCreated",
            "OnUndoWithdrawn",
            "OnShowPreferences",
            "OnClosePreferences",
            "OnActionBegin",
            "OnActionEnd",
            "OnEditMouseClick",
            "OnEditMouseDoubleClick",
            "OnEditMouseDown",
            "OnEditMouseEnter",
            "OnEditMouseLeave",
            "OnEditMouseMove",
            "OnEditMouseUp",
            "OnEditKeyDown",
            "OnEditKeyUp",
            "OnEditMouseInput",
            "OnEditRedrawDisplayBegin",
            "OnEditRedrawDisplayEnd",
            "OnPresentDisplayBegin",
            "OnSectorCeilingSurfaceUpdate",
            "OnSectorFloorSurfaceUpdate",
            "OnHighlightSector",
            "OnHighlightLinedef",
            "OnHighlightThing",
            "OnHighlightVertex",
            "OnHighlightRefreshed",
            "OnHighlightLost"
        }, names);
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void UdbCallbackCatalogMarksAbortableCallbacks()
    {
        string[] abortable = DBuilderPluginHostModel.UdbCallbackDescriptors
            .Where(callback => callback.CanAbort)
            .Select(callback => callback.Name)
            .ToArray();

        Assert.Equal(new[]
        {
            "OnModeChange",
            "OnCopyBegin",
            "OnPasteBegin",
            "OnUndoBegin",
            "OnRedoBegin"
        }, abortable);
    }

    [Fact]
    public void UdbCallbackCatalogDeclaresKnownCallbackParameters()
    {
        var parameters = DBuilderPluginHostModel.UdbCallbackDescriptors
            .Where(callback => callback.Parameters?.Count > 0)
            .ToDictionary(
                callback => callback.Name,
                callback => callback.Parameters!.ToArray(),
                StringComparer.Ordinal);

        Assert.Equal(
            new[] { DBuilderPluginCallbackParameterKind.SavePurpose },
            parameters["OnMapSaveBegin"]);
        Assert.Equal(
            new[] { DBuilderPluginCallbackParameterKind.SavePurpose },
            parameters["OnMapSaveEnd"]);
        Assert.Equal(
            new[] { DBuilderPluginCallbackParameterKind.CurrentResult },
            parameters["OnCopyBegin"]);
        Assert.Equal(
            new[] { DBuilderPluginCallbackParameterKind.PasteOptions, DBuilderPluginCallbackParameterKind.CurrentResult },
            parameters["OnPasteBegin"]);
        Assert.Equal(
            new[] { DBuilderPluginCallbackParameterKind.PasteOptions },
            parameters["OnPasteEnd"]);
        Assert.Equal(
            new[] { DBuilderPluginCallbackParameterKind.CurrentResult },
            parameters["OnUndoBegin"]);
        Assert.Equal(
            new[] { DBuilderPluginCallbackParameterKind.CurrentResult },
            parameters["OnRedoBegin"]);
        Assert.Equal(7, parameters.Count);
    }

    [Fact]
    public void UdbCallbackCatalogHasTypedHelpersForNoArgumentRuntimeCallbacks()
    {
        HashSet<string> helperNames = typeof(DBuilderPluginHostModel)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name.StartsWith("ExecuteReflection", StringComparison.Ordinal))
            .Where(method => method.ReturnType == typeof(DBuilderPluginCallbackExecutionResult))
            .Where(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 1
                    && parameters[0].ParameterType == typeof(DBuilderPluginRuntimeInstancePlan);
            })
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        string[] missingHelpers = DBuilderPluginHostModel.UdbCallbackDescriptors
            .Where(callback => callback.Name != "Dispose")
            .Where(callback => !RequiresCallerArgument(callback))
            .Select(callback => "ExecuteReflection" + callback.Name[2..])
            .Where(helperName => !helperNames.Contains(helperName))
            .ToArray();

        Assert.Empty(missingHelpers);
    }

    [Fact]
    public void UdbCallbackCatalogGroupsCallbacksByArea()
    {
        var categories = DBuilderPluginHostModel.UdbCallbackDescriptors
            .GroupBy(callback => callback.Category)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(2, categories["Load"]);
        Assert.Equal(10, categories["Map"]);
        Assert.Equal(2, categories["Configuration"]);
        Assert.Equal(2, categories["Resources"]);
        Assert.Equal(5, categories["EditMode"]);
        Assert.Equal(10, categories["EditOperation"]);
        Assert.Equal(2, categories["Preferences"]);
        Assert.Equal(2, categories["Action"]);
        Assert.Equal(10, categories["Input"]);
        Assert.Equal(5, categories["Rendering"]);
        Assert.Equal(6, categories["Highlight"]);
    }

    [Fact]
    public void SavePurposeValuesMatchUdb()
    {
        Assert.Equal(new[]
        {
            SavePurpose.Normal,
            SavePurpose.AsNewFile,
            SavePurpose.IntoFile,
            SavePurpose.Testing,
            SavePurpose.Autosave
        }, Enum.GetValues<SavePurpose>());
        Assert.Equal(0, (int)SavePurpose.Normal);
        Assert.Equal(1, (int)SavePurpose.AsNewFile);
        Assert.Equal(2, (int)SavePurpose.IntoFile);
        Assert.Equal(3, (int)SavePurpose.Testing);
        Assert.Equal(4, (int)SavePurpose.Autosave);
    }

    [Fact]
    public void UdbHostApiCatalogCoversGeneralAndMapManagerSurface()
    {
        Assert.Equal(new[]
        {
            "General.Interface",
            "General.Actions",
            "General.Settings",
            "General.Colors",
            "General.Types",
            "General.Editing",
            "General.Map",
            "General.Map.Map",
            "General.Map.Data",
            "General.Map.Config",
            "General.Map.ThingsFilter",
            "General.Map.UndoRedo",
            "General.Map.VisualCamera"
        }, DBuilderPluginHostModel.UdbHostApiDescriptors.Select(service => service.Name).ToArray());
        Assert.Equal(
            DBuilderPluginHostModel.UdbHostApiDescriptors.Count,
            DBuilderPluginHostModel.UdbHostApiDescriptors.Select(service => service.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void PlanHostApiServicesMarksMapServicesUnavailableWithoutOpenMap()
    {
        DBuilderPluginHostApiPlan plan = DBuilderPluginHostModel.PlanHostApiServices(mapOpen: false);

        Assert.True(plan.Services.Where(service => !service.RequiresMap).All(service => service.Available));
        Assert.True(plan.Services.Where(service => service.RequiresMap).All(service => !service.Available));
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("(host)", diagnostic.PluginName);
        Assert.Equal("Map-scoped plugin API services are unavailable until a map is open.", diagnostic.Message);
    }

    [Fact]
    public void PlanHostApiServicesExposesMapServicesWhenMapIsOpen()
    {
        DBuilderPluginHostApiPlan plan = DBuilderPluginHostModel.PlanHostApiServices(mapOpen: true);

        Assert.Empty(plan.Diagnostics);
        Assert.All(plan.Services, service => Assert.True(service.Available));
        DBuilderPluginHostApiService mapData = Assert.Single(plan.Services, service => service.Name == "General.Map.Data");
        Assert.Equal("MapManager.Data", mapData.Source);
        Assert.True(mapData.RequiresMap);
    }

    [Fact]
    public void NormalizeDescriptorsKeepsFirstPluginByNameAndSortsByTitle()
    {
        var descriptors = DBuilderPluginHostModel.NormalizeDescriptors(new[]
        {
            new DBuilderPluginDescriptor("  TagRange  ", "/plugins/tagrange.dll"),
            new DBuilderPluginDescriptor("tagrange", "/plugins/duplicate.dll"),
            new DBuilderPluginDescriptor("CommentsPanel", " /plugins/comments.dll "),
            new DBuilderPluginDescriptor("", "/plugins/missing.dll"),
            new DBuilderPluginDescriptor("NoPath", "")
        });

        Assert.Collection(
            descriptors,
            descriptor =>
            {
                Assert.Equal("CommentsPanel", descriptor.Name);
                Assert.Equal("/plugins/comments.dll", descriptor.AssemblyPath);
            },
            descriptor =>
            {
                Assert.Equal("TagRange", descriptor.Name);
                Assert.Equal("/plugins/tagrange.dll", descriptor.AssemblyPath);
            });
    }

    [Fact]
    public void PlanDescriptorsReportsInvalidDuplicateAndDisabledPlugins()
    {
        DBuilderPluginDescriptorPlan plan = DBuilderPluginHostModel.PlanDescriptors(new[]
        {
            new DBuilderPluginDescriptor("", "/plugins/missing-name.dll"),
            new DBuilderPluginDescriptor("NoPath", ""),
            new DBuilderPluginDescriptor("  TagRange  ", " /plugins/tagrange.dll "),
            new DBuilderPluginDescriptor("tagrange", "/plugins/duplicate.dll"),
            new DBuilderPluginDescriptor("Disabled", "/plugins/disabled.dll", Enabled: false)
        });

        DBuilderPluginDescriptor descriptor = Assert.Single(plan.Descriptors);
        Assert.Equal("TagRange", descriptor.Name);
        Assert.Equal("/plugins/tagrange.dll", descriptor.AssemblyPath);
        Assert.Collection(
            plan.Diagnostics,
            diagnostic =>
            {
                Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
                Assert.Equal("(unnamed plugin)", diagnostic.PluginName);
                Assert.Equal("Plugin name is missing.", diagnostic.Message);
            },
            diagnostic =>
            {
                Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
                Assert.Equal("NoPath", diagnostic.PluginName);
                Assert.Equal("Plugin assembly path is missing.", diagnostic.Message);
            },
            diagnostic =>
            {
                Assert.Equal(DBuilderPluginDiagnosticSeverity.Warning, diagnostic.Severity);
                Assert.Equal("tagrange", diagnostic.PluginName);
                Assert.Equal("Duplicate plugin tagrange was ignored.", diagnostic.Message);
            },
            diagnostic =>
            {
                Assert.Equal(DBuilderPluginDiagnosticSeverity.Warning, diagnostic.Severity);
                Assert.Equal("Disabled", diagnostic.PluginName);
                Assert.Equal("Plugin Disabled is disabled.", diagnostic.Message);
            });
    }

    [Fact]
    public void PlanDescriptorsNormalizesContributionRows()
    {
        DBuilderPluginDescriptorPlan plan = DBuilderPluginHostModel.PlanDescriptors(new[]
        {
            new DBuilderPluginDescriptor(
                "CommentsPanel",
                "/plugins/comments.dll",
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, " comments.open ", " Open Comments "),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "comments.open", "Duplicate"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "", "Missing Id"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "comments.empty", "")
                })
        });

        Assert.Empty(plan.Diagnostics);
        DBuilderPluginContribution contribution = Assert.Single(plan.Descriptors.Single().Contributions!);
        Assert.Equal(DBuilderPluginContributionKind.Menu, contribution.Kind);
        Assert.Equal("comments.open", contribution.Id);
        Assert.Equal("Open Comments", contribution.Title);
    }

    [Fact]
    public void PlanLoadCandidatesUsesNormalizedPluginOrder()
    {
        DBuilderPluginDescriptorPlan descriptorPlan = DBuilderPluginHostModel.PlanDescriptors(new[]
        {
            new DBuilderPluginDescriptor("TagRange", " /plugins/tagrange.dll "),
            new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll", RequiresMap: true),
            new DBuilderPluginDescriptor("Disabled", "/plugins/disabled.dll", Enabled: false)
        });

        DBuilderPluginLoadPlan plan = DBuilderPluginHostModel.PlanLoadCandidates(descriptorPlan);

        Assert.Collection(
            plan.Candidates,
            candidate =>
            {
                Assert.Equal("BuilderModes", candidate.PluginName);
                Assert.Equal("/plugins/buildermodes.dll", candidate.AssemblyPath);
                Assert.Equal(0, candidate.Order);
                Assert.True(candidate.RequiresMap);
            },
            candidate =>
            {
                Assert.Equal("TagRange", candidate.PluginName);
                Assert.Equal("/plugins/tagrange.dll", candidate.AssemblyPath);
                Assert.Equal(1, candidate.Order);
                Assert.False(candidate.RequiresMap);
            });
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal("Plugin Disabled is disabled.", diagnostic.Message);
    }

    [Fact]
    public void PlanLoadCandidatesAppliesUdbLoadOrderFilenamesBeforeRemainingDlls()
    {
        DBuilderPluginDescriptorPlan descriptorPlan = DBuilderPluginHostModel.PlanDescriptors(new[]
        {
            new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
            new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll"),
            new DBuilderPluginDescriptor("CommentsPanel", "/plugins/comments.dll")
        });

        DBuilderPluginLoadPlan plan = DBuilderPluginHostModel.PlanLoadCandidates(
            descriptorPlan,
            new[] { "TAGRANGE.DLL", "missing.dll", "tagrange.dll" });

        Assert.Collection(
            plan.Candidates,
            candidate =>
            {
                Assert.Equal("TagRange", candidate.PluginName);
                Assert.Equal(0, candidate.Order);
            },
            candidate =>
            {
                Assert.Equal("BuilderModes", candidate.PluginName);
                Assert.Equal(1, candidate.Order);
            },
            candidate =>
            {
                Assert.Equal("CommentsPanel", candidate.PluginName);
                Assert.Equal(2, candidate.Order);
            });
    }

    [Fact]
    public void PlanLoadCandidatesMatchesLoadOrderByFilenameOnly()
    {
        DBuilderPluginDescriptorPlan descriptorPlan = DBuilderPluginHostModel.PlanDescriptors(new[]
        {
            new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll"),
            new DBuilderPluginDescriptor("TagRange", "/other/tagrange.dll")
        });

        DBuilderPluginLoadPlan plan = DBuilderPluginHostModel.PlanLoadCandidates(
            descriptorPlan,
            new[] { "tagrange.dll" });

        Assert.Collection(
            plan.Candidates,
            candidate => Assert.Equal("TagRange", candidate.PluginName),
            candidate => Assert.Equal("BuilderModes", candidate.PluginName));
    }

    [Fact]
    public void PlanLoadCandidatesRejectsNonDllAssemblyPaths()
    {
        DBuilderPluginDescriptorPlan descriptorPlan = DBuilderPluginHostModel.PlanDescriptors(new[]
        {
            new DBuilderPluginDescriptor("LooseScript", "/plugins/loose.txt"),
            new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
        });

        DBuilderPluginLoadPlan plan = DBuilderPluginHostModel.PlanLoadCandidates(descriptorPlan);

        DBuilderPluginLoadCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Equal("BuilderModes", candidate.PluginName);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("LooseScript", diagnostic.PluginName);
        Assert.Equal("Plugin LooseScript assembly path must point to a .dll file.", diagnostic.Message);
    }

    [Fact]
    public void PlanAssemblyLoadAttemptsProbesEachLoadCandidate()
    {
        DBuilderPluginLoadPlan loadPlan = DBuilderPluginHostModel.PlanLoadCandidates(
            DBuilderPluginHostModel.PlanDescriptors(new[]
            {
                new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
            }));

        DBuilderPluginAssemblyLoadPlan plan = DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
            loadPlan,
            path => path.EndsWith("buildermodes.dll", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("tagrange.dll", StringComparison.OrdinalIgnoreCase));

        Assert.Empty(plan.Diagnostics);
        Assert.Collection(
            plan.Attempts,
            attempt =>
            {
                Assert.Equal("BuilderModes", attempt.PluginName);
                Assert.Equal("/plugins/buildermodes.dll", attempt.AssemblyPath);
                Assert.Equal(0, attempt.Order);
                Assert.True(attempt.AssemblyFound);
            },
            attempt =>
            {
                Assert.Equal("TagRange", attempt.PluginName);
                Assert.Equal("/plugins/tagrange.dll", attempt.AssemblyPath);
                Assert.Equal(1, attempt.Order);
                Assert.True(attempt.AssemblyFound);
            });
    }

    [Fact]
    public void PlanAssemblyLoadAttemptsReportsMissingAssembliesWithoutDroppingOtherAttempts()
    {
        DBuilderPluginLoadPlan loadPlan = DBuilderPluginHostModel.PlanLoadCandidates(
            DBuilderPluginHostModel.PlanDescriptors(new[]
            {
                new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
            }));

        DBuilderPluginAssemblyLoadPlan plan = DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
            loadPlan,
            path => path.EndsWith("buildermodes.dll", StringComparison.OrdinalIgnoreCase));

        Assert.Collection(
            plan.Attempts,
            attempt => Assert.True(attempt.AssemblyFound),
            attempt =>
            {
                Assert.Equal("TagRange", attempt.PluginName);
                Assert.False(attempt.AssemblyFound);
            });
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("TagRange", diagnostic.PluginName);
        Assert.Equal("Plugin TagRange assembly was not found at /plugins/tagrange.dll.", diagnostic.Message);
    }

    [Fact]
    public void PlanAssemblyLoadAttemptsPreservesLoadPlanDiagnostics()
    {
        DBuilderPluginLoadPlan loadPlan = DBuilderPluginHostModel.PlanLoadCandidates(
            DBuilderPluginHostModel.PlanDescriptors(new[]
            {
                new DBuilderPluginDescriptor("LooseScript", "/plugins/loose.txt"),
                new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
            }));

        DBuilderPluginAssemblyLoadPlan plan = DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
            loadPlan,
            _ => true);

        DBuilderPluginAssemblyLoadAttempt attempt = Assert.Single(plan.Attempts);
        Assert.Equal("BuilderModes", attempt.PluginName);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal("LooseScript", diagnostic.PluginName);
        Assert.Equal("Plugin LooseScript assembly path must point to a .dll file.", diagnostic.Message);
    }

    [Fact]
    public void PlanTypeDiscoveryRecordsDiscoveredPluginTypesInLoadOrder()
    {
        DBuilderPluginAssemblyLoadPlan assemblyLoadPlan = DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
            DBuilderPluginHostModel.PlanLoadCandidates(
                DBuilderPluginHostModel.PlanDescriptors(new[]
                {
                    new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                    new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
                })),
            _ => true);

        DBuilderPluginTypeDiscoveryPlan plan = DBuilderPluginHostModel.PlanTypeDiscovery(
            assemblyLoadPlan,
            attempt => attempt.PluginName == "BuilderModes"
                ? " BuilderModes.BuilderModesPlugin "
                : "TagRange.TagRangePlugin");

        Assert.Empty(plan.Diagnostics);
        Assert.Collection(
            plan.Discoveries,
            discovery =>
            {
                Assert.Equal("BuilderModes", discovery.PluginName);
                Assert.Equal("/plugins/buildermodes.dll", discovery.AssemblyPath);
                Assert.Equal(0, discovery.Order);
                Assert.Equal("BuilderModes.BuilderModesPlugin", discovery.PluginTypeName);
            },
            discovery =>
            {
                Assert.Equal("TagRange", discovery.PluginName);
                Assert.Equal("TagRange.TagRangePlugin", discovery.PluginTypeName);
                Assert.Equal(1, discovery.Order);
            });
    }

    [Fact]
    public void PlanTypeDiscoveryReportsFoundAssembliesWithoutPluginTypes()
    {
        DBuilderPluginAssemblyLoadPlan assemblyLoadPlan = DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
            DBuilderPluginHostModel.PlanLoadCandidates(
                DBuilderPluginHostModel.PlanDescriptors(new[]
                {
                    new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                    new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
                })),
            _ => true);

        DBuilderPluginTypeDiscoveryPlan plan = DBuilderPluginHostModel.PlanTypeDiscovery(
            assemblyLoadPlan,
            attempt => attempt.PluginName == "BuilderModes" ? "BuilderModes.BuilderModesPlugin" : "");

        DBuilderPluginTypeDiscovery discovery = Assert.Single(plan.Discoveries);
        Assert.Equal("BuilderModes", discovery.PluginName);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("TagRange", diagnostic.PluginName);
        Assert.Equal("Plugin TagRange assembly does not expose a plugin type.", diagnostic.Message);
    }

    [Fact]
    public void PlanTypeDiscoverySkipsMissingAssembliesAndPreservesDiagnostics()
    {
        DBuilderPluginAssemblyLoadPlan assemblyLoadPlan = DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
            DBuilderPluginHostModel.PlanLoadCandidates(
                DBuilderPluginHostModel.PlanDescriptors(new[]
                {
                    new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                    new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
                })),
            path => path.EndsWith("buildermodes.dll", StringComparison.OrdinalIgnoreCase));

        DBuilderPluginTypeDiscoveryPlan plan = DBuilderPluginHostModel.PlanTypeDiscovery(
            assemblyLoadPlan,
            _ => "BuilderModes.BuilderModesPlugin");

        DBuilderPluginTypeDiscovery discovery = Assert.Single(plan.Discoveries);
        Assert.Equal("BuilderModes", discovery.PluginName);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal("TagRange", diagnostic.PluginName);
        Assert.Equal("Plugin TagRange assembly was not found at /plugins/tagrange.dll.", diagnostic.Message);
    }

    [Fact]
    public void PlanReflectionTypeDiscoveryFindsDBuilderPluginContractTypesDeterministically()
    {
        string assemblyPath = typeof(ReflectionPluginHostTestPlugin).Assembly.Location;
        DBuilderPluginAssemblyLoadPlan assemblyLoadPlan = DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
            DBuilderPluginHostModel.PlanLoadCandidates(
                DBuilderPluginHostModel.PlanDescriptors(new[]
                {
                    new DBuilderPluginDescriptor("ReflectionTest", assemblyPath)
                })),
            _ => true);

        DBuilderPluginTypeDiscoveryPlan plan = DBuilderPluginHostModel.PlanReflectionTypeDiscovery(assemblyLoadPlan);

        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("ReflectionTest", diagnostic.PluginName);
        Assert.Equal(
            $"Plugin ReflectionTest assembly exposes multiple {typeof(IDBuilderPlugin).FullName} types; using {typeof(ReflectionAbortCallbackPlugin).FullName}.",
            diagnostic.Message);
        DBuilderPluginTypeDiscovery discovery = Assert.Single(plan.Discoveries);
        Assert.Equal("ReflectionTest", discovery.PluginName);
        Assert.Equal(assemblyPath, discovery.AssemblyPath);
        Assert.Equal(0, discovery.Order);
        Assert.Equal(typeof(ReflectionAbortCallbackPlugin).FullName, discovery.PluginTypeName);
    }

    [Fact]
    public void PlanReflectionTypeDiscoveryReportsAssembliesWithoutContractTypes()
    {
        string assemblyPath = typeof(DBuilderPluginHostModelTests).Assembly.Location;
        DBuilderPluginAssemblyLoadPlan assemblyLoadPlan = DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
            DBuilderPluginHostModel.PlanLoadCandidates(
                DBuilderPluginHostModel.PlanDescriptors(new[]
                {
                    new DBuilderPluginDescriptor("NoContract", assemblyPath)
                })),
            _ => true);

        DBuilderPluginTypeDiscoveryPlan plan = DBuilderPluginHostModel.PlanReflectionTypeDiscovery(
            assemblyLoadPlan,
            typeof(MissingPluginContract));

        Assert.Empty(plan.Discoveries);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("NoContract", diagnostic.PluginName);
        Assert.Equal(
            $"Plugin NoContract assembly does not expose a {typeof(MissingPluginContract).FullName} type.",
            diagnostic.Message);
    }

    [Fact]
    public void PlanReflectionTypeDiscoveryReportsInspectionErrors()
    {
        string assemblyPath = Path.Combine(Path.GetTempPath(), "dbuilder-broken-plugin-" + Guid.NewGuid() + ".dll");
        File.WriteAllText(assemblyPath, "not an assembly");
        try
        {
            DBuilderPluginAssemblyLoadPlan assemblyLoadPlan = DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
                DBuilderPluginHostModel.PlanLoadCandidates(
                    DBuilderPluginHostModel.PlanDescriptors(new[]
                    {
                        new DBuilderPluginDescriptor("Broken", assemblyPath)
                    })),
                _ => true);

            DBuilderPluginTypeDiscoveryPlan plan = DBuilderPluginHostModel.PlanReflectionTypeDiscovery(assemblyLoadPlan);

            Assert.Empty(plan.Discoveries);
            DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
            Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("Broken", diagnostic.PluginName);
            Assert.StartsWith("Plugin Broken assembly could not be inspected:", diagnostic.Message);
        }
        finally
        {
            File.Delete(assemblyPath);
        }
    }

    [Fact]
    public void PlanActivationAttemptsRecordsSuccessfulPluginActivation()
    {
        DBuilderPluginTypeDiscoveryPlan typeDiscoveryPlan = DBuilderPluginHostModel.PlanTypeDiscovery(
            DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
                DBuilderPluginHostModel.PlanLoadCandidates(
                    DBuilderPluginHostModel.PlanDescriptors(new[]
                    {
                        new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
                    })),
                _ => true),
            _ => "BuilderModes.BuilderModesPlugin");

        DBuilderPluginActivationPlan plan = DBuilderPluginHostModel.PlanActivationAttempts(
            typeDiscoveryPlan,
            _ => null);

        Assert.Empty(plan.Diagnostics);
        DBuilderPluginActivationAttempt attempt = Assert.Single(plan.Attempts);
        Assert.Equal("BuilderModes", attempt.PluginName);
        Assert.Equal("/plugins/buildermodes.dll", attempt.AssemblyPath);
        Assert.Equal("BuilderModes.BuilderModesPlugin", attempt.PluginTypeName);
        Assert.Equal(0, attempt.Order);
        Assert.True(attempt.Activated);
        Assert.Null(attempt.Error);
    }

    [Fact]
    public void PlanActivationAttemptsReportsPluginActivationErrors()
    {
        DBuilderPluginTypeDiscoveryPlan typeDiscoveryPlan = DBuilderPluginHostModel.PlanTypeDiscovery(
            DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
                DBuilderPluginHostModel.PlanLoadCandidates(
                    DBuilderPluginHostModel.PlanDescriptors(new[]
                    {
                        new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                        new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
                    })),
                _ => true),
            attempt => attempt.PluginName + ".Plugin");

        DBuilderPluginActivationPlan plan = DBuilderPluginHostModel.PlanActivationAttempts(
            typeDiscoveryPlan,
            discovery => discovery.PluginName == "TagRange" ? " constructor failed " : null);

        Assert.Collection(
            plan.Attempts,
            attempt =>
            {
                Assert.Equal("BuilderModes", attempt.PluginName);
                Assert.True(attempt.Activated);
            },
            attempt =>
            {
                Assert.Equal("TagRange", attempt.PluginName);
                Assert.False(attempt.Activated);
                Assert.Equal("constructor failed", attempt.Error);
            });
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("TagRange", diagnostic.PluginName);
        Assert.Equal("constructor failed", diagnostic.Message);
    }

    [Fact]
    public void PlanActivationAttemptsPreservesTypeDiscoveryDiagnostics()
    {
        DBuilderPluginTypeDiscoveryPlan typeDiscoveryPlan = DBuilderPluginHostModel.PlanTypeDiscovery(
            DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
                DBuilderPluginHostModel.PlanLoadCandidates(
                    DBuilderPluginHostModel.PlanDescriptors(new[]
                    {
                        new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                        new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
                    })),
                _ => true),
            attempt => attempt.PluginName == "BuilderModes" ? "BuilderModes.BuilderModesPlugin" : null);

        DBuilderPluginActivationPlan plan = DBuilderPluginHostModel.PlanActivationAttempts(
            typeDiscoveryPlan,
            _ => null);

        DBuilderPluginActivationAttempt attempt = Assert.Single(plan.Attempts);
        Assert.Equal("BuilderModes", attempt.PluginName);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal("TagRange", diagnostic.PluginName);
        Assert.Equal("Plugin TagRange assembly does not expose a plugin type.", diagnostic.Message);
    }

    [Fact]
    public void ActivateReflectionPluginsCreatesPluginInstances()
    {
        string assemblyPath = typeof(ReflectionPluginHostTestPlugin).Assembly.Location;
        var typeDiscoveryPlan = new DBuilderPluginTypeDiscoveryPlan(
            new[]
            {
                new DBuilderPluginTypeDiscovery(
                    "ReflectionTest",
                    assemblyPath,
                    0,
                    typeof(ReflectionPluginHostTestPlugin).FullName!)
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginRuntimeInstancePlan plan = DBuilderPluginHostModel.ActivateReflectionPlugins(typeDiscoveryPlan);

        Assert.Empty(plan.Diagnostics);
        DBuilderPluginRuntimeInstance instance = Assert.Single(plan.Instances);
        Assert.Equal("ReflectionTest", instance.PluginName);
        Assert.Equal(assemblyPath, instance.AssemblyPath);
        Assert.Equal(typeof(ReflectionPluginHostTestPlugin).FullName, instance.PluginTypeName);
        Assert.Equal(0, instance.Order);
        Assert.IsType<ReflectionPluginHostTestPlugin>(instance.Instance);
    }

    [Fact]
    public void ActivateReflectionPluginsPreservesTypeDiscoveryDiagnostics()
    {
        var typeDiscoveryPlan = new DBuilderPluginTypeDiscoveryPlan(
            Array.Empty<DBuilderPluginTypeDiscovery>(),
            new[]
            {
                new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    "NoContract",
                    "Plugin NoContract assembly does not expose a plugin type.")
            });

        DBuilderPluginRuntimeInstancePlan plan = DBuilderPluginHostModel.ActivateReflectionPlugins(typeDiscoveryPlan);

        Assert.Empty(plan.Instances);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal("NoContract", diagnostic.PluginName);
        Assert.Equal("Plugin NoContract assembly does not expose a plugin type.", diagnostic.Message);
    }

    [Fact]
    public void ActivateReflectionPluginsReportsMissingRuntimeTypes()
    {
        string assemblyPath = typeof(ReflectionPluginHostTestPlugin).Assembly.Location;
        var typeDiscoveryPlan = new DBuilderPluginTypeDiscoveryPlan(
            new[]
            {
                new DBuilderPluginTypeDiscovery("Missing", assemblyPath, 0, "DBuilder.Tests.MissingPlugin")
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginRuntimeInstancePlan plan = DBuilderPluginHostModel.ActivateReflectionPlugins(typeDiscoveryPlan);

        Assert.Empty(plan.Instances);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Missing", diagnostic.PluginName);
        Assert.Equal("Plugin Missing type DBuilder.Tests.MissingPlugin was not found.", diagnostic.Message);
    }

    [Fact]
    public void ActivateReflectionPluginsReportsTypesWithoutPluginContract()
    {
        string assemblyPath = typeof(ReflectionNonPluginHostTestType).Assembly.Location;
        var typeDiscoveryPlan = new DBuilderPluginTypeDiscoveryPlan(
            new[]
            {
                new DBuilderPluginTypeDiscovery(
                    "NonPlugin",
                    assemblyPath,
                    0,
                    typeof(ReflectionNonPluginHostTestType).FullName!)
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginRuntimeInstancePlan plan = DBuilderPluginHostModel.ActivateReflectionPlugins(typeDiscoveryPlan);

        Assert.Empty(plan.Instances);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("NonPlugin", diagnostic.PluginName);
        Assert.Equal(
            $"Plugin NonPlugin type {typeof(ReflectionNonPluginHostTestType).FullName} does not implement {typeof(IDBuilderPlugin).FullName}.",
            diagnostic.Message);
    }

    [Fact]
    public void ActivateReflectionPluginsReportsConstructorErrorsWithoutDroppingOtherPlugins()
    {
        string assemblyPath = typeof(ReflectionPluginHostTestPlugin).Assembly.Location;
        var typeDiscoveryPlan = new DBuilderPluginTypeDiscoveryPlan(
            new[]
            {
                new DBuilderPluginTypeDiscovery(
                    "Broken",
                    assemblyPath,
                    0,
                    typeof(ReflectionBrokenPluginHostTestPlugin).FullName!),
                new DBuilderPluginTypeDiscovery(
                    "ReflectionTest",
                    assemblyPath,
                    1,
                    typeof(ReflectionPluginHostTestPlugin).FullName!)
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginRuntimeInstancePlan plan = DBuilderPluginHostModel.ActivateReflectionPlugins(typeDiscoveryPlan);

        DBuilderPluginRuntimeInstance instance = Assert.Single(plan.Instances);
        Assert.Equal("ReflectionTest", instance.PluginName);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Broken", diagnostic.PluginName);
        Assert.StartsWith(
            $"Plugin Broken type {typeof(ReflectionBrokenPluginHostTestPlugin).FullName} could not be activated:",
            diagnostic.Message);
    }

    [Fact]
    public void PlanReflectionPluginCompatibilityKeepsPluginsWithoutRevisionProperties()
    {
        DBuilderPluginRuntimeInstancePlan instancePlan = RuntimeInstancePlan(
            new ReflectionPluginHostTestPlugin(),
            "Plain");

        DBuilderPluginCompatibilityPlan plan = DBuilderPluginHostModel.PlanReflectionPluginCompatibility(
            instancePlan,
            hostRevision: 37);

        DBuilderPluginCompatibilityCheck check = Assert.Single(plan.Checks);
        Assert.Equal("Plain", check.PluginName);
        Assert.Equal(0, check.MinimumRevision);
        Assert.False(check.StrictRevisionMatching);
        Assert.True(check.Compatible);
        Assert.Single(plan.Instances);
        Assert.Empty(plan.Diagnostics);
    }

    [Fact]
    public void PlanReflectionPluginCompatibilityRejectsPluginsMadeForNewerRevisions()
    {
        DBuilderPluginRuntimeInstancePlan instancePlan = RuntimeInstancePlan(
            new ReflectionMinimumRevisionPlugin(),
            "Newer");

        DBuilderPluginCompatibilityPlan plan = DBuilderPluginHostModel.PlanReflectionPluginCompatibility(
            instancePlan,
            hostRevision: 41);

        Assert.Empty(plan.Instances);
        DBuilderPluginCompatibilityCheck check = Assert.Single(plan.Checks);
        Assert.Equal(42, check.MinimumRevision);
        Assert.False(check.Compatible);
        Assert.Equal("Plugin Newer requires host revision 42 or newer; host revision is 41.", check.Error);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Newer", diagnostic.PluginName);
        Assert.Equal(check.Error, diagnostic.Message);
    }

    [Fact]
    public void PlanReflectionPluginCompatibilityIgnoresMinimumRevisionWhenHostRevisionIsZero()
    {
        DBuilderPluginRuntimeInstancePlan instancePlan = RuntimeInstancePlan(
            new ReflectionMinimumRevisionPlugin(),
            "Newer");

        DBuilderPluginCompatibilityPlan plan = DBuilderPluginHostModel.PlanReflectionPluginCompatibility(
            instancePlan,
            hostRevision: 0);

        Assert.Single(plan.Instances);
        Assert.Empty(plan.Diagnostics);
        Assert.True(Assert.Single(plan.Checks).Compatible);
    }

    [Fact]
    public void PlanReflectionPluginCompatibilityRejectsStrictRevisionMismatches()
    {
        DBuilderPluginRuntimeInstancePlan instancePlan = RuntimeInstancePlan(
            new ReflectionStrictRevisionPlugin(),
            "Strict");

        DBuilderPluginCompatibilityPlan plan = DBuilderPluginHostModel.PlanReflectionPluginCompatibility(
            instancePlan,
            hostRevision: 41);

        Assert.Empty(plan.Instances);
        DBuilderPluginCompatibilityCheck check = Assert.Single(plan.Checks);
        Assert.Equal(42, check.MinimumRevision);
        Assert.True(check.StrictRevisionMatching);
        Assert.False(check.Compatible);
        Assert.Equal("Plugin Strict revision 42 must match host revision 41.", check.Error);
    }

    [Fact]
    public void FindReflectionPluginByAssemblyReturnsLoadedRuntimeInstance()
    {
        DBuilderPluginRuntimeInstancePlan instancePlan = RuntimeInstancePlan(
            new ReflectionPluginHostTestPlugin(),
            "Plain");

        DBuilderPluginRuntimeInstance? match = DBuilderPluginHostModel.FindReflectionPluginByAssembly(
            instancePlan.Instances,
            typeof(ReflectionPluginHostTestPlugin).Assembly);

        Assert.NotNull(match);
        Assert.Equal("Plain", match.PluginName);
    }

    [Fact]
    public void FindReflectionPluginByAssemblyRequiresExactAssemblyMatch()
    {
        DBuilderPluginRuntimeInstancePlan instancePlan = RuntimeInstancePlan(
            new ReflectionPluginHostTestPlugin(),
            "Plain");

        DBuilderPluginRuntimeInstance? match = DBuilderPluginHostModel.FindReflectionPluginByAssembly(
            instancePlan.Instances,
            typeof(string).Assembly);

        Assert.Null(match);
    }

    [Fact]
    public void ListReflectionPluginAssembliesReturnsLoadedAssembliesInRuntimeOrder()
    {
        DBuilderPluginRuntimeInstance[] instances =
        {
            RuntimeInstance(new ReflectionPluginHostTestPlugin(), "First", 0),
            RuntimeInstance(new ReflectionCallbackPlugin("Second"), "Second", 1)
        };

        IReadOnlyList<Assembly> assemblies = DBuilderPluginHostModel.ListReflectionPluginAssemblies(instances);

        Assert.Equal(new[]
        {
            typeof(ReflectionPluginHostTestPlugin).Assembly,
            typeof(ReflectionCallbackPlugin).Assembly
        }, assemblies);
    }

    [Fact]
    public void ListReflectionPluginAssembliesAllowsEmptyRuntimePlans()
    {
        IReadOnlyList<Assembly> assemblies = DBuilderPluginHostModel.ListReflectionPluginAssemblies(
            Array.Empty<DBuilderPluginRuntimeInstance>());

        Assert.Empty(assemblies);
    }

    [Fact]
    public void ResolveReflectionPluginDisplayNameReadsUdbNameProperty()
    {
        DBuilderPluginRuntimeInstance instance = RuntimeInstance(
            new ReflectionNamedPlugin(),
            "PluginFile",
            0);

        string displayName = DBuilderPluginHostModel.ResolveReflectionPluginDisplayName(instance);

        Assert.Equal("Friendly Plugin", displayName);
    }

    [Fact]
    public void ResolveReflectionPluginDisplayNameFallsBackToRuntimePluginName()
    {
        Assert.Equal(
            "PluginFile",
            DBuilderPluginHostModel.ResolveReflectionPluginDisplayName(
                RuntimeInstance(new ReflectionPluginHostTestPlugin(), "PluginFile", 0)));
        Assert.Equal(
            "PluginFile",
            DBuilderPluginHostModel.ResolveReflectionPluginDisplayName(
                RuntimeInstance(new ReflectionEmptyNamePlugin(), "PluginFile", 0)));
    }

    [Fact]
    public void BuildReflectionRuntimePlanKeepsActivatedInstancesInReadyHost()
    {
        string assemblyPath = typeof(ReflectionPluginHostTestPlugin).Assembly.Location;
        DBuilderPluginReflectionRuntimePlan plan = DBuilderPluginHostModel.BuildReflectionRuntimePlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "ReflectionTest",
                    assemblyPath,
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "reflection.action", "Reflection Action")
                    })
            },
            new DBuilderPluginLifecycleRequest(Engage: true),
            _ => true);

        DBuilderPluginRuntimeInstance instance = Assert.Single(plan.InstancePlan.Instances);
        Assert.Equal("ReflectionTest", instance.PluginName);
        DBuilderPluginCompatibilityCheck check = Assert.Single(plan.CompatibilityPlan.Checks);
        Assert.True(check.Compatible);
        DBuilderPluginDescriptor readyDescriptor = Assert.Single(plan.ReadyHostPlan.DescriptorPlan.Descriptors);
        Assert.Equal("ReflectionTest", readyDescriptor.Name);
        DBuilderPluginApiContribution action = Assert.Single(plan.ReadyHostPlan.ApiContributions.Actions);
        Assert.Equal("reflection.action", action.Id);
        Assert.Contains(
            plan.TypeDiscoveryPlan.Diagnostics,
            diagnostic => diagnostic.PluginName == "ReflectionTest"
                && diagnostic.Severity == DBuilderPluginDiagnosticSeverity.Warning
                && diagnostic.Message.Contains("exposes multiple", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildReflectionRuntimePlanKeepsIncompatiblePluginsOutOfReadyHost()
    {
        string assemblyPath = typeof(ReflectionMinimumRevisionPlugin).Assembly.Location;
        DBuilderPluginReflectionRuntimePlan plan = DBuilderPluginHostModel.BuildReflectionRuntimePlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "Newer",
                    assemblyPath,
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "newer.action", "Newer Action")
                    })
            },
            new DBuilderPluginLifecycleRequest(Engage: true),
            _ => true,
            hostRevision: 41);

        Assert.Single(plan.InstancePlan.Instances);
        Assert.Empty(plan.CompatibilityPlan.Instances);
        Assert.Empty(plan.ReadyHostPlan.DescriptorPlan.Descriptors);
        Assert.Empty(plan.ReadyHostPlan.ApiContributions.Actions);
        Assert.Contains(
            plan.CompatibilityPlan.Diagnostics,
            diagnostic => diagnostic.PluginName == "Newer"
                && diagnostic.Message == "Plugin Newer requires host revision 42 or newer; host revision is 41.");
    }

    [Fact]
    public void BuildReflectionRuntimePlanKeepsMissingAssembliesOutOfReadyHost()
    {
        string assemblyPath = typeof(ReflectionPluginHostTestPlugin).Assembly.Location;
        DBuilderPluginReflectionRuntimePlan plan = DBuilderPluginHostModel.BuildReflectionRuntimePlan(
            new[]
            {
                new DBuilderPluginDescriptor("Missing", "/plugins/missing.dll"),
                new DBuilderPluginDescriptor("ReflectionTest", assemblyPath)
            },
            new DBuilderPluginLifecycleRequest(),
            path => path == assemblyPath);

        Assert.Collection(
            plan.AssemblyLoadPlan.Attempts,
            attempt =>
            {
                Assert.Equal("Missing", attempt.PluginName);
                Assert.False(attempt.AssemblyFound);
            },
            attempt =>
            {
                Assert.Equal("ReflectionTest", attempt.PluginName);
                Assert.True(attempt.AssemblyFound);
            });
        Assert.Single(plan.InstancePlan.Instances);
        DBuilderPluginDescriptor readyDescriptor = Assert.Single(plan.ReadyHostPlan.DescriptorPlan.Descriptors);
        Assert.Equal("ReflectionTest", readyDescriptor.Name);
        Assert.Contains(
            plan.AssemblyLoadPlan.Diagnostics,
            diagnostic => diagnostic.PluginName == "Missing"
                && diagnostic.Message == "Plugin Missing assembly was not found at /plugins/missing.dll.");
    }

    [Fact]
    public void BuildReflectionRuntimePlanKeepsInspectionFailuresOutOfReadyHost()
    {
        string assemblyPath = Path.Combine(Path.GetTempPath(), "dbuilder-broken-runtime-plugin-" + Guid.NewGuid() + ".dll");
        File.WriteAllText(assemblyPath, "not an assembly");
        try
        {
            DBuilderPluginReflectionRuntimePlan plan = DBuilderPluginHostModel.BuildReflectionRuntimePlan(
                new[]
                {
                    new DBuilderPluginDescriptor("Broken", assemblyPath)
                },
                new DBuilderPluginLifecycleRequest(),
                _ => true);

            Assert.Empty(plan.InstancePlan.Instances);
            Assert.Empty(plan.ReadyHostPlan.DescriptorPlan.Descriptors);
            Assert.Contains(
                plan.TypeDiscoveryPlan.Diagnostics,
                diagnostic => diagnostic.PluginName == "Broken"
                    && diagnostic.Severity == DBuilderPluginDiagnosticSeverity.Error
                    && diagnostic.Message.Contains("could not be inspected", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(assemblyPath);
        }
    }

    [Fact]
    public void BuildReflectionRuntimePlanFeedsReadyInstancesToCallbackExecution()
    {
        string assemblyPath = typeof(ReflectionCallbackPlugin).Assembly.Location;
        DBuilderPluginReflectionRuntimePlan plan = DBuilderPluginHostModel.BuildReflectionRuntimePlan(
            new[]
            {
                new DBuilderPluginDescriptor("Callback", assemblyPath)
            },
            new DBuilderPluginLifecycleRequest(),
            _ => true);

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.ExecuteReflectionCallback(
            plan.InstancePlan,
            "OnCopyBegin");

        Assert.True(result.Completed);
        Assert.False(result.Aborted);
        DBuilderPluginCallbackOutcome outcome = Assert.Single(result.Outcomes);
        Assert.Equal("Callback", outcome.PluginName);
        Assert.False(outcome.Aborted);
    }

    [Fact]
    public void PlanShutdownAttemptsDisposesActivatedPluginsInReverseActivationOrder()
    {
        DBuilderPluginActivationPlan activationPlan = DBuilderPluginHostModel.PlanActivationAttempts(
            DBuilderPluginHostModel.PlanTypeDiscovery(
                DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
                    DBuilderPluginHostModel.PlanLoadCandidates(
                        DBuilderPluginHostModel.PlanDescriptors(new[]
                        {
                            new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                            new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
                        })),
                    _ => true),
                attempt => attempt.PluginName + ".Plugin"),
            _ => null);

        DBuilderPluginShutdownPlan plan = DBuilderPluginHostModel.PlanShutdownAttempts(
            activationPlan,
            _ => null);

        Assert.Empty(plan.Diagnostics);
        Assert.Collection(
            plan.Attempts,
            attempt =>
            {
                Assert.Equal("TagRange", attempt.PluginName);
                Assert.Equal("/plugins/tagrange.dll", attempt.AssemblyPath);
                Assert.Equal("TagRange.Plugin", attempt.PluginTypeName);
                Assert.Equal(1, attempt.Order);
                Assert.True(attempt.Disposed);
                Assert.Null(attempt.Error);
            },
            attempt =>
            {
                Assert.Equal("BuilderModes", attempt.PluginName);
                Assert.Equal(0, attempt.Order);
                Assert.True(attempt.Disposed);
            });
    }

    [Fact]
    public void PlanShutdownAttemptsSkipsActivationFailures()
    {
        DBuilderPluginActivationPlan activationPlan = DBuilderPluginHostModel.PlanActivationAttempts(
            DBuilderPluginHostModel.PlanTypeDiscovery(
                DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
                    DBuilderPluginHostModel.PlanLoadCandidates(
                        DBuilderPluginHostModel.PlanDescriptors(new[]
                        {
                            new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                            new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
                        })),
                    _ => true),
                attempt => attempt.PluginName + ".Plugin"),
            discovery => discovery.PluginName == "TagRange" ? "activation failed" : null);

        DBuilderPluginShutdownPlan plan = DBuilderPluginHostModel.PlanShutdownAttempts(
            activationPlan,
            _ => null);

        DBuilderPluginShutdownAttempt attempt = Assert.Single(plan.Attempts);
        Assert.Equal("BuilderModes", attempt.PluginName);
        Assert.Collection(
            plan.Diagnostics,
            diagnostic =>
            {
                Assert.Equal("TagRange", diagnostic.PluginName);
                Assert.Equal("activation failed", diagnostic.Message);
            });
    }

    [Fact]
    public void PlanShutdownAttemptsReportsDisposeErrorsWithoutDroppingOtherPlugins()
    {
        DBuilderPluginActivationPlan activationPlan = DBuilderPluginHostModel.PlanActivationAttempts(
            DBuilderPluginHostModel.PlanTypeDiscovery(
                DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
                    DBuilderPluginHostModel.PlanLoadCandidates(
                        DBuilderPluginHostModel.PlanDescriptors(new[]
                        {
                            new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                            new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
                        })),
                    _ => true),
                attempt => attempt.PluginName + ".Plugin"),
            _ => null);

        DBuilderPluginShutdownPlan plan = DBuilderPluginHostModel.PlanShutdownAttempts(
            activationPlan,
            activation => activation.PluginName == "TagRange" ? " dispose failed " : null);

        Assert.Collection(
            plan.Attempts,
            attempt =>
            {
                Assert.Equal("TagRange", attempt.PluginName);
                Assert.False(attempt.Disposed);
                Assert.Equal("dispose failed", attempt.Error);
            },
            attempt =>
            {
                Assert.Equal("BuilderModes", attempt.PluginName);
                Assert.True(attempt.Disposed);
            });
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("TagRange", diagnostic.PluginName);
        Assert.Equal("dispose failed", diagnostic.Message);
    }

    [Fact]
    public void PlanShutdownAttemptsPreservesActivationDiagnostics()
    {
        DBuilderPluginActivationPlan activationPlan = DBuilderPluginHostModel.PlanActivationAttempts(
            DBuilderPluginHostModel.PlanTypeDiscovery(
                DBuilderPluginHostModel.PlanAssemblyLoadAttempts(
                    DBuilderPluginHostModel.PlanLoadCandidates(
                        DBuilderPluginHostModel.PlanDescriptors(new[]
                        {
                            new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                            new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
                        })),
                    _ => true),
                attempt => attempt.PluginName == "BuilderModes" ? "BuilderModes.Plugin" : null),
            _ => null);

        DBuilderPluginShutdownPlan plan = DBuilderPluginHostModel.PlanShutdownAttempts(
            activationPlan,
            _ => null);

        DBuilderPluginShutdownAttempt attempt = Assert.Single(plan.Attempts);
        Assert.Equal("BuilderModes", attempt.PluginName);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal("TagRange", diagnostic.PluginName);
        Assert.Equal("Plugin TagRange assembly does not expose a plugin type.", diagnostic.Message);
    }

    [Fact]
    public void BuildRuntimePlanKeepsMissingAssembliesOutOfReadyHost()
    {
        DBuilderPluginRuntimePlan plan = DBuilderPluginHostModel.BuildRuntimePlan(
            new[]
            {
                new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                new DBuilderPluginDescriptor(
                    "BuilderModes",
                    "/plugins/buildermodes.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "builder.draw.action", "Draw action")
                    })
            },
            new DBuilderPluginLifecycleRequest(Engage: true),
            path => path.EndsWith("buildermodes.dll", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(2, plan.HostPlan.LoadPlan.Candidates.Count);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.AssemblyLoadPlan.Diagnostics);
        Assert.Equal("TagRange", diagnostic.PluginName);
        Assert.Equal("Plugin TagRange assembly was not found at /plugins/tagrange.dll.", diagnostic.Message);
        DBuilderPluginTypeDiscovery typeDiscovery = Assert.Single(plan.TypeDiscoveryPlan.Discoveries);
        Assert.Equal("BuilderModes", typeDiscovery.PluginName);
        DBuilderPluginActivationAttempt activation = Assert.Single(plan.ActivationPlan.Attempts);
        Assert.Equal("BuilderModes", activation.PluginName);
        Assert.True(activation.Activated);
        DBuilderPluginDescriptor readyDescriptor = Assert.Single(plan.ReadyHostPlan.DescriptorPlan.Descriptors);
        Assert.Equal("BuilderModes", readyDescriptor.Name);
        DBuilderPluginApiContribution action = Assert.Single(plan.ReadyHostPlan.ApiContributions.Actions);
        Assert.Equal("builder.draw.action", action.Id);

        DBuilderPluginCallbackInvocationPlan callbackPlan = DBuilderPluginHostModel.PlanCallbackInvocations(
            plan.ReadyHostPlan,
            "OnMapOpenBegin");

        DBuilderPluginCallbackInvocation invocation = Assert.Single(callbackPlan.Invocations);
        Assert.Equal("BuilderModes", invocation.PluginName);
    }

    [Fact]
    public void BuildRuntimePlanKeepsInvalidLoadCandidatesOutOfReadyHost()
    {
        DBuilderPluginRuntimePlan plan = DBuilderPluginHostModel.BuildRuntimePlan(
            new[]
            {
                new DBuilderPluginDescriptor("LooseScript", "/plugins/loose.txt"),
                new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
            },
            new DBuilderPluginLifecycleRequest(),
            _ => true);

        DBuilderPluginAssemblyLoadAttempt attempt = Assert.Single(plan.AssemblyLoadPlan.Attempts);
        Assert.Equal("BuilderModes", attempt.PluginName);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.AssemblyLoadPlan.Diagnostics);
        Assert.Equal("LooseScript", diagnostic.PluginName);
        Assert.Equal("Plugin LooseScript assembly path must point to a .dll file.", diagnostic.Message);
        DBuilderPluginDescriptor readyDescriptor = Assert.Single(plan.ReadyHostPlan.DescriptorPlan.Descriptors);
        Assert.Equal("BuilderModes", readyDescriptor.Name);
    }

    [Fact]
    public void BuildRuntimePlanKeepsAssembliesWithoutPluginTypesOutOfReadyHost()
    {
        DBuilderPluginRuntimePlan plan = DBuilderPluginHostModel.BuildRuntimePlan(
            new[]
            {
                new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                new DBuilderPluginDescriptor(
                    "BuilderModes",
                    "/plugins/buildermodes.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "builder.draw.action", "Draw action")
                    })
            },
            new DBuilderPluginLifecycleRequest(Engage: true),
            _ => true,
            attempt => attempt.PluginName == "BuilderModes" ? "BuilderModes.BuilderModesPlugin" : null);

        Assert.Equal(2, plan.AssemblyLoadPlan.Attempts.Count);
        DBuilderPluginTypeDiscovery typeDiscovery = Assert.Single(plan.TypeDiscoveryPlan.Discoveries);
        Assert.Equal("BuilderModes", typeDiscovery.PluginName);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.TypeDiscoveryPlan.Diagnostics);
        Assert.Equal("TagRange", diagnostic.PluginName);
        Assert.Equal("Plugin TagRange assembly does not expose a plugin type.", diagnostic.Message);
        DBuilderPluginDescriptor readyDescriptor = Assert.Single(plan.ReadyHostPlan.DescriptorPlan.Descriptors);
        Assert.Equal("BuilderModes", readyDescriptor.Name);
        DBuilderPluginApiContribution action = Assert.Single(plan.ReadyHostPlan.ApiContributions.Actions);
        Assert.Equal("builder.draw.action", action.Id);
    }

    [Fact]
    public void BuildRuntimePlanKeepsActivationFailuresOutOfReadyHost()
    {
        DBuilderPluginRuntimePlan plan = DBuilderPluginHostModel.BuildRuntimePlan(
            new[]
            {
                new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                new DBuilderPluginDescriptor(
                    "BuilderModes",
                    "/plugins/buildermodes.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "builder.draw.action", "Draw action")
                    })
            },
            new DBuilderPluginLifecycleRequest(Engage: true),
            _ => true,
            attempt => attempt.PluginName + ".Plugin",
            discovery => discovery.PluginName == "TagRange" ? "activation failed" : null);

        Assert.Equal(2, plan.TypeDiscoveryPlan.Discoveries.Count);
        Assert.Collection(
            plan.ActivationPlan.Attempts,
            attempt => Assert.True(attempt.Activated),
            attempt =>
            {
                Assert.Equal("TagRange", attempt.PluginName);
                Assert.False(attempt.Activated);
            });
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.ActivationPlan.Diagnostics);
        Assert.Equal("TagRange", diagnostic.PluginName);
        Assert.Equal("activation failed", diagnostic.Message);
        DBuilderPluginDescriptor readyDescriptor = Assert.Single(plan.ReadyHostPlan.DescriptorPlan.Descriptors);
        Assert.Equal("BuilderModes", readyDescriptor.Name);
        Assert.Single(plan.ReadyHostPlan.ApiContributions.Actions);
    }

    [Fact]
    public void BuildHostPlanAggregatesDescriptorsLifecycleAndContributionPlans()
    {
        DBuilderPluginHostPlan plan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "BuilderModes",
                    "/plugins/buildermodes.dll",
                    RequiresMap: true,
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "builder.draw.action", "Draw action"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "builder.draw.menu", "Draw menu"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Toolbar, "builder.draw.toolbar", "Draw toolbar"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.EditMode, "builder.draw.mode", "Draw mode"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Docker, "builder.tags.docker", "Tags docker"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "builder.resource", "Builder resources")
                    }),
                new DBuilderPluginDescriptor("Disabled", "/plugins/disabled.dll", Enabled: false),
                new DBuilderPluginDescriptor("NoPath", "")
            },
            new DBuilderPluginLifecycleRequest(
                MapOpen: true,
                MapClose: true,
                MapSave: true,
                MapReconfigure: true,
                ProgramReconfigure: true,
                ReloadResources: true,
                MapNodesRebuilt: true,
                Engage: true,
                Shutdown: true));

        DBuilderPluginDescriptor descriptor = Assert.Single(plan.DescriptorPlan.Descriptors);
        Assert.Equal("BuilderModes", descriptor.Name);
        Assert.Equal(2, plan.DescriptorPlan.Diagnostics.Count);
        DBuilderPluginLoadCandidate loadCandidate = Assert.Single(plan.LoadPlan.Candidates);
        Assert.Equal("BuilderModes", loadCandidate.PluginName);
        Assert.Equal(2, plan.LoadPlan.Diagnostics.Count);
        DBuilderPluginLifecyclePlan lifecycle = Assert.Single(plan.LifecyclePlans);
        Assert.Equal(new[]
        {
            DBuilderPluginLifecycleHook.Load,
            DBuilderPluginLifecycleHook.RegisterActions,
            DBuilderPluginLifecycleHook.RegisterHints,
            DBuilderPluginLifecycleHook.Initialize,
            DBuilderPluginLifecycleHook.RegisterUi,
            DBuilderPluginLifecycleHook.RegisterEditModes,
            DBuilderPluginLifecycleHook.RegisterDockers,
            DBuilderPluginLifecycleHook.RegisterResourceHandlers,
            DBuilderPluginLifecycleHook.MapOpened,
            DBuilderPluginLifecycleHook.MapClosed,
            DBuilderPluginLifecycleHook.MapSaved,
            DBuilderPluginLifecycleHook.MapReconfigured,
            DBuilderPluginLifecycleHook.ProgramReconfigured,
            DBuilderPluginLifecycleHook.ResourcesReloaded,
            DBuilderPluginLifecycleHook.MapNodesRebuilt,
            DBuilderPluginLifecycleHook.Engage,
            DBuilderPluginLifecycleHook.Dispose
        }, lifecycle.Hooks);
        Assert.Single(plan.UiContributions.Menus);
        Assert.Single(plan.UiContributions.Toolbars);
        Assert.Single(plan.ApiContributions.Actions);
        Assert.Single(plan.ApiContributions.EditModes);
        Assert.Single(plan.ApiContributions.Dockers);
        Assert.Single(plan.ResourceHandlers.Handlers);
        Assert.Empty(plan.HostApi.Diagnostics);
        Assert.All(plan.HostApi.Services, service => Assert.True(service.Available));
        Assert.Empty(plan.UiContributions.Warnings);
        Assert.Empty(plan.ApiContributions.Warnings);
        Assert.Empty(plan.ResourceHandlers.Warnings);
    }

    [Fact]
    public void BuildHostPlanKeepsInvalidLoadCandidatesOutOfRuntimePlans()
    {
        DBuilderPluginHostPlan plan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "LooseScript",
                    "/plugins/loose.txt",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "loose.action", "Loose action")
                    }),
                new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
            },
            new DBuilderPluginLifecycleRequest(Engage: true));

        DBuilderPluginLoadCandidate loadCandidate = Assert.Single(plan.LoadPlan.Candidates);
        Assert.Equal("BuilderModes", loadCandidate.PluginName);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.LoadPlan.Diagnostics);
        Assert.Equal("LooseScript", diagnostic.PluginName);
        Assert.Single(plan.LifecyclePlans);
        Assert.Equal("BuilderModes", plan.LifecyclePlans.Single().Descriptor.Name);
        Assert.Empty(plan.ApiContributions.Actions);

        DBuilderPluginCallbackInvocationPlan callbackPlan = DBuilderPluginHostModel.PlanCallbackInvocations(
            plan,
            "OnMapOpenBegin");

        DBuilderPluginCallbackInvocation invocation = Assert.Single(callbackPlan.Invocations);
        Assert.Equal("BuilderModes", invocation.PluginName);
    }

    [Fact]
    public void BuildHostPlanUsesNormalizedDescriptorRowsAcrossAllSubplans()
    {
        DBuilderPluginHostPlan plan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "  CommentsPanel  ",
                    " /plugins/comments.dll ",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, " comments.open ", " Open Comments "),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "comments.open", "Duplicate"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, " comments.action ", " Open Comments Action ")
                    })
            },
            new DBuilderPluginLifecycleRequest());

        Assert.Empty(plan.DescriptorPlan.Diagnostics);
        Assert.Equal("CommentsPanel", plan.DescriptorPlan.Descriptors.Single().Name);
        Assert.Equal("comments.open", plan.UiContributions.Menus.Single().Id);
        Assert.Equal("comments.action", plan.ApiContributions.Actions.Single().Id);
        Assert.Empty(plan.ResourceHandlers.Handlers);
    }

    [Fact]
    public void PlanCallbackInvocationsUsesNormalizedPluginOrder()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
                new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll"),
                new DBuilderPluginDescriptor("Disabled", "/plugins/disabled.dll", Enabled: false)
            },
            new DBuilderPluginLifecycleRequest());

        DBuilderPluginCallbackInvocationPlan plan = DBuilderPluginHostModel.PlanCallbackInvocations(
            hostPlan,
            "OnMapOpenBegin");

        Assert.NotNull(plan.Callback);
        Assert.Equal("OnMapOpenBegin", plan.Callback.Name);
        Assert.False(plan.Callback.CanAbort);
        Assert.Collection(
            plan.Invocations,
            invocation =>
            {
                Assert.Equal("BuilderModes", invocation.PluginName);
                Assert.Equal("OnMapOpenBegin", invocation.CallbackName);
                Assert.Equal(0, invocation.Order);
                Assert.False(invocation.CanAbort);
            },
            invocation =>
            {
                Assert.Equal("TagRange", invocation.PluginName);
                Assert.Equal("OnMapOpenBegin", invocation.CallbackName);
                Assert.Equal(1, invocation.Order);
                Assert.False(invocation.CanAbort);
            });
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal("Plugin Disabled is disabled.", diagnostic.Message);
    }

    [Fact]
    public void PlanCallbackInvocationsMarksAbortableCallbacksOnEachInvocation()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
            },
            new DBuilderPluginLifecycleRequest());

        DBuilderPluginCallbackInvocationPlan plan = DBuilderPluginHostModel.PlanCallbackInvocations(
            hostPlan,
            " OnPasteBegin ");

        DBuilderPluginCallbackInvocation invocation = Assert.Single(plan.Invocations);
        Assert.NotNull(plan.Callback);
        Assert.Equal("OnPasteBegin", plan.Callback.Name);
        Assert.True(plan.Callback.CanAbort);
        Assert.True(invocation.CanAbort);
    }

    [Fact]
    public void PlanCallbackInvocationsReportsUnknownCallbacks()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
            },
            new DBuilderPluginLifecycleRequest());

        DBuilderPluginCallbackInvocationPlan plan = DBuilderPluginHostModel.PlanCallbackInvocations(
            hostPlan,
            "OnMissingCallback");

        Assert.Null(plan.Callback);
        Assert.Empty(plan.Invocations);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("(plugin host)", diagnostic.PluginName);
        Assert.Equal("Unknown plugin callback OnMissingCallback.", diagnostic.Message);
    }

    [Fact]
    public void PlanCallbackExecutionResultDefaultsMissingOutcomesToSuccess()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
            },
            new DBuilderPluginLifecycleRequest());
        DBuilderPluginCallbackInvocationPlan invocationPlan = DBuilderPluginHostModel.PlanCallbackInvocations(
            hostPlan,
            "OnMapOpenEnd");

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.PlanCallbackExecutionResult(
            invocationPlan,
            Array.Empty<DBuilderPluginCallbackOutcome>());

        Assert.True(result.Completed);
        Assert.False(result.Aborted);
        DBuilderPluginCallbackOutcome outcome = Assert.Single(result.Outcomes);
        Assert.Equal("BuilderModes", outcome.PluginName);
        Assert.True(outcome.Completed);
        Assert.False(outcome.Aborted);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void PlanCallbackExecutionResultPreservesAbortForAbortableCallbacks()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
            },
            new DBuilderPluginLifecycleRequest());
        DBuilderPluginCallbackInvocationPlan invocationPlan = DBuilderPluginHostModel.PlanCallbackInvocations(
            hostPlan,
            "OnPasteBegin");

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.PlanCallbackExecutionResult(
            invocationPlan,
            new[] { new DBuilderPluginCallbackOutcome("BuilderModes", Aborted: true) });

        Assert.True(result.Completed);
        Assert.True(result.Aborted);
        Assert.True(result.Outcomes.Single().Aborted);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void PlanCallbackExecutionResultWarnsWhenNonAbortableCallbackAborts()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll")
            },
            new DBuilderPluginLifecycleRequest());
        DBuilderPluginCallbackInvocationPlan invocationPlan = DBuilderPluginHostModel.PlanCallbackInvocations(
            hostPlan,
            "OnMapOpenEnd");

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.PlanCallbackExecutionResult(
            invocationPlan,
            new[] { new DBuilderPluginCallbackOutcome("BuilderModes", Aborted: true) });

        Assert.True(result.Completed);
        Assert.False(result.Aborted);
        Assert.False(result.Outcomes.Single().Aborted);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("BuilderModes", diagnostic.PluginName);
        Assert.Equal(
            "Plugin BuilderModes returned an abort for non-abortable callback OnMapOpenEnd.",
            diagnostic.Message);
    }

    [Fact]
    public void PlanCallbackExecutionResultReportsPluginErrorsWithoutDroppingOtherOutcomes()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll"),
                new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll")
            },
            new DBuilderPluginLifecycleRequest());
        DBuilderPluginCallbackInvocationPlan invocationPlan = DBuilderPluginHostModel.PlanCallbackInvocations(
            hostPlan,
            "OnReloadResources");

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.PlanCallbackExecutionResult(
            invocationPlan,
            new[]
            {
                new DBuilderPluginCallbackOutcome("BuilderModes", Error: " reload failed "),
                new DBuilderPluginCallbackOutcome("TagRange")
            });

        Assert.False(result.Completed);
        Assert.False(result.Aborted);
        Assert.Collection(
            result.Outcomes,
            outcome =>
            {
                Assert.Equal("BuilderModes", outcome.PluginName);
                Assert.False(outcome.Completed);
                Assert.Equal("reload failed", outcome.Error);
            },
            outcome =>
            {
                Assert.Equal("TagRange", outcome.PluginName);
                Assert.True(outcome.Completed);
            });
        DBuilderPluginDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("BuilderModes", diagnostic.PluginName);
        Assert.Equal("reload failed", diagnostic.Message);
    }

    [Fact]
    public void ExecuteReflectionCallbackInvokesRuntimePluginCallbacksInOrder()
    {
        ReflectionCallbackPlugin.Calls.Clear();
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "Second",
                    "/plugins/second.dll",
                    typeof(ReflectionCallbackPlugin).FullName!,
                    1,
                    new ReflectionCallbackPlugin("Second")),
                new DBuilderPluginRuntimeInstance(
                    "First",
                    "/plugins/first.dll",
                    typeof(ReflectionCallbackPlugin).FullName!,
                    0,
                    new ReflectionCallbackPlugin("First"))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.ExecuteReflectionInitialize(plan);

        Assert.True(result.Completed);
        Assert.False(result.Aborted);
        Assert.Equal(new[] { "First:OnInitialize", "Second:OnInitialize" }, ReflectionCallbackPlugin.Calls);
        Assert.Collection(
            result.Outcomes,
            outcome => Assert.Equal("First", outcome.PluginName),
            outcome => Assert.Equal("Second", outcome.PluginName));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ExecuteReflectionCallbackTreatsMissingMethodsAsSuccessfulNoOps()
    {
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "NoCallback",
                    "/plugins/no-callback.dll",
                    typeof(ReflectionPluginHostTestPlugin).FullName!,
                    0,
                    new ReflectionPluginHostTestPlugin())
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.ExecuteReflectionCallback(
            plan,
            "OnReloadResources");

        Assert.True(result.Completed);
        Assert.False(result.Aborted);
        DBuilderPluginCallbackOutcome outcome = Assert.Single(result.Outcomes);
        Assert.Equal("NoCallback", outcome.PluginName);
        Assert.True(outcome.Completed);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ExecuteReflectionMapConfigurationAndResourceHelpersDispatchCallbacksInOrder()
    {
        ReflectionMapLifecycleCallbackPlugin.Calls.Clear();
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "Second",
                    "/plugins/second.dll",
                    typeof(ReflectionMapLifecycleCallbackPlugin).FullName!,
                    1,
                    new ReflectionMapLifecycleCallbackPlugin("Second")),
                new DBuilderPluginRuntimeInstance(
                    "First",
                    "/plugins/first.dll",
                    typeof(ReflectionMapLifecycleCallbackPlugin).FullName!,
                    0,
                    new ReflectionMapLifecycleCallbackPlugin("First"))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult openBegin = DBuilderPluginHostModel.ExecuteReflectionMapOpenBegin(plan);
        DBuilderPluginCallbackExecutionResult openEnd = DBuilderPluginHostModel.ExecuteReflectionMapOpenEnd(plan);
        DBuilderPluginCallbackExecutionResult newBegin = DBuilderPluginHostModel.ExecuteReflectionMapNewBegin(plan);
        DBuilderPluginCallbackExecutionResult newEnd = DBuilderPluginHostModel.ExecuteReflectionMapNewEnd(plan);
        DBuilderPluginCallbackExecutionResult closeBegin = DBuilderPluginHostModel.ExecuteReflectionMapCloseBegin(plan);
        DBuilderPluginCallbackExecutionResult closeEnd = DBuilderPluginHostModel.ExecuteReflectionMapCloseEnd(plan);
        DBuilderPluginCallbackExecutionResult mapSetBegin = DBuilderPluginHostModel.ExecuteReflectionMapSetChangeBegin(plan);
        DBuilderPluginCallbackExecutionResult mapSetEnd = DBuilderPluginHostModel.ExecuteReflectionMapSetChangeEnd(plan);
        DBuilderPluginCallbackExecutionResult programReconfigure = DBuilderPluginHostModel.ExecuteReflectionProgramReconfigure(plan);
        DBuilderPluginCallbackExecutionResult mapReconfigure = DBuilderPluginHostModel.ExecuteReflectionMapReconfigure(plan);
        DBuilderPluginCallbackExecutionResult reloadResources = DBuilderPluginHostModel.ExecuteReflectionReloadResources(plan);
        DBuilderPluginCallbackExecutionResult mapNodesRebuilt = DBuilderPluginHostModel.ExecuteReflectionMapNodesRebuilt(plan);

        Assert.All(
            new[]
            {
                openBegin,
                openEnd,
                newBegin,
                newEnd,
                closeBegin,
                closeEnd,
                mapSetBegin,
                mapSetEnd,
                programReconfigure,
                mapReconfigure,
                reloadResources,
                mapNodesRebuilt
            },
            result =>
            {
                Assert.True(result.Completed);
                Assert.False(result.Aborted);
                Assert.Empty(result.Diagnostics);
            });
        Assert.Equal(new[]
        {
            "First:OnMapOpenBegin",
            "Second:OnMapOpenBegin",
            "First:OnMapOpenEnd",
            "Second:OnMapOpenEnd",
            "First:OnMapNewBegin",
            "Second:OnMapNewBegin",
            "First:OnMapNewEnd",
            "Second:OnMapNewEnd",
            "First:OnMapCloseBegin",
            "Second:OnMapCloseBegin",
            "First:OnMapCloseEnd",
            "Second:OnMapCloseEnd",
            "First:OnMapSetChangeBegin",
            "Second:OnMapSetChangeBegin",
            "First:OnMapSetChangeEnd",
            "Second:OnMapSetChangeEnd",
            "First:OnProgramReconfigure",
            "Second:OnProgramReconfigure",
            "First:OnMapReconfigure",
            "Second:OnMapReconfigure",
            "First:OnReloadResources",
            "Second:OnReloadResources",
            "First:OnMapNodesRebuilt",
            "Second:OnMapNodesRebuilt"
        }, ReflectionMapLifecycleCallbackPlugin.Calls);
    }

    [Fact]
    public void ExecuteReflectionEditModeHelpersDispatchCallbacksInOrder()
    {
        ReflectionEditModeCallbackPlugin.Calls.Clear();
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "Second",
                    "/plugins/second.dll",
                    typeof(ReflectionEditModeCallbackPlugin).FullName!,
                    1,
                    new ReflectionEditModeCallbackPlugin("Second", continueResult: true)),
                new DBuilderPluginRuntimeInstance(
                    "First",
                    "/plugins/first.dll",
                    typeof(ReflectionEditModeCallbackPlugin).FullName!,
                    0,
                    new ReflectionEditModeCallbackPlugin("First", continueResult: false))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult modeChange = DBuilderPluginHostModel.ExecuteReflectionModeChange(plan);
        DBuilderPluginCallbackExecutionResult editEngage = DBuilderPluginHostModel.ExecuteReflectionEditEngage(plan);
        DBuilderPluginCallbackExecutionResult editDisengage = DBuilderPluginHostModel.ExecuteReflectionEditDisengage(plan);

        Assert.True(modeChange.Completed);
        Assert.True(modeChange.Aborted);
        Assert.False(editEngage.Aborted);
        Assert.False(editDisengage.Aborted);
        Assert.Empty(modeChange.Diagnostics);
        Assert.Empty(editEngage.Diagnostics);
        Assert.Empty(editDisengage.Diagnostics);
        Assert.Equal(new[]
        {
            "First:ModeChange",
            "Second:ModeChange",
            "First:EditEngage",
            "Second:EditEngage",
            "First:EditDisengage",
            "Second:EditDisengage"
        }, ReflectionEditModeCallbackPlugin.Calls);
    }

    [Fact]
    public void ExecuteReflectionCallbackPreservesAbortableCallbackAbort()
    {
        ReflectionAbortCallbackPlugin.Calls.Clear();
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "Aborter",
                    "/plugins/aborter.dll",
                    typeof(ReflectionAbortCallbackPlugin).FullName!,
                    0,
                    new ReflectionAbortCallbackPlugin(continueResult: false, name: "Aborter"))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.ExecuteReflectionCallback(
            plan,
            "OnCopyBegin");

        Assert.True(result.Completed);
        Assert.True(result.Aborted);
        Assert.True(result.Outcomes.Single().Aborted);
        Assert.Equal(new[] { "Aborter:True" }, ReflectionAbortCallbackPlugin.Calls);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ExecuteReflectionCallbackPassesCurrentAbortResultThroughUdbChain()
    {
        ReflectionAbortCallbackPlugin.Calls.Clear();
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "First",
                    "/plugins/first.dll",
                    typeof(ReflectionAbortCallbackPlugin).FullName!,
                    0,
                    new ReflectionAbortCallbackPlugin(continueResult: false, name: "First")),
                new DBuilderPluginRuntimeInstance(
                    "Second",
                    "/plugins/second.dll",
                    typeof(ReflectionAbortCallbackPlugin).FullName!,
                    1,
                    new ReflectionAbortCallbackPlugin(continueResult: true, name: "Second"))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.ExecuteReflectionCallback(
            plan,
            "OnUndoBegin");

        Assert.True(result.Completed);
        Assert.True(result.Aborted);
        Assert.Collection(
            result.Outcomes,
            outcome => Assert.True(outcome.Aborted),
            outcome => Assert.False(outcome.Aborted));
        Assert.Equal(new[] { "First:True", "Second:False" }, ReflectionAbortCallbackPlugin.Calls);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ExecuteReflectionEditOperationHelpersDispatchBeginAndEndCallbacks()
    {
        ReflectionEditOperationCallbackPlugin.Calls.Clear();
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "First",
                    "/plugins/first.dll",
                    typeof(ReflectionEditOperationCallbackPlugin).FullName!,
                    0,
                    new ReflectionEditOperationCallbackPlugin("First", continueResult: false)),
                new DBuilderPluginRuntimeInstance(
                    "Second",
                    "/plugins/second.dll",
                    typeof(ReflectionEditOperationCallbackPlugin).FullName!,
                    1,
                    new ReflectionEditOperationCallbackPlugin("Second", continueResult: true))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult copyBegin = DBuilderPluginHostModel.ExecuteReflectionCopyBegin(plan);
        DBuilderPluginCallbackExecutionResult copyEnd = DBuilderPluginHostModel.ExecuteReflectionCopyEnd(plan);
        DBuilderPluginCallbackExecutionResult undoCreated = DBuilderPluginHostModel.ExecuteReflectionUndoCreated(plan);
        DBuilderPluginCallbackExecutionResult undoWithdrawn = DBuilderPluginHostModel.ExecuteReflectionUndoWithdrawn(plan);
        DBuilderPluginCallbackExecutionResult redoBegin = DBuilderPluginHostModel.ExecuteReflectionRedoBegin(plan);
        DBuilderPluginCallbackExecutionResult redoEnd = DBuilderPluginHostModel.ExecuteReflectionRedoEnd(plan);
        DBuilderPluginCallbackExecutionResult editCancel = DBuilderPluginHostModel.ExecuteReflectionEditCancel(plan);
        DBuilderPluginCallbackExecutionResult editAccept = DBuilderPluginHostModel.ExecuteReflectionEditAccept(plan);

        Assert.True(copyBegin.Aborted);
        Assert.True(redoBegin.Aborted);
        Assert.False(copyEnd.Aborted);
        Assert.False(undoCreated.Aborted);
        Assert.False(undoWithdrawn.Aborted);
        Assert.False(redoEnd.Aborted);
        Assert.False(editCancel.Aborted);
        Assert.False(editAccept.Aborted);
        Assert.Equal(new[]
        {
            "First:CopyBegin:True",
            "Second:CopyBegin:False",
            "First:CopyEnd",
            "Second:CopyEnd",
            "First:UndoCreated",
            "Second:UndoCreated",
            "First:UndoWithdrawn",
            "Second:UndoWithdrawn",
            "First:RedoBegin:True",
            "Second:RedoBegin:False",
            "First:RedoEnd",
            "Second:RedoEnd",
            "First:EditCancel",
            "Second:EditCancel",
            "First:EditAccept",
            "Second:EditAccept"
        }, ReflectionEditOperationCallbackPlugin.Calls);
        Assert.Empty(copyBegin.Diagnostics);
        Assert.Empty(copyEnd.Diagnostics);
        Assert.Empty(undoCreated.Diagnostics);
        Assert.Empty(undoWithdrawn.Diagnostics);
        Assert.Empty(redoBegin.Diagnostics);
        Assert.Empty(redoEnd.Diagnostics);
        Assert.Empty(editCancel.Diagnostics);
        Assert.Empty(editAccept.Diagnostics);
    }

    [Fact]
    public void ExecuteReflectionPreferenceAndActionHelpersDispatchCallbacksInOrder()
    {
        ReflectionPreferenceActionCallbackPlugin.Calls.Clear();
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "Second",
                    "/plugins/second.dll",
                    typeof(ReflectionPreferenceActionCallbackPlugin).FullName!,
                    1,
                    new ReflectionPreferenceActionCallbackPlugin("Second")),
                new DBuilderPluginRuntimeInstance(
                    "First",
                    "/plugins/first.dll",
                    typeof(ReflectionPreferenceActionCallbackPlugin).FullName!,
                    0,
                    new ReflectionPreferenceActionCallbackPlugin("First"))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult showPreferences = DBuilderPluginHostModel.ExecuteReflectionShowPreferences(plan);
        DBuilderPluginCallbackExecutionResult closePreferences = DBuilderPluginHostModel.ExecuteReflectionClosePreferences(plan);
        DBuilderPluginCallbackExecutionResult actionBegin = DBuilderPluginHostModel.ExecuteReflectionActionBegin(plan);
        DBuilderPluginCallbackExecutionResult actionEnd = DBuilderPluginHostModel.ExecuteReflectionActionEnd(plan);

        Assert.All(
            new[] { showPreferences, closePreferences, actionBegin, actionEnd },
            result =>
            {
                Assert.True(result.Completed);
                Assert.False(result.Aborted);
                Assert.Empty(result.Diagnostics);
            });
        Assert.Equal(new[]
        {
            "First:ShowPreferences",
            "Second:ShowPreferences",
            "First:ClosePreferences",
            "Second:ClosePreferences",
            "First:ActionBegin",
            "Second:ActionBegin",
            "First:ActionEnd",
            "Second:ActionEnd"
        }, ReflectionPreferenceActionCallbackPlugin.Calls);
    }

    [Fact]
    public void ExecuteReflectionInputHelpersDispatchCallbacksInOrder()
    {
        ReflectionInputCallbackPlugin.Calls.Clear();
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "Second",
                    "/plugins/second.dll",
                    typeof(ReflectionInputCallbackPlugin).FullName!,
                    1,
                    new ReflectionInputCallbackPlugin("Second")),
                new DBuilderPluginRuntimeInstance(
                    "First",
                    "/plugins/first.dll",
                    typeof(ReflectionInputCallbackPlugin).FullName!,
                    0,
                    new ReflectionInputCallbackPlugin("First"))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult mouseClick = DBuilderPluginHostModel.ExecuteReflectionEditMouseClick(plan);
        DBuilderPluginCallbackExecutionResult mouseDoubleClick = DBuilderPluginHostModel.ExecuteReflectionEditMouseDoubleClick(plan);
        DBuilderPluginCallbackExecutionResult mouseDown = DBuilderPluginHostModel.ExecuteReflectionEditMouseDown(plan);
        DBuilderPluginCallbackExecutionResult mouseEnter = DBuilderPluginHostModel.ExecuteReflectionEditMouseEnter(plan);
        DBuilderPluginCallbackExecutionResult mouseLeave = DBuilderPluginHostModel.ExecuteReflectionEditMouseLeave(plan);
        DBuilderPluginCallbackExecutionResult mouseMove = DBuilderPluginHostModel.ExecuteReflectionEditMouseMove(plan);
        DBuilderPluginCallbackExecutionResult mouseUp = DBuilderPluginHostModel.ExecuteReflectionEditMouseUp(plan);
        DBuilderPluginCallbackExecutionResult keyDown = DBuilderPluginHostModel.ExecuteReflectionEditKeyDown(plan);
        DBuilderPluginCallbackExecutionResult keyUp = DBuilderPluginHostModel.ExecuteReflectionEditKeyUp(plan);
        DBuilderPluginCallbackExecutionResult mouseInput = DBuilderPluginHostModel.ExecuteReflectionEditMouseInput(plan);

        Assert.All(
            new[]
            {
                mouseClick,
                mouseDoubleClick,
                mouseDown,
                mouseEnter,
                mouseLeave,
                mouseMove,
                mouseUp,
                keyDown,
                keyUp,
                mouseInput
            },
            result =>
            {
                Assert.True(result.Completed);
                Assert.False(result.Aborted);
                Assert.Empty(result.Diagnostics);
            });
        Assert.Equal(new[]
        {
            "First:MouseClick",
            "Second:MouseClick",
            "First:MouseDoubleClick",
            "Second:MouseDoubleClick",
            "First:MouseDown",
            "Second:MouseDown",
            "First:MouseEnter",
            "Second:MouseEnter",
            "First:MouseLeave",
            "Second:MouseLeave",
            "First:MouseMove",
            "Second:MouseMove",
            "First:MouseUp",
            "Second:MouseUp",
            "First:KeyDown",
            "Second:KeyDown",
            "First:KeyUp",
            "Second:KeyUp",
            "First:MouseInput",
            "Second:MouseInput"
        }, ReflectionInputCallbackPlugin.Calls);
    }

    [Fact]
    public void ExecuteReflectionRenderingHelpersDispatchCallbacksInOrder()
    {
        ReflectionRenderingCallbackPlugin.Calls.Clear();
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "Second",
                    "/plugins/second.dll",
                    typeof(ReflectionRenderingCallbackPlugin).FullName!,
                    1,
                    new ReflectionRenderingCallbackPlugin("Second")),
                new DBuilderPluginRuntimeInstance(
                    "First",
                    "/plugins/first.dll",
                    typeof(ReflectionRenderingCallbackPlugin).FullName!,
                    0,
                    new ReflectionRenderingCallbackPlugin("First"))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult redrawBegin = DBuilderPluginHostModel.ExecuteReflectionEditRedrawDisplayBegin(plan);
        DBuilderPluginCallbackExecutionResult redrawEnd = DBuilderPluginHostModel.ExecuteReflectionEditRedrawDisplayEnd(plan);
        DBuilderPluginCallbackExecutionResult presentBegin = DBuilderPluginHostModel.ExecuteReflectionPresentDisplayBegin(plan);
        DBuilderPluginCallbackExecutionResult ceilingUpdate = DBuilderPluginHostModel.ExecuteReflectionSectorCeilingSurfaceUpdate(plan);
        DBuilderPluginCallbackExecutionResult floorUpdate = DBuilderPluginHostModel.ExecuteReflectionSectorFloorSurfaceUpdate(plan);

        Assert.All(
            new[] { redrawBegin, redrawEnd, presentBegin, ceilingUpdate, floorUpdate },
            result =>
            {
                Assert.True(result.Completed);
                Assert.False(result.Aborted);
                Assert.Empty(result.Diagnostics);
            });
        Assert.Equal(new[]
        {
            "First:RedrawBegin",
            "Second:RedrawBegin",
            "First:RedrawEnd",
            "Second:RedrawEnd",
            "First:PresentBegin",
            "Second:PresentBegin",
            "First:CeilingUpdate",
            "Second:CeilingUpdate",
            "First:FloorUpdate",
            "Second:FloorUpdate"
        }, ReflectionRenderingCallbackPlugin.Calls);
    }

    [Fact]
    public void ExecuteReflectionHighlightHelpersDispatchCallbacksInOrder()
    {
        ReflectionHighlightCallbackPlugin.Calls.Clear();
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "Second",
                    "/plugins/second.dll",
                    typeof(ReflectionHighlightCallbackPlugin).FullName!,
                    1,
                    new ReflectionHighlightCallbackPlugin("Second")),
                new DBuilderPluginRuntimeInstance(
                    "First",
                    "/plugins/first.dll",
                    typeof(ReflectionHighlightCallbackPlugin).FullName!,
                    0,
                    new ReflectionHighlightCallbackPlugin("First"))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult sector = DBuilderPluginHostModel.ExecuteReflectionHighlightSector(plan);
        DBuilderPluginCallbackExecutionResult linedef = DBuilderPluginHostModel.ExecuteReflectionHighlightLinedef(plan);
        DBuilderPluginCallbackExecutionResult thing = DBuilderPluginHostModel.ExecuteReflectionHighlightThing(plan);
        DBuilderPluginCallbackExecutionResult vertex = DBuilderPluginHostModel.ExecuteReflectionHighlightVertex(plan);
        DBuilderPluginCallbackExecutionResult refreshed = DBuilderPluginHostModel.ExecuteReflectionHighlightRefreshed(plan);
        DBuilderPluginCallbackExecutionResult lost = DBuilderPluginHostModel.ExecuteReflectionHighlightLost(plan);

        Assert.All(
            new[] { sector, linedef, thing, vertex, refreshed, lost },
            result =>
            {
                Assert.True(result.Completed);
                Assert.False(result.Aborted);
                Assert.Empty(result.Diagnostics);
            });
        Assert.Equal(new[]
        {
            "First:Sector",
            "Second:Sector",
            "First:Linedef",
            "Second:Linedef",
            "First:Thing",
            "Second:Thing",
            "First:Vertex",
            "Second:Vertex",
            "First:Refreshed",
            "Second:Refreshed",
            "First:Lost",
            "Second:Lost"
        }, ReflectionHighlightCallbackPlugin.Calls);
    }

    [Fact]
    public void ExecuteReflectionCallbackPassesCopiedPasteOptionsToEachPlugin()
    {
        ReflectionPasteCallbackPlugin.Calls.Clear();
        var options = new PasteOptions
        {
            ChangeTags = PasteTagMode.Renumber,
            RemoveActions = true
        };
        ReflectionPasteCallbackPlugin.OriginalOptions = options;
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "First",
                    "/plugins/first.dll",
                    typeof(ReflectionPasteCallbackPlugin).FullName!,
                    0,
                    new ReflectionPasteCallbackPlugin("First", continueResult: false)),
                new DBuilderPluginRuntimeInstance(
                    "Second",
                    "/plugins/second.dll",
                    typeof(ReflectionPasteCallbackPlugin).FullName!,
                    1,
                    new ReflectionPasteCallbackPlugin("Second", continueResult: true))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.ExecuteReflectionPasteBegin(
            plan,
            options);

        Assert.True(result.Completed);
        Assert.True(result.Aborted);
        Assert.Collection(
            result.Outcomes,
            outcome => Assert.True(outcome.Aborted),
            outcome => Assert.False(outcome.Aborted));
        Assert.Equal(new[]
        {
            "First:Renumber:True:True:copy",
            "Second:Renumber:True:False:copy"
        }, ReflectionPasteCallbackPlugin.Calls);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ExecuteReflectionPasteEndPassesCopiedPasteOptionsToEachPlugin()
    {
        ReflectionPasteCallbackPlugin.Calls.Clear();
        var options = new PasteOptions
        {
            ChangeTags = PasteTagMode.Remove
        };
        ReflectionPasteCallbackPlugin.OriginalOptions = options;
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "First",
                    "/plugins/first.dll",
                    typeof(ReflectionPasteCallbackPlugin).FullName!,
                    0,
                    new ReflectionPasteCallbackPlugin("First", continueResult: true)),
                new DBuilderPluginRuntimeInstance(
                    "Second",
                    "/plugins/second.dll",
                    typeof(ReflectionPasteCallbackPlugin).FullName!,
                    1,
                    new ReflectionPasteCallbackPlugin("Second", continueResult: true))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.ExecuteReflectionPasteEnd(
            plan,
            options);

        Assert.True(result.Completed);
        Assert.False(result.Aborted);
        Assert.Equal(new[]
        {
            "First:End:Remove:False:copy",
            "Second:End:Remove:False:copy"
        }, ReflectionPasteCallbackPlugin.Calls);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ExecuteReflectionMapSaveCallbacksPassUdbSavePurpose()
    {
        ReflectionMapSaveCallbackPlugin.Calls.Clear();
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "First",
                    "/plugins/first.dll",
                    typeof(ReflectionMapSaveCallbackPlugin).FullName!,
                    0,
                    new ReflectionMapSaveCallbackPlugin("First")),
                new DBuilderPluginRuntimeInstance(
                    "Second",
                    "/plugins/second.dll",
                    typeof(ReflectionMapSaveCallbackPlugin).FullName!,
                    1,
                    new ReflectionMapSaveCallbackPlugin("Second"))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult begin = DBuilderPluginHostModel.ExecuteReflectionMapSaveBegin(
            plan,
            SavePurpose.Testing);
        DBuilderPluginCallbackExecutionResult end = DBuilderPluginHostModel.ExecuteReflectionMapSaveEnd(
            plan,
            SavePurpose.Testing);

        Assert.True(begin.Completed);
        Assert.True(end.Completed);
        Assert.False(begin.Aborted);
        Assert.False(end.Aborted);
        Assert.Equal(new[]
        {
            "First:Begin:Testing",
            "Second:Begin:Testing",
            "First:End:Testing",
            "Second:End:Testing"
        }, ReflectionMapSaveCallbackPlugin.Calls);
        Assert.Empty(begin.Diagnostics);
        Assert.Empty(end.Diagnostics);
    }

    [Fact]
    public void ExecuteReflectionCallbackReportsCallbackParameterErrors()
    {
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "BadSignature",
                    "/plugins/bad-signature.dll",
                    typeof(ReflectionBadSignatureCallbackPlugin).FullName!,
                    0,
                    new ReflectionBadSignatureCallbackPlugin())
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.ExecuteReflectionCallback(
            plan,
            "OnInitialize");

        Assert.False(result.Completed);
        DBuilderPluginCallbackOutcome outcome = Assert.Single(result.Outcomes);
        Assert.False(outcome.Completed);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("BadSignature", diagnostic.PluginName);
        Assert.Equal("Plugin BadSignature callback OnInitialize has unsupported parameters.", diagnostic.Message);
    }

    [Fact]
    public void ExecuteReflectionCallbackRejectsUnexpectedArgumentsForNoArgumentCallbacks()
    {
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "Callback",
                    "/plugins/callback.dll",
                    typeof(ReflectionCallbackPlugin).FullName!,
                    0,
                    new ReflectionCallbackPlugin("Callback"))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.ExecuteReflectionCallback(
            plan,
            "OnInitialize",
            new object[] { SavePurpose.Normal });

        Assert.False(result.Completed);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Callback", diagnostic.PluginName);
        Assert.Equal("Plugin Callback callback OnInitialize has unsupported parameters.", diagnostic.Message);
    }

    [Fact]
    public void ExecuteReflectionCallbackReportsThrownCallbackErrorsWithoutDroppingOtherPlugins()
    {
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new IDBuilderPlugin[]
            {
                new ReflectionThrowingCallbackPlugin(),
                new ReflectionPluginHostTestPlugin()
            }
            .Select((plugin, index) => new DBuilderPluginRuntimeInstance(
                index == 0 ? "Throwing" : "Plain",
                $"/plugins/{index}.dll",
                plugin.GetType().FullName!,
                index,
                plugin))
            .ToArray(),
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.ExecuteReflectionCallback(
            plan,
            "OnInitialize");

        Assert.False(result.Completed);
        Assert.False(result.Aborted);
        Assert.Collection(
            result.Outcomes,
            outcome =>
            {
                Assert.Equal("Throwing", outcome.PluginName);
                Assert.False(outcome.Completed);
                Assert.Equal("callback failed", outcome.Error);
            },
            outcome =>
            {
                Assert.Equal("Plain", outcome.PluginName);
                Assert.True(outcome.Completed);
            });
        DBuilderPluginDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("Throwing", diagnostic.PluginName);
        Assert.Equal("callback failed", diagnostic.Message);
    }

    [Fact]
    public void ExecuteReflectionCallbackReportsUnknownCallbacks()
    {
        var plan = new DBuilderPluginRuntimeInstancePlan(
            Array.Empty<DBuilderPluginRuntimeInstance>(),
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginCallbackExecutionResult result = DBuilderPluginHostModel.ExecuteReflectionCallback(
            plan,
            "OnMissingCallback");

        Assert.False(result.Completed);
        Assert.Empty(result.Outcomes);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("(plugin host)", diagnostic.PluginName);
        Assert.Equal("Unknown plugin callback OnMissingCallback.", diagnostic.Message);
    }

    [Fact]
    public void ExecuteReflectionShutdownDisposesRuntimePluginsInReverseOrder()
    {
        ReflectionDisposablePlugin.Calls.Clear();
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "First",
                    "/plugins/first.dll",
                    typeof(ReflectionDisposablePlugin).FullName!,
                    0,
                    new ReflectionDisposablePlugin("First")),
                new DBuilderPluginRuntimeInstance(
                    "Second",
                    "/plugins/second.dll",
                    typeof(ReflectionDisposablePlugin).FullName!,
                    1,
                    new ReflectionDisposablePlugin("Second"))
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginShutdownPlan shutdown = DBuilderPluginHostModel.ExecuteReflectionShutdown(plan);

        Assert.Equal(new[] { "Second:Dispose", "First:Dispose" }, ReflectionDisposablePlugin.Calls);
        Assert.Empty(shutdown.Diagnostics);
        Assert.Collection(
            shutdown.Attempts,
            attempt =>
            {
                Assert.Equal("Second", attempt.PluginName);
                Assert.Equal(1, attempt.Order);
                Assert.True(attempt.Disposed);
                Assert.Null(attempt.Error);
            },
            attempt =>
            {
                Assert.Equal("First", attempt.PluginName);
                Assert.Equal(0, attempt.Order);
                Assert.True(attempt.Disposed);
            });
    }

    [Fact]
    public void ExecuteReflectionShutdownTreatsMissingDisposeAsSuccessfulNoOp()
    {
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "Plain",
                    "/plugins/plain.dll",
                    typeof(ReflectionPluginHostTestPlugin).FullName!,
                    0,
                    new ReflectionPluginHostTestPlugin())
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginShutdownPlan shutdown = DBuilderPluginHostModel.ExecuteReflectionShutdown(plan);

        Assert.Empty(shutdown.Diagnostics);
        DBuilderPluginShutdownAttempt attempt = Assert.Single(shutdown.Attempts);
        Assert.Equal("Plain", attempt.PluginName);
        Assert.True(attempt.Disposed);
        Assert.Null(attempt.Error);
    }

    [Fact]
    public void ExecuteReflectionShutdownReportsDisposeSignatureErrors()
    {
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new[]
            {
                new DBuilderPluginRuntimeInstance(
                    "BadDispose",
                    "/plugins/bad-dispose.dll",
                    typeof(ReflectionBadDisposePlugin).FullName!,
                    0,
                    new ReflectionBadDisposePlugin())
            },
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginShutdownPlan shutdown = DBuilderPluginHostModel.ExecuteReflectionShutdown(plan);

        DBuilderPluginShutdownAttempt attempt = Assert.Single(shutdown.Attempts);
        Assert.False(attempt.Disposed);
        Assert.Equal("Plugin BadDispose Dispose callback must not declare parameters.", attempt.Error);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(shutdown.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("BadDispose", diagnostic.PluginName);
        Assert.Equal("Plugin BadDispose Dispose callback must not declare parameters.", diagnostic.Message);
    }

    [Fact]
    public void ExecuteReflectionShutdownReportsDisposeErrorsWithoutDroppingOtherPlugins()
    {
        var plan = new DBuilderPluginRuntimeInstancePlan(
            new IDBuilderPlugin[]
            {
                new ReflectionThrowingDisposePlugin(),
                new ReflectionPluginHostTestPlugin()
            }
            .Select((plugin, index) => new DBuilderPluginRuntimeInstance(
                index == 0 ? "Throwing" : "Plain",
                $"/plugins/{index}.dll",
                plugin.GetType().FullName!,
                index,
                plugin))
            .ToArray(),
            Array.Empty<DBuilderPluginDiagnostic>());

        DBuilderPluginShutdownPlan shutdown = DBuilderPluginHostModel.ExecuteReflectionShutdown(plan);

        Assert.Collection(
            shutdown.Attempts,
            attempt =>
            {
                Assert.Equal("Plain", attempt.PluginName);
                Assert.True(attempt.Disposed);
            },
            attempt =>
            {
                Assert.Equal("Throwing", attempt.PluginName);
                Assert.False(attempt.Disposed);
                Assert.Equal("dispose failed", attempt.Error);
            });
        DBuilderPluginDiagnostic diagnostic = Assert.Single(shutdown.Diagnostics);
        Assert.Equal("Throwing", diagnostic.PluginName);
        Assert.Equal("dispose failed", diagnostic.Message);
    }

    [Fact]
    public void ExecuteReflectionShutdownPreservesInstanceDiagnostics()
    {
        var plan = new DBuilderPluginRuntimeInstancePlan(
            Array.Empty<DBuilderPluginRuntimeInstance>(),
            new[]
            {
                new DBuilderPluginDiagnostic(
                    DBuilderPluginDiagnosticSeverity.Error,
                    "Broken",
                    "activation failed")
            });

        DBuilderPluginShutdownPlan shutdown = DBuilderPluginHostModel.ExecuteReflectionShutdown(plan);

        Assert.Empty(shutdown.Attempts);
        DBuilderPluginDiagnostic diagnostic = Assert.Single(shutdown.Diagnostics);
        Assert.Equal("Broken", diagnostic.PluginName);
        Assert.Equal("activation failed", diagnostic.Message);
    }

    [Fact]
    public void FindPluginResourceNameMatchesUdbSuffixRule()
    {
        string? resource = DBuilderPluginHostModel.FindPluginResourceName(
            new[]
            {
                "Plugin.Properties.Resources.SuperCoolMode.png",
                "Plugin.Resources.CoolMode.png",
                "Plugin.Resources.Other.txt"
            },
            "coolmode.png");

        Assert.Equal("Plugin.Resources.CoolMode.png", resource);
    }

    [Fact]
    public void FindPluginResourceNameDoesNotMatchPartialNames()
    {
        string? resource = DBuilderPluginHostModel.FindPluginResourceName(
            new[] { "Plugin.Properties.Resources.SuperCoolMode.png" },
            "CoolMode.png");

        Assert.Null(resource);
    }

    [Fact]
    public void OpenReflectionPluginResourceStreamReadsRuntimePluginAssemblyResources()
    {
        var runtime = new DBuilderPluginRuntimeInstance(
            "ResourcePlugin",
            "/plugins/resource.dll",
            typeof(ReflectionPluginHostTestPlugin).FullName!,
            0,
            new ReflectionPluginHostTestPlugin());

        using Stream? stream = DBuilderPluginHostModel.OpenReflectionPluginResourceStream(runtime, "CoolMode.txt");

        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        Assert.Equal("resource fixture", reader.ReadToEnd().Trim());
    }

    [Fact]
    public void PlanLifecycleRegistersContributionHooksInStableOrder()
    {
        var descriptor = new DBuilderPluginDescriptor(
            "BuilderModes",
            "/plugins/buildermodes.dll",
            RequiresMap: true,
            Contributions: new[]
            {
                new DBuilderPluginContribution(DBuilderPluginContributionKind.Toolbar, "toolbar.draw", "Draw toolbar"),
                new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "action.draw", "Draw action"),
                new DBuilderPluginContribution(DBuilderPluginContributionKind.EditMode, "mode.draw", "Draw mode"),
                new DBuilderPluginContribution(DBuilderPluginContributionKind.Docker, "docker.tags", "Tags docker"),
                new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "resource.pk3", "PK3 resource")
            });

        DBuilderPluginLifecyclePlan plan = DBuilderPluginHostModel.PlanLifecycle(
            descriptor,
            new DBuilderPluginLifecycleRequest(
                MapOpen: true,
                MapClose: true,
                MapSave: true,
                MapReconfigure: true,
                ProgramReconfigure: true,
                Engage: true,
                Shutdown: true));

        Assert.Equal(new[]
        {
            DBuilderPluginLifecycleHook.Load,
            DBuilderPluginLifecycleHook.RegisterActions,
            DBuilderPluginLifecycleHook.RegisterHints,
            DBuilderPluginLifecycleHook.Initialize,
            DBuilderPluginLifecycleHook.RegisterUi,
            DBuilderPluginLifecycleHook.RegisterEditModes,
            DBuilderPluginLifecycleHook.RegisterDockers,
            DBuilderPluginLifecycleHook.RegisterResourceHandlers,
            DBuilderPluginLifecycleHook.MapOpened,
            DBuilderPluginLifecycleHook.MapClosed,
            DBuilderPluginLifecycleHook.MapSaved,
            DBuilderPluginLifecycleHook.MapReconfigured,
            DBuilderPluginLifecycleHook.ProgramReconfigured,
            DBuilderPluginLifecycleHook.Engage,
            DBuilderPluginLifecycleHook.Dispose
        }, plan.Hooks);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void PlanLifecycleAddsMapCloseOnlyForMapScopedPlugins()
    {
        DBuilderPluginLifecyclePlan mapScoped = DBuilderPluginHostModel.PlanLifecycle(
            new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll", RequiresMap: true),
            new DBuilderPluginLifecycleRequest(MapClose: true));
        DBuilderPluginLifecyclePlan global = DBuilderPluginHostModel.PlanLifecycle(
            new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
            new DBuilderPluginLifecycleRequest(MapClose: true));

        Assert.Contains(DBuilderPluginLifecycleHook.MapClosed, mapScoped.Hooks);
        Assert.DoesNotContain(DBuilderPluginLifecycleHook.MapClosed, global.Hooks);
    }

    [Fact]
    public void PlanLifecycleAddsMapSaveOnlyForMapScopedPlugins()
    {
        DBuilderPluginLifecyclePlan mapScoped = DBuilderPluginHostModel.PlanLifecycle(
            new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll", RequiresMap: true),
            new DBuilderPluginLifecycleRequest(MapSave: true));
        DBuilderPluginLifecyclePlan global = DBuilderPluginHostModel.PlanLifecycle(
            new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
            new DBuilderPluginLifecycleRequest(MapSave: true));

        Assert.Contains(DBuilderPluginLifecycleHook.MapSaved, mapScoped.Hooks);
        Assert.DoesNotContain(DBuilderPluginLifecycleHook.MapSaved, global.Hooks);
    }

    [Fact]
    public void PlanLifecycleAddsMapReconfigureOnlyForMapScopedPlugins()
    {
        DBuilderPluginLifecyclePlan mapScoped = DBuilderPluginHostModel.PlanLifecycle(
            new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll", RequiresMap: true),
            new DBuilderPluginLifecycleRequest(MapReconfigure: true));
        DBuilderPluginLifecyclePlan global = DBuilderPluginHostModel.PlanLifecycle(
            new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
            new DBuilderPluginLifecycleRequest(MapReconfigure: true));

        Assert.Contains(DBuilderPluginLifecycleHook.MapReconfigured, mapScoped.Hooks);
        Assert.DoesNotContain(DBuilderPluginLifecycleHook.MapReconfigured, global.Hooks);
    }

    [Fact]
    public void PlanLifecycleAddsProgramReconfigureForGlobalAndMapScopedPlugins()
    {
        DBuilderPluginLifecyclePlan mapScoped = DBuilderPluginHostModel.PlanLifecycle(
            new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll", RequiresMap: true),
            new DBuilderPluginLifecycleRequest(ProgramReconfigure: true));
        DBuilderPluginLifecyclePlan global = DBuilderPluginHostModel.PlanLifecycle(
            new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
            new DBuilderPluginLifecycleRequest(ProgramReconfigure: true));

        Assert.Contains(DBuilderPluginLifecycleHook.ProgramReconfigured, mapScoped.Hooks);
        Assert.Contains(DBuilderPluginLifecycleHook.ProgramReconfigured, global.Hooks);
    }

    [Fact]
    public void PlanLifecycleAddsResourceReloadOnlyForMapScopedPlugins()
    {
        DBuilderPluginLifecyclePlan mapScoped = DBuilderPluginHostModel.PlanLifecycle(
            new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll", RequiresMap: true),
            new DBuilderPluginLifecycleRequest(ReloadResources: true));
        DBuilderPluginLifecyclePlan global = DBuilderPluginHostModel.PlanLifecycle(
            new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
            new DBuilderPluginLifecycleRequest(ReloadResources: true));

        Assert.Contains(DBuilderPluginLifecycleHook.ResourcesReloaded, mapScoped.Hooks);
        Assert.DoesNotContain(DBuilderPluginLifecycleHook.ResourcesReloaded, global.Hooks);
    }

    [Fact]
    public void PlanLifecycleAddsMapNodesRebuiltOnlyForMapScopedPlugins()
    {
        DBuilderPluginLifecyclePlan mapScoped = DBuilderPluginHostModel.PlanLifecycle(
            new DBuilderPluginDescriptor("BuilderModes", "/plugins/buildermodes.dll", RequiresMap: true),
            new DBuilderPluginLifecycleRequest(MapNodesRebuilt: true));
        DBuilderPluginLifecyclePlan global = DBuilderPluginHostModel.PlanLifecycle(
            new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll"),
            new DBuilderPluginLifecycleRequest(MapNodesRebuilt: true));

        Assert.Contains(DBuilderPluginLifecycleHook.MapNodesRebuilt, mapScoped.Hooks);
        Assert.DoesNotContain(DBuilderPluginLifecycleHook.MapNodesRebuilt, global.Hooks);
    }

    [Fact]
    public void PlanLifecycleKeepsInvalidOrDisabledPluginsOutOfHooks()
    {
        DBuilderPluginLifecyclePlan invalid = DBuilderPluginHostModel.PlanLifecycle(
            new DBuilderPluginDescriptor("", ""),
            new DBuilderPluginLifecycleRequest(MapOpen: true, Engage: true, Shutdown: true));

        Assert.Empty(invalid.Hooks);
        Assert.Equal(new[] { "Plugin name is missing.", "Plugin assembly path is missing." }, invalid.Warnings);

        DBuilderPluginLifecyclePlan disabled = DBuilderPluginHostModel.PlanLifecycle(
            new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll", Enabled: false),
            new DBuilderPluginLifecycleRequest(MapOpen: true));

        Assert.Empty(disabled.Hooks);
        Assert.Equal(new[] { "Plugin TagRange is disabled." }, disabled.Warnings);
    }

    [Fact]
    public void PlanLifecycleNormalizesContributions()
    {
        var descriptor = new DBuilderPluginDescriptor(
            "CommentsPanel",
            "/plugins/comments.dll",
            Contributions: new[]
            {
                new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, " comments.open ", " Open Comments ", ActionId: " comments.toggle "),
                new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "comments.open", "Duplicate"),
                new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "", "No Id"),
                new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "comments.toggle", "")
            });

        DBuilderPluginLifecyclePlan plan = DBuilderPluginHostModel.PlanLifecycle(
            descriptor,
            new DBuilderPluginLifecycleRequest());

        DBuilderPluginContribution contribution = Assert.Single(plan.Descriptor.Contributions!);
        Assert.Equal(DBuilderPluginContributionKind.Menu, contribution.Kind);
        Assert.Equal("comments.open", contribution.Id);
        Assert.Equal("Open Comments", contribution.Title);
        Assert.Equal("comments.toggle", contribution.ActionId);
        Assert.Equal(new[]
        {
            DBuilderPluginLifecycleHook.Load,
            DBuilderPluginLifecycleHook.RegisterActions,
            DBuilderPluginLifecycleHook.RegisterHints,
            DBuilderPluginLifecycleHook.Initialize,
            DBuilderPluginLifecycleHook.RegisterUi
        }, plan.Hooks);
    }

    [Fact]
    public void PlanUiContributionsSeparatesMenusAndToolbarsInStableOrder()
    {
        var plan = DBuilderPluginHostModel.PlanUiContributions(new[]
        {
            new DBuilderPluginDescriptor(
                "BuilderModes",
                "/plugins/buildermodes.dll",
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Toolbar, "builder.draw.toolbar", "Draw", ActionId: "builder.draw.action"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "builder.draw.menu", "Draw", ActionId: "builder.draw.action"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "builder.draw.action", "Draw action")
                }),
            new DBuilderPluginDescriptor(
                "CommentsPanel",
                "/plugins/comments.dll",
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Toolbar, "comments.open.toolbar", "Open Comments", ActionId: "comments.open.action"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "comments.open.menu", "Open Comments", ActionId: "comments.open.action")
                })
        });

        Assert.Empty(plan.Warnings);
        Assert.Collection(
            plan.Menus,
            contribution =>
            {
                Assert.Equal("BuilderModes", contribution.PluginName);
                Assert.Equal(DBuilderPluginContributionKind.Menu, contribution.Kind);
                Assert.Equal("builder.draw.menu", contribution.Id);
                Assert.Equal("Draw", contribution.Title);
                Assert.Equal("builder.draw.action", contribution.ActionId);
            },
            contribution =>
            {
                Assert.Equal("CommentsPanel", contribution.PluginName);
                Assert.Equal(DBuilderPluginContributionKind.Menu, contribution.Kind);
                Assert.Equal("comments.open.menu", contribution.Id);
                Assert.Equal("Open Comments", contribution.Title);
            });
        Assert.Collection(
            plan.Toolbars,
            contribution =>
            {
                Assert.Equal("BuilderModes", contribution.PluginName);
                Assert.Equal(DBuilderPluginContributionKind.Toolbar, contribution.Kind);
                Assert.Equal("builder.draw.toolbar", contribution.Id);
                Assert.Equal("Draw", contribution.Title);
                Assert.Equal("builder.draw.action", contribution.ActionId);
            },
            contribution =>
            {
                Assert.Equal("CommentsPanel", contribution.PluginName);
                Assert.Equal(DBuilderPluginContributionKind.Toolbar, contribution.Kind);
                Assert.Equal("comments.open.toolbar", contribution.Id);
                Assert.Equal("Open Comments", contribution.Title);
            });
    }

    [Fact]
    public void PlanUiContributionsUsesNormalizedPluginAndContributionRows()
    {
        var plan = DBuilderPluginHostModel.PlanUiContributions(new[]
        {
            new DBuilderPluginDescriptor(
                "  CommentsPanel  ",
                " /plugins/comments.dll ",
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, " comments.open ", " Open Comments ", ActionId: " comments.open.action "),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "comments.open", "Duplicate"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Toolbar, "", "Missing Id"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Toolbar, "comments.toolbar", "")
                })
        });

        DBuilderPluginUiContribution contribution = Assert.Single(plan.Menus);
        Assert.Empty(plan.Toolbars);
        Assert.Equal("CommentsPanel", contribution.PluginName);
        Assert.Equal("comments.open", contribution.Id);
        Assert.Equal("Open Comments", contribution.Title);
        Assert.Equal("comments.open.action", contribution.ActionId);
    }

    [Fact]
    public void PlanUiCommandResolvesMenuAndToolbarActionBindings()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "BuilderModes",
                    "/plugins/buildermodes.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "builder.draw.action", "Draw action"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "builder.draw.menu", "Draw", ActionId: "builder.draw.action"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Toolbar, "builder.draw.toolbar", "Draw", ActionId: "builder.draw.action")
                    })
            },
            new DBuilderPluginLifecycleRequest());

        DBuilderPluginUiCommandPlan menu = DBuilderPluginHostModel.PlanUiCommand(hostPlan, " builder.draw.menu ");
        DBuilderPluginUiCommandPlan toolbar = DBuilderPluginHostModel.PlanUiCommand(hostPlan, "builder.draw.toolbar");

        Assert.Empty(menu.Diagnostics);
        Assert.NotNull(menu.UiContribution);
        Assert.Equal("builder.draw.menu", menu.UiContribution.Id);
        Assert.NotNull(menu.Action);
        Assert.Equal("builder.draw.action", menu.Action.Id);
        Assert.Empty(toolbar.Diagnostics);
        Assert.NotNull(toolbar.UiContribution);
        Assert.Equal(DBuilderPluginContributionKind.Toolbar, toolbar.UiContribution.Kind);
        Assert.NotNull(toolbar.Action);
        Assert.Equal("builder.draw.action", toolbar.Action.Id);
    }

    [Fact]
    public void PlanUiCommandReportsMissingAmbiguousAndUnboundUiContributions()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "BuilderModes",
                    "/plugins/buildermodes.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "builder.draw.action", "Draw action"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "shared.ui", "Draw", ActionId: "builder.draw.action"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "unbound.ui", "Unbound"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Toolbar, "missing.action.ui", "Missing", ActionId: "missing.action")
                    }),
                new DBuilderPluginDescriptor(
                    "CommentsPanel",
                    "/plugins/comments.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Toolbar, "shared.ui", "Open Comments", ActionId: "builder.draw.action")
                    })
            },
            new DBuilderPluginLifecycleRequest());

        DBuilderPluginUiCommandPlan missingId = DBuilderPluginHostModel.PlanUiCommand(hostPlan, " ");
        DBuilderPluginUiCommandPlan missingUi = DBuilderPluginHostModel.PlanUiCommand(hostPlan, "missing.ui");
        DBuilderPluginUiCommandPlan ambiguous = DBuilderPluginHostModel.PlanUiCommand(hostPlan, "shared.ui");
        DBuilderPluginUiCommandPlan unbound = DBuilderPluginHostModel.PlanUiCommand(hostPlan, "unbound.ui");
        DBuilderPluginUiCommandPlan missingAction = DBuilderPluginHostModel.PlanUiCommand(hostPlan, "missing.action.ui");

        Assert.Null(missingId.UiContribution);
        Assert.Equal("Plugin UI contribution id is missing.", Assert.Single(missingId.Diagnostics).Message);
        Assert.Null(missingUi.UiContribution);
        Assert.Equal("Plugin UI contribution missing.ui was not found.", Assert.Single(missingUi.Diagnostics).Message);
        Assert.Null(ambiguous.UiContribution);
        Assert.Equal("Plugin UI contribution shared.ui is ambiguous.", Assert.Single(ambiguous.Diagnostics).Message);
        Assert.NotNull(unbound.UiContribution);
        Assert.Null(unbound.Action);
        Assert.Equal("Plugin UI contribution unbound.ui does not specify an action id.", Assert.Single(unbound.Diagnostics).Message);
        Assert.NotNull(missingAction.UiContribution);
        Assert.Null(missingAction.Action);
        Assert.Equal("Plugin action missing.action was not found.", Assert.Single(missingAction.Diagnostics).Message);
    }

    [Fact]
    public void PlanUiContributionsSkipsInvalidOrDisabledPlugins()
    {
        var plan = DBuilderPluginHostModel.PlanUiContributions(new[]
        {
            new DBuilderPluginDescriptor(
                "",
                "/plugins/missing.dll",
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "missing.menu", "Missing")
                }),
            new DBuilderPluginDescriptor(
                "TagRange",
                "/plugins/tagrange.dll",
                Enabled: false,
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Toolbar, "tagrange.toolbar", "Tag Range")
                })
        });

        Assert.Empty(plan.Menus);
        Assert.Empty(plan.Toolbars);
        Assert.Equal(new[] { "Plugin TagRange is disabled." }, plan.Warnings);
    }

    [Fact]
    public void PlanResourceHandlersCollectsResourceContributionsInStableOrder()
    {
        var plan = DBuilderPluginHostModel.PlanResourceHandlers(new[]
        {
            new DBuilderPluginDescriptor(
                "ZipResource",
                "/plugins/zipresource.dll",
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "resource.zip", "Zip archives"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "resource.menu", "Resource menu")
                }),
            new DBuilderPluginDescriptor(
                "DirectoryResource",
                "/plugins/directoryresource.dll",
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "resource.directory", "Directory resources")
                })
        });

        Assert.Empty(plan.Warnings);
        Assert.Collection(
            plan.Handlers,
            handler =>
            {
                Assert.Equal("DirectoryResource", handler.PluginName);
                Assert.Equal("resource.directory", handler.Id);
                Assert.Equal("Directory resources", handler.Title);
            },
            handler =>
            {
                Assert.Equal("ZipResource", handler.PluginName);
                Assert.Equal("resource.zip", handler.Id);
                Assert.Equal("Zip archives", handler.Title);
            });
    }

    [Fact]
    public void PlanResourceHandlersUsesNormalizedRowsAndWarnings()
    {
        var plan = DBuilderPluginHostModel.PlanResourceHandlers(new[]
        {
            new DBuilderPluginDescriptor(
                "  ZipResource  ",
                " /plugins/zipresource.dll ",
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, " resource.zip ", " Zip archives ", " OpenZipResource "),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "resource.zip", "Duplicate"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "", "Missing Id"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "resource.empty", "")
                }),
            new DBuilderPluginDescriptor(
                "DisabledResource",
                "/plugins/disabled.dll",
                Enabled: false,
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "resource.disabled", "Disabled")
                })
        });

        DBuilderPluginResourceHandler handler = Assert.Single(plan.Handlers);
        Assert.Equal("ZipResource", handler.PluginName);
        Assert.Equal("resource.zip", handler.Id);
        Assert.Equal("Zip archives", handler.Title);
        Assert.Equal("OpenZipResource", handler.MethodName);
        Assert.Equal(new[] { "Plugin DisabledResource is disabled." }, plan.Warnings);
    }

    [Fact]
    public void PlanResourceHandlerCommandResolvesResourceHandlerById()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "ZipResource",
                    "/plugins/zipresource.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "resource.zip", "Zip archives", "OpenZipResource")
                    }),
                new DBuilderPluginDescriptor(
                    "DirectoryResource",
                    "/plugins/directoryresource.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "resource.directory", "Directory resources")
                    })
            },
            new DBuilderPluginLifecycleRequest());

        DBuilderPluginResourceHandlerCommandPlan command = DBuilderPluginHostModel.PlanResourceHandlerCommand(
            hostPlan,
            " RESOURCE.ZIP ");

        Assert.Empty(command.Diagnostics);
        Assert.NotNull(command.Handler);
        Assert.Equal("ZipResource", command.Handler.PluginName);
        Assert.Equal("resource.zip", command.Handler.Id);
        Assert.Equal("Zip archives", command.Handler.Title);
        Assert.Equal("OpenZipResource", command.Handler.MethodName);
    }

    [Fact]
    public void PlanResourceHandlerCommandReportsMissingAndAmbiguousHandlers()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "ZipResource",
                    "/plugins/zipresource.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "resource.shared", "Zip archives")
                    }),
                new DBuilderPluginDescriptor(
                    "DirectoryResource",
                    "/plugins/directoryresource.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "resource.shared", "Directory resources")
                    })
            },
            new DBuilderPluginLifecycleRequest());

        DBuilderPluginResourceHandlerCommandPlan missingId = DBuilderPluginHostModel.PlanResourceHandlerCommand(hostPlan, " ");
        DBuilderPluginResourceHandlerCommandPlan missingHandler = DBuilderPluginHostModel.PlanResourceHandlerCommand(hostPlan, "resource.missing");
        DBuilderPluginResourceHandlerCommandPlan ambiguous = DBuilderPluginHostModel.PlanResourceHandlerCommand(hostPlan, "resource.shared");

        Assert.Null(missingId.Handler);
        Assert.Equal("Plugin resource handler id is missing.", Assert.Single(missingId.Diagnostics).Message);
        Assert.Null(missingHandler.Handler);
        Assert.Equal("Plugin resource handler resource.missing was not found.", Assert.Single(missingHandler.Diagnostics).Message);
        Assert.Null(ambiguous.Handler);
        Assert.Equal("Plugin resource handler resource.shared is ambiguous.", Assert.Single(ambiguous.Diagnostics).Message);
    }

    [Fact]
    public void PlanApiContributionsSeparatesActionsEditModesAndDockers()
    {
        var plan = DBuilderPluginHostModel.PlanApiContributions(new[]
        {
            new DBuilderPluginDescriptor(
                "BuilderModes",
                "/plugins/buildermodes.dll",
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "builder.draw.action", "Draw action"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.EditMode, "builder.draw.mode", "Draw mode"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Docker, "builder.tags.docker", "Tags docker"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "builder.draw.menu", "Draw menu")
                }),
            new DBuilderPluginDescriptor(
                "CommentsPanel",
                "/plugins/comments.dll",
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "comments.open.action", "Open Comments"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Docker, "comments.panel.docker", "Comments panel")
                })
        });

        Assert.Empty(plan.Warnings);
        Assert.Collection(
            plan.Actions,
            contribution =>
            {
                Assert.Equal("BuilderModes", contribution.PluginName);
                Assert.Equal(DBuilderPluginContributionKind.Action, contribution.Kind);
                Assert.Equal("builder.draw.action", contribution.Id);
            },
            contribution =>
            {
                Assert.Equal("CommentsPanel", contribution.PluginName);
                Assert.Equal(DBuilderPluginContributionKind.Action, contribution.Kind);
                Assert.Equal("comments.open.action", contribution.Id);
            });
        DBuilderPluginApiContribution editMode = Assert.Single(plan.EditModes);
        Assert.Equal("BuilderModes", editMode.PluginName);
        Assert.Equal(DBuilderPluginContributionKind.EditMode, editMode.Kind);
        Assert.Equal("builder.draw.mode", editMode.Id);
        Assert.Collection(
            plan.Dockers,
            contribution =>
            {
                Assert.Equal("BuilderModes", contribution.PluginName);
                Assert.Equal(DBuilderPluginContributionKind.Docker, contribution.Kind);
                Assert.Equal("builder.tags.docker", contribution.Id);
            },
            contribution =>
            {
                Assert.Equal("CommentsPanel", contribution.PluginName);
                Assert.Equal(DBuilderPluginContributionKind.Docker, contribution.Kind);
                Assert.Equal("comments.panel.docker", contribution.Id);
            });
    }

    [Fact]
    public void PlanApiContributionsUsesNormalizedRowsAndWarnings()
    {
        var plan = DBuilderPluginHostModel.PlanApiContributions(new[]
        {
            new DBuilderPluginDescriptor(
                "  BuilderModes  ",
                " /plugins/buildermodes.dll ",
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, " builder.draw.action ", " Draw action ", " DrawAction "),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "builder.draw.action", "Duplicate"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.EditMode, "", "Missing Id"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Docker, "builder.empty", "")
                }),
            new DBuilderPluginDescriptor(
                "DisabledModes",
                "/plugins/disabled.dll",
                Enabled: false,
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "disabled.action", "Disabled")
                })
        });

        DBuilderPluginApiContribution action = Assert.Single(plan.Actions);
        Assert.Empty(plan.EditModes);
        Assert.Empty(plan.Dockers);
        Assert.Equal("BuilderModes", action.PluginName);
        Assert.Equal("builder.draw.action", action.Id);
        Assert.Equal("Draw action", action.Title);
        Assert.Equal("DrawAction", action.MethodName);
        Assert.Equal(new[] { "Plugin DisabledModes is disabled." }, plan.Warnings);
    }

    [Fact]
    public void PlanEditModeAndDockerCommandsResolveApiContributionsById()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "BuilderModes",
                    "/plugins/buildermodes.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.EditMode, "builder.draw.mode", "Draw mode"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Docker, "builder.tags.docker", "Tags docker")
                    }),
                new DBuilderPluginDescriptor(
                    "CommentsPanel",
                    "/plugins/comments.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Docker, "comments.panel.docker", "Comments panel")
                    })
            },
            new DBuilderPluginLifecycleRequest());

        DBuilderPluginEditModeCommandPlan editMode = DBuilderPluginHostModel.PlanEditModeCommand(
            hostPlan,
            " BUILDER.DRAW.MODE ");
        DBuilderPluginDockerCommandPlan docker = DBuilderPluginHostModel.PlanDockerCommand(
            hostPlan,
            "comments.panel.docker");

        Assert.Empty(editMode.Diagnostics);
        Assert.NotNull(editMode.EditMode);
        Assert.Equal("BuilderModes", editMode.EditMode.PluginName);
        Assert.Equal("builder.draw.mode", editMode.EditMode.Id);
        Assert.Equal("Draw mode", editMode.EditMode.Title);
        Assert.Empty(docker.Diagnostics);
        Assert.NotNull(docker.Docker);
        Assert.Equal("CommentsPanel", docker.Docker.PluginName);
        Assert.Equal("comments.panel.docker", docker.Docker.Id);
        Assert.Equal("Comments panel", docker.Docker.Title);
    }

    [Fact]
    public void PlanEditModeCommandReportsMissingAndAmbiguousEditModes()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "BuilderModes",
                    "/plugins/buildermodes.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.EditMode, "shared.mode", "Draw mode")
                    }),
                new DBuilderPluginDescriptor(
                    "VisualModes",
                    "/plugins/visualmodes.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.EditMode, "shared.mode", "Visual mode")
                    })
            },
            new DBuilderPluginLifecycleRequest());

        DBuilderPluginEditModeCommandPlan missingId = DBuilderPluginHostModel.PlanEditModeCommand(hostPlan, " ");
        DBuilderPluginEditModeCommandPlan missingEditMode = DBuilderPluginHostModel.PlanEditModeCommand(hostPlan, "missing.mode");
        DBuilderPluginEditModeCommandPlan ambiguous = DBuilderPluginHostModel.PlanEditModeCommand(hostPlan, "shared.mode");

        Assert.Null(missingId.EditMode);
        Assert.Equal("Plugin edit mode id is missing.", Assert.Single(missingId.Diagnostics).Message);
        Assert.Null(missingEditMode.EditMode);
        Assert.Equal("Plugin edit mode missing.mode was not found.", Assert.Single(missingEditMode.Diagnostics).Message);
        Assert.Null(ambiguous.EditMode);
        Assert.Equal("Plugin edit mode shared.mode is ambiguous.", Assert.Single(ambiguous.Diagnostics).Message);
    }

    [Fact]
    public void PlanDockerCommandReportsMissingAndAmbiguousDockers()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "BuilderModes",
                    "/plugins/buildermodes.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Docker, "shared.docker", "Tags docker")
                    }),
                new DBuilderPluginDescriptor(
                    "CommentsPanel",
                    "/plugins/comments.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Docker, "shared.docker", "Comments panel")
                    })
            },
            new DBuilderPluginLifecycleRequest());

        DBuilderPluginDockerCommandPlan missingId = DBuilderPluginHostModel.PlanDockerCommand(hostPlan, " ");
        DBuilderPluginDockerCommandPlan missingDocker = DBuilderPluginHostModel.PlanDockerCommand(hostPlan, "missing.docker");
        DBuilderPluginDockerCommandPlan ambiguous = DBuilderPluginHostModel.PlanDockerCommand(hostPlan, "shared.docker");

        Assert.Null(missingId.Docker);
        Assert.Equal("Plugin docker id is missing.", Assert.Single(missingId.Diagnostics).Message);
        Assert.Null(missingDocker.Docker);
        Assert.Equal("Plugin docker missing.docker was not found.", Assert.Single(missingDocker.Diagnostics).Message);
        Assert.Null(ambiguous.Docker);
        Assert.Equal("Plugin docker shared.docker is ambiguous.", Assert.Single(ambiguous.Diagnostics).Message);
    }

    [Fact]
    public void PlanActionCommandResolvesActionContributionById()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "BuilderModes",
                    "/plugins/buildermodes.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "builder.draw.action", "Draw action"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "builder.draw.menu", "Draw menu")
                    }),
                new DBuilderPluginDescriptor(
                    "CommentsPanel",
                    "/plugins/comments.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "comments.open.action", "Open Comments")
                    })
            },
            new DBuilderPluginLifecycleRequest());

        DBuilderPluginActionCommandPlan command = DBuilderPluginHostModel.PlanActionCommand(
            hostPlan,
            " comments.open.action ");

        Assert.Empty(command.Diagnostics);
        Assert.NotNull(command.Action);
        Assert.Equal("CommentsPanel", command.Action.PluginName);
        Assert.Equal("comments.open.action", command.Action.Id);
        Assert.Equal("Open Comments", command.Action.Title);
    }

    [Fact]
    public void PlanActionCommandReportsMissingAndAmbiguousActions()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "BuilderModes",
                    "/plugins/buildermodes.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "shared.action", "Draw action")
                    }),
                new DBuilderPluginDescriptor(
                    "CommentsPanel",
                    "/plugins/comments.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "shared.action", "Open Comments")
                    })
            },
            new DBuilderPluginLifecycleRequest());

        DBuilderPluginActionCommandPlan missingId = DBuilderPluginHostModel.PlanActionCommand(hostPlan, " ");
        DBuilderPluginActionCommandPlan missingAction = DBuilderPluginHostModel.PlanActionCommand(hostPlan, "missing.action");
        DBuilderPluginActionCommandPlan ambiguous = DBuilderPluginHostModel.PlanActionCommand(hostPlan, "shared.action");

        Assert.Null(missingId.Action);
        Assert.Equal("Plugin action id is missing.", Assert.Single(missingId.Diagnostics).Message);
        Assert.Null(missingAction.Action);
        Assert.Equal("Plugin action missing.action was not found.", Assert.Single(missingAction.Diagnostics).Message);
        Assert.Null(ambiguous.Action);
        Assert.Equal("Plugin action shared.action is ambiguous.", Assert.Single(ambiguous.Diagnostics).Message);
    }

    [Fact]
    public void ExecuteReflectionActionCommandInvokesConfiguredActionMethod()
    {
        ReflectionActionCommandPlugin.Calls.Clear();
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "ActionPlugin",
                    "/plugins/actionplugin.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(
                            DBuilderPluginContributionKind.Action,
                            "action.run",
                            "Run Action",
                            "RunAction")
                    })
            },
            new DBuilderPluginLifecycleRequest());
        DBuilderPluginRuntimeInstancePlan instancePlan = RuntimeInstancePlan(
            new ReflectionActionCommandPlugin("ActionPlugin"),
            "ActionPlugin");

        DBuilderPluginActionExecutionResult result = DBuilderPluginHostModel.ExecuteReflectionActionCommand(
            instancePlan,
            hostPlan,
            "action.run");

        Assert.True(result.Completed);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Action);
        Assert.Equal("RunAction", result.Action.MethodName);
        Assert.Equal(new[] { "ActionPlugin:RunAction" }, ReflectionActionCommandPlugin.Calls);
    }

    [Fact]
    public void ExecuteReflectionActionCommandReportsInactiveMissingAndBadActionMethods()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "ActionPlugin",
                    "/plugins/actionplugin.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "action.no-method", "No Method"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "action.missing", "Missing", "MissingAction"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "action.bad", "Bad", "BadAction"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "action.throw", "Throw", "ThrowAction")
                    })
            },
            new DBuilderPluginLifecycleRequest());
        DBuilderPluginRuntimeInstancePlan instancePlan = RuntimeInstancePlan(
            new ReflectionActionCommandPlugin("ActionPlugin"),
            "ActionPlugin");

        DBuilderPluginActionExecutionResult noMethod = DBuilderPluginHostModel.ExecuteReflectionActionCommand(
            instancePlan,
            hostPlan,
            "action.no-method");
        DBuilderPluginActionExecutionResult missing = DBuilderPluginHostModel.ExecuteReflectionActionCommand(
            instancePlan,
            hostPlan,
            "action.missing");
        DBuilderPluginActionExecutionResult bad = DBuilderPluginHostModel.ExecuteReflectionActionCommand(
            instancePlan,
            hostPlan,
            "action.bad");
        DBuilderPluginActionExecutionResult throwing = DBuilderPluginHostModel.ExecuteReflectionActionCommand(
            instancePlan,
            hostPlan,
            "action.throw");
        DBuilderPluginActionExecutionResult inactive = DBuilderPluginHostModel.ExecuteReflectionActionCommand(
            new DBuilderPluginRuntimeInstancePlan(Array.Empty<DBuilderPluginRuntimeInstance>(), Array.Empty<DBuilderPluginDiagnostic>()),
            hostPlan,
            "action.missing");

        Assert.False(noMethod.Completed);
        Assert.Equal("Plugin action action.no-method does not specify a method name.", Assert.Single(noMethod.Diagnostics).Message);
        Assert.False(missing.Completed);
        Assert.Equal("Plugin action action.missing method MissingAction was not found.", Assert.Single(missing.Diagnostics).Message);
        Assert.False(bad.Completed);
        Assert.Equal("Plugin action action.bad method BadAction must be public void with no parameters.", Assert.Single(bad.Diagnostics).Message);
        Assert.False(throwing.Completed);
        Assert.Equal("action failed", Assert.Single(throwing.Diagnostics).Message);
        Assert.False(inactive.Completed);
        Assert.Equal("Plugin ActionPlugin is not active.", Assert.Single(inactive.Diagnostics).Message);
    }

    [Fact]
    public void ExecuteReflectionEditModeAndDockerCommandsInvokeConfiguredMethods()
    {
        ReflectionApiCommandPlugin.Calls.Clear();
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "ApiPlugin",
                    "/plugins/apiplugin.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.EditMode, "mode.draw", "Draw Mode", "OpenEditMode"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Docker, "docker.tags", "Tags Docker", "OpenDocker")
                    })
            },
            new DBuilderPluginLifecycleRequest());
        DBuilderPluginRuntimeInstancePlan instancePlan = RuntimeInstancePlan(
            new ReflectionApiCommandPlugin("ApiPlugin"),
            "ApiPlugin");

        DBuilderPluginEditModeExecutionResult editMode = DBuilderPluginHostModel.ExecuteReflectionEditModeCommand(
            instancePlan,
            hostPlan,
            "mode.draw");
        DBuilderPluginDockerExecutionResult docker = DBuilderPluginHostModel.ExecuteReflectionDockerCommand(
            instancePlan,
            hostPlan,
            "docker.tags");

        Assert.True(editMode.Completed);
        Assert.Empty(editMode.Diagnostics);
        Assert.NotNull(editMode.EditMode);
        Assert.Equal("mode.draw", editMode.EditMode.Id);
        Assert.True(docker.Completed);
        Assert.Empty(docker.Diagnostics);
        Assert.NotNull(docker.Docker);
        Assert.Equal("docker.tags", docker.Docker.Id);
        Assert.Equal(new[] { "ApiPlugin:OpenEditMode", "ApiPlugin:OpenDocker" }, ReflectionApiCommandPlugin.Calls);
    }

    [Fact]
    public void ExecuteReflectionEditModeAndDockerCommandsReportMethodFailures()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "ApiPlugin",
                    "/plugins/apiplugin.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.EditMode, "mode.no-method", "No Method"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.EditMode, "mode.bad", "Bad Mode", "BadMode"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Docker, "docker.missing", "Missing Docker", "MissingDocker"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Docker, "docker.throw", "Throw Docker", "ThrowDocker")
                    })
            },
            new DBuilderPluginLifecycleRequest());
        DBuilderPluginRuntimeInstancePlan instancePlan = RuntimeInstancePlan(
            new ReflectionApiCommandPlugin("ApiPlugin"),
            "ApiPlugin");

        DBuilderPluginEditModeExecutionResult noMethod = DBuilderPluginHostModel.ExecuteReflectionEditModeCommand(
            instancePlan,
            hostPlan,
            "mode.no-method");
        DBuilderPluginEditModeExecutionResult bad = DBuilderPluginHostModel.ExecuteReflectionEditModeCommand(
            instancePlan,
            hostPlan,
            "mode.bad");
        DBuilderPluginDockerExecutionResult missing = DBuilderPluginHostModel.ExecuteReflectionDockerCommand(
            instancePlan,
            hostPlan,
            "docker.missing");
        DBuilderPluginDockerExecutionResult throwing = DBuilderPluginHostModel.ExecuteReflectionDockerCommand(
            instancePlan,
            hostPlan,
            "docker.throw");

        Assert.False(noMethod.Completed);
        Assert.Equal("Plugin edit mode mode.no-method does not specify a method name.", Assert.Single(noMethod.Diagnostics).Message);
        Assert.False(bad.Completed);
        Assert.Equal("Plugin edit mode mode.bad method BadMode must be public void with no parameters.", Assert.Single(bad.Diagnostics).Message);
        Assert.False(missing.Completed);
        Assert.Equal("Plugin docker docker.missing method MissingDocker was not found.", Assert.Single(missing.Diagnostics).Message);
        Assert.False(throwing.Completed);
        Assert.Equal("docker failed", Assert.Single(throwing.Diagnostics).Message);
    }

    [Fact]
    public void ExecuteReflectionResourceHandlerCommandInvokesConfiguredMethod()
    {
        ReflectionResourceHandlerCommandPlugin.Calls.Clear();
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "ResourcePlugin",
                    "/plugins/resourceplugin.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(
                            DBuilderPluginContributionKind.ResourceHandler,
                            "resource.zip",
                            "Zip Resource",
                            "OpenZipResource")
                    })
            },
            new DBuilderPluginLifecycleRequest());
        DBuilderPluginRuntimeInstancePlan instancePlan = RuntimeInstancePlan(
            new ReflectionResourceHandlerCommandPlugin("ResourcePlugin"),
            "ResourcePlugin");

        DBuilderPluginResourceHandlerExecutionResult result = DBuilderPluginHostModel.ExecuteReflectionResourceHandlerCommand(
            instancePlan,
            hostPlan,
            "resource.zip");

        Assert.True(result.Completed);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Handler);
        Assert.Equal("resource.zip", result.Handler.Id);
        Assert.Equal("OpenZipResource", result.Handler.MethodName);
        Assert.Equal(new[] { "ResourcePlugin:OpenZipResource" }, ReflectionResourceHandlerCommandPlugin.Calls);
    }

    [Fact]
    public void ExecuteReflectionResourceHandlerCommandReportsMethodFailures()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "ResourcePlugin",
                    "/plugins/resourceplugin.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "resource.no-method", "No Method"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "resource.missing", "Missing", "MissingResource"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "resource.bad", "Bad", "BadResource"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, "resource.throw", "Throw", "ThrowResource")
                    })
            },
            new DBuilderPluginLifecycleRequest());
        DBuilderPluginRuntimeInstancePlan instancePlan = RuntimeInstancePlan(
            new ReflectionResourceHandlerCommandPlugin("ResourcePlugin"),
            "ResourcePlugin");

        DBuilderPluginResourceHandlerExecutionResult noMethod = DBuilderPluginHostModel.ExecuteReflectionResourceHandlerCommand(
            instancePlan,
            hostPlan,
            "resource.no-method");
        DBuilderPluginResourceHandlerExecutionResult missing = DBuilderPluginHostModel.ExecuteReflectionResourceHandlerCommand(
            instancePlan,
            hostPlan,
            "resource.missing");
        DBuilderPluginResourceHandlerExecutionResult bad = DBuilderPluginHostModel.ExecuteReflectionResourceHandlerCommand(
            instancePlan,
            hostPlan,
            "resource.bad");
        DBuilderPluginResourceHandlerExecutionResult throwing = DBuilderPluginHostModel.ExecuteReflectionResourceHandlerCommand(
            instancePlan,
            hostPlan,
            "resource.throw");

        Assert.False(noMethod.Completed);
        Assert.Equal("Plugin resource handler resource.no-method does not specify a method name.", Assert.Single(noMethod.Diagnostics).Message);
        Assert.False(missing.Completed);
        Assert.Equal("Plugin resource handler resource.missing method MissingResource was not found.", Assert.Single(missing.Diagnostics).Message);
        Assert.False(bad.Completed);
        Assert.Equal("Plugin resource handler resource.bad method BadResource must be public void with no parameters.", Assert.Single(bad.Diagnostics).Message);
        Assert.False(throwing.Completed);
        Assert.Equal("resource failed", Assert.Single(throwing.Diagnostics).Message);
    }

    [Fact]
    public void ExecuteReflectionUiCommandInvokesBoundActionMethod()
    {
        ReflectionActionCommandPlugin.Calls.Clear();
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "ActionPlugin",
                    "/plugins/actionplugin.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "action.run", "Run Action", "RunAction"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "action.run.menu", "Run Action", ActionId: "action.run"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Toolbar, "action.run.toolbar", "Run Action", ActionId: "action.run")
                    })
            },
            new DBuilderPluginLifecycleRequest());
        DBuilderPluginRuntimeInstancePlan instancePlan = RuntimeInstancePlan(
            new ReflectionActionCommandPlugin("ActionPlugin"),
            "ActionPlugin");

        DBuilderPluginUiExecutionResult menu = DBuilderPluginHostModel.ExecuteReflectionUiCommand(
            instancePlan,
            hostPlan,
            "action.run.menu");
        DBuilderPluginUiExecutionResult toolbar = DBuilderPluginHostModel.ExecuteReflectionUiCommand(
            instancePlan,
            hostPlan,
            "action.run.toolbar");

        Assert.True(menu.Completed);
        Assert.Empty(menu.Diagnostics);
        Assert.NotNull(menu.UiContribution);
        Assert.Equal("action.run.menu", menu.UiContribution.Id);
        Assert.NotNull(menu.Action);
        Assert.Equal("action.run", menu.Action.Id);
        Assert.True(toolbar.Completed);
        Assert.Empty(toolbar.Diagnostics);
        Assert.NotNull(toolbar.UiContribution);
        Assert.Equal(DBuilderPluginContributionKind.Toolbar, toolbar.UiContribution.Kind);
        Assert.Equal(new[] { "ActionPlugin:RunAction", "ActionPlugin:RunAction" }, ReflectionActionCommandPlugin.Calls);
    }

    [Fact]
    public void ExecuteReflectionUiCommandReportsUiBindingAndActionFailures()
    {
        DBuilderPluginHostPlan hostPlan = DBuilderPluginHostModel.BuildHostPlan(
            new[]
            {
                new DBuilderPluginDescriptor(
                    "ActionPlugin",
                    "/plugins/actionplugin.dll",
                    Contributions: new[]
                    {
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "action.no-method", "No Method"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "action.no-method.menu", "No Method", ActionId: "action.no-method"),
                        new DBuilderPluginContribution(DBuilderPluginContributionKind.Toolbar, "unbound.toolbar", "Unbound")
                    })
            },
            new DBuilderPluginLifecycleRequest());
        DBuilderPluginRuntimeInstancePlan instancePlan = RuntimeInstancePlan(
            new ReflectionActionCommandPlugin("ActionPlugin"),
            "ActionPlugin");

        DBuilderPluginUiExecutionResult unbound = DBuilderPluginHostModel.ExecuteReflectionUiCommand(
            instancePlan,
            hostPlan,
            "unbound.toolbar");
        DBuilderPluginUiExecutionResult actionFailure = DBuilderPluginHostModel.ExecuteReflectionUiCommand(
            instancePlan,
            hostPlan,
            "action.no-method.menu");

        Assert.False(unbound.Completed);
        Assert.NotNull(unbound.UiContribution);
        Assert.Null(unbound.Action);
        Assert.Equal("Plugin UI contribution unbound.toolbar does not specify an action id.", Assert.Single(unbound.Diagnostics).Message);
        Assert.False(actionFailure.Completed);
        Assert.NotNull(actionFailure.UiContribution);
        Assert.NotNull(actionFailure.Action);
        Assert.Equal("Plugin action action.no-method does not specify a method name.", Assert.Single(actionFailure.Diagnostics).Message);
    }

    [Fact]
    public void NormalizeSettingsStoreTrimsPluginsAndSettingsWithoutDroppingUnknownValues()
    {
        var settings = DBuilderPluginHostModel.NormalizeSettingsStore(new Dictionary<string, Dictionary<string, object?>>
        {
            ["  TagRange  "] = new()
            {
                [" enabled "] = true,
                ["Enabled"] = false,
                [""] = "ignored"
            },
            ["tagrange"] = new()
            {
                ["duplicate"] = true
            },
            [""] = new()
            {
                ["ignored"] = true
            },
            ["CommentsPanel"] = new()
            {
                ["dock"] = "left"
            }
        });

        Assert.Equal(new[] { "CommentsPanel", "TagRange" }, settings.Keys.ToArray());
        Assert.Equal("left", settings["CommentsPanel"]["dock"]);
        Assert.Single(settings["TagRange"]);
        Assert.Equal(true, settings["TagRange"]["enabled"]);
    }

    [Fact]
    public void PlanReflectionSettingDescriptorsReadsAndNormalizesDescriptorProperty()
    {
        DBuilderPluginSettingDescriptorPlan descriptorPlan = DBuilderPluginHostModel.PlanReflectionSettingDescriptors(
            RuntimeInstance(new ReflectionSettingsPlugin(), "SettingsPlugin", 0));
        var settings = new Dictionary<string, Dictionary<string, object?>>
        {
            ["settingsplugin"] = new()
            {
                ["settings.step"] = 16
            }
        };

        DBuilderPluginSettingsSnapshot snapshot = DBuilderPluginHostModel.PlanSettings(
            new DBuilderPluginDescriptor("SettingsPlugin", "/plugins/settings.dll"),
            settings,
            descriptorPlan.Settings);

        Assert.Equal("SettingsPlugin", descriptorPlan.PluginName);
        Assert.Empty(descriptorPlan.Diagnostics);
        Assert.Collection(
            descriptorPlan.Settings,
            descriptor =>
            {
                Assert.Equal("settings.enabled", descriptor.Key);
                Assert.Equal(true, descriptor.DefaultValue);
            },
            descriptor =>
            {
                Assert.Equal("settings.step", descriptor.Key);
                Assert.Equal(8, descriptor.DefaultValue);
            });
        Assert.Equal(true, snapshot.Values["settings.enabled"]);
        Assert.Equal(16, snapshot.Values["settings.step"]);
    }

    [Fact]
    public void PlanReflectionSettingDescriptorsTreatsMissingDescriptorsAsEmpty()
    {
        DBuilderPluginSettingDescriptorPlan descriptorPlan = DBuilderPluginHostModel.PlanReflectionSettingDescriptors(
            RuntimeInstance(new ReflectionPluginHostTestPlugin(), "NoSettings", 0));

        Assert.Equal("NoSettings", descriptorPlan.PluginName);
        Assert.Empty(descriptorPlan.Settings);
        Assert.Empty(descriptorPlan.Diagnostics);
    }

    [Fact]
    public void PlanReflectionSettingDescriptorsReportsUnsupportedAndThrowingProperties()
    {
        DBuilderPluginSettingDescriptorPlan unsupported = DBuilderPluginHostModel.PlanReflectionSettingDescriptors(
            RuntimeInstance(new ReflectionUnsupportedSettingsPlugin(), "UnsupportedSettings", 0));
        DBuilderPluginSettingDescriptorPlan throwing = DBuilderPluginHostModel.PlanReflectionSettingDescriptors(
            RuntimeInstance(new ReflectionThrowingSettingsPlugin(), "ThrowingSettings", 0));

        Assert.Empty(unsupported.Settings);
        DBuilderPluginDiagnostic unsupportedDiagnostic = Assert.Single(unsupported.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, unsupportedDiagnostic.Severity);
        Assert.Equal("UnsupportedSettings", unsupportedDiagnostic.PluginName);
        Assert.Equal(
            "Plugin UnsupportedSettings SettingDescriptors property must return setting descriptors.",
            unsupportedDiagnostic.Message);
        Assert.Empty(throwing.Settings);
        DBuilderPluginDiagnostic throwingDiagnostic = Assert.Single(throwing.Diagnostics);
        Assert.Equal(DBuilderPluginDiagnosticSeverity.Error, throwingDiagnostic.Severity);
        Assert.Equal("ThrowingSettings", throwingDiagnostic.PluginName);
        Assert.Equal("settings failed", throwingDiagnostic.Message);
    }

    [Fact]
    public void PlanReflectionSettingsMergesDiscoveredDefaultsWithPersistedValues()
    {
        DBuilderPluginRuntimeInstance runtimeInstance = RuntimeInstance(new ReflectionSettingsPlugin(), "SettingsPlugin", 0);
        var descriptor = new DBuilderPluginDescriptor("SettingsPlugin", "/plugins/settings.dll");
        var settings = new Dictionary<string, Dictionary<string, object?>>
        {
            ["settingsplugin"] = new()
            {
                ["settings.step"] = 16,
                ["settings.extra"] = "preserved"
            }
        };

        DBuilderPluginSettingsSnapshot snapshot = DBuilderPluginHostModel.PlanReflectionSettings(
            runtimeInstance,
            descriptor,
            settings);

        Assert.Equal("SettingsPlugin", snapshot.PluginName);
        Assert.Empty(snapshot.Warnings);
        Assert.Equal(true, snapshot.Values["settings.enabled"]);
        Assert.Equal(16, snapshot.Values["settings.step"]);
        Assert.Equal("preserved", snapshot.Values["settings.extra"]);
    }

    [Fact]
    public void PlanReflectionSettingsPreservesDescriptorDiagnosticsAsWritebackWarnings()
    {
        DBuilderPluginRuntimeInstance runtimeInstance = RuntimeInstance(new ReflectionUnsupportedSettingsPlugin(), "UnsupportedSettings", 0);
        var descriptor = new DBuilderPluginDescriptor("UnsupportedSettings", "/plugins/settings.dll");
        var settings = new Dictionary<string, Dictionary<string, object?>>
        {
            ["UnsupportedSettings"] = new()
            {
                ["settings.step"] = 8
            }
        };

        DBuilderPluginSettingsSnapshot snapshot = DBuilderPluginHostModel.PlanReflectionSettings(
            runtimeInstance,
            descriptor,
            settings);

        DBuilderPluginHostModel.WriteSettings(settings, snapshot);

        Assert.Equal(
            new[] { "Plugin UnsupportedSettings SettingDescriptors property must return setting descriptors." },
            snapshot.Warnings);
        Assert.Equal(8, settings["UnsupportedSettings"]["settings.step"]);
    }

    [Fact]
    public void PlanSettingsMergesDescriptorDefaultsWithPersistedAndUnknownValues()
    {
        var descriptor = new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll");
        var settings = new Dictionary<string, Dictionary<string, object?>>
        {
            ["tagrange"] = new()
            {
                ["tagrange.step"] = 16,
                ["tagrange.extra"] = "preserved"
            }
        };

        DBuilderPluginSettingsSnapshot snapshot = DBuilderPluginHostModel.PlanSettings(
            descriptor,
            settings,
            new[]
            {
                new DBuilderPluginSettingDescriptor("tagrange.step", 8),
                new DBuilderPluginSettingDescriptor("tagrange.enabled", true),
                new DBuilderPluginSettingDescriptor("tagrange.step", 32),
                new DBuilderPluginSettingDescriptor("", "ignored")
            });

        Assert.Equal("TagRange", snapshot.PluginName);
        Assert.Empty(snapshot.Warnings);
        Assert.Equal(3, snapshot.Values.Count);
        Assert.Equal(true, snapshot.Values["tagrange.enabled"]);
        Assert.Equal(16, snapshot.Values["tagrange.step"]);
        Assert.Equal("preserved", snapshot.Values["tagrange.extra"]);
    }

    [Fact]
    public void PlanSettingsKeepsInvalidOrDisabledPluginsOutOfSettings()
    {
        DBuilderPluginSettingsSnapshot invalid = DBuilderPluginHostModel.PlanSettings(
            new DBuilderPluginDescriptor("", "/plugins/missing.dll"),
            null,
            new[] { new DBuilderPluginSettingDescriptor("setting", true) });

        Assert.Empty(invalid.Values);
        Assert.Equal(new[] { "Plugin name is missing." }, invalid.Warnings);

        DBuilderPluginSettingsSnapshot disabled = DBuilderPluginHostModel.PlanSettings(
            new DBuilderPluginDescriptor("TagRange", "/plugins/tagrange.dll", Enabled: false),
            null,
            new[] { new DBuilderPluginSettingDescriptor("setting", true) });

        Assert.Empty(disabled.Values);
        Assert.Equal(new[] { "Plugin TagRange is disabled." }, disabled.Warnings);
    }

    [Fact]
    public void WriteSettingsReplacesPluginSettingsCaseInsensitively()
    {
        var settings = new Dictionary<string, Dictionary<string, object?>>
        {
            ["tagrange"] = new()
            {
                ["tagrange.step"] = 8
            },
            ["CommentsPanel"] = new()
            {
                ["dock"] = "left"
            }
        };
        var snapshot = new DBuilderPluginSettingsSnapshot(
            " TagRange ",
            new Dictionary<string, object?>
            {
                [" tagrange.enabled "] = true,
                ["tagrange.step"] = 16
            },
            Array.Empty<string>());

        DBuilderPluginHostModel.WriteSettings(settings, snapshot);

        Assert.False(settings.ContainsKey("tagrange"));
        Assert.Equal("left", settings["CommentsPanel"]["dock"]);
        Assert.Equal(true, settings["TagRange"]["tagrange.enabled"]);
        Assert.Equal(16, settings["TagRange"]["tagrange.step"]);
    }

    [Fact]
    public void WriteSettingsRemovesPluginSectionWhenSnapshotHasNoValues()
    {
        var settings = new Dictionary<string, Dictionary<string, object?>>
        {
            ["tagrange"] = new()
            {
                ["tagrange.step"] = 8
            },
            ["CommentsPanel"] = new()
            {
                ["dock"] = "left"
            }
        };
        var snapshot = new DBuilderPluginSettingsSnapshot(
            "TagRange",
            new Dictionary<string, object?>(),
            Array.Empty<string>());

        DBuilderPluginHostModel.WriteSettings(settings, snapshot);

        Assert.False(settings.ContainsKey("tagrange"));
        Assert.False(settings.ContainsKey("TagRange"));
        Assert.Equal("left", settings["CommentsPanel"]["dock"]);
    }

    [Fact]
    public void WriteSettingsLeavesStoreUntouchedWhenSnapshotHasWarnings()
    {
        var settings = new Dictionary<string, Dictionary<string, object?>>
        {
            ["TagRange"] = new()
            {
                ["tagrange.step"] = 8
            }
        };
        var snapshot = new DBuilderPluginSettingsSnapshot(
            "TagRange",
            new Dictionary<string, object?>
            {
                ["tagrange.step"] = 16
            },
            new[] { "Plugin TagRange is disabled." });

        DBuilderPluginHostModel.WriteSettings(settings, snapshot);

        Assert.Equal(8, settings["TagRange"]["tagrange.step"]);
    }

    private static DBuilderPluginRuntimeInstancePlan RuntimeInstancePlan(
        IDBuilderPlugin plugin,
        string pluginName)
        => new(
            new[]
            {
                RuntimeInstance(plugin, pluginName, 0)
            },
            Array.Empty<DBuilderPluginDiagnostic>());

    private static bool RequiresCallerArgument(DBuilderPluginCallbackDescriptor callback)
        => callback.Parameters?.Any(parameter => parameter != DBuilderPluginCallbackParameterKind.CurrentResult) == true;

    private static DBuilderPluginRuntimeInstance RuntimeInstance(
        IDBuilderPlugin plugin,
        string pluginName,
        int order)
        => new(
            pluginName,
            "/plugins/" + pluginName + ".dll",
            plugin.GetType().FullName!,
            order,
            plugin);
}

public sealed class ReflectionPluginHostTestPlugin : IDBuilderPlugin
{
}

public sealed class ReflectionBrokenPluginHostTestPlugin : IDBuilderPlugin
{
    public ReflectionBrokenPluginHostTestPlugin()
    {
        throw new InvalidOperationException("constructor failed");
    }
}

public sealed class ReflectionMinimumRevisionPlugin : IDBuilderPlugin
{
    public int MinimumRevision => 42;
}

public sealed class ReflectionStrictRevisionPlugin : IDBuilderPlugin
{
    public int MinimumRevision => 42;

    public bool StrictRevisionMatching => true;
}

public sealed class ReflectionNamedPlugin : IDBuilderPlugin
{
    public string Name => "Friendly Plugin";
}

public sealed class ReflectionEmptyNamePlugin : IDBuilderPlugin
{
    public string Name => " ";
}

public sealed class ReflectionNonPluginHostTestType
{
}

public sealed class ReflectionCallbackPlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    private readonly string _name;

    public ReflectionCallbackPlugin(string name)
    {
        _name = name;
    }

    public void OnInitialize()
    {
        Calls.Add(_name + ":OnInitialize");
    }
}

public sealed class ReflectionActionCommandPlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    private readonly string _name;

    public ReflectionActionCommandPlugin(string name)
    {
        _name = name;
    }

    public void RunAction()
    {
        Calls.Add(_name + ":RunAction");
    }

    public bool BadAction() => true;

    public void ThrowAction()
    {
        throw new InvalidOperationException("action failed");
    }
}

public sealed class ReflectionApiCommandPlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    private readonly string _name;

    public ReflectionApiCommandPlugin(string name)
    {
        _name = name;
    }

    public void OpenEditMode()
    {
        Calls.Add(_name + ":OpenEditMode");
    }

    public void OpenDocker()
    {
        Calls.Add(_name + ":OpenDocker");
    }

    public bool BadMode() => true;

    public void ThrowDocker()
    {
        throw new InvalidOperationException("docker failed");
    }
}

public sealed class ReflectionResourceHandlerCommandPlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    private readonly string _name;

    public ReflectionResourceHandlerCommandPlugin(string name)
    {
        _name = name;
    }

    public void OpenZipResource()
    {
        Calls.Add(_name + ":OpenZipResource");
    }

    public bool BadResource() => true;

    public void ThrowResource()
    {
        throw new InvalidOperationException("resource failed");
    }
}

public sealed class ReflectionSettingsPlugin : IDBuilderPlugin
{
    public IReadOnlyList<DBuilderPluginSettingDescriptor> SettingDescriptors { get; } = new[]
    {
        new DBuilderPluginSettingDescriptor(" settings.step ", 8),
        new DBuilderPluginSettingDescriptor("settings.enabled", true),
        new DBuilderPluginSettingDescriptor("settings.step", 32),
        new DBuilderPluginSettingDescriptor("", "ignored")
    };
}

public sealed class ReflectionUnsupportedSettingsPlugin : IDBuilderPlugin
{
    public string SettingDescriptors => "unsupported";
}

public sealed class ReflectionThrowingSettingsPlugin : IDBuilderPlugin
{
    public IReadOnlyList<DBuilderPluginSettingDescriptor> SettingDescriptors
        => throw new InvalidOperationException("settings failed");
}

public sealed class ReflectionMapLifecycleCallbackPlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    private readonly string _name;

    public ReflectionMapLifecycleCallbackPlugin(string name)
    {
        _name = name;
    }

    public void OnMapOpenBegin() => Calls.Add(_name + ":OnMapOpenBegin");

    public void OnMapOpenEnd() => Calls.Add(_name + ":OnMapOpenEnd");

    public void OnMapNewBegin() => Calls.Add(_name + ":OnMapNewBegin");

    public void OnMapNewEnd() => Calls.Add(_name + ":OnMapNewEnd");

    public void OnMapCloseBegin() => Calls.Add(_name + ":OnMapCloseBegin");

    public void OnMapCloseEnd() => Calls.Add(_name + ":OnMapCloseEnd");

    public void OnMapSetChangeBegin() => Calls.Add(_name + ":OnMapSetChangeBegin");

    public void OnMapSetChangeEnd() => Calls.Add(_name + ":OnMapSetChangeEnd");

    public void OnProgramReconfigure() => Calls.Add(_name + ":OnProgramReconfigure");

    public void OnMapReconfigure() => Calls.Add(_name + ":OnMapReconfigure");

    public void OnReloadResources() => Calls.Add(_name + ":OnReloadResources");

    public void OnMapNodesRebuilt() => Calls.Add(_name + ":OnMapNodesRebuilt");
}

public sealed class ReflectionEditModeCallbackPlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    private readonly string _name;
    private readonly bool _continueResult;

    public ReflectionEditModeCallbackPlugin(string name, bool continueResult)
    {
        _name = name;
        _continueResult = continueResult;
    }

    public bool OnModeChange()
    {
        Calls.Add(_name + ":ModeChange");
        return _continueResult;
    }

    public void OnEditEngage()
    {
        Calls.Add(_name + ":EditEngage");
    }

    public void OnEditDisengage()
    {
        Calls.Add(_name + ":EditDisengage");
    }
}

public sealed class ReflectionAbortCallbackPlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    private readonly bool _continueResult;
    private readonly string _name;

    public ReflectionAbortCallbackPlugin()
        : this(continueResult: true, name: "Aborter")
    {
    }

    public ReflectionAbortCallbackPlugin(bool continueResult, string name)
    {
        _continueResult = continueResult;
        _name = name;
    }

    public int MinimumRevision => 42;

    public bool OnCopyBegin(bool result)
    {
        Calls.Add(_name + ":" + result);
        return _continueResult;
    }

    public bool OnUndoBegin(bool result)
    {
        Calls.Add(_name + ":" + result);
        return _continueResult;
    }
}

public sealed class ReflectionPasteCallbackPlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    public static PasteOptions? OriginalOptions { get; set; }

    private readonly string _name;
    private readonly bool _continueResult;

    public ReflectionPasteCallbackPlugin(string name, bool continueResult)
    {
        _name = name;
        _continueResult = continueResult;
    }

    public bool OnPasteBegin(PasteOptions options, bool result)
    {
        string copied = ReferenceEquals(options, OriginalOptions)
            ? "original"
            : "copy";
        Calls.Add(_name + ":" + options.ChangeTags + ":" + options.RemoveActions + ":" + result + ":" + copied);
        return _continueResult;
    }

    public void OnPasteEnd(PasteOptions options)
    {
        string copied = ReferenceEquals(options, OriginalOptions)
            ? "original"
            : "copy";
        Calls.Add(_name + ":End:" + options.ChangeTags + ":" + options.RemoveActions + ":" + copied);
    }
}

public sealed class ReflectionEditOperationCallbackPlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    private readonly string _name;
    private readonly bool _continueResult;

    public ReflectionEditOperationCallbackPlugin(string name, bool continueResult)
    {
        _name = name;
        _continueResult = continueResult;
    }

    public bool OnCopyBegin(bool result)
    {
        Calls.Add(_name + ":CopyBegin:" + result);
        return _continueResult;
    }

    public void OnCopyEnd()
    {
        Calls.Add(_name + ":CopyEnd");
    }

    public void OnUndoCreated()
    {
        Calls.Add(_name + ":UndoCreated");
    }

    public void OnUndoWithdrawn()
    {
        Calls.Add(_name + ":UndoWithdrawn");
    }

    public bool OnRedoBegin(bool result)
    {
        Calls.Add(_name + ":RedoBegin:" + result);
        return _continueResult;
    }

    public void OnRedoEnd()
    {
        Calls.Add(_name + ":RedoEnd");
    }

    public void OnEditCancel()
    {
        Calls.Add(_name + ":EditCancel");
    }

    public void OnEditAccept()
    {
        Calls.Add(_name + ":EditAccept");
    }
}

public sealed class ReflectionPreferenceActionCallbackPlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    private readonly string _name;

    public ReflectionPreferenceActionCallbackPlugin(string name)
    {
        _name = name;
    }

    public void OnShowPreferences()
    {
        Calls.Add(_name + ":ShowPreferences");
    }

    public void OnClosePreferences()
    {
        Calls.Add(_name + ":ClosePreferences");
    }

    public void OnActionBegin()
    {
        Calls.Add(_name + ":ActionBegin");
    }

    public void OnActionEnd()
    {
        Calls.Add(_name + ":ActionEnd");
    }
}

public sealed class ReflectionInputCallbackPlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    private readonly string _name;

    public ReflectionInputCallbackPlugin(string name)
    {
        _name = name;
    }

    public void OnEditMouseClick()
    {
        Calls.Add(_name + ":MouseClick");
    }

    public void OnEditMouseDoubleClick()
    {
        Calls.Add(_name + ":MouseDoubleClick");
    }

    public void OnEditMouseDown()
    {
        Calls.Add(_name + ":MouseDown");
    }

    public void OnEditMouseEnter()
    {
        Calls.Add(_name + ":MouseEnter");
    }

    public void OnEditMouseLeave()
    {
        Calls.Add(_name + ":MouseLeave");
    }

    public void OnEditMouseMove()
    {
        Calls.Add(_name + ":MouseMove");
    }

    public void OnEditMouseUp()
    {
        Calls.Add(_name + ":MouseUp");
    }

    public void OnEditKeyDown()
    {
        Calls.Add(_name + ":KeyDown");
    }

    public void OnEditKeyUp()
    {
        Calls.Add(_name + ":KeyUp");
    }

    public void OnEditMouseInput()
    {
        Calls.Add(_name + ":MouseInput");
    }
}

public sealed class ReflectionRenderingCallbackPlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    private readonly string _name;

    public ReflectionRenderingCallbackPlugin(string name)
    {
        _name = name;
    }

    public void OnEditRedrawDisplayBegin()
    {
        Calls.Add(_name + ":RedrawBegin");
    }

    public void OnEditRedrawDisplayEnd()
    {
        Calls.Add(_name + ":RedrawEnd");
    }

    public void OnPresentDisplayBegin()
    {
        Calls.Add(_name + ":PresentBegin");
    }

    public void OnSectorCeilingSurfaceUpdate()
    {
        Calls.Add(_name + ":CeilingUpdate");
    }

    public void OnSectorFloorSurfaceUpdate()
    {
        Calls.Add(_name + ":FloorUpdate");
    }
}

public sealed class ReflectionHighlightCallbackPlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    private readonly string _name;

    public ReflectionHighlightCallbackPlugin(string name)
    {
        _name = name;
    }

    public void OnHighlightSector()
    {
        Calls.Add(_name + ":Sector");
    }

    public void OnHighlightLinedef()
    {
        Calls.Add(_name + ":Linedef");
    }

    public void OnHighlightThing()
    {
        Calls.Add(_name + ":Thing");
    }

    public void OnHighlightVertex()
    {
        Calls.Add(_name + ":Vertex");
    }

    public void OnHighlightRefreshed()
    {
        Calls.Add(_name + ":Refreshed");
    }

    public void OnHighlightLost()
    {
        Calls.Add(_name + ":Lost");
    }
}

public sealed class ReflectionMapSaveCallbackPlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    private readonly string _name;

    public ReflectionMapSaveCallbackPlugin(string name)
    {
        _name = name;
    }

    public void OnMapSaveBegin(SavePurpose purpose)
    {
        Calls.Add(_name + ":Begin:" + purpose);
    }

    public void OnMapSaveEnd(SavePurpose purpose)
    {
        Calls.Add(_name + ":End:" + purpose);
    }
}

public sealed class ReflectionBadSignatureCallbackPlugin : IDBuilderPlugin
{
    public void OnInitialize(object context)
    {
    }
}

public sealed class ReflectionThrowingCallbackPlugin : IDBuilderPlugin
{
    public void OnInitialize()
    {
        throw new InvalidOperationException("callback failed");
    }
}

public sealed class ReflectionDisposablePlugin : IDBuilderPlugin
{
    public static List<string> Calls { get; } = new();

    private readonly string _name;

    public ReflectionDisposablePlugin(string name)
    {
        _name = name;
    }

    public void Dispose()
    {
        Calls.Add(_name + ":Dispose");
    }
}

public sealed class ReflectionBadDisposePlugin : IDBuilderPlugin
{
    public void Dispose(object context)
    {
    }
}

public sealed class ReflectionThrowingDisposePlugin : IDBuilderPlugin
{
    public void Dispose()
    {
        throw new InvalidOperationException("dispose failed");
    }
}
