// ABOUTME: Verifies packaged editor asset path resolution for default configurations and tools.
// ABOUTME: Keeps release output layout stable while bundled UDB-compatible cfg files are added.

using DBuilder.Editor;

namespace DBuilder.Tests;

public sealed class EditorAssetPathsTests
{
    [Fact]
    public void DefaultConfigDirPrefersBundledAssetsWhenPresent()
    {
        string root = NewTempDir();
        try
        {
            string configDir = Path.Combine(root, "assets", "Common", "Configurations");
            Directory.CreateDirectory(configDir);

            Assert.Equal(configDir, EditorAssetPaths.DefaultConfigDir(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AssetPathHelpersResolveCompilerAndScriptDirectoriesFromConfigRoot()
    {
        string configDir = Path.Combine("/publish", "assets", "Common", "Configurations");

        Assert.Equal(
            Path.Combine("/publish", "assets", "Windows", "Compilers", "Nodebuilders"),
            EditorAssetPaths.NodebuilderConfigDir(configDir, isWindows: true));
        Assert.Equal(
            Path.Combine("/publish", "assets", "Linux", "Compilers", "Nodebuilders"),
            EditorAssetPaths.NodebuilderConfigDir(configDir, isWindows: false));
        Assert.Equal(
            Path.Combine("/publish", "assets", "Common", "Scripting"),
            EditorAssetPaths.ScriptConfigDir(configDir));
    }

    [Fact]
    public void EditorProjectPublishesDefaultAssetLayout()
    {
        string project = File.ReadAllText(RepositoryPath("src/DBuilder.Editor/DBuilder.Editor.csproj"));

        Assert.Contains("Include=\"..\\..\\assets\\Common\\**\\*\"", project, StringComparison.Ordinal);
        Assert.Contains("Link=\"assets\\Common\\%(RecursiveDir)%(Filename)%(Extension)\"", project, StringComparison.Ordinal);
        Assert.Contains("PackagePath=\"assets\\Common\\%(RecursiveDir)\"", project, StringComparison.Ordinal);
        Assert.True(File.Exists(RepositoryPath("assets/Common/Configurations/README.md")));
        Assert.True(File.Exists(RepositoryPath("assets/Common/Scripting/README.md")));
        Assert.True(File.Exists(RepositoryPath("assets/Linux/Compilers/Nodebuilders/README.md")));
        Assert.True(File.Exists(RepositoryPath("assets/Windows/Compilers/Nodebuilders/README.md")));
    }

    private static string NewTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "dbuilder-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string RepositoryPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
    }
}
