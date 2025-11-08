using System.Text.Json;
using Round.SDK.Entry;

namespace Round.SDK.Helper;

public class PluginFileInfoHelper
{
    private static string TempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RoundStudio", "SDK", "SDK.Plugin.Temp");
    
    public static PackConfig? GetFileInfo(string filePath)
    {
        var ext = Path.Combine(TempPath, Guid.NewGuid().ToString().Replace("-", ""));
        ZipHelper.ExtractZipFile(filePath, ext);

        var jsonFile = Path.Combine(ext, "pack.json");
        var result = JsonSerializer.Deserialize<PackConfig>(File.ReadAllText(jsonFile));

        if (!string.IsNullOrEmpty(result.PackIconPath))
        {
            result.PackIconPath = Path.Combine(ext, "assets", "icon", result.PackIconPath);
        }
        
        return result;
    }
}