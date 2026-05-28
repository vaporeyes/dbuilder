// ABOUTME: Stream-level UDMF reader facade mirroring UDB's UniversalStreamReader entry point.
// ABOUTME: Delegates parsed TEXTMAP content to UdmfMapLoader while preserving parser diagnostics for callers.

using System.IO;
using System.Text;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed class UniversalStreamReader
{
    public bool StrictChecking { get; set; }
    public UniversalParser? LastParser { get; private set; }

    public MapSet Read(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        string text = reader.ReadToEnd();
        var map = UdmfMapLoader.Load(text, out var parser, StrictChecking);
        LastParser = parser;
        if (map == null)
            throw new InvalidDataException($"Error on line {parser.ErrorLine} while parsing UDMF map data: {parser.ErrorDescription}");

        return map;
    }
}
