// ABOUTME: macOS development launcher that reruns dotnet-run builds through a tiny app bundle.
// ABOUTME: Gives LaunchServices a foreground app identity so the editor can receive keyboard input.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace DBuilder.Editor;

internal static class MacAppBundleLauncher
{
    private const string RelaunchedEnvironmentVariable = "DBUILDER_MAC_APP_BUNDLE";
    private const string BundleName = "DBuilder.Editor.app";
    private const string BundleIdentifier = "dev.jsh.dbuilder.editor";

    public static bool TryRelaunch(string[] args)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return false;
        if (Environment.GetEnvironmentVariable(RelaunchedEnvironmentVariable) == "1") return false;
        if (Environment.ProcessPath is not { Length: > 0 } executablePath) return false;
        if (IsInsideAppBundle(executablePath)) return false;

        string outputDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string bundlePath = Path.Combine(outputDirectory, BundleName);
        string macOsPath = Path.Combine(bundlePath, "Contents", "MacOS");
        string plistPath = Path.Combine(bundlePath, "Contents", "Info.plist");
        string launcherPath = Path.Combine(macOsPath, "DBuilder.Editor");

        Directory.CreateDirectory(macOsPath);
        WriteIfChanged(plistPath, InfoPlist());
        WriteIfChanged(launcherPath, LauncherScript());
        File.SetUnixFileMode(
            launcherPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        try
        {
            using var process = Process.Start(CreateOpenStartInfo(bundlePath, args));
            if (process is null) return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsInsideAppBundle(string executablePath) =>
        executablePath.Contains(".app/Contents/MacOS/", StringComparison.Ordinal);

    private static ProcessStartInfo CreateOpenStartInfo(string bundlePath, string[] args)
    {
        var startInfo = new ProcessStartInfo("open")
        {
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add("-W");
        startInfo.ArgumentList.Add(bundlePath);
        startInfo.ArgumentList.Add("--args");
        foreach (string arg in args) startInfo.ArgumentList.Add(arg);
        return startInfo;
    }

    private static void WriteIfChanged(string path, string contents)
    {
        if (File.Exists(path) && File.ReadAllText(path) == contents) return;
        File.WriteAllText(path, contents);
    }

    private static string InfoPlist() =>
        $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
            <key>CFBundleDevelopmentRegion</key>
            <string>en</string>
            <key>CFBundleExecutable</key>
            <string>DBuilder.Editor</string>
            <key>CFBundleIdentifier</key>
            <string>{{BundleIdentifier}}</string>
            <key>CFBundleInfoDictionaryVersion</key>
            <string>6.0</string>
            <key>CFBundleName</key>
            <string>DBuilder Editor</string>
            <key>CFBundlePackageType</key>
            <string>APPL</string>
            <key>CFBundleShortVersionString</key>
            <string>1.0</string>
            <key>CFBundleVersion</key>
            <string>1</string>
            <key>LSMinimumSystemVersion</key>
            <string>13.0</string>
            <key>NSHighResolutionCapable</key>
            <true/>
            <key>NSPrincipalClass</key>
            <string>NSApplication</string>
        </dict>
        </plist>
        """ + Environment.NewLine;

    private static string LauncherScript() =>
        $"""
        #!/bin/sh
        APP_ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
        export {RelaunchedEnvironmentVariable}=1
        exec "$APP_ROOT/DBuilder.Editor" "$@"
        """ + Environment.NewLine;
}
