using Round.SDK.Enum;

namespace Round.SDK.Helper.IO;

public static class DirectoryLinkChecker
{
    public static DirectoryType CheckFolderType(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return DirectoryType.NotFound;

        var di = new DirectoryInfo(folderPath);

        // 检查是否是重解析点
        if (!di.Attributes.HasFlag(FileAttributes.ReparsePoint))
            return DirectoryType.Folder; // 普通文件夹

        // 在.NET 6+中，我们可以进一步判断
#if NET6_0_OR_GREATER
        if (di.LinkTarget != null)
        {
            // 检查链接目标是否存在
            var target = di.LinkTarget;

            // 判断是符号链接还是连接点
            if (Path.IsPathRooted(target))
                // 如果是完整路径，可能是符号链接
                try
                {
                    // 检查目标是否是文件或文件夹
                    if (File.Exists(target))
                        return DirectoryType.SymbolicLink; // 文件符号链接
                    if (Directory.Exists(target))
                        return DirectoryType.SymbolicLink; // 文件夹符号链接
                }
                catch
                {
                    return DirectoryType.JunctionLink; // 可能是连接点
                }

            return DirectoryType.JunctionLink; // 连接点
        }
#endif

        return DirectoryType.JunctionLink; // 默认认为是连接点
    }
}