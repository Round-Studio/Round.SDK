using System.Runtime.InteropServices;

namespace Round.SDK.Helper;

public class OS
{
    public static string GetSystemType()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macos";
        return "unknown";
    }
}