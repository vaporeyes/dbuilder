// ABOUTME: Resolves editor asset directories for packaged builds and development checkouts.
// ABOUTME: Keeps bundled configuration, compiler, and script paths consistent across the editor.

using System;
using System.IO;

namespace DBuilder.Editor;

public static class EditorAssetPaths
{
    public static string BundledAssetsRoot(string baseDirectory)
        => Path.Combine(baseDirectory, "assets");

    public static string BundledConfigDir(string baseDirectory)
        => Path.Combine(BundledAssetsRoot(baseDirectory), "Common", "Configurations");

    public static string DevelopmentConfigDir()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "dev",
            "repos",
            "UltimateDoomBuilder",
            "Assets",
            "Common",
            "Configurations");

    public static string DefaultConfigDir(string baseDirectory)
    {
        string bundled = BundledConfigDir(baseDirectory);
        return Directory.Exists(bundled) ? bundled : DevelopmentConfigDir();
    }

    public static string? AssetsRootFromConfigDir(string configDir)
    {
        var dir = new DirectoryInfo(configDir);
        if (dir.Name != "Configurations" || dir.Parent?.Name != "Common") return null;
        return dir.Parent.Parent?.FullName;
    }

    public static string NodebuilderConfigDir(string configDir, bool isWindows)
    {
        string platform = isWindows ? "Windows" : "Linux";
        string? assetsRoot = AssetsRootFromConfigDir(configDir);
        return assetsRoot is null ? "" : Path.Combine(assetsRoot, platform, "Compilers", "Nodebuilders");
    }

    public static string ScriptConfigDir(string configDir)
    {
        string? assetsRoot = AssetsRootFromConfigDir(configDir);
        return assetsRoot is null ? "" : Path.Combine(assetsRoot, "Common", "Scripting");
    }
}
