using System.IO.Compression;

namespace Round.SDK.Helper;

public class ZipHelper
{
    public static void CreateZipFile(string sourceFolder, string zipPath)
    {
        // 如果ZIP文件已存在，先删除
        if (File.Exists(zipPath)) File.Delete(zipPath);

        // 创建ZIP文件
        ZipFile.CreateFromDirectory(sourceFolder, zipPath);
    }

    public static void ExtractZipFile(string file, string extractDir, bool isExists = false)
    {
        // 如果目录已存在，退出
        if (Directory.Exists(extractDir) &&
            !isExists) return;

        Directory.CreateDirectory(extractDir);

        // 解压ZIP文件
        ZipFile.ExtractToDirectory(file, extractDir);
        Console.WriteLine($@"包已解压到: {extractDir}");
    }
    
    public static string GetTextFileContent(string zipPath, string targetFileName)
    {
        using (ZipArchive archive = ZipFile.OpenRead(zipPath))
        {
            ZipArchiveEntry entry = archive.GetEntry(targetFileName);
            
            if (entry != null)
            {
                using (StreamReader reader = new StreamReader(entry.Open()))
                {
                    return reader.ReadToEnd();
                }
            }
        }
        return null;
    }
}