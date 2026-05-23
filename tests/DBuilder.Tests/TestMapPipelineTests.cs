// ABOUTME: Smoke test for the Test Map pipeline - builds the same map-only PWAD the editor writes and reloads it.
// ABOUTME: Skips when the Heretic IWAD is unavailable; writes /tmp output a source port can then be launched on.

using System.IO;
using System.Linq;
using DBuilder.IO;

namespace DBuilder.Tests;

public class TestMapPipelineTests
{
    [Fact]
    public void MapOnlyPwadRoundTripsForTesting()
    {
        const string iwad = "/Users/jsh/media/DoomStuff/heretic.wad";
        if (!File.Exists(iwad)) return; // smoke only when the IWAD is present

        // Load the first map exactly as the editor does.
        MapEntry entry;
        DBuilder.Map.MapSet map;
        using (var src = new WAD(iwad, openreadonly: true))
        {
            entry = WadMaps.Find(src).First();
            map = WadMaps.Load(src, entry)!;
        }

        // Build a minimal PWAD with only that map block (mirrors OnTestMap).
        byte[] bytes;
        using (var ms = new MemoryStream())
        using (var dst = new WAD(ms))
        {
            WadMaps.SaveMap(dst, entry.Name, map, entry.Format);
            bytes = ms.ToArray();
        }

        string outPath = Path.Combine(Path.GetTempPath(), $"dbuilder_test_{entry.Name}.wad");
        File.WriteAllBytes(outPath, bytes);

        // The produced PWAD must reopen and contain the map.
        using var reopened = new WAD(new MemoryStream(bytes), openreadonly: true);
        Assert.False(reopened.IsIWAD);
        var found = WadMaps.Find(reopened);
        Assert.Contains(found, m => m.Name == entry.Name);
        Assert.NotNull(WadMaps.Load(reopened, found.First(m => m.Name == entry.Name)));
    }
}
