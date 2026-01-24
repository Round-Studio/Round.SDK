using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Round.SDK.Entry;
using Round.SDK.Enum;
using Round.SDK.Global;

namespace Round.SDK.Entity;

public class ConfigEntity<T> where T : new()
{
    public T Data { get; set; }
    public string Path { get; private set; }
    private JsonTypeInfo<T>? TypeInfo;
    public bool IsSave { get; set; } = true;
    
    // 保存前的回调列表
    private readonly List<Action<ConfigEntity<T>>> _beforeSaveCallbacks = new();
    
    // 保存后的回调列表
    private readonly List<Action<ConfigEntity<T>>> _afterSaveCallbacks = new();
    
    // 保存事件
    public event EventHandler<ConfigSaveEventArgs<T>>? BeforeSave;
    public event EventHandler<ConfigSaveEventArgs<T>>? AfterSave;

    public ConfigEntity(string configFile, bool isSave = true, JsonTypeInfo<T>? typeInfo = default)
    {
        Path = configFile;
        TypeInfo = typeInfo;
        IsSave = isSave;
        Load();
    }
    
    // 添加保存前回调
    public void AddBeforeSaveCallback(Action<ConfigEntity<T>> callback)
    {
        _beforeSaveCallbacks.Add(callback);
    }
    
    // 添加保存后回调
    public void AddAfterSaveCallback(Action<ConfigEntity<T>> callback)
    {
        _afterSaveCallbacks.Add(callback);
    }
    
    // 移除回调
    public bool RemoveBeforeSaveCallback(Action<ConfigEntity<T>> callback)
    {
        return _beforeSaveCallbacks.Remove(callback);
    }
    
    public bool RemoveAfterSaveCallback(Action<ConfigEntity<T>> callback)
    {
        return _afterSaveCallbacks.Remove(callback);
    }
    
    // 清空所有回调
    public void ClearAllCallbacks()
    {
        _beforeSaveCallbacks.Clear();
        _afterSaveCallbacks.Clear();
    }

    public void Load()
    {
        if (!File.Exists(Path))
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            Save();
            return;
        }

        var json = File.ReadAllText(Path);
        if (string.IsNullOrEmpty(json))
        {
            Save();
        }
        else
        {
            try
            {
                Data = TypeInfo != null
                    ? JsonSerializer.Deserialize<T>(json, TypeInfo)
                    : JsonSerializer.Deserialize<T>(json, JsonSerializerOption.Options);
            }
            catch
            {
                Save();
            }
        }
    }

    public void Save()
    {
        if (!IsSave)
            return;
        
        // 触发保存前回调
        TriggerBeforeSave();

        Console.WriteLine($"触发保存配置项：{Path}");
        if (Data == null)
        {
            Data = new T();
        }

        string jsresult = TypeInfo != null
            ? JsonSerializer.Serialize(Data, TypeInfo)
            : JsonSerializer.Serialize(Data, JsonSerializerOption.Options);
        File.WriteAllText(Path, jsresult);
        
        // 触发保存后回调
        TriggerAfterSave();
    }
    
    private void TriggerBeforeSave()
    {
        try
        {
            // 触发回调函数
            foreach (var callback in _beforeSaveCallbacks)
            {
                callback?.Invoke(this);
            }
            
            // 触发事件
            BeforeSave?.Invoke(this, new ConfigSaveEventArgs<T>(this, SavePhase.Before));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存前回调执行失败: {ex.Message}");
        }
    }
    
    private void TriggerAfterSave()
    {
        try
        {
            // 触发回调函数
            foreach (var callback in _afterSaveCallbacks)
            {
                callback?.Invoke(this);
            }
            
            // 触发事件
            AfterSave?.Invoke(this, new ConfigSaveEventArgs<T>(this, SavePhase.After));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存后回调执行失败: {ex.Message}");
        }
    }
}