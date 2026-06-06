// ABOUTME: Tests plugin descriptor normalization and lifecycle hook planning.
// ABOUTME: Covers the UI-independent plugin host foundation before runtime loading exists.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class DBuilderPluginHostModelTests
{
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
