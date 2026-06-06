// ABOUTME: Tests plugin descriptor normalization and lifecycle hook planning.
// ABOUTME: Covers the UI-independent plugin host foundation before runtime loading exists.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class DBuilderPluginHostModelTests
{
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
            new DBuilderPluginLifecycleRequest(MapOpen: true, Engage: true, Shutdown: true));

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
            DBuilderPluginLifecycleHook.Initialize,
            DBuilderPluginLifecycleHook.RegisterActions,
            DBuilderPluginLifecycleHook.RegisterUi,
            DBuilderPluginLifecycleHook.RegisterEditModes,
            DBuilderPluginLifecycleHook.RegisterDockers,
            DBuilderPluginLifecycleHook.RegisterResourceHandlers,
            DBuilderPluginLifecycleHook.MapOpened,
            DBuilderPluginLifecycleHook.Engage,
            DBuilderPluginLifecycleHook.Dispose
        }, lifecycle.Hooks);
        Assert.Single(plan.UiContributions.Menus);
        Assert.Single(plan.UiContributions.Toolbars);
        Assert.Single(plan.ApiContributions.Actions);
        Assert.Single(plan.ApiContributions.EditModes);
        Assert.Single(plan.ApiContributions.Dockers);
        Assert.Single(plan.ResourceHandlers.Handlers);
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
            new DBuilderPluginLifecycleRequest(MapOpen: true, Engage: true, Shutdown: true));

        Assert.Equal(new[]
        {
            DBuilderPluginLifecycleHook.Load,
            DBuilderPluginLifecycleHook.Initialize,
            DBuilderPluginLifecycleHook.RegisterActions,
            DBuilderPluginLifecycleHook.RegisterUi,
            DBuilderPluginLifecycleHook.RegisterEditModes,
            DBuilderPluginLifecycleHook.RegisterDockers,
            DBuilderPluginLifecycleHook.RegisterResourceHandlers,
            DBuilderPluginLifecycleHook.MapOpened,
            DBuilderPluginLifecycleHook.Engage,
            DBuilderPluginLifecycleHook.Dispose
        }, plan.Hooks);
        Assert.Empty(plan.Warnings);
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
                new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, " comments.open ", " Open Comments "),
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
        Assert.Equal(new[]
        {
            DBuilderPluginLifecycleHook.Load,
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
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Toolbar, "builder.draw.toolbar", "Draw"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "builder.draw.menu", "Draw"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, "builder.draw.action", "Draw action")
                }),
            new DBuilderPluginDescriptor(
                "CommentsPanel",
                "/plugins/comments.dll",
                Contributions: new[]
                {
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Toolbar, "comments.open.toolbar", "Open Comments"),
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, "comments.open.menu", "Open Comments")
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
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Menu, " comments.open ", " Open Comments "),
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
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.ResourceHandler, " resource.zip ", " Zip archives "),
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
        Assert.Equal(new[] { "Plugin DisabledResource is disabled." }, plan.Warnings);
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
                    new DBuilderPluginContribution(DBuilderPluginContributionKind.Action, " builder.draw.action ", " Draw action "),
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
        Assert.Equal(new[] { "Plugin DisabledModes is disabled." }, plan.Warnings);
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
}
