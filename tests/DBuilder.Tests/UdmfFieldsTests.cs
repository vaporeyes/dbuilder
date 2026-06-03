// ABOUTME: Tests custom UDMF field format/parse with type inference and string escaping.
// ABOUTME: Covers the property-dialog text model used for raw UDMF custom field editing.

using System.Collections.Generic;
using DBuilder.Map;

namespace DBuilder.Tests;

public class UdmfFieldsTests
{
    [Fact]
    public void ParsesInferredTypes()
    {
        var f = UdmfFields.Parse("lightcolor = 16711680\ncomment = hello world\ngravity = 0.5\nhidden = true");
        Assert.Equal(16711680, Assert.IsType<int>(f["lightcolor"]));
        Assert.Equal("hello world", Assert.IsType<string>(f["comment"]));
        Assert.Equal(0.5, Assert.IsType<double>(f["gravity"]), 6);
        Assert.True(Assert.IsType<bool>(f["hidden"]));
    }

    [Fact]
    public void ParsesLargeWholeNumbersAsLong()
    {
        var f = UdmfFields.Parse("large = 4294967295\nsmall = 2147483647\nnegative = -2147483649");

        Assert.Equal(4294967295L, Assert.IsType<long>(f["large"]));
        Assert.Equal(2147483647, Assert.IsType<int>(f["small"]));
        Assert.Equal(-2147483649L, Assert.IsType<long>(f["negative"]));
    }

    [Fact]
    public void QuotedValueStaysString()
    {
        var f = UdmfFields.Parse("label = \"123\"");
        Assert.Equal("123", Assert.IsType<string>(f["label"])); // quoted -> string even though numeric
    }

    [Fact]
    public void QuotedValueUnescapesUdmfStringCharacters()
    {
        var f = UdmfFields.Parse("label = \"line 1\\n\\\"quoted\\\"\\\\path\"");

        Assert.Equal("line 1\n\"quoted\"\\path", Assert.IsType<string>(f["label"]));
    }

    [Fact]
    public void IgnoresBlankAndMalformedLines()
    {
        var f = UdmfFields.Parse("\n  \na = 1\nnoequalshere\n= 5\n");
        Assert.Single(f);
        Assert.Equal(1, f["a"]);
    }

    [Theory]
    [InlineData(" Light Color ", "lightcolor")]
    [InlineData("1bad name", "badname")]
    [InlineData("_Zone-Id", "_zoneid")]
    [InlineData("@#$", "")]
    public void ValidateNameMatchesUdbFieldNameRules(string input, string expected)
        => Assert.Equal(expected, UdmfFields.ValidateName(input));

    [Fact]
    public void ParseNormalizesFieldNamesLikeUdbCustomFieldEditor()
    {
        var f = UdmfFields.Parse(" Light Color = 7\n1bad-name = true\n@#$ = 4");

        Assert.Equal(2, f.Count);
        Assert.Equal(7, f["lightcolor"]);
        Assert.True((bool)f["badname"]);
    }

    [Fact]
    public void FormatThenParseRoundTrips()
    {
        var src = new Dictionary<string, object>
        {
            ["comment"] = "a note",
            ["lightcolor"] = 255,
            ["large"] = 4294967295L,
            ["whole_float"] = 1.0,
            ["precise"] = 0.123456789012345,
            ["scale"] = 1.25,
            ["flag"] = false,
        };
        var round = UdmfFields.Parse(UdmfFields.Format(src));
        Assert.Equal("a note", round["comment"]);
        Assert.Equal(255, round["lightcolor"]);
        Assert.Equal(4294967295L, round["large"]);
        Assert.Equal(1.0, Assert.IsType<double>(round["whole_float"]));
        Assert.Equal(0.123456789012345, Assert.IsType<double>(round["precise"]));
        Assert.Equal(1.25, (double)round["scale"], 6);
        Assert.False((bool)round["flag"]);
    }

    [Fact]
    public void FormatKeepsDoublePrecisionAndType()
    {
        var src = new Dictionary<string, object>
        {
            ["whole"] = 1.0,
            ["precise"] = 0.123456789012345,
        };

        string text = UdmfFields.Format(src);

        Assert.Equal("1.0", ExtractValue(text, "whole"));
        Assert.Equal("0.123456789012345", ExtractValue(text, "precise"));
    }

    [Fact]
    public void FormatQuotesStringsSoTypeInferenceDoesNotChangeThem()
    {
        var src = new Dictionary<string, object>
        {
            ["bool_text"] = "true",
            ["number_text"] = "123",
            ["decimal_text"] = "1.25",
            ["quoted"] = "say \"hello\"",
            ["path"] = "a\\b",
            ["multiline"] = "line 1\nline 2",
        };

        string text = UdmfFields.Format(src);
        var round = UdmfFields.Parse(text);

        Assert.Equal("\"true\"", ExtractValue(text, "bool_text"));
        Assert.Equal("\"123\"", ExtractValue(text, "number_text"));
        Assert.Equal("\"1.25\"", ExtractValue(text, "decimal_text"));
        Assert.Equal("\"say \\\"hello\\\"\"", ExtractValue(text, "quoted"));
        Assert.Equal("\"a\\\\b\"", ExtractValue(text, "path"));
        Assert.Equal("\"line 1\\nline 2\"", ExtractValue(text, "multiline"));
        Assert.Equal(src, round);
    }

    [Fact]
    public void FormatIsSortedAndEmptyForEmpty()
    {
        Assert.Equal("", UdmfFields.Format(new Dictionary<string, object>()));
        var s = UdmfFields.Format(new Dictionary<string, object> { ["zeta"] = 1, ["alpha"] = 2 });
        Assert.StartsWith("alpha = 2", s); // sorted by key
    }

    private static string ExtractValue(string text, string key)
    {
        foreach (string line in text.Split('\n'))
        {
            if (!line.StartsWith(key + " = ", System.StringComparison.Ordinal)) continue;
            return line[(key.Length + 3)..];
        }

        return "";
    }
}
