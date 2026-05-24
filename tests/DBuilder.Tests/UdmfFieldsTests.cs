// ABOUTME: Tests custom UDMF field format/parse with type inference (bool/int/double/string) and round-tripping.

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
    public void QuotedValueStaysString()
    {
        var f = UdmfFields.Parse("label = \"123\"");
        Assert.Equal("123", Assert.IsType<string>(f["label"])); // quoted -> string even though numeric
    }

    [Fact]
    public void IgnoresBlankAndMalformedLines()
    {
        var f = UdmfFields.Parse("\n  \na = 1\nnoequalshere\n= 5\n");
        Assert.Single(f);
        Assert.Equal(1, f["a"]);
    }

    [Fact]
    public void FormatThenParseRoundTrips()
    {
        var src = new Dictionary<string, object>
        {
            ["comment"] = "a note",
            ["lightcolor"] = 255,
            ["scale"] = 1.25,
            ["flag"] = false,
        };
        var round = UdmfFields.Parse(UdmfFields.Format(src));
        Assert.Equal("a note", round["comment"]);
        Assert.Equal(255, round["lightcolor"]);
        Assert.Equal(1.25, (double)round["scale"], 6);
        Assert.False((bool)round["flag"]);
    }

    [Fact]
    public void FormatIsSortedAndEmptyForEmpty()
    {
        Assert.Equal("", UdmfFields.Format(new Dictionary<string, object>()));
        var s = UdmfFields.Format(new Dictionary<string, object> { ["zeta"] = 1, ["alpha"] = 2 });
        Assert.StartsWith("alpha = 2", s); // sorted by key
    }
}
