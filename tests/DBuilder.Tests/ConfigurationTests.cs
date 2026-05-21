// ABOUTME: Configuration text parser verification tests.
// ABOUTME: Scalars, structures, ReadSetting/WriteSetting/DeleteSetting, null/bare keys, sorted vs hashed, errors, round-trip.

using System.Collections;
using System.Collections.Specialized;
using DBuilder.IO;

namespace DBuilder.Tests;

public class ConfigurationTests
{
    [Fact]
    public void ParsesBasicScalars()
    {
        var cfg = new Configuration();
        bool ok = cfg.InputConfiguration("""
            name = "doom";
            count = 42;
            ratio = 3.14;
            single = 1.5f;
            big = 12345678901234;
            flag = true;
            blank = null;
            """);
        Assert.True(ok, cfg.ErrorDescription);

        Assert.Equal("doom", cfg.ReadSetting("name", ""));
        Assert.Equal(42, cfg.ReadSetting("count", 0));
        Assert.Equal(3.14, cfg.ReadSetting("ratio", 0.0), 1e-12);
        Assert.Equal(1.5f, cfg.ReadSetting("single", 0f));
        Assert.Equal(12345678901234L, cfg.ReadSetting("big", 0L));
        Assert.True(cfg.ReadSetting("flag", false));
        Assert.True(cfg.SettingExists("blank"));
        Assert.Null(cfg.ReadSetting("blank", "fallback"));
    }

    [Fact]
    public void BareKeyTerminatorIsTreatedAsNull()
    {
        var cfg = new Configuration();
        Assert.True(cfg.InputConfiguration("alone;"));
        Assert.True(cfg.SettingExists("alone"));
        Assert.Null(cfg.ReadSettingObject("alone", "fallback"));
    }

    [Fact]
    public void DotSeparatedPathDescendsStructs()
    {
        var cfg = new Configuration();
        Assert.True(cfg.InputConfiguration("""
            game
            {
                name = "Doom";
                version = 19;
                settings
                {
                    sky = "SKY1";
                }
            }
            """));

        Assert.Equal("Doom", cfg.ReadSetting("game.name", ""));
        Assert.Equal(19, cfg.ReadSetting("game.version", 0));
        Assert.Equal("SKY1", cfg.ReadSetting("game.settings.sky", ""));
        Assert.Equal("fallback", cfg.ReadSetting("game.missing", "fallback"));
    }

    [Fact]
    public void WriteSettingCreatesNestedPath()
    {
        var cfg = new Configuration();
        Assert.True(cfg.WriteSetting("game.options.fullscreen", true));
        Assert.True(cfg.SettingExists("game.options.fullscreen"));
        Assert.True(cfg.ReadSetting("game.options.fullscreen", false));
    }

    [Fact]
    public void WriteSettingOverwritesScalar()
    {
        var cfg = new Configuration();
        cfg.WriteSetting("x", 1);
        cfg.WriteSetting("x", 99);
        Assert.Equal(99, cfg.ReadSetting("x", 0));
    }

    [Fact]
    public void DeleteSettingRemovesLeaf()
    {
        var cfg = new Configuration();
        Assert.True(cfg.InputConfiguration("a = 1; b = 2;"));
        Assert.True(cfg.DeleteSetting("a"));
        Assert.False(cfg.SettingExists("a"));
        Assert.True(cfg.SettingExists("b"));
    }

    [Fact]
    public void DeleteMissingKeyReturnsFalse()
    {
        var cfg = new Configuration();
        cfg.WriteSetting("x", 1);
        Assert.False(cfg.DeleteSetting("y"));
    }

    [Fact]
    public void SortedConfigurationUsesListDictionary()
    {
        var cfg = new Configuration(sorted: true);
        Assert.True(cfg.Sorted);
        Assert.True(cfg.InputConfiguration("a = 1; b = 2; c = 3;", sorted: true));

        // Keys come back in insertion order
        var keys = new List<string>();
        foreach (DictionaryEntry e in cfg.Root) keys.Add(e.Key.ToString()!);
        Assert.Equal(new[] { "a", "b", "c" }, keys);
    }

    [Fact]
    public void UnsortedConfigurationUsesHashtable()
    {
        var cfg = new Configuration();
        Assert.False(cfg.Sorted);
        Assert.IsType<Hashtable>(cfg.Root);
    }

    [Fact]
    public void LineAndBlockCommentsAreSkipped()
    {
        var cfg = new Configuration();
        Assert.True(cfg.InputConfiguration("""
            // a leading comment
            x = 1; // trailing
            /* block
               comment */
            y = 2;
            """));

        Assert.Equal(1, cfg.ReadSetting("x", 0));
        Assert.Equal(2, cfg.ReadSetting("y", 0));
    }

    [Fact]
    public void StringEscapesAreHonored()
    {
        var cfg = new Configuration();
        Assert.True(cfg.InputConfiguration(@"msg = ""hi\nthere\t\""ok\"""";"));
        Assert.Equal("hi\nthere\t\"ok\"", cfg.ReadSetting("msg", ""));
    }

    [Fact]
    public void UnknownKeywordRaisesError()
    {
        var cfg = new Configuration();
        Assert.False(cfg.InputConfiguration("x = wat;"));
        Assert.True(cfg.ErrorResult);
        Assert.Contains("Unknown keyword", cfg.ErrorDescription);
    }

    [Fact]
    public void KeyWithSpacesRaisesError()
    {
        var cfg = new Configuration();
        // "two words" before = will fail key validation
        // (parser appends a space for each newline; an actual embedded space in the source triggers this)
        Assert.False(cfg.InputConfiguration("foo bar = 1;"));
        Assert.True(cfg.ErrorResult);
    }

    [Fact]
    public void RoundTripPreservesAllTypes()
    {
        var c1 = new Configuration(sorted: true);
        Assert.True(c1.InputConfiguration("""
            name = "doom";
            count = 7;
            ratio = 3.14;
            flag = true;
            holes
            {
                first = 1;
                second = 2;
            }
            """, sorted: true));

        string serialized = c1.OutputConfiguration("\n", true);

        var c2 = new Configuration(sorted: true);
        Assert.True(c2.InputConfiguration(serialized, sorted: true), c2.ErrorDescription);

        Assert.Equal("doom", c2.ReadSetting("name", ""));
        Assert.Equal(7, c2.ReadSetting("count", 0));
        Assert.Equal(3.14, c2.ReadSetting("ratio", 0.0), 1e-9);
        Assert.True(c2.ReadSetting("flag", false));
        Assert.Equal(1, c2.ReadSetting("holes.first", 0));
        Assert.Equal(2, c2.ReadSetting("holes.second", 0));
    }

    [Fact]
    public void ReadSettingNonexistentReturnsDefault()
    {
        var cfg = new Configuration();
        cfg.InputConfiguration("x = 5;");
        Assert.Equal(42, cfg.ReadSetting("does_not_exist", 42));
        Assert.Equal("fallback", cfg.ReadSetting("missing.path", "fallback"));
    }

    [Fact]
    public void MergeViaIncludeIsRejectedForInMemoryInput()
    {
        // include() in input parsed from a string (no source file path) should be rejected.
        var cfg = new Configuration();
        Assert.False(cfg.InputConfiguration("include(\"other.cfg\");"));
        Assert.Contains("Include function is not supported", cfg.ErrorDescription);
    }
}
