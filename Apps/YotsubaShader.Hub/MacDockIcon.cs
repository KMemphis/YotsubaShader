using System.Runtime.InteropServices;

namespace YotsubaShader.Hub;

/// <summary>
/// Sets the Dock icon of a process that is not running from a .app bundle (for example `dotnet run`). Packaged builds get the
/// icon from the bundle Velopack creates, so this is only a convenience; any failure is ignored.
/// </summary>
internal static class MacDockIcon
{
    [DllImport("/usr/lib/libobjc.dylib")] private static extern IntPtr objc_getClass(string name);
    [DllImport("/usr/lib/libobjc.dylib")] private static extern IntPtr sel_registerName(string name);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr target, IntPtr selector);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr target, IntPtr selector, IntPtr argument);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")] private static extern IntPtr SendString(IntPtr target, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string argument);

    public static void Set(string pngPath)
    {
        if (!OperatingSystem.IsMacOS() || !File.Exists(pngPath)) return;
        try
        {
            var path = SendString(objc_getClass("NSString"), sel_registerName("stringWithUTF8String:"), pngPath);
            var image = Send(Send(objc_getClass("NSImage"), sel_registerName("alloc")), sel_registerName("initWithContentsOfFile:"), path);
            if (image == IntPtr.Zero) return;
            var app = Send(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            Send(app, sel_registerName("setApplicationIconImage:"), image);
        }
        catch { /* cosmetic */ }
    }
}
