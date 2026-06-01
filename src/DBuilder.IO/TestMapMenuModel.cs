// ABOUTME: Builds UDB-style Test Map dropdown entries from configured skill levels.
// ABOUTME: Keeps with-monsters and no-monsters skill selection state testable outside the editor UI.

namespace DBuilder.IO;

public enum TestMapMenuEntryKind
{
    SkillWithMonsters,
    Separator,
    SkillWithoutMonsters,
    AdditionalParameters,
}

public sealed record TestMapMenuEntry(
    TestMapMenuEntryKind Kind,
    string Header,
    int Skill,
    bool Monsters,
    bool Checked);

public static class TestMapMenuModel
{
    public const string AdditionalParametersHeader = "Additional parameters";

    public static IReadOnlyList<TestMapMenuEntry> Build(
        IReadOnlyDictionary<int, string> skills,
        int selectedSkill,
        bool testMonsters)
    {
        var entries = new List<TestMapMenuEntry>();
        var orderedSkills = skills.OrderBy(skill => skill.Key).ToArray();

        foreach (var skill in orderedSkills)
            entries.Add(SkillEntry(TestMapMenuEntryKind.SkillWithMonsters, skill, selectedSkill, testMonsters));

        if (orderedSkills.Length > 0)
            entries.Add(new TestMapMenuEntry(TestMapMenuEntryKind.Separator, "", 0, true, false));

        foreach (var skill in orderedSkills)
            entries.Add(SkillEntry(TestMapMenuEntryKind.SkillWithoutMonsters, skill, selectedSkill, !testMonsters));

        if (orderedSkills.Length > 0)
            entries.Add(new TestMapMenuEntry(TestMapMenuEntryKind.Separator, "", 0, true, false));

        entries.Add(new TestMapMenuEntry(TestMapMenuEntryKind.AdditionalParameters, AdditionalParametersHeader, 0, true, false));
        return entries;
    }

    public static int SelectedSkillFromEntry(TestMapMenuEntry entry)
        => Math.Abs(entry.Skill);

    public static bool TestMonstersFromEntry(TestMapMenuEntry entry)
        => entry.Monsters;

    private static TestMapMenuEntry SkillEntry(
        TestMapMenuEntryKind kind,
        KeyValuePair<int, string> skill,
        int selectedSkill,
        bool checkedState)
    {
        bool monsters = kind == TestMapMenuEntryKind.SkillWithMonsters;
        return new TestMapMenuEntry(
            kind,
            $"{skill.Key} - {skill.Value}",
            monsters ? skill.Key : -skill.Key,
            monsters,
            checkedState && selectedSkill == skill.Key);
    }
}
