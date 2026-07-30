using System.IO.Compression;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace Round.SDK.Helper;

public class ZipHelper
{
    public static void CreateZipFile(string sourceFolder, string zipPath)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(sourceFolder, zipPath);
    }

    public static void ExtractZipFile(string file, string extractDir, bool isExists = false)
    {
        if (Directory.Exists(extractDir) && !isExists) return;
        Directory.CreateDirectory(extractDir);
        ZipFile.ExtractToDirectory(file, extractDir, true);
        Console.WriteLine($@"包已解压到: {extractDir}");
    }

    public static void ExtractTarGz(string tarGzPath, string extractDir, bool isExists = false)
    {
        if (Directory.Exists(extractDir) && !isExists) return;
        Directory.CreateDirectory(extractDir);

        using (var fileStream = File.OpenRead(tarGzPath))
        using (var gzipStream = new GZipInputStream(fileStream))
        using (var tarArchive = TarArchive.CreateInputTarArchive(gzipStream, System.Text.Encoding.UTF8))
        {
            try { tarArchive.ExtractContents(extractDir); } catch { }
        }

        // Read tar again to restore execute permissions from entry headers
        using (var fileStream = File.OpenRead(tarGzPath))
        using (var gzipStream = new GZipInputStream(fileStream))
        using (var tarIn = new TarInputStream(gzipStream, System.Text.Encoding.UTF8))
        {
            TarEntry entry;
            while ((entry = tarIn.GetNextEntry()) != null)
            {
                if (entry.IsDirectory) continue;

                var outPath = Path.Combine(extractDir, entry.Name);
                if (!File.Exists(outPath)) continue;

                if (OperatingSystem.IsLinux() && (entry.TarHeader.Mode & 0x49) != 0)
                {
                    File.SetUnixFileMode(outPath,
                        File.GetUnixFileMode(outPath) |
                        UnixFileMode.UserExecute |
                        UnixFileMode.GroupExecute |
                        UnixFileMode.OtherExecute);
                }
            }
        }

        Console.WriteLine($@"tar.gz包已解压到: {extractDir}");
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