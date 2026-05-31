// ABOUTME: Tests UDB-style script resource identity and resource-scoped search results.
// ABOUTME: Verifies filename normalization, entry matching, metadata, and find-usages behavior.

using DBuilder.IO;

namespace DBuilder.Tests;

public class ScriptResourceTests
{
    [Fact]
    public void StoresResourceIdentityLikeUdb()
    {
        string root = Path.Combine("archives", "base.pk3");
        var resource = ScriptResource.FromText(
            root,
            "base.pk3",
            "actors\\monsters\\zombie.txt",
            ScriptType.Decorate,
            "actor Zombie 3004 {}",
            lumpIndex: 12,
            isReadOnly: true);

        string filename = Path.Combine("actors", "monsters", "zombie.txt");
        Assert.Equal(filename, resource.Filename);
        Assert.Equal(Path.Combine(root, filename), resource.FilePathName);
        Assert.Equal(root, resource.ResourcePath);
        Assert.Equal("base.pk3", resource.ResourceDisplayName);
        Assert.Equal(12, resource.LumpIndex);
        Assert.Equal(ScriptType.Decorate, resource.ScriptType);
        Assert.True(resource.IsReadOnly);
    }

    [Fact]
    public void EntriesUseCaseInsensitiveLookupLikeUdb()
    {
        var resource = ScriptResource.FromText("", "map.wad", "SCRIPTS", ScriptType.Acs, "");

        resource.Entries.Add("Thing_Spawn");

        Assert.Contains("thing_spawn", resource.Entries);
    }

    [Fact]
    public void FindsResourceScopedUsagesLikeUdb()
    {
        var resource = ScriptResource.FromText(
            "/mods/base.pk3",
            "base.pk3",
            "zscript/actors.txt",
            ScriptType.ZScript,
            """
            class Zombie : Actor {}
            class FastZombie : Zombie {}
            """);

        var usages = resource.FindUsages("Zombie", wholeWord: true, caseSensitive: true);

        Assert.True(resource.ContainsText("Zombie", wholeWord: true, caseSensitive: true));
        Assert.Collection(
            usages,
            usage =>
            {
                Assert.Same(resource, usage.Resource);
                Assert.Equal(0, usage.LineIndex);
                Assert.Equal("class Zombie : Actor {}", usage.Line);
                Assert.Equal(6, usage.MatchStart);
                Assert.Equal(12, usage.MatchEnd);
            },
            usage =>
            {
                Assert.Same(resource, usage.Resource);
                Assert.Equal(1, usage.LineIndex);
                Assert.Equal("class FastZombie : Zombie {}", usage.Line);
                Assert.Equal(19, usage.MatchStart);
                Assert.Equal(25, usage.MatchEnd);
            });
    }
}
