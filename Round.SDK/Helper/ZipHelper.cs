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

    public static void ExtractZipFile(string file, string extractDir)
    {
        // 如果目录已存在，退出
        if (Directory.Exists(extractDir)) return;

        Directory.CreateDirectory(extractDir);

        // 解压ZIP文件
        ZipFile.ExtractToDirectory(file, extractDir);
        Console.WriteLine($@"包已解压到: {extractDir}");
    }
}