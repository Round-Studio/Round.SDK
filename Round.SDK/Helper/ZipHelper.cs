using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;

namespace Round.SDK.Helper;

public class ZipHelper
{
    public static void CreateZipFile(string sourceFolder, string zipPath)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        System.IO.Compression.ZipFile.CreateFromDirectory(sourceFolder, zipPath);
    }

    public static void ExtractZipFile(string file, string extractDir, bool isExists = false)
    {
        extractDir = Path.GetFullPath(extractDir);
    
        if (Directory.Exists(extractDir) && !isExists)
        {
            Console.WriteLine($"目录已存在，跳过解压: {extractDir}");
            return;
        }
    
        Directory.CreateDirectory(extractDir);
    
        using (var zipFile = new ZipFile(file))
        {
            foreach (ZipEntry entry in zipFile)
            {
                if (entry.IsDirectory) continue;
            
                string entryName = entry.Name.Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar);
            
                string fullPath = Path.Combine(extractDir, entryName);
            
                if (!fullPath.StartsWith(extractDir, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"跳过非法路径: {entryName}");
                    continue;
                }
            
                string? directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            
                using (var streamReader = zipFile.GetInputStream(entry))
                using (var streamWriter = File.Create(fullPath))
                {
                    streamReader.CopyTo(streamWriter);
                }
            }
        }
    
        Console.WriteLine($"包已解压到: {extractDir}");
    }

    public static void ExtractTarGz(string tarGzPath, string extractDir, bool isExists = false)
    {
        if (Directory.Exists(extractDir) && !isExists) return;
        Directory.CreateDirectory(extractDir);

        try
        {
            using (var fileStream = File.OpenRead(tarGzPath))
            using (var gzipStream = new GZipInputStream(fileStream))
            using (var tarIn = new TarInputStream(gzipStream, System.Text.Encoding.UTF8))
            {
                TarEntry entry;
                while ((entry = tarIn.GetNextEntry()) != null)
                {
                    var outPath = GetSafePath(extractDir, entry.Name);
                    if (outPath == null)
                    {
                        Console.WriteLine($"跳过越界路径: {entry.Name}");
                        continue;
                    }

                    if (entry.IsDirectory)
                    {
                        Directory.CreateDirectory(outPath);
                        continue;
                    }

                    if (entry.TarHeader.TypeFlag == TarHeader.LF_SYMLINK)
                    {
                        ExtractSymlink(entry, outPath, extractDir);
                        continue;
                    }

                    var parentDir = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(parentDir)) Directory.CreateDirectory(parentDir);

                    if (EntryExists(outPath)) File.Delete(outPath);

                    using (var outStream = File.Create(outPath))
                    {
                        tarIn.CopyEntryContents(outStream);
                    }

                    if (OperatingSystem.IsLinux())
                    {
                        RestoreUnixMode(outPath, entry.TarHeader.Mode);
                    }
                }
            }

            Console.WriteLine($"tar.gz包已解压到: {extractDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"解压失败: {ex.Message}");
            throw;
        }
    }

    private static void ExtractSymlink(TarEntry entry, string linkPath, string extractDir)
    {
        var linkName = entry.TarHeader.LinkName;
        if (string.IsNullOrEmpty(linkName)) return;

        var linkDir = Path.GetDirectoryName(linkPath);
        if (Path.IsPathRooted(linkName) || !IsPathWithin(Path.Combine(linkDir, linkName), extractDir))
        {
            Console.WriteLine($"跳过非法软链接 {entry.Name} -> {linkName}");
            return;
        }

        if (!string.IsNullOrEmpty(linkDir)) Directory.CreateDirectory(linkDir);

        if (EntryExists(linkPath)) File.Delete(linkPath);

        try
        {
            File.CreateSymbolicLink(linkPath, linkName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"创建软链接失败 {entry.Name} -> {linkName}: {ex.Message}");
        }
    }

    private static string GetSafePath(string extractDir, string name)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(extractDir, name));
        }
        catch (Exception)
        {
            return null;
        }

        return IsPathWithin(fullPath, extractDir) ? fullPath : null;
    }

    private static bool IsPathWithin(string path, string rootDir)
    {
        string fullPath;
        string fullRoot;
        try
        {
            fullPath = Path.GetFullPath(path);
            fullRoot = Path.GetFullPath(rootDir);
        }
        catch (Exception)
        {
            return false;
        }

        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            fullRoot += Path.DirectorySeparatorChar;
        }

        return fullPath.StartsWith(fullRoot);
    }

    private static bool EntryExists(string path)
    {
        try
        {
            return File.Exists(path) || Directory.Exists(path) || File.ResolveLinkTarget(path, false) != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void RestoreUnixMode(string path, int mode)
    {
        if ((mode & 0x40) == 0) return;

        try
        {
            var newMode = File.GetUnixFileMode(path);

            if ((mode & 0x40) != 0) newMode |= UnixFileMode.UserExecute;
            if ((mode & 0x08) != 0) newMode |= UnixFileMode.GroupExecute;
            if ((mode & 0x01) != 0) newMode |= UnixFileMode.OtherExecute;

            File.SetUnixFileMode(path, newMode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"设置权限失败 {path}: {ex.Message}");
        }
    }


    public static string GetTextFileContent(string zipPath, string targetFileName)
    {
        using ( System.IO.Compression.ZipArchive archive =  System.IO.Compression.ZipFile.OpenRead(zipPath))
        {
            System.IO.Compression.ZipArchiveEntry entry = archive.GetEntry(targetFileName);
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