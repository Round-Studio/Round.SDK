using System.IO.Compression;

namespace Round.SDK.Helper;

public class ZipHelper
{
    public static void CreateZipFile(string sourceFolder, string zipPath)
    {
        // 如果ZIP文件已存在，先删除
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }
        
        // 创建ZIP文件
        ZipFile.CreateFromDirectory(sourceFolder, zipPath);
    }
}