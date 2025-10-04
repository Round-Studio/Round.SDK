using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Round.SDK.Entity;
using Round.SDK.Entry;

namespace Round.SDK.Plugin;

public class PlugLoader
{
    public Type PluginType { get; private set; }
    
    /// <summary>
    /// 插件解压路径
    /// </summary>
    public string ExtractPath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "temp");
    
    /// <summary>
    /// 已加载的程序集缓存
    /// </summary>
    private List<Assembly> _loadedAssemblies = new List<Assembly>();

    public PlugLoader(Type pluginType)
    {
        PluginType = pluginType;
        
        // 确保解压目录存在
        if (!Directory.Exists(ExtractPath))
        {
            Directory.CreateDirectory(ExtractPath);
        }
    }

    /// <summary>
    /// 从插件包加载插件
    /// </summary>
    /// <param name="pluginPackagePath">插件包路径 (.rplck 文件)</param>
    public object Load(string pluginPackagePath)
    {
        if (string.IsNullOrEmpty(pluginPackagePath))
        {
            throw new ArgumentException("插件包路径不能为空");
        }

        if (!File.Exists(pluginPackagePath))
        {
            throw new FileNotFoundException($"插件包文件不存在: {pluginPackagePath}");
        }

        try
        {
            // 1. 解压插件包
            string extractDir = ExtractPluginPackage(pluginPackagePath);
            
            // 2. 读取插件配置
            var packConfig = ReadPackConfig(extractDir);
            
            // 3. 加载依赖的DLL文件
            LoadDependencies(extractDir, packConfig.BodyFile);
            
            // 4. 加载插件主体
            return LoadPluginBody(extractDir, packConfig.BodyFile);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载插件包失败 {pluginPackagePath}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 解压插件包
    /// </summary>
    private string ExtractPluginPackage(string pluginPackagePath)
    {
        string tempExtractDir = Path.Combine(ExtractPath, Path.GetFileNameWithoutExtension(pluginPackagePath));
        
        // 如果目录已存在，先删除
        if (Directory.Exists(tempExtractDir))
        {
            Directory.Delete(tempExtractDir, true);
        }
        
        Directory.CreateDirectory(tempExtractDir);
        
        // 解压ZIP文件
        ZipFile.ExtractToDirectory(pluginPackagePath, tempExtractDir);
        Console.WriteLine($"插件包已解压到: {tempExtractDir}");
        
        return tempExtractDir;
    }

    /// <summary>
    /// 读取插件包配置
    /// </summary>
    private PackConfig ReadPackConfig(string extractDir)
    {
        string configPath = Path.Combine(extractDir, "pack.json");
        
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"插件包配置文件不存在: {configPath}");
        }

        // 这里需要根据你的 ConfigEntity 实现来读取配置
        // 假设你有一个方法来读取JSON配置
        var config = LoadConfig<PackConfig>(configPath);
        
        if (string.IsNullOrEmpty(config.BodyFile))
        {
            throw new InvalidOperationException("插件包配置中未指定主体文件");
        }
        
        Console.WriteLine($"读取插件配置: {config.PackName} v{config.PackVersion}");
        return config;
    }

    /// <summary>
    /// 加载依赖的DLL文件
    /// </summary>
    private void LoadDependencies(string extractDir, string bodyFile)
    {
        string filesDir = Path.Combine(extractDir, "files");
        
        if (!Directory.Exists(filesDir))
        {
            throw new DirectoryNotFoundException($"插件文件目录不存在: {filesDir}");
        }

        // 获取所有DLL文件（排除主体文件）
        var dllFiles = Directory.GetFiles(filesDir, "*.dll")
            .Where(file => !Path.GetFileName(file).Equals(bodyFile, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var dllPath in dllFiles)
        {
            try
            {
                // 加载程序集到当前应用程序域
                var assembly = Assembly.LoadFrom(dllPath);
                _loadedAssemblies.Add(assembly);
                Console.WriteLine($"已加载依赖: {Path.GetFileName(dllPath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载依赖失败 {dllPath}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 加载插件主体
    /// </summary>
    private object LoadPluginBody(string extractDir, string bodyFile)
    {
        string bodyFilePath = Path.Combine(extractDir, "files", bodyFile);
        
        if (!File.Exists(bodyFilePath))
        {
            throw new FileNotFoundException($"插件主体文件不存在: {bodyFilePath}");
        }

        try
        {
            // 加载主体程序集
            var bodyAssembly = Assembly.LoadFrom(bodyFilePath);
            _loadedAssemblies.Add(bodyAssembly);
            
            // 查找插件类型
            var pluginType = bodyAssembly.GetTypes()
                .FirstOrDefault(t => PluginType.IsAssignableFrom(t) && 
                                   !t.IsInterface && 
                                   !t.IsAbstract);

            if (pluginType != null)
            {
                var pluginInstance = Activator.CreateInstance(pluginType);
                Console.WriteLine($"已加载插件主体: {pluginType.FullName}");
                return pluginInstance;
            }
            else
            {
                throw new InvalidOperationException($"在主体文件中未找到符合条件的插件类型: {bodyFile}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载插件主体失败 {bodyFilePath}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 加载配置文件的辅助方法（需要根据你的实际实现调整）
    /// </summary>
    private T LoadConfig<T>(string configPath) where T : new()
    {
        try
        {
            // 这里需要根据你的 ConfigEntity 实现来调整
            // 假设你有一个 ConfigEntity 类可以加载配置
            var configEntity = new ConfigEntity<T>(configPath);
            configEntity.Load();
            return configEntity.Data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取配置文件失败 {configPath}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 清理解压的临时文件
    /// </summary>
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(ExtractPath))
            {
                Directory.Delete(ExtractPath, true);
                Console.WriteLine("已清理临时插件文件");
            }
            
            _loadedAssemblies.Clear();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"清理临时文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取插件信息
    /// </summary>
    public PluginInfo GetPluginInfo(string pluginPackagePath)
    {
        var plugin = Load(pluginPackagePath);
        
        if (plugin != null)
        {
            try
            {
                var info = new PluginInfo
                {
                    Type = plugin.GetType(),
                    Name = GetPropertyValue(plugin, "Name") as string ?? "Unknown",
                    Description = GetPropertyValue(plugin, "Description") as string ?? string.Empty,
                    Version = GetPropertyValue(plugin, "Version") as string ?? "1.0.0",
                    Author = GetPropertyValue(plugin, "Author") as string ?? "Unknown"
                };
                
                return info;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取插件信息失败 {plugin.GetType().FullName}: {ex.Message}");
            }
        }
        
        return null;
    }

    /// <summary>
    /// 获取属性值
    /// </summary>
    private object GetPropertyValue(object obj, string propertyName)
    {
        var property = obj.GetType().GetProperty(propertyName);
        return property?.GetValue(obj);
    }

    /// <summary>
    /// 执行插件方法
    /// </summary>
    public void ExecuteMethod(object plugin, string methodName, params object[] parameters)
    {
        if (plugin != null)
        {
            try
            {
                var method = plugin.GetType().GetMethod(methodName);
                if (method != null)
                {
                    method.Invoke(plugin, parameters);
                    Console.WriteLine($"已执行插件 {plugin.GetType().Name} 的方法 {methodName}");
                }
                else
                {
                    Console.WriteLine($"插件 {plugin.GetType().Name} 没有找到方法 {methodName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行插件 {plugin.GetType().Name} 的方法 {methodName} 时出错: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 初始化插件
    /// </summary>
    public void InitializePlugin(object plugin)
    {
        ExecuteMethod(plugin, "Initialize");
    }
}
