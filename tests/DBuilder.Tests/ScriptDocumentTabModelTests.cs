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
    public void DisplaysChangedMarkerInTitleLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            scripttype = "ACS";
            extensions = "acs";
            """);
        var tab = ScriptDocumentTabModel.NewFile(config);

        Assert.Equal("Untitled.acs", tab.DisplayTitle(isChanged: false));
        Assert.Equal("\u25CF Untitled.acs", tab.DisplayTitle(isChanged: true));
    }

    [Fact]
    public void ChangesUntitledFileConfigurationTitleLikeUdb()
    {
        var acs = ScriptConfigurationInfo.FromText("""
            scripttype = "ACS";
            extensions = "acs";
            """);
        var zscript = ScriptConfigurationInfo.FromText("""
            scripttype = "ZSCRIPT";
            extensions = "zs";
            """);
        var tab = ScriptDocumentTabModel.NewFile(acs);

        var changed = tab.WithScriptConfiguration(zscript);

        Assert.Equal(ScriptType.ZScript, changed.ScriptType);
        Assert.Equal("Untitled.zs", changed.Title);
        Assert.Equal("", changed.Filename);
        Assert.True(changed.IsSaveAsRequired);
    }

    [Fact]
    public void KeepsSavedFileTitleWhenChangingConfigurationLikeUdb()
    {
        var acs = ScriptConfigurationInfo.FromText("""
            scripttype = "ACS";
            extensions = "acs";
            """);
        var decorate = ScriptConfigurationInfo.FromText("""
            scripttype = "DECORATE";
            extensions = "dec";
            """);
        var tab = ScriptDocumentTabModel.OpenFile("/mods/scripts/test.acs", acs);

        var changed = tab.WithScriptConfiguration(decorate);

        Assert.Equal(ScriptType.Decorate, changed.ScriptType);
        Assert.Equal("test.acs", changed.Title);
        Assert.Equal("/mods/scripts/test.acs", changed.Filename);
        Assert.False(changed.IsSaveAsRequired);
    }

    [Fact]
    public void SkipsNonReconfigurableTabsWhenChangingConfigurationLikeUdb()
    {
        var acs = ScriptConfigurationInfo.FromText("""
            scripttype = "ACS";
            """);
        var decorate = ScriptConfigurationInfo.FromText("""
            scripttype = "DECORATE";
            """);
        var tab = ScriptDocumentTabModel.Lump("SCRIPTS", "SCRIPTS", acs);

        Assert.Same(tab, tab.WithScriptConfiguration(decorate));
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
        Assert.Equal(resource.Filename, tab.ErrorFilename);
        Assert.Equal("7:zombie.txt", tab.Title);
        Assert.Equal(resource.FilePathName, tab.ToolTip);
        Assert.Equal(7, tab.ResourceLumpIndex);
        Assert.True(tab.ExplicitSave);
        Assert.False(tab.IsSaveAsRequired);
        Assert.True(tab.IsClosable);
        Assert.False(tab.IsReconfigurable);
        Assert.True(tab.IsReadOnly);
    }

    [Fact]
    public void MatchesResourceTabsByPathAndLumpIndexLikeUdb()
    {
        var first = ScriptResource.FromText(
            "maps/test.wad",
            "test.wad",
            "SCRIPTS",
            ScriptType.Acs,
            "script 1 OPEN {}",
            lumpIndex: 4);
        var same = ScriptResource.FromText(
            "maps/test.wad",
            "test.wad",
            "SCRIPTS",
            ScriptType.Acs,
            "script 1 OPEN {}",
            lumpIndex: 4);
        var duplicateLump = ScriptResource.FromText(
            "maps/test.wad",
            "test.wad",
            "SCRIPTS",
            ScriptType.Acs,
            "script 2 OPEN {}",
            lumpIndex: 9);
        var tab = ScriptDocumentTabModel.Resource(first);

        Assert.True(tab.MatchesResource(same));
        Assert.False(tab.MatchesResource(duplicateLump));
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

    [Fact]
    public void MatchesFileCompilerErrorsLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            scripttype = "ACS";
            """);
        var tab = ScriptDocumentTabModel.OpenFile("/mods/test.acs", config);

        Assert.True(tab.AppliesToError(new ScriptCompilerError("boom", "/MODS/TEST.ACS", 3)));
        Assert.False(tab.AppliesToError(new ScriptCompilerError("boom", "?SCRIPTS", 3)));
    }

    [Fact]
    public void MatchesLumpCompilerErrorsLikeUdb()
    {
        var config = ScriptConfigurationInfo.FromText("""
            scripttype = "ACS";
            """);
        var tab = ScriptDocumentTabModel.Lump("SCRIPTS", "SCRIPTS", config);

        Assert.True(tab.AppliesToError(new ScriptCompilerError("boom", "?scripts", 3)));
        Assert.False(tab.AppliesToError(new ScriptCompilerError("boom", "SCRIPTS", 3)));
    }

    [Fact]
    public void MatchesResourceCompilerErrorsLikeUdb()
    {
        var resource = ScriptResource.FromText(
            "archives/base.pk3",
            "base.pk3",
            "zscript/actors.txt",
            ScriptType.ZScript,
            "");
        var tab = ScriptDocumentTabModel.Resource(resource);

        Assert.True(tab.AppliesToError(new ScriptCompilerError("boom", "ZSCRIPT/ACTORS.TXT", 3)));
        Assert.False(tab.AppliesToError(new ScriptCompilerError("boom", resource.FilePathName, 3)));
    }
}
