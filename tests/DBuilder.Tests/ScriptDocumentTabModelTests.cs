// ABOUTME: Tests UDB-style script document tab identity and view setting snapshots.
// ABOUTME: Verifies file, lump and resource tab metadata without editor UI controls.

using DBuilder.IO;

namespace DBuilder.Tests;

public class ScriptDocumentTabModelTests
{
    [Fact]
    public void CreatesUntitledFileTabLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            scripttype = "ACS";
            extensions = "acs,h";
            """);

        var tab = ScriptDocumentTabModel.NewFile(config);

        Assert.Equal(ScriptDocumentTabType.File, tab.TabType);
        Assert.Equal(ScriptType.Acs, tab.ScriptType);
        Assert.Equal("", tab.Filename);
        Assert.Equal("Untitled.acs", tab.Title);
        Assert.True(tab.ExplicitSave);
        Assert.True(tab.IsSaveAsRequired);
        Assert.True(tab.IsClosable);
        Assert.True(tab.IsReconfigurable);
        Assert.False(tab.IsReadOnly);
    }

    [Fact]
    public void CreatesLumpTabLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            scripttype = "ACS";
            """);

        var tab = ScriptDocumentTabModel.Lump("SCRIPTS", "SCRIPTS", config);

        Assert.Equal(ScriptDocumentTabType.Lump, tab.TabType);
        Assert.Equal("SCRIPTS", tab.Filename);
        Assert.Equal("SCRIPTS", tab.Title);
        Assert.False(tab.ExplicitSave);
        Assert.False(tab.IsSaveAsRequired);
        Assert.False(tab.IsClosable);
        Assert.False(tab.IsReconfigurable);
    }

    [Fact]
    public void CreatesResourceTabLikeUdb()
    {
        var resource = ScriptResource.FromText(
            "archives/base.pk3",
            "base.pk3",
            "actors/zombie.txt",
            ScriptType.Decorate,
            "",
            lumpIndex: 7,
            isReadOnly: true);

        var tab = ScriptDocumentTabModel.Resource(resource);

        Assert.Equal(ScriptDocumentTabType.Resource, tab.TabType);
        Assert.Equal(resource.FilePathName, tab.Filename);
        Assert.Equal("7:zombie.txt", tab.Title);
        Assert.Equal(resource.FilePathName, tab.ToolTip);
        Assert.True(tab.ExplicitSave);
        Assert.False(tab.IsSaveAsRequired);
        Assert.True(tab.IsClosable);
        Assert.False(tab.IsReconfigurable);
        Assert.True(tab.IsReadOnly);
    }

    [Fact]
    public void BuildsViewSettingsLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            scripttype = "ACS";
            """);
        var tab = ScriptDocumentTabModel.OpenFile("/mods/test.acs", config);
        var folds = new Dictionary<int, HashSet<int>>
        {
            [1025] = new HashSet<int> { 4, 8 },
        };

        var settings = tab.GetViewSettings(
            "script 1 OPEN {}\n",
            new ScriptDocumentTabViewState(
                CaretPosition: 5,
                FirstVisibleLine: 2,
                IsActiveTab: true,
                FoldLevels: folds));

        Assert.Equal("/mods/test.acs", settings.Filename);
        Assert.Equal(ScriptDocumentTabType.File, settings.TabType);
        Assert.Equal(ScriptType.Acs, settings.ScriptType);
        Assert.Equal(5, settings.CaretPosition);
        Assert.Equal(2, settings.FirstVisibleLine);
        Assert.True(settings.IsActiveTab);
        Assert.Equal(MurmurHash2.Hash("script 1 OPEN {}\n"), settings.Hash);
        Assert.Equal(new[] { 4, 8 }, settings.FoldLevels[1025].OrderBy(v => v));
        Assert.NotSame(folds[1025], settings.FoldLevels[1025]);
    }

    [Fact]
    public void BuildsResourceViewSettingsWithUniqueFilenameLikeUdb()
    {
        var resource = ScriptResource.FromText(
            "archives/base.pk3",
            "base.pk3",
            "zscript/actors.txt",
            ScriptType.ZScript,
            "");
        var tab = ScriptDocumentTabModel.Resource(resource);

        var settings = tab.GetViewSettings("class Zombie : Actor {}", new ScriptDocumentTabViewState());

        Assert.Equal(Path.Combine(resource.ResourcePath, resource.FilePathName), settings.Filename);
        Assert.Equal(resource.ResourcePath, settings.ResourceLocation);
        Assert.Equal(ScriptDocumentTabType.Resource, settings.TabType);
        Assert.Equal(ScriptType.ZScript, settings.ScriptType);
    }
}
