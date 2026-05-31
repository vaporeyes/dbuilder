// ABOUTME: Verifies that repository licensing stays aligned with the upstream Ultimate Doom Builder port source.
// ABOUTME: Keeps package metadata pointed at the checked-in GPL-3.0 license file.

using System.Xml.Linq;

namespace DBuilder.Tests;

public class LicensingTests
{
    [Fact]
    public void LicenseTextMatchesUltimateDoomBuilderWhenCloneIsAvailable()
    {
        string? udbRoot = FindUdbRoot();
        if (udbRoot == null) return;

        byte[] actual = File.ReadAllBytes(RepositoryPath("LICENSE.txt"));
        byte[] expected = File.ReadAllBytes(Path.Combine(udbRoot, "LICENSE.txt"));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SharedBuildMetadataPackagesGplLicenseFile()
    {
        var document = XDocument.Load(RepositoryPath("Directory.Build.props"));
        string xml = document.ToString(SaveOptions.DisableFormatting);

        Assert.Contains("<PackageLicenseFile>LICENSE.txt</PackageLicenseFile>", xml);
        Assert.Contains("<PackageRequireLicenseAcceptance>true</PackageRequireLicenseAcceptance>", xml);
        Assert.Contains("<_Parameter1>License</_Parameter1>", xml);
        Assert.Contains("<_Parameter2>GPL-3.0</_Parameter2>", xml);
        Assert.Contains("<_Parameter2>Ultimate Doom Builder LICENSE.txt</_Parameter2>", xml);
    }

    private static string? FindUdbRoot()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string root = Path.Combine(home, "dev", "repos", "UltimateDoomBuilder");
        return File.Exists(Path.Combine(root, "LICENSE.txt")) ? root : null;
    }

    private static string RepositoryPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
    }
}
