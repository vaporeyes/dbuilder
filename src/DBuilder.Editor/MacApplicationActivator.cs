// ABOUTME: macOS AppKit activation helper for editor launches started from dotnet run.
// ABOUTME: Promotes the process to a foreground app so the editor receives keyboard input.

using System;
using System.Runtime.InteropServices;

namespace DBuilder.Editor;

internal static class MacApplicationActivator
{
    public static void Activate()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        try
        {
            IntPtr nsApplication = objc_getClass("NSApplication");
            if (nsApplication == IntPtr.Zero) return;

            IntPtr sharedApplication = objc_msgSend(nsApplication, sel_registerName("sharedApplication"));
            if (sharedApplication == IntPtr.Zero) return;

            objc_msgSend(sharedApplication, sel_registerName("setActivationPolicy:"), 0);
            objc_msgSend(sharedApplication, sel_registerName("activateIgnoringOtherApps:"), true);
        }
        catch
        {
            // Best-effort activation only. Avalonia's normal focus path still runs on unsupported hosts.
        }
    }

    [DllImport("/usr/lib/libobjc.A.dylib", CharSet = CharSet.Ansi)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", CharSet = CharSet.Ansi)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend(IntPtr receiver, IntPtr selector, nint argument);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool argument);
}
