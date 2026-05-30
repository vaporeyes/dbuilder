// ABOUTME: Tests for VoxeldefParser over VOXELDEF sprite-to-voxel declarations.
// ABOUTME: Covers sprite lists, model names, preview settings, palette overrides, and replacement behavior.

using DBuilder.IO;

namespace DBuilder.Tests;

public class VoxeldefParserTests
{
    [Fact]
    public void ParsesSpriteListAndVoxelSettings()
    {
        const string text = @"
BAR1, BAR2 = ""barrel.kvx""
{
    AngleOffset = -90
    Scale = 1.25
    OverridePalette
    Spin = 4
}";

        var voxeldef = VoxeldefParser.Parse(text);

        Assert.Equal(2, voxeldef.Entries.Count);
        var entry = voxeldef.Entries["BAR1"];
        Assert.Same(entry, voxeldef.Entries["BAR2"]);
        Assert.Equal("BARREL.KVX", entry.ModelName);
        Assert.Equal(-90f, entry.AngleOffset);
        Assert.Equal(1.25f, entry.Scale);
        Assert.True(entry.OverridePalette);
        Assert.Equal("spin", entry.Spin);
    }

    [Fact]
    public void LaterDeclarationsReplaceSpriteEntries()
    {
        const string text = @"
FOO = old.kvx { Scale = 1 }
FOO = new.kvx { Scale = 2 }";

        var entry = VoxeldefParser.Parse(text).Entries["FOO"];

        Assert.Equal("NEW.KVX", entry.ModelName);
        Assert.Equal(2f, entry.Scale);
    }

    [Fact]
    public void SkipsDefinitionsWithMalformedNumericSettings()
    {
        const string text = @"
BADANGLE = badangle.kvx { AngleOffset bogus Scale = 1 }
BADANGLESYNTAX = badanglesyntax.kvx { AngleOffset 90 Scale = 1 }
BADSCALE = badscale.kvx { Scale bogus }
BADSCALESYNTAX = badscalesyntax.kvx { Scale 2 }
GOOD = good.kvx { AngleOffset = -45 Scale = 2 }";

        var voxeldef = VoxeldefParser.Parse(text);

        Assert.False(voxeldef.Entries.ContainsKey("BADANGLE"));
        Assert.False(voxeldef.Entries.ContainsKey("BADANGLESYNTAX"));
        Assert.False(voxeldef.Entries.ContainsKey("BADSCALE"));
        Assert.False(voxeldef.Entries.ContainsKey("BADSCALESYNTAX"));
        Assert.Equal("GOOD.KVX", voxeldef.Entries["GOOD"].ModelName);
        Assert.Equal(-45f, voxeldef.Entries["GOOD"].AngleOffset);
        Assert.Equal(2f, voxeldef.Entries["GOOD"].Scale);
    }
}
