// ABOUTME: Tests UDB-style paste-properties option tab selection and apply behavior.
// ABOUTME: Covers copied property availability without depending on an editor dialog.

using DBuilder.Map;

namespace DBuilder.Tests;

public class PastePropertiesOptionsModelTests
{
    [Fact]
    public void BuildReturnsWarningWhenNoCopiedPropertiesCanApply()
    {
        PastePropertiesOptionsResult result = PastePropertiesOptionsModel.Build(
            new PastePropertiesCopiedState(Sector: true),
            [PastePropertiesElementKind.Thing],
            CreateCatalog());

        Assert.False(result.IsAvailable);
        Assert.Equal(PastePropertiesOptionsModel.NoCopiedPropertiesMessage, result.StatusMessage);
        Assert.Empty(result.Tabs);
    }

    [Theory]
    [InlineData(PastePropertiesElementKind.Linedef)]
    [InlineData(PastePropertiesElementKind.Sidedef)]
    public void BuildAddsLineAndSideTabsWhenEitherLineSidePropertiesWereCopied(PastePropertiesElementKind targetKind)
    {
        PastePropertiesOptionsResult result = PastePropertiesOptionsModel.Build(
            new PastePropertiesCopiedState(Sidedef: true),
            [targetKind],
            CreateCatalog());

        Assert.True(result.IsAvailable);
        Assert.Null(result.StatusMessage);
        Assert.Equal(
            [PastePropertiesElementKind.Linedef, PastePropertiesElementKind.Sidedef],
            result.Tabs.Select(tab => tab.Kind).ToArray());
    }

    [Fact]
    public void BuildDeduplicatesTabsForMixedVisualTargets()
    {
        PastePropertiesOptionsResult result = PastePropertiesOptionsModel.Build(
            new PastePropertiesCopiedState(Vertex: true, Linedef: true, Sector: true, Thing: true),
            [
                PastePropertiesElementKind.Sector,
                PastePropertiesElementKind.Linedef,
                PastePropertiesElementKind.Sidedef,
                PastePropertiesElementKind.Thing,
                PastePropertiesElementKind.Vertex,
                PastePropertiesElementKind.Linedef,
            ],
            CreateCatalog());

        Assert.True(result.IsAvailable);
        Assert.Equal(
            [
                PastePropertiesElementKind.Sector,
                PastePropertiesElementKind.Linedef,
                PastePropertiesElementKind.Sidedef,
                PastePropertiesElementKind.Thing,
                PastePropertiesElementKind.Vertex,
            ],
            result.Tabs.Select(tab => tab.Kind).ToArray());
    }

    [Fact]
    public void BuildFiltersOptionsUnsupportedByCurrentMapFormat()
    {
        PastePropertiesOptionsResult result = PastePropertiesOptionsModel.Build(
            new PastePropertiesCopiedState(Sector: true),
            [PastePropertiesElementKind.Sector],
            CreateCatalog(
                sectorOptions:
                [
                    new PastePropertiesOption("sector.floor", "Floor", true),
                    new PastePropertiesOption("sector.extra", "Extra", true, supportsCurrentMapFormat: false),
                ]));

        PastePropertiesOptionsTab tab = Assert.Single(result.Tabs);
        PastePropertiesOption option = Assert.Single(tab.Options);
        Assert.Equal("sector.floor", option.Key);
    }

    [Fact]
    public void BuildReturnsWarningWhenAllCandidateOptionsAreUnsupported()
    {
        PastePropertiesOptionsResult result = PastePropertiesOptionsModel.Build(
            new PastePropertiesCopiedState(Thing: true),
            [PastePropertiesElementKind.Thing],
            CreateCatalog(
                thingOptions:
                [
                    new PastePropertiesOption("thing.angle", "Angle", true, supportsCurrentMapFormat: false),
                ]));

        Assert.False(result.IsAvailable);
        Assert.Equal(PastePropertiesOptionsModel.NoSupportedPropertiesMessage, result.StatusMessage);
        Assert.Empty(result.Tabs);
    }

    [Fact]
    public void ApplyUpdatesMatchingOptionsAndLeavesOthersUnchanged()
    {
        PastePropertiesOption action = new("linedef.action", "Action", true);
        PastePropertiesOption flags = new("linedef.flags", "Flags", false);
        PastePropertiesOptionsCatalog catalog = CreateCatalog(linedefOptions: [action, flags]);
        PastePropertiesOptionsResult result = PastePropertiesOptionsModel.Build(
            new PastePropertiesCopiedState(Linedef: true),
            [PastePropertiesElementKind.Linedef],
            catalog);

        PastePropertiesOptionsModel.Apply(
            result,
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["linedef.action"] = false,
                ["missing"] = true,
            });

        Assert.False(action.IsChecked);
        Assert.False(flags.IsChecked);
    }

    [Fact]
    public void SetTabEnabledUpdatesEveryOptionInTab()
    {
        PastePropertiesOptionsResult result = PastePropertiesOptionsModel.Build(
            new PastePropertiesCopiedState(Vertex: true),
            [PastePropertiesElementKind.Vertex],
            CreateCatalog());

        PastePropertiesOptionsTab tab = Assert.Single(result.Tabs);

        PastePropertiesOptionsModel.SetTabEnabled(tab, isChecked: false);

        Assert.All(tab.Options, option => Assert.False(option.IsChecked));
    }

    private static PastePropertiesOptionsCatalog CreateCatalog(
        IReadOnlyList<PastePropertiesOption>? vertexOptions = null,
        IReadOnlyList<PastePropertiesOption>? linedefOptions = null,
        IReadOnlyList<PastePropertiesOption>? sidedefOptions = null,
        IReadOnlyList<PastePropertiesOption>? sectorOptions = null,
        IReadOnlyList<PastePropertiesOption>? thingOptions = null)
    {
        return new PastePropertiesOptionsCatalog(
            new PastePropertiesOptionGroup(
                PastePropertiesElementKind.Vertex,
                "Vertices",
                vertexOptions ?? [new PastePropertiesOption("vertex.position", "Position", true)]),
            new PastePropertiesOptionGroup(
                PastePropertiesElementKind.Linedef,
                "Linedefs",
                linedefOptions ?? [new PastePropertiesOption("linedef.action", "Action", true)]),
            new PastePropertiesOptionGroup(
                PastePropertiesElementKind.Sidedef,
                "Sidedefs",
                sidedefOptions ?? [new PastePropertiesOption("sidedef.texture", "Texture", true)]),
            new PastePropertiesOptionGroup(
                PastePropertiesElementKind.Sector,
                "Sectors",
                sectorOptions ?? [new PastePropertiesOption("sector.floor", "Floor", true)]),
            new PastePropertiesOptionGroup(
                PastePropertiesElementKind.Thing,
                "Things",
                thingOptions ?? [new PastePropertiesOption("thing.type", "Type", true)]));
    }
}
