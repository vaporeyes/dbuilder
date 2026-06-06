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
}
