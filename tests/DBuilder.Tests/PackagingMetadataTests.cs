// ABOUTME: Verifies editor packaging metadata before platform-specific installers are complete.
// ABOUTME: Pins the shared app icon asset and Windows manifest identity used by release builds.

using System.Xml.Linq;

namespace DBuilder.Tests;

public sealed class PackagingMetadataTests
{
    [Fact]
    public void EditorProjectDefinesPackageIconAndAppMetadata()
    {
        var document = XDocument.Load(RepositoryPath("src/DBuilder.Editor/DBuilder.Editor.csproj"));
        string xml = document.ToString(SaveOptions.DisableFormatting);

        Assert.Contains("<AssemblyTitle>DBuilder Editor</AssemblyTitle>", xml);
        Assert.Contains("<Product>DBuilder</Product>", xml);
        Assert.Contains("<Description>Cross-platform Doom map editor ported toward Ultimate Doom Builder parity.</Description>", xml);
        Assert.Contains("<PackageIcon>main.png</PackageIcon>", xml);
        Assert.Contains("Include=\"..\\..\\assets\\main.png\"", xml);
        Assert.Contains("Link=\"main.png\"", xml);
        Assert.Contains("Pack=\"true\"", xml);
        Assert.Contains("CopyToOutputDirectory=\"PreserveNewest\"", xml);
        Assert.True(File.Exists(RepositoryPath("assets/main.png")));
    }

    [Fact]
    public void MainIconAssetIsPng()
    {
        byte[] bytes = File.ReadAllBytes(RepositoryPath("assets/main.png"));

        Assert.True(bytes.Length > 8);
        Assert.Equal([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a], bytes[..8]);
    }

    [Fact]
    public void WindowsManifestKeepsDBuilderIdentityAndPerMonitorDpi()
    {
        var document = XDocument.Load(RepositoryPath("src/DBuilder.Editor/app.manifest"));
        XNamespace asm = "urn:schemas-microsoft-com:asm.v1";
        XNamespace win = "http://schemas.microsoft.com/SMI/2016/WindowsSettings";

        XElement identity = document.Root!.Element(asm + "assemblyIdentity")!;
        Assert.Equal("DBuilder.Editor", identity.Attribute("name")!.Value);
        Assert.Equal("1.0.0.0", identity.Attribute("version")!.Value);
        Assert.Equal("PerMonitorV2", document.Descendants(win + "dpiAwareness").Single().Value);
    }

    private static string RepositoryPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
    }
}
