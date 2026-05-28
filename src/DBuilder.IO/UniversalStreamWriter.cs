// ABOUTME: Stream-level UDMF writer facade mirroring UDB's UniversalStreamWriter entry point.
// ABOUTME: Writes MapSet data as ASCII TEXTMAP content, with optional namespace omission support.

using System.IO;
using System.Text;
using DBuilder.Map;

namespace DBuilder.IO;

public sealed class UniversalStreamWriter
{
    public void Write(MapSet map, Stream stream, string? writeNamespace)
    {
        string text = UdmfMapWriter.Write(map, writeNamespace);
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(text);
        writer.Flush();
    }
}
