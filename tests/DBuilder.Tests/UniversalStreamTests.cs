// ABOUTME: Tests stream-level UDMF reader and writer facades over UniversalParser and UdmfMapLoader.
// ABOUTME: Covers parse diagnostics, stream lifetime, ASCII output, and optional namespace emission.

using System.IO;
using System.Text;
using DBuilder.Geometry;
using DBuilder.IO;
using DBuilder.Map;

namespace DBuilder.Tests;

public class UniversalStreamTests
{
    [Fact]
    public void ReaderLoadsMapFromStreamAndLeavesStreamOpen()
    {
        const string text = """
            namespace = "Doom";
            vertex { x = 1; y = 2; }
            """;
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(text));

        var map = new UniversalStreamReader().Read(stream);

        Assert.True(stream.CanRead);
        Assert.Equal("Doom", map.Namespace);
        Assert.Single(map.Vertices);
        Assert.Equal(new Vector2D(1, 2), map.Vertices[0].Position);
    }

    [Fact]
    public void ReaderThrowsParserErrorInStrictMode()
    {
        const string text = """
            namespace = "Doom";
            bad-key = 1;
            """;
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(text));
        var reader = new UniversalStreamReader { StrictChecking = true };

        var ex = Assert.Throws<InvalidDataException>(() => reader.Read(stream));

        Assert.Contains("Error on line", ex.Message);
        Assert.NotNull(reader.LastParser);
        Assert.NotEqual(0, reader.LastParser!.ErrorResult);
    }

    [Fact]
    public void WriterWritesAsciiMapAndLeavesStreamOpen()
    {
        var map = new MapSet { Namespace = "Doom" };
        map.Vertices.Add(new Vertex(new Vector2D(8, 16)));
        using var stream = new MemoryStream();

        new UniversalStreamWriter().Write(map, stream, "Doom");

        Assert.True(stream.CanWrite);
        string text = Encoding.ASCII.GetString(stream.ToArray());
        Assert.Contains("namespace = \"Doom\";", text);
        Assert.Contains("vertex // 0", text);
        Assert.Contains("x = 8.0;", text);
    }

    [Fact]
    public void WriterCanOmitNamespace()
    {
        var map = new MapSet { Namespace = "Doom" };
        map.Fields["comment"] = "kept";
        using var stream = new MemoryStream();

        new UniversalStreamWriter().Write(map, stream, null);

        string text = Encoding.ASCII.GetString(stream.ToArray());
        Assert.DoesNotContain("namespace", text);
        Assert.Contains("comment = \"kept\";", text);
    }
}
