// ABOUTME: Verifies the main solution includes all source projects that ship with the port.
// ABOUTME: Keeps the standard verification script from missing buildable app and tool projects.

using System.Xml.Linq;

namespace DBuilder.Tests;

public sealed class SolutionMembershipTests
{
    [Fact]
    public void MainSolutionIncludesEverySourceProject()
    {
        string repositoryRoot = RepositoryPath(".");
        string solutionPath = RepositoryPath("DBuilder.slnx");
        XDocument solution = XDocument.Load(solutionPath);

        string[] expected = Directory.EnumerateFiles(
                Path.Combine(repositoryRoot, "src"),
                "*.csproj",
                SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] actual = solution.Descendants("Project")
            .Select(project => project.Attribute("Path")?.Value ?? "")
            .Where(path => path.StartsWith("src/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    private static string RepositoryPath(string relativePath)
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
}
