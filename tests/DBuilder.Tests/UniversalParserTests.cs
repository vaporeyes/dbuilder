// ABOUTME: UDMF text parser verification tests.
// ABOUTME: Covers all value types (int/long/float/double/bool/string/hex), nesting, comments, errors, warnings, round-trip.

using DBuilder.IO;

namespace DBuilder.Tests;

public class UniversalParserTests
{
    private static UniversalCollection ParseOk(string text)
    {
        var p = new UniversalParser();
        bool ok = p.InputConfiguration(text);
        Assert.True(ok, $"Parse failed: {p.ErrorDescription} (line {p.ErrorLine})");
        return p.Root;
    }

    [Fact]
    public void ParsesBasicScalars()
    {
        var root = ParseOk("""
            namespace = "ZDoomTranslated";
            count = 42;
            big = 12345678901234;
            ratio = 3.14;
            tiny = 1.5e-06;
            flag = true;
            other = false;
            hex = 0xDEADBEEF;
            """);

        Assert.Equal("ZDoomTranslated", root.Find(e => e.Key == "namespace").Value);
        Assert.Equal(42, root.Find(e => e.Key == "count").Value);
        Assert.Equal(12345678901234L, root.Find(e => e.Key == "big").Value);
        Assert.Equal(3.14, (double)root.Find(e => e.Key == "ratio").Value, 1e-12);
        Assert.True(((double)root.Find(e => e.Key == "tiny").Value) > 0);
        Assert.Equal(true, root.Find(e => e.Key == "flag").Value);
        Assert.Equal(false, root.Find(e => e.Key == "other").Value);
        // Convert.ToInt32(s, 16) accepts values up to 0xFFFFFFFF and returns the signed Int32 representation,
        // so 0xDEADBEEF parses to -559038737 (Int32) rather than overflowing into Int64.
        Assert.Equal(unchecked((int)0xDEADBEEF), root.Find(e => e.Key == "hex").Value);
    }

    [Fact]
    public void ParsesNestedStructs()
    {
        var root = ParseOk("""
            namespace = "Doom";
            vertex
            {
                x = 16;
                y = 32;
            }
            linedef
            {
                v1 = 0;
                v2 = 1;
                blocking = true;
            }
            """);

        Assert.Equal(3, root.Count);
        var vertex = (UniversalCollection)root.Find(e => e.Key == "vertex").Value;
        Assert.Equal(16, vertex.Find(e => e.Key == "x").Value);
        Assert.Equal(32, vertex.Find(e => e.Key == "y").Value);

        var line = (UniversalCollection)root.Find(e => e.Key == "linedef").Value;
        Assert.Equal(0, line.Find(e => e.Key == "v1").Value);
        Assert.Equal(true, line.Find(e => e.Key == "blocking").Value);
    }

    [Fact]
    public void KeysAreLoweredAndStringValuesPreserveCase()
    {
        var root = ParseOk("""
            NameSpace = "DoomBuilder";
            """);

        // Keys lowered to canonical form.
        Assert.Equal("namespace", root[0].Key);
        // Quoted strings preserve case.
        Assert.Equal("DoomBuilder", root[0].Value);
    }

    [Fact]
    public void LineAndBlockCommentsAreSkipped()
    {
        var root = ParseOk("""
            // a leading line comment
            namespace = "Doom"; // trailing comment
            /* a block
               comment that spans
               multiple lines */
            count = 7;
            """);

        Assert.Equal("Doom", root.Find(e => e.Key == "namespace").Value);
        Assert.Equal(7, root.Find(e => e.Key == "count").Value);
    }

    [Fact]
    public void StringEscapesAreHonored()
    {
        var root = ParseOk(@"name = ""hello\nworld\t\""quoted\"""";");
        string s = (string)root[0].Value;
        Assert.Equal("hello\nworld\t\"quoted\"", s);
    }

    [Fact]
    public void NaNValuesAreDroppedWithWarning()
    {
        var p = new UniversalParser();
        bool ok = p.InputConfiguration("x = nan;");
        Assert.True(ok); // NaN doesn't error, it warns
        Assert.True(p.HasWarnings);
        Assert.Empty(p.Root); // entry dropped
    }

    [Fact]
    public void InvalidKeywordRaisesError()
    {
        var p = new UniversalParser();
        bool ok = p.InputConfiguration("x = wat;");
        Assert.False(ok);
        Assert.NotEqual(0, p.ErrorResult);
        Assert.Contains("Unknown keyword", p.ErrorDescription);
    }

    [Fact]
    public void RoundTripPreservesAllTypes()
    {
        var p1 = new UniversalParser();
        p1.InputConfiguration("""
            namespace = "Doom";
            count = 7;
            ratio = 3.14;
            flag = true;
            sub
            {
                x = 1;
                y = 2;
            }
            """);
        string output = p1.OutputConfiguration("\n", true);

        var p2 = new UniversalParser();
        bool ok = p2.InputConfiguration(output);
        Assert.True(ok, $"Re-parse failed: {p2.ErrorDescription}");
        Assert.Equal("Doom", p2.Root.Find(e => e.Key == "namespace").Value);
        Assert.Equal(7, p2.Root.Find(e => e.Key == "count").Value);
        Assert.Equal(3.14, (double)p2.Root.Find(e => e.Key == "ratio").Value, 1e-9);
        Assert.Equal(true, p2.Root.Find(e => e.Key == "flag").Value);
        var sub = (UniversalCollection)p2.Root.Find(e => e.Key == "sub").Value;
        Assert.Equal(1, sub.Find(e => e.Key == "x").Value);
    }

    [Fact]
    public void EntryValidateTypeThrowsOnMismatch()
    {
        var root = ParseOk("count = 3;");
        var entry = root[0];
        entry.ValidateType(typeof(int)); // OK
        Assert.Throws<System.Exception>(() => entry.ValidateType(typeof(string)));
    }

    [Fact]
    public void ParsesAUdmfMapSnippet()
    {
        // A realistic snippet that resembles UDB-format UDMF map data
        var root = ParseOk("""
            namespace = "ZDoomTranslated";

            thing
            {
                x = 32.000;
                y = 64.000;
                angle = 90;
                type = 1;
                skill1 = true;
                skill2 = true;
            }

            vertex { x = 0; y = 0; }
            vertex { x = 256; y = 0; }
            vertex { x = 256; y = 256; }
            vertex { x = 0; y = 256; }

            linedef { v1 = 0; v2 = 1; blocking = true; sidefront = 0; }
            linedef { v1 = 1; v2 = 2; blocking = true; sidefront = 1; }
            linedef { v1 = 2; v2 = 3; blocking = true; sidefront = 2; }
            linedef { v1 = 3; v2 = 0; blocking = true; sidefront = 3; }
            """);

        // 1 namespace + 1 thing + 4 vertex + 4 linedef = 10 entries
        Assert.Equal(10, root.Count);
        Assert.Equal(4, root.FindAll(e => e.Key == "vertex").Count);
        Assert.Equal(4, root.FindAll(e => e.Key == "linedef").Count);
    }
}
