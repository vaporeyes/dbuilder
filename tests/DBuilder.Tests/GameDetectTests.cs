// ABOUTME: Tests game detection from a WAD's signature lumps (Hexen/Heretic/Doom2/Doom).
// ABOUTME: Builds tiny in-memory WADs with one signature lump each; the real-IWAD case is covered by a skippable smoke test.

using System.IO;
using DBuilder.IO;

namespace DBuilder.Tests;

public class GameDetectTests
{
    private static WAD WadWith(params string[] lumps)
    {
        var wad = new WAD(new MemoryStream());
        foreach (var name in lumps) wad.Insert(name, wad.Lumps.Count, 0);
        wad.WriteHeaders();
        return wad;
    }

    [Fact]
    public void DetectsHexenByWinnowr()
    {
        using var wad = WadWith("MAP01", "WINNOWR"); // MAP01 present but Hexen signature wins
        Assert.Equal(DetectedGame.Hexen, GameDetect.FromWad(wad));
    }

    [Fact]
    public void DetectsHereticByMumsit()
    {
        using var wad = WadWith("E1M1", "MUMSIT");
        Assert.Equal(DetectedGame.Heretic, GameDetect.FromWad(wad));
    }

    [Fact]
    public void DetectsDoom2ByMap01()
    {
        using var wad = WadWith("MAP01", "THINGS");
        Assert.Equal(DetectedGame.Doom2, GameDetect.FromWad(wad));
    }

    [Fact]
    public void DetectsDoomByE1M1()
    {
        using var wad = WadWith("E1M1", "THINGS");
        Assert.Equal(DetectedGame.Doom, GameDetect.FromWad(wad));
    }

    [Fact]
    public void UnknownWhenNoSignature()
    {
        using var wad = WadWith("PLAYPAL", "COLORMAP");
        Assert.Equal(DetectedGame.Unknown, GameDetect.FromWad(wad));
    }

    [Fact]
    public void RealHereticIwadDetectsHeretic()
    {
        const string path = "/Users/jsh/media/DoomStuff/heretic.wad";
        if (!File.Exists(path)) return; // smoke only when the IWAD is available
        using var wad = new WAD(path, openreadonly: true);
        Assert.Equal(DetectedGame.Heretic, GameDetect.FromWad(wad));
    }
}
