using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Round.SDK.Helper;

public static class CheckVersion
{
    public static bool CheckTimeAndExecute24Hour(DateTime targetTime)
    {
        DateTime currentTime = DateTime.Now;
    
        // 计算时间差
        TimeSpan timeDifference = currentTime - targetTime;
    
        // 检查是否在24小时内
        return timeDifference.TotalHours <= 24 && timeDifference.TotalHours >= 0;
    }
    /// <summary>
    /// 获取程序集的构建时间戳（从 AssemblyMetadata）
    /// </summary>
    public static DateTime? GetBuildTimestamp(Assembly assembly)
    {
        if (assembly == null) return null;

        var attribute = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildTimestamp");

        return DateTime.TryParse(attribute?.Value, out var dt) ? dt : null;
    }

    /// <summary>
    /// 获取程序集文件的最后写入时间
    /// </summary>
    public static DateTime? GetLastWriteTime(Assembly assembly)
    {
        if (assembly == null) return null;

        try
        {
            return new FileInfo(assembly.Location).LastWriteTime;
        }
        catch
        {
            // 避免异常暴露，返回 null
            return null;
        }
    }

    /// <summary>
    /// 从文件路径加载程序集并获取构建时间戳
    /// </summary>
    public static DateTime? GetBuildTimestampFromPath(string assemblyPath)
    {
        if (!File.Exists(assemblyPath)) return null;

        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            return GetBuildTimestamp(assembly);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从文件路径获取程序集的最后写入时间
    /// </summary>
    public static DateTime? GetLastWriteTimeFromPath(string assemblyPath)
    {
        return File.Exists(assemblyPath) ? (DateTime?)File.GetLastWriteTime(assemblyPath) : null;
    }
}