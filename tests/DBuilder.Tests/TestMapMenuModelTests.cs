// ABOUTME: Verifies UDB-style Test Map skill dropdown entry ordering and checked state.
// ABOUTME: Covers with-monsters and no-monsters skill selection values used by launch token expansion.

using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class TestMapMenuModelTests
{
    [Fact]
    public void BuildCreatesWithMonstersAndNoMonstersSkillGroups()
    {
        var skills = new Dictionary<int, string>
        {
            [3] = "Hurt me plenty",
            [1] = "I'm too young to die",
        };

        IReadOnlyList<TestMapMenuEntry> entries = TestMapMenuModel.Build(skills, selectedSkill: 3, testMonsters: true);

        Assert.Equal(7, entries.Count);
        Assert.Equal(TestMapMenuEntryKind.SkillWithMonsters, entries[0].Kind);
        Assert.Equal("1 - I'm too young to die", entries[0].Header);
        Assert.Equal(1, entries[0].Skill);
        Assert.True(entries[0].Monsters);
        Assert.Equal("3 - Hurt me plenty", entries[1].Header);
        Assert.True(entries[1].Checked);
        Assert.Equal(TestMapMenuEntryKind.Separator, entries[2].Kind);
        Assert.Equal(TestMapMenuEntryKind.SkillWithoutMonsters, entries[3].Kind);
        Assert.Equal(-1, entries[3].Skill);
        Assert.False(entries[3].Monsters);
        Assert.Equal("3 - Hurt me plenty", entries[4].Header);
        Assert.False(entries[4].Checked);
        Assert.Equal(TestMapMenuEntryKind.Separator, entries[5].Kind);
        Assert.Equal(TestMapMenuEntryKind.AdditionalParameters, entries[6].Kind);
    }

    [Fact]
    public void BuildChecksNoMonstersSkillWhenRequested()
    {
        var skills = new Dictionary<int, string>
        {
            [2] = "Not too rough",
            [4] = "Ultra-Violence",
        };

        IReadOnlyList<TestMapMenuEntry> entries = TestMapMenuModel.Build(skills, selectedSkill: 4, testMonsters: false);

        Assert.False(entries[1].Checked);
        Assert.True(entries[4].Checked);
        Assert.Equal(4, TestMapMenuModel.SelectedSkillFromEntry(entries[4]));
        Assert.False(TestMapMenuModel.TestMonstersFromEntry(entries[4]));
    }

    [Fact]
    public void BuildStillProvidesAdditionalParametersWhenNoSkillsAreConfigured()
    {
        IReadOnlyList<TestMapMenuEntry> entries = TestMapMenuModel.Build(new Dictionary<int, string>(), selectedSkill: 0, testMonsters: true);

        var entry = Assert.Single(entries);
        Assert.Equal(TestMapMenuEntryKind.AdditionalParameters, entry.Kind);
        Assert.Equal(TestMapMenuModel.AdditionalParametersHeader, entry.Header);
    }
}
