// ABOUTME: Coverage inventory for DBuilder.IO parser, reader, writer, and serializer test surfaces.
// ABOUTME: Keeps the broad parser and serializer validation tracker grounded in explicit test ownership.

using System.Reflection;
using DBuilder.IO;

namespace DBuilder.Tests;

public sealed class ParserSerializerCoverageInventoryTests
{
    private static readonly IReadOnlyDictionary<string, string> CoveredBy = new Dictionary<string, string>
    {
        ["AnimdefsParser"] = nameof(AnimdefsParserTests),
        ["ClipboardStreamReader"] = nameof(ClipboardStreamTests),
        ["ClipboardStreamWriter"] = nameof(ClipboardStreamTests),
        ["ClippedStream"] = nameof(ClippedStreamTests),
        ["CvarInfoParser"] = nameof(CvarInfoParserTests),
        ["DecaldefParser"] = nameof(DecaldefParserTests),
        ["DecorateParser"] = nameof(DecorateParserTests),
        ["DehackedParser"] = nameof(DehackedParserTests),
        ["DeserializerStream"] = nameof(SerializerStreamTests),
        ["DoomColormapReader"] = nameof(DoomColormapTests),
        ["DoomFlatReader"] = nameof(DoomPaletteAndFlatTests),
        ["DoomMapWriter"] = nameof(DoomMapWriterTests),
        ["DoomPalette"] = nameof(DoomPaletteAndFlatTests),
        ["DoomPatchNames"] = nameof(DoomTextureCompositionTests),
        ["DoomPictureReader"] = nameof(DoomPictureReaderTests),
        ["DoomTextureListReader"] = nameof(DoomTextureCompositionTests),
        ["GldefsParser"] = nameof(GldefsParserTests),
        ["HexenMapWriter"] = nameof(HexenMapWriterTests),
        ["IwadInfoParser"] = nameof(IwadInfoParserTests),
        ["LockdefsParser"] = nameof(LockdefsParserTests),
        ["ModeldefParser"] = nameof(ModeldefParserTests),
        ["NodesReader"] = nameof(NodesReaderTests),
        ["ReverbsParser"] = nameof(ReverbsParserTests),
        ["SerializerStream"] = nameof(SerializerStreamTests),
        ["SndInfoParser"] = nameof(SndInfoParserTests),
        ["SndSeqParser"] = nameof(SndSeqParserTests),
        ["TerrainParser"] = nameof(TerrainParserTests),
        ["TexturesParser"] = nameof(TexturesParserTests),
        ["UdmfMapWriter"] = nameof(UdmfMapWriterTests),
        ["UniversalParser"] = nameof(UniversalParserTests),
        ["UniversalStreamReader"] = nameof(UniversalStreamTests),
        ["UniversalStreamWriter"] = nameof(UniversalStreamTests),
        ["UsdfDialogueParser"] = nameof(UsdfDialogueParserTests),
        ["VoxeldefParser"] = nameof(VoxeldefParserTests),
        ["X11RgbParser"] = nameof(X11RgbParserTests),
        ["ZScriptParser"] = nameof(ZScriptAndDoomEdNumsTests),
    };

    [Fact]
    public void DBuilderIoParserSerializerSurfacesHaveDeclaredTestCoverage()
    {
        string[] expected = CoveredBy.Keys.Order(StringComparer.Ordinal).ToArray();
        string[] actual = typeof(DecorateParser).Assembly.GetTypes()
            .Where(IsParserSerializerSurface)
            .Select(type => type.Name)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DeclaredCoveragePointsAtExistingTestClasses()
    {
        HashSet<string> tests = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type => type.Namespace == typeof(ParserSerializerCoverageInventoryTests).Namespace)
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach ((string surface, string testClass) in CoveredBy)
            Assert.True(tests.Contains(testClass), $"{surface} coverage points at missing test class {testClass}.");
    }

    private static bool IsParserSerializerSurface(Type type)
    {
        if (type.Namespace != "DBuilder.IO") return false;
        if (!type.IsPublic && !type.IsNestedPublic) return false;
        if (type.Name is "IReadWriteStream" or "UniversalStreamReaderOptions") return false;
        if (type.Name.EndsWith("Parser", StringComparison.Ordinal)) return true;
        if (type.Name.EndsWith("Reader", StringComparison.Ordinal)) return true;
        if (type.Name.EndsWith("Writer", StringComparison.Ordinal)) return true;
        if (type.Name.EndsWith("Stream", StringComparison.Ordinal)) return true;
        return type.Name is "DoomPatchNames" or "DoomPalette";
    }
}
