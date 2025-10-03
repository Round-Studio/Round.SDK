using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Round.SDK.Entry;

namespace Round.SDK.Plugin;

public class PlugLoader
{
    public Type PluginType { get; private set; }

    public PlugLoader(Type pluginType)
    {
        PluginType = pluginType;
    }

    /// <summary>
    /// 加载单个插件
    /// </summary>
    /// <param name="assemblyPath">DLL文件路径，如果为null则从已加载程序集中查找</param>
    private object Load(string assemblyPath = null)
    {
        if (!string.IsNullOrEmpty(assemblyPath))
        {
            // 从指定DLL文件加载单个插件
            return LoadSingleFromAssembly(assemblyPath);
        }
        else
        {
            // 从已加载程序集中查找第一个符合条件的插件
            return LoadSingleFromAppDomain();
        }
    }

    /// <summary>
    /// 从已加载程序集中加载单个插件
    /// </summary>
    private object LoadSingleFromAppDomain()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        
        foreach (var assembly in assemblies)
        {
            try
            {
                var pluginType = assembly.GetTypes()
                    .FirstOrDefault(t => PluginType.IsAssignableFrom(t) && 
                                       !t.IsInterface && 
                                       !t.IsAbstract);

                if (pluginType != null)
                {
                    var pluginInstance = Activator.CreateInstance(pluginType);
                    Console.WriteLine($"已加载插件: {pluginType.FullName}");
                    return pluginInstance;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载程序集 {assembly.FullName} 时出错: {ex.Message}");
            }
        }
        
        Console.WriteLine("未找到符合条件的插件");
        return null;
    }

    /// <summary>
    /// 从指定程序集文件加载单个插件
    /// </summary>
    private object LoadSingleFromAssembly(string assemblyPath)
    {
        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => PluginType.IsAssignableFrom(t) && 
                                   !t.IsInterface && 
                                   !t.IsAbstract);

            if (pluginType != null)
            {
                var pluginInstance = Activator.CreateInstance(pluginType);
                Console.WriteLine($"已从程序集加载插件: {pluginType.FullName}");
                return pluginInstance;
            }
            else
            {
                Console.WriteLine($"程序集 {assemblyPath} 中未找到符合条件的插件");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载程序集 {assemblyPath} 时出错: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 加载插件并执行指定方法
    /// </summary>
    /// <param name="assemblyPath">DLL文件路径</param>
    /// <param name="methodName">要执行的方法名</param>
    /// <param name="parameters">方法参数</param>
    private void LoadAndExecute(string assemblyPath, string methodName, params object[] parameters)
    {
        var plugin = Load(assemblyPath);
        
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
    /// 加载插件并初始化
    /// </summary>
    /// <param name="assemblyPath">DLL文件路径</param>
    public void LoadAndInitialize(string assemblyPath)
    {
        LoadAndExecute(assemblyPath, "Initialize");
    }

    /// <summary>
    /// 获取插件信息
    /// </summary>
    /// <param name="assemblyPath">DLL文件路径</param>
    public PluginInfo GetPluginInfo(string assemblyPath = null)
    {
        var plugin = Load(assemblyPath);
        
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
}