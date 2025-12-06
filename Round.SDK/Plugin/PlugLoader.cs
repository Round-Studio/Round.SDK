using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Round.SDK.Entity;
using Round.SDK.Entry;
using Round.SDK.Helper;
using Round.SDK.Helper.IO;

namespace Round.SDK.Plugin;

public class PlugLoader
{
    public Type PluginType { get; private set; }

    /// <summary>
    /// 插件解压路径
    /// </summary>
    public string ExtractPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Templates), "RoundStudio", "SDK",
            "Plugin.Templates");
    
    /// <summary>
    /// 已加载的程序集缓存
    /// </summary>
    private List<Assembly> _loadedAssemblies = new List<Assembly>();
    
    /// <summary>
    /// 插件包配置信息
    /// </summary>
    public PackConfig PackConfig { get; private set; }
    
    /// <summary>
    /// 插件实例
    /// </summary>
    public object PluginInstance { get; private set; }
    
    /// <summary>
    /// 插件包路径
    /// </summary>
    public string PluginPackagePath { get; private set; }
    
    /// <summary>
    /// 是否已加载
    /// </summary>
    public bool IsLoaded { get; private set; }

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
        if (IsLoaded)
        {
            throw new InvalidOperationException("该PlugLoader实例已加载过插件包，请创建新的实例来加载其他插件包");
        }

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
            PluginPackagePath = pluginPackagePath;
            
            // 1. 解压插件包
            string extractDir = Path.Combine(ExtractPath,
                FileHashCalculator.CalculateHash(PluginPackagePath, FileHashCalculator.HashType.MD5));
            
            ZipHelper.ExtractZipFile(pluginPackagePath, extractDir);
            
            // 2. 读取插件配置
            PackConfig = ReadPackConfig(extractDir);
            
            // 3. 加载依赖的DLL文件
            LoadDependencies(extractDir, PackConfig.BodyFile);
            
            // 4. 加载插件主体
            PluginInstance = LoadPluginBody(extractDir, PackConfig.BodyFile);
            
            IsLoaded = true;
            
            return PluginInstance;
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"加载插件包失败 {pluginPackagePath}: {ex.Message}");
            throw;
        }
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

        var config = LoadConfig<PackConfig>(configPath);
        
        if (string.IsNullOrEmpty(config.BodyFile))
        {
            throw new InvalidOperationException("插件包配置中未指定主体文件");
        }
        
        Console.WriteLine($@"读取插件配置: {config.PackName} v{config.PackVersion}");
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
                Console.WriteLine($@"已加载依赖: {Path.GetFileName(dllPath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($@"加载依赖失败 {dllPath}: {ex.Message}");
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
                Console.WriteLine($@"已加载插件主体: {pluginType.FullName}");
                return pluginInstance;
            }
            else
            {
                throw new InvalidOperationException($"在主体文件中未找到符合条件的插件类型: {bodyFile}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"加载插件主体失败 {bodyFilePath}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 加载配置文件的辅助方法
    /// </summary>
    private T LoadConfig<T>(string configPath,JsonTypeInfo<T>? typeInfo = default) where T : new()
    {
        try
        {
            var configEntity = new ConfigEntity<T>(configPath,typeInfo !=null? typeInfo : default);
            configEntity.Load();
            return configEntity.Data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"读取配置文件失败 {configPath}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 获取插件包信息（不依赖插件实例）
    /// </summary>
    public PackConfig GetPackConfig()
    {
        if (!IsLoaded)
        {
            throw new InvalidOperationException("插件尚未加载，请先调用Load方法");
        }
        
        return PackConfig;
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
    public void ExecuteMethod(string methodName, params object[] parameters)
    {
        if (!IsLoaded || PluginInstance == null)
        {
            throw new InvalidOperationException("插件尚未加载，请先调用Load方法");
        }

        try
        {
            var method = PluginInstance.GetType().GetMethod(methodName);
            if (method != null)
            {
                method.Invoke(PluginInstance, parameters);
                Console.WriteLine($@"已执行插件 {PluginInstance.GetType().Name} 的方法 {methodName}");
            }
            else
            {
                Console.WriteLine($@"插件 {PluginInstance.GetType().Name} 没有找到方法 {methodName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"执行插件 {PluginInstance.GetType().Name} 的方法 {methodName} 时出错: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 初始化插件
    /// </summary>
    public void InitializePlugin()
    {
        ExecuteMethod("Initialize");
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
                Console.WriteLine(@"已清理临时插件文件");
            }
            
            _loadedAssemblies.Clear();
            PluginInstance = null;
            PackConfig = null;
            IsLoaded = false;
            PluginPackagePath = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"清理临时文件失败: {ex.Message}");
        }
    }
}