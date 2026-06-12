// ABOUTME: Verifies the machine-readable UDB parity matrix covers upstream source areas.
// ABOUTME: Keeps release tracking grounded in the current Ultimate Doom Builder checkout.

using System.Text.Json;

namespace DBuilder.Tests;

public sealed class ParityMatrixTests
{
    [Fact]
    public void CoreParityMatrixCoversEveryUdbCoreSourceFolderWhenCloneIsAvailable()
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        string sourceCore = Path.Combine(udbRoot, "Source", "Core");
        Assert.True(Directory.Exists(sourceCore), $"Expected UDB Source/Core folder at {sourceCore}.");

        string[] expected = Directory.EnumerateDirectories(sourceCore)
            .Select(path => "Source/Core/" + Path.GetFileName(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] actual = ReadCoreParityAreas()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CoreParityMatrixEntriesDeclareStatusAndReplacementLocation()
    {
        string path = RepositoryPath("docs/parity-matrix.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        foreach (JsonElement entry in document.RootElement.GetProperty("core").EnumerateArray())
        {
            string area = entry.GetProperty("udbArea").GetString() ?? "";
            if (!area.StartsWith("Source/Core/", StringComparison.Ordinal)) continue;

            string status = entry.GetProperty("status").GetString() ?? "";
            string location = entry.GetProperty("dbuilderLocation").GetString() ?? "";

            Assert.Contains(status, new[] { "ported", "partial", "missing" });
            if (status != "missing")
                Assert.False(string.IsNullOrWhiteSpace(location), $"{area} needs an explicit DBuilder replacement location.");
        }
    }

    private static IReadOnlyList<string> ReadCoreParityAreas()
    {
        string path = RepositoryPath("docs/parity-matrix.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("core")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("udbArea").GetString() ?? "")
            .Where(area => area.StartsWith("Source/Core/", StringComparison.Ordinal))
            .ToArray();
    }

    private static string? FindUdbRoot()
    {
        string repositoryRoot = RepositoryPath(".");
        string sibling = Path.GetFullPath(Path.Combine(repositoryRoot, "..", "UltimateDoomBuilder"));
        if (Directory.Exists(Path.Combine(sibling, "Source", "Core"))) return sibling;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string root = Path.Combine(home, "dev", "repos", "UltimateDoomBuilder");
        return Directory.Exists(Path.Combine(root, "Source", "Core")) ? root : null;
    }

    private static string RepositoryPath(string relativePath)
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
}
