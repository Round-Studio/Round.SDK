using System.Text.Json;
using Round.SDK.Entry;
using Round.SDK.Helper.IO;

namespace Round.SDK.Helper;

public class PluginFileInfoHelper
{
    private static readonly string TempPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RoundStudio", "SDK", "SDK.Plugin.Temp");

    public static PackConfig? GetFileInfo(string filePath)
    {
        var ext = Path.Combine(TempPath, FileHashCalculator.CalculateHash(filePath, FileHashCalculator.HashType.MD5));
        ZipHelper.ExtractZipFile(filePath, ext);

        var jsonFile = Path.Combine(ext, "pack.json");
        var result = JsonSerializer.Deserialize<PackConfig>(File.ReadAllText(jsonFile));

        if (!string.IsNullOrEmpty(result.PackIconPath))
            result.PackIconPath = Path.Combine(ext, "assets", "icon", result.PackIconPath);

        return result;
    }
}